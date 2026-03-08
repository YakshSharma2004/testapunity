using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testapi1.Application;

namespace testapi1.Services.Embeddings
{
    public sealed class ConfigurableOnnxEmbeddingService : IEmbeddingService, IDisposable
    {
        private readonly ILogger<ConfigurableOnnxEmbeddingService> _logger;
        private readonly IOptionsMonitor<EmbeddingsOptions> _optionsMonitor;
        private readonly Dictionary<string, OnnxSentenceEmbeddingModel> _models;

        public ConfigurableOnnxEmbeddingService(
            ILogger<ConfigurableOnnxEmbeddingService> logger,
            ILoggerFactory loggerFactory,
            IOptionsMonitor<EmbeddingsOptions> optionsMonitor,
            IWebHostEnvironment env)
        {
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            _models = BuildModels(optionsMonitor.CurrentValue, env.ContentRootPath, loggerFactory);
        }

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            var activeModel = EmbeddingModelName.Normalize(_optionsMonitor.CurrentValue.Model);

            if (!_models.TryGetValue(activeModel, out var model))
            {
                throw new InvalidOperationException(
                    $"Embedding model '{activeModel}' is not configured. " +
                    $"Available models: {string.Join(", ", _models.Keys.OrderBy(key => key))}");
            }

            _logger.LogDebug("Embedding text with model {ModelName}", activeModel);
            return model.EmbedAsync(text, ct);
        }

        public void Dispose()
        {
            foreach (var model in _models.Values)
            {
                model.Dispose();
            }
        }

        private static Dictionary<string, OnnxSentenceEmbeddingModel> BuildModels(
            EmbeddingsOptions options,
            string contentRootPath,
            ILoggerFactory loggerFactory)
        {
            if (options.Models.Count == 0)
            {
                throw new InvalidOperationException("Embeddings:Models must configure at least one model.");
            }

            var models = new Dictionary<string, OnnxSentenceEmbeddingModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in options.Models)
            {
                var key = EmbeddingModelName.Normalize(entry.Key);
                models[key] = new OnnxSentenceEmbeddingModel(
                    key,
                    entry.Value,
                    contentRootPath,
                    loggerFactory.CreateLogger<OnnxSentenceEmbeddingModel>());
            }

            return models;
        }
    }
}
