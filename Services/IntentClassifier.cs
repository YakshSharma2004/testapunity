using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testapi1.Application;
using testapi1.ApiContracts;
using testapi1.Services.Caching;
using testapi1.Services.Intent;

namespace testapi1.Services
{
    public class IntentClassifier : IIntentClassifier
    {
        private readonly ILogger<IntentClassifier> _logger;
        private readonly IOptionsMonitor<ApiCacheOptions> _cacheOptionsMonitor;
        private readonly IOptionsMonitor<IntentClassificationOptions> _intentOptionsMonitor;
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorStore _vectorStore;

        public IntentClassifier(
            ILogger<IntentClassifier> logger,
            IOptionsMonitor<ApiCacheOptions> cacheOptionsMonitor,
            IOptionsMonitor<IntentClassificationOptions> intentOptionsMonitor,
            IEmbeddingService embeddingService,
            IVectorStore vectorStore)
        {
            _logger = logger;
            _cacheOptionsMonitor = cacheOptionsMonitor;
            _intentOptionsMonitor = intentOptionsMonitor;
            _embeddingService = embeddingService;
            _vectorStore = vectorStore;
        }

        public async Task<IntentResponse> ClassifyAsync(IntentRequest request, CancellationToken cancellationToken = default)
        {
            var text = request?.Text ?? string.Empty;
            _logger.LogInformation("Classifying intent for text length {Length}", text.Length);

            if (string.IsNullOrWhiteSpace(text))
            {
                return UnknownResponse("empty-input");
            }

            var intentOptions = _intentOptionsMonitor.CurrentValue;
            var embedding = await _embeddingService.EmbedAsync(text, cancellationToken);
            var neighbors = await _vectorStore.QuerySimilar(embedding, Math.Max(1, intentOptions.TopK), cancellationToken);

            if (neighbors.Count == 0)
            {
                return UnknownResponse("no-neighbors");
            }

            var topByIntent = neighbors
                .GroupBy(item => item.IntentId)
                .Select(group => new
                {
                    Intent = group.Key,
                    Score = group.Max(item => item.Score)
                })
                .OrderByDescending(item => item.Score)
                .First();

            var confidence = (float)topByIntent.Score;
            if (topByIntent.Score < intentOptions.MinConfidence)
            {
                return UnknownResponse($"below-threshold:{topByIntent.Score.ToString("0.000", CultureInfo.InvariantCulture)}");
            }

            return new IntentResponse
            {
                intent = topByIntent.Intent,
                confidence = confidence,
                notes = intentOptions.IncludeDebugNotes
                    ? BuildNotes(neighbors)
                    : "seeded-similarity-poc",
                modelVersion = _cacheOptionsMonitor.CurrentValue.ModelVersion ?? string.Empty
            };
        }

        private IntentResponse UnknownResponse(string reason)
        {
            return new IntentResponse
            {
                intent = "unknown",
                confidence = 0.0f,
                notes = reason,
                modelVersion = _cacheOptionsMonitor.CurrentValue.ModelVersion ?? string.Empty
            };
        }

        private static string BuildNotes(IReadOnlyList<Infrastructure.VectorStores.VectorSearchResult> neighbors)
        {
            var top = neighbors
                .Take(3)
                .Select(item => $"{item.IntentId}:{item.Score.ToString("0.000", CultureInfo.InvariantCulture)}");
            return $"seeded-similarity-poc top={string.Join(",", top)}";
        }
    }
}
