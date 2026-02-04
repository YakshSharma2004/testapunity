using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testapi1.Application;
using testapi1.Contracts;

namespace testapi1.Services
{
    public class CachedIntentClassifier : IIntentClassifier
    {
        private readonly IIntentClassifier _inner;
        private readonly IDistributedCache _cache;
        private readonly ITextNormalizer _textNormalizer;
        private readonly IOptionsMonitor<ApiCacheOptions> _optionsMonitor;
        private readonly ILogger<CachedIntentClassifier> _logger;

        public CachedIntentClassifier(
            IIntentClassifier inner,
            IDistributedCache cache,
            ITextNormalizer textNormalizer,
            IOptionsMonitor<ApiCacheOptions> optionsMonitor,
            ILogger<CachedIntentClassifier> logger)
        {
            _inner = inner;
            _cache = cache;
            _textNormalizer = textNormalizer;
            _optionsMonitor = optionsMonitor;
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
            var cacheKey = $"intent:{modelVersion}:{normalized}:{npcKey}:{contextKey}";

            var cachedValue = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(cachedValue))
            {
                _logger.LogDebug("Intent cache hit for key {CacheKey}", cacheKey);
                return JsonSerializer.Deserialize<IntentResponse>(cachedValue) ?? new IntentResponse();
            }

            var response = await _inner.ClassifyAsync(request, cancellationToken);
            var ttlSeconds = _optionsMonitor.CurrentValue.IntentTtlSeconds;
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
            };
            var serialized = JsonSerializer.Serialize(response);
            await _cache.SetStringAsync(cacheKey, serialized, cacheOptions, cancellationToken);
            _logger.LogDebug("Caching intent response for key {CacheKey} with TTL {TtlSeconds}s", cacheKey, ttlSeconds);
            return response;
        }
    }
}
