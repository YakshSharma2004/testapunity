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
    public class CachedLlmService : ILLMService
    {
        private readonly ILLMService _inner;
        private readonly IMemoryCache _cache;
        private readonly ITextNormalizer _textNormalizer;
        private readonly IOptionsMonitor<ApiCacheOptions> _optionsMonitor;
        private readonly ICacheInvalidationTokenSource _invalidationTokenSource;
        private readonly ILogger<CachedLlmService> _logger;

        public CachedLlmService(
            ILLMService inner,
            IMemoryCache cache,
            ITextNormalizer textNormalizer,
            IOptionsMonitor<ApiCacheOptions> optionsMonitor,
            ICacheInvalidationTokenSource invalidationTokenSource,
            ILogger<CachedLlmService> logger)
        {
            _inner = inner;
            _cache = cache;
            _textNormalizer = textNormalizer;
            _optionsMonitor = optionsMonitor;
            _invalidationTokenSource = invalidationTokenSource;
            _logger = logger;
        }

        public Task<LlmRawResponse> GenerateResponseAsync(LlmPromptPayload payload, CancellationToken cancellationToken = default)
        {
            if (payload == null)
            {
                return _inner.GenerateResponseAsync(payload, cancellationToken);
            }

            var normalized = _textNormalizer.NormalizeForMatch(payload.promptText);
            var npcKey = payload.npcId ?? "";
            var contextKey = payload.contextKey ?? "";
            var conversationKey = payload.conversationId ?? "";
            var systemContextKey = payload.systemContext ?? "";
            var modelVersion = _optionsMonitor.CurrentValue.ModelVersion ?? "";
            var cacheKey = $"llm:{modelVersion}:{normalized}:{npcKey}:{contextKey}:{conversationKey}:{systemContextKey}:{payload.maxTokens}";

            return _cache.GetOrCreateAsync(cacheKey, entry =>
            {
                var ttlSeconds = _optionsMonitor.CurrentValue.LlmTtlSeconds;
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds);
                entry.AddExpirationToken(_invalidationTokenSource.GetChangeToken());
                _logger.LogDebug("Caching LLM response for key {CacheKey} with TTL {TtlSeconds}s", cacheKey, ttlSeconds);
                return _inner.GenerateResponseAsync(payload, cancellationToken);
            });
        }
    }
}
