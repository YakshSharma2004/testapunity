using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testapi1.Application;
using testapi1.Contracts;

namespace testapi1.Services
{
    public class CachedIntentClassifier : IIntentClassifier
    {
        private readonly IIntentClassifier _inner;
        private readonly IMemoryCache _cache;
        private readonly ITextNormalizer _textNormalizer;
        private readonly IOptionsMonitor<ApiCacheOptions> _optionsMonitor;
        private readonly ICacheInvalidationTokenSource _invalidationTokenSource;
        private readonly ILogger<CachedIntentClassifier> _logger;

        public CachedIntentClassifier(
            IIntentClassifier inner,
            IMemoryCache cache,
            ITextNormalizer textNormalizer,
            IOptionsMonitor<ApiCacheOptions> optionsMonitor,
            ICacheInvalidationTokenSource invalidationTokenSource,
            ILogger<CachedIntentClassifier> logger)
        {
            _inner = inner;
            _cache = cache;
            _textNormalizer = textNormalizer;
            _optionsMonitor = optionsMonitor;
            _invalidationTokenSource = invalidationTokenSource;
            _logger = logger;
        }

        public Task<IntentResponse> ClassifyAsync(IntentRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return _inner.ClassifyAsync(request, cancellationToken);
            }

            var normalized = _textNormalizer.NormalizeForMatch(request.Text);
            var npcKey = request.NpcId ?? "";
            var contextKey = request.ContextKey ?? "";
            var modelVersion = _optionsMonitor.CurrentValue.ModelVersion ?? "";
            var cacheKey = $"intent:{modelVersion}:{normalized}:{npcKey}:{contextKey}";

            return _cache.GetOrCreateAsync(cacheKey, entry =>
            {
                var ttlSeconds = _optionsMonitor.CurrentValue.IntentTtlSeconds;
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds);
                entry.AddExpirationToken(_invalidationTokenSource.GetChangeToken());
                _logger.LogDebug("Caching intent response for key {CacheKey} with TTL {TtlSeconds}s", cacheKey, ttlSeconds);
                return _inner.ClassifyAsync(request, cancellationToken);
            });
        }
    }
}
