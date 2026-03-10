using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Tokenizers.HuggingFace.Tokenizer;

namespace testapi1.Services.Embeddings
{
    public sealed class OnnxSentenceEmbeddingModel : IDisposable
    {
        private readonly string _modelName;
        private readonly int _maxLen;
        private readonly InferenceSession _session;
        private readonly Tokenizer _tokenizer;
        private readonly string _inputIdsName;
        private readonly string _attentionMaskName;
        private readonly string? _tokenTypeIdsName;

        public string ModelName => _modelName;

        public OnnxSentenceEmbeddingModel(
            string modelName,
            EmbeddingModelOptions options,
            string contentRootPath,
            ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                throw new InvalidOperationException("Embedding model name cannot be empty.");
            }

            _modelName = modelName.Trim().ToLowerInvariant();
            _maxLen = options.MaxLen > 0 ? options.MaxLen : 384;

            var modelPath = ResolvePath(contentRootPath, options.ModelPath);
            var tokenizerPath = ResolvePath(contentRootPath, options.TokenizerPath);

            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException(
                    $"Embedding model '{_modelName}' ONNX file not found at '{modelPath}'.");
            }

            if (!File.Exists(tokenizerPath))
            {
                throw new FileNotFoundException(
                    $"Embedding model '{_modelName}' tokenizer file not found at '{tokenizerPath}'.");
            }

            _tokenizer = Tokenizer.FromFile(tokenizerPath);
            _session = new InferenceSession(modelPath);

            _inputIdsName = FindInputName(_session.InputMetadata.Keys, "input_ids", "input_ids:0")
                ?? throw new InvalidOperationException(
                    $"Model '{_modelName}' does not expose a readable input_ids input.");

            _attentionMaskName = FindInputName(_session.InputMetadata.Keys, "attention_mask", "attention_mask:0", "mask")
                ?? throw new InvalidOperationException(
                    $"Model '{_modelName}' does not expose a readable attention_mask input.");

            _tokenTypeIdsName = FindInputName(_session.InputMetadata.Keys, "token_type_ids", "token_type_ids:0", "segment_ids");

            logger?.LogInformation(
                "Loaded embedding model {ModelName}. ONNX inputs: {Inputs}. ONNX outputs: {Outputs}",
                _modelName,
                string.Join(", ", _session.InputMetadata.Keys),
                string.Join(", ", _session.OutputMetadata.Keys));
        }

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var encoded = _tokenizer.Encode(text ?? string.Empty, addSpecialTokens: true).FirstOrDefault()
                ?? throw new InvalidOperationException($"Tokenizer encode failed for embedding model '{_modelName}'.");

            var tokenIds = encoded.Ids.Select(id => (long)id).ToArray();
            if (tokenIds.Length > _maxLen)
            {
                tokenIds = tokenIds.Take(_maxLen).ToArray();
            }

            var inputIds = new long[_maxLen];
            var attentionMask = new long[_maxLen];
            var tokenTypeIds = new long[_maxLen];

            for (var index = 0; index < _maxLen; index++)
            {
                if (index < tokenIds.Length)
                {
                    inputIds[index] = tokenIds[index];
                    attentionMask[index] = 1;
                }
            }

            var inputIdsTensor = new DenseTensor<long>(new[] { 1, _maxLen });
            var attentionTensor = new DenseTensor<long>(new[] { 1, _maxLen });
            var tokenTypeTensor = new DenseTensor<long>(new[] { 1, _maxLen });

            for (var index = 0; index < _maxLen; index++)
            {
                inputIdsTensor[0, index] = inputIds[index];
                attentionTensor[0, index] = attentionMask[index];
                tokenTypeTensor[0, index] = tokenTypeIds[index];
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputIdsName, inputIdsTensor),
                NamedOnnxValue.CreateFromTensor(_attentionMaskName, attentionTensor)
            };

            if (!string.IsNullOrWhiteSpace(_tokenTypeIdsName))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(_tokenTypeIdsName, tokenTypeTensor));
            }

            using var results = _session.Run(inputs);

            foreach (var result in results)
            {
                if (result.Value is Tensor<float> tensor)
                {
                    var embedding = ExtractEmbedding(tensor, attentionMask);
                    return Task.FromResult(embedding);
                }
            }

            throw new InvalidOperationException($"Model '{_modelName}' did not return any float tensor output.");
        }

        public void Dispose()
        {
            _session.Dispose();
        }

        private static float[] ExtractEmbedding(Tensor<float> output, long[] attentionMask)
        {
            var dims = output.Dimensions.ToArray();
            var values = output.ToArray();

            if (dims.Length == 2 && dims[0] == 1)
            {
                var embedding = new float[dims[1]];
                Array.Copy(values, embedding, embedding.Length);
                NormalizeInPlace(embedding);
                return embedding;
            }

            if (dims.Length == 3 && dims[0] == 1)
            {
                var seqLen = dims[1];
                var hidden = dims[2];
                var pooled = new float[hidden];
                var tokenCount = 0f;

                for (var token = 0; token < seqLen && token < attentionMask.Length; token++)
                {
                    if (attentionMask[token] == 0)
                    {
                        continue;
                    }

                    var offset = token * hidden;
                    for (var channel = 0; channel < hidden; channel++)
                    {
                        pooled[channel] += values[offset + channel];
                    }

                    tokenCount += 1f;
                }

                if (tokenCount > 0f)
                {
                    for (var channel = 0; channel < hidden; channel++)
                    {
                        pooled[channel] /= tokenCount;
                    }
                }

                NormalizeInPlace(pooled);
                return pooled;
            }

            throw new InvalidOperationException(
                $"Unsupported embedding tensor shape [{string.Join(", ", dims)}].");
        }

        private static void NormalizeInPlace(float[] vector)
        {
            double norm = 0d;
            for (var index = 0; index < vector.Length; index++)
            {
                norm += vector[index] * vector[index];
            }

            norm = Math.Sqrt(norm);
            if (norm <= 0d)
            {
                return;
            }

            for (var index = 0; index < vector.Length; index++)
            {
                vector[index] = (float)(vector[index] / norm);
            }
        }

        private static string ResolvePath(string contentRootPath, string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                throw new InvalidOperationException("ModelPath and TokenizerPath must be configured.");
            }

            if (Path.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            return Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
        }

        private static string? FindInputName(IEnumerable<string> keys, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                var exact = keys.FirstOrDefault(key =>
                    string.Equals(key, candidate, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exact))
                {
                    return exact;
                }
            }

            foreach (var key in keys)
            {
                foreach (var candidate in candidates)
                {
                    if (key.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return key;
                    }
                }
            }

            return null;
        }
    }
}
