using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using testapi1.Services.Embeddings;
using testapi1.Services.Intent;

namespace testapi1.Services.Evaluation
{
    public static class IntentEvaluationCli
    {
        private static readonly double[] DefaultSweepThresholds = new[] { 0.35, 0.45, 0.55, 0.65 };

        public static bool IsRequested(string[] args)
        {
            return args.Any(arg => string.Equals(arg, "--eval-intents", StringComparison.OrdinalIgnoreCase));
        }

        public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
        {
            try
            {
                var cliOptions = ParseArguments(args);
                var config = LoadConfiguration();

                var embeddingsOptions = new EmbeddingsOptions();
                config.GetSection("Embeddings").Bind(embeddingsOptions);

                if (embeddingsOptions.Models.Count == 0)
                {
                    throw new InvalidOperationException("Embeddings:Models is empty. Configure at least one embedding model.");
                }

                var intentOptions = new IntentClassificationOptions();
                config.GetSection("IntentClassification").Bind(intentOptions);
                if (intentOptions.TopK <= 0)
                {
                    intentOptions.TopK = 3;
                }

                var datasetPath = ResolvePath(cliOptions.DatasetPath);
                var outputDir = ResolvePath(cliOptions.OutputDir);
                var samples = await LoadValidationSamplesAsync(datasetPath, cancellationToken);
                ValidateSamples(samples);

                var modelKeys = ResolveModels(cliOptions.ModelsCsv, embeddingsOptions);
                var sweepThresholds = ResolveSweepThresholds(cliOptions.SweepCsv);

                var modelResults = new List<ModelRunResult>(modelKeys.Count);
                foreach (var modelKey in modelKeys)
                {
                    if (!embeddingsOptions.Models.TryGetValue(modelKey, out var modelConfig))
                    {
                        throw new InvalidOperationException($"Embeddings:Models:{modelKey} is not configured.");
                    }

                    var result = await EvaluateModelAsync(
                        modelKey,
                        modelConfig,
                        samples,
                        intentOptions.TopK,
                        cliOptions.FixedThreshold,
                        sweepThresholds,
                        cancellationToken);
                    modelResults.Add(result);
                }

                var report = BuildReport(
                    datasetPath,
                    outputDir,
                    cliOptions.FixedThreshold,
                    sweepThresholds,
                    intentOptions.TopK,
                    modelResults);

                Directory.CreateDirectory(outputDir);
                var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                var jsonPath = Path.Combine(outputDir, $"{timestamp}-comparison.json");
                var markdownPath = Path.Combine(outputDir, $"{timestamp}-comparison.md");

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(report, jsonOptions);
                await File.WriteAllTextAsync(jsonPath, json, cancellationToken);

                var markdown = BuildMarkdownReport(report);
                await File.WriteAllTextAsync(markdownPath, markdown, cancellationToken);

                Console.WriteLine($"Intent model evaluation completed.");
                Console.WriteLine($"JSON report: {jsonPath}");
                Console.WriteLine($"Markdown report: {markdownPath}");
                Console.WriteLine($"Fixed-threshold winner: {report.FixedThresholdWinner}");
                Console.WriteLine($"Best-threshold winner: {report.BestThresholdWinner}");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Intent evaluation failed: {ex.Message}");
                return 1;
            }
        }

        private static async Task<ModelRunResult> EvaluateModelAsync(
            string modelKey,
            EmbeddingModelOptions modelConfig,
            IReadOnlyList<IntentValidationSample> samples,
            int topK,
            double fixedThreshold,
            IReadOnlyList<double> sweepThresholds,
            CancellationToken cancellationToken)
        {
            var contentRoot = Directory.GetCurrentDirectory();
            using var embedder = new OnnxSentenceEmbeddingModel(
                modelKey,
                modelConfig,
                contentRoot,
                NullLogger.Instance);

            var seedEmbeddings = new List<SeedEmbedding>(IntentSeed.Examples.Length);
            foreach (var seed in IntentSeed.Examples)
            {
                var embedding = await embedder.EmbedAsync(seed.Text, cancellationToken);
                seedEmbeddings.Add(new SeedEmbedding(seed.Intent, seed.Text, embedding));
            }

            var scoredSamples = new List<ScoredSample>(samples.Count);
            foreach (var sample in samples)
            {
                var queryEmbedding = await embedder.EmbedAsync(sample.Text, cancellationToken);
                var score = ScoreSample(queryEmbedding, seedEmbeddings, topK);
                scoredSamples.Add(new ScoredSample(sample.Text, sample.ExpectedIntent, score.TopIntent, score.TopScore, score.Top2Gap));
            }

            var fixedMetrics = ComputeMetrics(scoredSamples, fixedThreshold);
            var sweep = sweepThresholds
                .Distinct()
                .OrderBy(value => value)
                .Select(threshold => ComputeMetrics(scoredSamples, threshold))
                .ToList();

            var constrained = sweep.Where(item => item.UnknownRate <= 0.20).ToList();
            var bestPool = constrained.Count > 0 ? constrained : sweep;
            var best = bestPool
                .OrderByDescending(item => item.MacroF1)
                .ThenByDescending(item => item.Accuracy)
                .ThenBy(item => item.UnknownRate)
                .ThenByDescending(item => item.AverageTop2Gap)
                .First();

            return new ModelRunResult(
                modelKey,
                fixedMetrics,
                sweep,
                best,
                constrained.Count == 0);
        }

        private static IntentEvaluationReport BuildReport(
            string datasetPath,
            string outputDir,
            double fixedThreshold,
            IReadOnlyList<double> sweepThresholds,
            int topK,
            IReadOnlyList<ModelRunResult> modelResults)
        {
            var fixedWinner = modelResults
                .OrderByDescending(result => result.Fixed.Accuracy)
                .ThenByDescending(result => result.Fixed.MacroF1)
                .ThenBy(result => result.Fixed.UnknownRate)
                .ThenByDescending(result => result.Fixed.AverageTop2Gap)
                .First()
                .Model;

            var bestWinner = modelResults
                .OrderByDescending(result => result.Best.MacroF1)
                .ThenByDescending(result => result.Best.Accuracy)
                .ThenBy(result => result.Best.UnknownRate)
                .ThenByDescending(result => result.Best.AverageTop2Gap)
                .First()
                .Model;

            return new IntentEvaluationReport(
                DateTimeOffset.UtcNow,
                datasetPath,
                outputDir,
                IntentSeed.Examples.Length,
                topK,
                fixedThreshold,
                sweepThresholds,
                fixedWinner,
                bestWinner,
                modelResults);
        }

        private static string BuildMarkdownReport(IntentEvaluationReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Intent Embedding A/B Comparison");
            sb.AppendLine();
            sb.AppendLine($"- Generated (UTC): {report.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- Dataset: `{report.DatasetPath}`");
            sb.AppendLine($"- Seeds: {report.SeedCount}");
            sb.AppendLine($"- TopK: {report.TopK}");
            sb.AppendLine($"- Fixed threshold: {report.FixedThreshold.ToString("0.00", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"- Sweep thresholds: {string.Join(", ", report.SweepThresholds.Select(ToMetricString))}");
            sb.AppendLine();
            sb.AppendLine("## Fixed Threshold Comparison");
            sb.AppendLine();
            sb.AppendLine("| Model | Accuracy | Macro F1 | Unknown Rate | Avg Top1 | Avg Top2 Gap |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|");
            foreach (var model in report.Models.OrderBy(item => item.Model))
            {
                sb.AppendLine(
                    $"| {model.Model} | {ToMetricString(model.Fixed.Accuracy)} | {ToMetricString(model.Fixed.MacroF1)} | {ToMetricString(model.Fixed.UnknownRate)} | {ToMetricString(model.Fixed.AverageTop1Score)} | {ToMetricString(model.Fixed.AverageTop2Gap)} |");
            }
            sb.AppendLine();
            sb.AppendLine($"Winner (fixed threshold): **{report.FixedThresholdWinner}**");
            sb.AppendLine();
            sb.AppendLine("## Best Threshold Per Model");
            sb.AppendLine();
            sb.AppendLine("| Model | Best Threshold | Accuracy | Macro F1 | Unknown Rate | Constraint Fallback |");
            sb.AppendLine("|---|---:|---:|---:|---:|---|");
            foreach (var model in report.Models.OrderBy(item => item.Model))
            {
                sb.AppendLine(
                    $"| {model.Model} | {ToMetricString(model.Best.Threshold)} | {ToMetricString(model.Best.Accuracy)} | {ToMetricString(model.Best.MacroF1)} | {ToMetricString(model.Best.UnknownRate)} | {(model.UsedFallbackForBestThreshold ? "yes" : "no")} |");
            }
            sb.AppendLine();
            sb.AppendLine($"Winner (best per-model threshold): **{report.BestThresholdWinner}**");
            sb.AppendLine();
            sb.AppendLine("## Confusion Summary");
            sb.AppendLine();
            foreach (var model in report.Models.OrderBy(item => item.Model))
            {
                sb.AppendLine($"### {model.Model}");
                if (model.Fixed.TopConfusions.Count == 0)
                {
                    sb.AppendLine("- No misclassifications at fixed threshold.");
                }
                else
                {
                    foreach (var confusion in model.Fixed.TopConfusions.Take(5))
                    {
                        sb.AppendLine($"- `{confusion.ExpectedIntent}` -> `{confusion.PredictedIntent}`: {confusion.Count}");
                    }
                }
                sb.AppendLine();
            }
            sb.AppendLine("## Notes");
            sb.AppendLine();
            sb.AppendLine("- Both models are evaluated against the same seed set (`IntentSeed.Examples`) and v1 decision rule (top intent by max neighbor score + threshold-to-unknown).");
            sb.AppendLine("- Consider adding top2-margin confidence gating (`top1 - top2`) if fixed-threshold confidence is over-optimistic on ambiguous phrases.");
            return sb.ToString();
        }

        private static ThresholdMetrics ComputeMetrics(IReadOnlyList<ScoredSample> scoredSamples, double threshold)
        {
            var predictions = new List<PredictionRecord>(scoredSamples.Count);
            foreach (var item in scoredSamples)
            {
                var predicted = item.TopScore < threshold ? "unknown" : item.TopIntent;
                predictions.Add(new PredictionRecord(
                    item.Text,
                    item.ExpectedIntent,
                    predicted,
                    item.TopScore,
                    item.Top2Gap,
                    string.Equals(item.ExpectedIntent, predicted, StringComparison.OrdinalIgnoreCase)));
            }

            var accuracy = predictions.Count == 0
                ? 0d
                : predictions.Count(prediction => prediction.IsCorrect) / (double)predictions.Count;
            var unknownRate = predictions.Count == 0
                ? 0d
                : predictions.Count(prediction => prediction.PredictedIntent == "unknown") / (double)predictions.Count;

            var labels = predictions
                .Select(prediction => prediction.ExpectedIntent)
                .Where(label => !string.Equals(label, "unknown", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var perIntent = new List<PerIntentMetric>(labels.Count);
            foreach (var label in labels)
            {
                var tp = predictions.Count(prediction =>
                    string.Equals(prediction.ExpectedIntent, label, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(prediction.PredictedIntent, label, StringComparison.OrdinalIgnoreCase));
                var fp = predictions.Count(prediction =>
                    !string.Equals(prediction.ExpectedIntent, label, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(prediction.PredictedIntent, label, StringComparison.OrdinalIgnoreCase));
                var fn = predictions.Count(prediction =>
                    string.Equals(prediction.ExpectedIntent, label, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(prediction.PredictedIntent, label, StringComparison.OrdinalIgnoreCase));

                var precision = tp + fp == 0 ? 0d : tp / (double)(tp + fp);
                var recall = tp + fn == 0 ? 0d : tp / (double)(tp + fn);
                var f1 = precision + recall == 0d ? 0d : 2d * precision * recall / (precision + recall);
                var support = predictions.Count(prediction =>
                    string.Equals(prediction.ExpectedIntent, label, StringComparison.OrdinalIgnoreCase));

                perIntent.Add(new PerIntentMetric(label, precision, recall, f1, support));
            }

            var macroF1 = perIntent.Count == 0 ? 0d : perIntent.Average(item => item.F1);
            var averageTop1 = predictions.Count == 0 ? 0d : predictions.Average(item => item.TopScore);
            var averageTop2Gap = predictions.Count == 0 ? 0d : predictions.Average(item => item.Top2Gap);

            var topConfusions = predictions
                .Where(prediction => !prediction.IsCorrect)
                .GroupBy(prediction => new { prediction.ExpectedIntent, prediction.PredictedIntent })
                .Select(group => new ConfusionSummary(
                    group.Key.ExpectedIntent,
                    group.Key.PredictedIntent,
                    group.Count()))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.ExpectedIntent, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.PredictedIntent, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ThresholdMetrics(
                threshold,
                accuracy,
                macroF1,
                unknownRate,
                averageTop1,
                averageTop2Gap,
                perIntent,
                topConfusions,
                predictions);
        }

        private static SampleScore ScoreSample(float[] queryEmbedding, IReadOnlyList<SeedEmbedding> seeds, int topK)
        {
            var nearest = seeds
                .Select(seed => new IntentScore(seed.Intent, CosineSimilarity(seed.Embedding, queryEmbedding)))
                .OrderByDescending(score => score.Score)
                .Take(Math.Max(1, topK))
                .ToList();

            if (nearest.Count == 0)
            {
                return new SampleScore("unknown", 0d, 0d);
            }

            var topByIntent = nearest
                .GroupBy(item => item.Intent)
                .Select(group => new IntentScore(group.Key, group.Max(item => item.Score)))
                .OrderByDescending(item => item.Score)
                .ToList();

            var top1 = topByIntent[0];
            var top2Score = topByIntent.Count > 1 ? topByIntent[1].Score : 0d;
            return new SampleScore(top1.Intent, top1.Score, top1.Score - top2Score);
        }

        private static double CosineSimilarity(float[] left, float[] right)
        {
            if (left.Length == 0 || left.Length != right.Length)
            {
                return 0d;
            }

            double dot = 0d;
            double normLeft = 0d;
            double normRight = 0d;

            for (var index = 0; index < left.Length; index++)
            {
                dot += left[index] * right[index];
                normLeft += left[index] * left[index];
                normRight += right[index] * right[index];
            }

            if (normLeft == 0d || normRight == 0d)
            {
                return 0d;
            }

            return dot / (Math.Sqrt(normLeft) * Math.Sqrt(normRight));
        }

        private static IConfiguration LoadConfiguration()
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }

        private static async Task<IReadOnlyList<IntentValidationSample>> LoadValidationSamplesAsync(
            string datasetPath,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(datasetPath))
            {
                throw new FileNotFoundException($"Validation dataset not found at '{datasetPath}'.");
            }

            await using var stream = File.OpenRead(datasetPath);
            var data = await JsonSerializer.DeserializeAsync<List<IntentValidationSample>>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                },
                cancellationToken);

            return data ?? new List<IntentValidationSample>();
        }

        private static void ValidateSamples(IReadOnlyList<IntentValidationSample> samples)
        {
            if (samples.Count == 0)
            {
                throw new InvalidOperationException("Validation dataset is empty.");
            }

            for (var index = 0; index < samples.Count; index++)
            {
                var sample = samples[index];
                if (string.IsNullOrWhiteSpace(sample.Text))
                {
                    throw new InvalidOperationException($"Validation sample {index} has an empty 'text' value.");
                }

                if (string.IsNullOrWhiteSpace(sample.ExpectedIntent))
                {
                    throw new InvalidOperationException($"Validation sample {index} has an empty 'expected_intent' value.");
                }
            }
        }

        private static IntentEvalCliOptions ParseArguments(string[] args)
        {
            var options = new IntentEvalCliOptions();
            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index];
                if (string.Equals(arg, "--eval-intents", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(arg, "--dataset", StringComparison.OrdinalIgnoreCase))
                {
                    options.DatasetPath = ReadValue(args, ref index, "--dataset");
                    continue;
                }

                if (string.Equals(arg, "--models", StringComparison.OrdinalIgnoreCase))
                {
                    options.ModelsCsv = ReadValue(args, ref index, "--models");
                    continue;
                }

                if (string.Equals(arg, "--threshold", StringComparison.OrdinalIgnoreCase))
                {
                    options.FixedThreshold = ParseDouble(ReadValue(args, ref index, "--threshold"), "--threshold");
                    continue;
                }

                if (string.Equals(arg, "--sweep", StringComparison.OrdinalIgnoreCase))
                {
                    options.SweepCsv = ReadValue(args, ref index, "--sweep");
                    continue;
                }

                if (string.Equals(arg, "--output-dir", StringComparison.OrdinalIgnoreCase))
                {
                    options.OutputDir = ReadValue(args, ref index, "--output-dir");
                    continue;
                }

                throw new InvalidOperationException($"Unknown CLI argument '{arg}'.");
            }

            return options;
        }

        private static string ReadValue(string[] args, ref int index, string flagName)
        {
            if (index + 1 >= args.Length)
            {
                throw new InvalidOperationException($"{flagName} requires a value.");
            }

            index++;
            return args[index];
        }

        private static IReadOnlyList<string> ResolveModels(string? modelsCsv, EmbeddingsOptions options)
        {
            var parsed = ParseCsv(modelsCsv ?? "mpnet,multiqa")
                .Select(EmbeddingModelName.Normalize)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (parsed.Count == 0)
            {
                throw new InvalidOperationException("At least one model must be provided via --models.");
            }

            foreach (var model in parsed)
            {
                if (!options.Models.ContainsKey(model))
                {
                    throw new InvalidOperationException(
                        $"Model '{model}' is not configured in Embeddings:Models.");
                }
            }

            return parsed;
        }

        private static IReadOnlyList<double> ResolveSweepThresholds(string? sweepCsv)
        {
            if (string.IsNullOrWhiteSpace(sweepCsv))
            {
                return DefaultSweepThresholds;
            }

            var values = ParseCsv(sweepCsv)
                .Select(value => ParseDouble(value, "--sweep"))
                .OrderBy(value => value)
                .ToList();

            if (values.Count == 0)
            {
                throw new InvalidOperationException("--sweep must contain at least one numeric value.");
            }

            return values;
        }

        private static List<string> ParseCsv(string value)
        {
            return value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        private static double ParseDouble(string value, string flagName)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new InvalidOperationException($"{flagName} value '{value}' is not a valid number.");
            }

            return parsed;
        }

        private static string ResolvePath(string path)
        {
            return Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        }

        private static string ToMetricString(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private sealed class IntentEvalCliOptions
        {
            public string DatasetPath { get; set; } = "evaluation/intent-validation.json";
            public string? ModelsCsv { get; set; }
            public double FixedThreshold { get; set; } = 0.45d;
            public string? SweepCsv { get; set; }
            public string OutputDir { get; set; } = "Logs/model-eval";
        }
    }

    public sealed record IntentEvaluationReport(
        DateTimeOffset GeneratedAtUtc,
        string DatasetPath,
        string OutputDir,
        int SeedCount,
        int TopK,
        double FixedThreshold,
        IReadOnlyList<double> SweepThresholds,
        string FixedThresholdWinner,
        string BestThresholdWinner,
        IReadOnlyList<ModelRunResult> Models);

    public sealed record ModelRunResult(
        string Model,
        ThresholdMetrics Fixed,
        IReadOnlyList<ThresholdMetrics> Sweep,
        ThresholdMetrics Best,
        bool UsedFallbackForBestThreshold);

    public sealed record ThresholdMetrics(
        double Threshold,
        double Accuracy,
        double MacroF1,
        double UnknownRate,
        double AverageTop1Score,
        double AverageTop2Gap,
        IReadOnlyList<PerIntentMetric> PerIntent,
        IReadOnlyList<ConfusionSummary> TopConfusions,
        IReadOnlyList<PredictionRecord> Predictions);

    public sealed record PerIntentMetric(
        string Intent,
        double Precision,
        double Recall,
        double F1,
        int Support);

    public sealed record ConfusionSummary(
        string ExpectedIntent,
        string PredictedIntent,
        int Count);

    public sealed record PredictionRecord(
        string Text,
        string ExpectedIntent,
        string PredictedIntent,
        double TopScore,
        double Top2Gap,
        bool IsCorrect);

    internal sealed record SeedEmbedding(string Intent, string Text, float[] Embedding);
    internal sealed record IntentScore(string Intent, double Score);
    internal sealed record SampleScore(string TopIntent, double TopScore, double Top2Gap);
    internal sealed record ScoredSample(
        string Text,
        string ExpectedIntent,
        string TopIntent,
        double TopScore,
        double Top2Gap);

    public sealed class IntentValidationSample
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("expected_intent")]
        public string ExpectedIntent { get; set; } = string.Empty;
    }
}
