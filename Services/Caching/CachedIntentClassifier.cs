using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testapi1.Application;
using testapi1.Contracts;
using testapi1.Services.Embeddings;

namespace testapi1.Services.Caching
{
    public class CachedIntentClassifier : IIntentClassifier
    {
        private readonly IIntentClassifier _inner;
        private readonly IDistributedCache _cache;
        private readonly ITextNormalizer _textNormalizer;
        private readonly IOptionsMonitor<ApiCacheOptions> _optionsMonitor;
        private readonly IOptionsMonitor<EmbeddingsOptions> _embeddingsOptionsMonitor;
        private readonly ILogger<CachedIntentClassifier> _logger;

        public CachedIntentClassifier(
            IIntentClassifier inner,
            IDistributedCache cache,
            ITextNormalizer textNormalizer,
            IOptionsMonitor<ApiCacheOptions> optionsMonitor,
            IOptionsMonitor<EmbeddingsOptions> embeddingsOptionsMonitor,
            ILogger<CachedIntentClassifier> logger)
        {
            _inner = inner;
            _cache = cache;
            _textNormalizer = textNormalizer;
            _optionsMonitor = optionsMonitor;
            _embeddingsOptionsMonitor = embeddingsOptionsMonitor;
            _logger = logger;
        }

        public async Task<IntentResponse> ClassifyAsync(IntentRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return await _inner.ClassifyAsync(request, cancellationToken);
            }

            var normalized = _textNormalizer.NormalizeForMatch(request.Text);
            var npcKey = request.NpcId ?? "";
            var contextKey = request.ContextKey ?? "";
            var modelVersion = _optionsMonitor.CurrentValue.ModelVersion ?? "";
            var embeddingModel = EmbeddingModelName.Normalize(_embeddingsOptionsMonitor.CurrentValue.Model);
            var cacheKey = $"intent:{modelVersion}:{embeddingModel}:{normalized}:{npcKey}:{contextKey}";

            try
            {
                var cachedValue = await _cache.GetStringAsync(cacheKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(cachedValue))
                {
                    _logger.LogDebug("Intent cache hit for key {CacheKey}", cacheKey);
                    return JsonSerializer.Deserialize<IntentResponse>(cachedValue) ?? new IntentResponse();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Intent cache get failed for key {CacheKey}. Falling back to classifier.", cacheKey);
            }

            var response = await _inner.ClassifyAsync(request, cancellationToken);
            var ttlSeconds = _optionsMonitor.CurrentValue.IntentTtlSeconds;

            if (ttlSeconds > 0)
            {
                try
                {
                    var cacheOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
                    };
                    var serialized = JsonSerializer.Serialize(response);
                    await _cache.SetStringAsync(cacheKey, serialized, cacheOptions, cancellationToken);
                    _logger.LogDebug("Caching intent response for key {CacheKey} with TTL {TtlSeconds}s", cacheKey, ttlSeconds);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Intent cache set failed for key {CacheKey}. Returning classifier result.", cacheKey);
                }
            }

            return response;
        }
    }
}
