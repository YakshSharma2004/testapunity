using System;
using System.Collections.Concurrent;
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
        private readonly ILoggerFactory _loggerFactory;
        private readonly IOptionsMonitor<EmbeddingsOptions> _optionsMonitor;
        private readonly string _contentRootPath;
        private readonly ConcurrentDictionary<string, Lazy<OnnxSentenceEmbeddingModel>> _modelCache =
            new(StringComparer.OrdinalIgnoreCase);

        public ConfigurableOnnxEmbeddingService(
            ILogger<ConfigurableOnnxEmbeddingService> logger,
            ILoggerFactory loggerFactory,
            IOptionsMonitor<EmbeddingsOptions> optionsMonitor,
            IWebHostEnvironment env)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _optionsMonitor = optionsMonitor;
            _contentRootPath = env.ContentRootPath;

            if (_optionsMonitor.CurrentValue.Models.Count == 0)
            {
                throw new InvalidOperationException("Embeddings:Models must configure at least one model.");
            }
        }

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            var activeModel = EmbeddingModelName.Normalize(_optionsMonitor.CurrentValue.Model);
            var model = GetOrCreateModel(activeModel);

            _logger.LogDebug("Embedding text with model {ModelName}", activeModel);
            return model.EmbedAsync(text, ct);
        }

        public void Dispose()
        {
            foreach (var lazy in _modelCache.Values)
            {
                if (lazy.IsValueCreated)
                {
                    lazy.Value.Dispose();
                }
            }
        }

        private OnnxSentenceEmbeddingModel GetOrCreateModel(string modelName)
        {
            ValidateModelConfigured(modelName);
            var lazy = _modelCache.GetOrAdd(modelName, key =>
                new Lazy<OnnxSentenceEmbeddingModel>(() => BuildModel(key), true));

            try
            {
                return lazy.Value;
            }
            catch
            {
                _modelCache.TryRemove(modelName, out _);
                throw;
            }
        }

        private void ValidateModelConfigured(string modelName)
        {
            if (TryGetModelConfig(modelName, out _))
            {
                return;
            }

            var models = _optionsMonitor.CurrentValue.Models;
            var configured = string.Join(", ", models.Keys.Select(EmbeddingModelName.Normalize).OrderBy(key => key));
            throw new InvalidOperationException(
                $"Embedding model '{modelName}' is not configured. Available models: {configured}");
        }

        private OnnxSentenceEmbeddingModel BuildModel(string modelName)
        {
            if (!TryGetModelConfig(modelName, out var config))
            {
                throw new InvalidOperationException($"Embedding model '{modelName}' is not configured in Embeddings:Models.");
            }

            return new OnnxSentenceEmbeddingModel(
                modelName,
                config,
                _contentRootPath,
                _loggerFactory.CreateLogger<OnnxSentenceEmbeddingModel>());
        }

        private bool TryGetModelConfig(string modelName, out EmbeddingModelOptions config)
        {
            var models = _optionsMonitor.CurrentValue.Models;
            foreach (var entry in models)
            {
                if (EmbeddingModelName.Normalize(entry.Key) == modelName)
                {
                    config = entry.Value;
                    return true;
                }
            }

            config = new EmbeddingModelOptions();
            return false;
        }
    }
}
