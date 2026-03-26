using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testapi1.Application;
using testapi1.ApiContracts;

namespace testapi1.Services.Caching
{
    public class CachedLlmService : ILLMService
    {
        private readonly ILLMService _inner;
        private readonly IDistributedCache _cache;
        private readonly ITextNormalizer _textNormalizer;
        private readonly IOptionsMonitor<ApiCacheOptions> _optionsMonitor;
        private readonly ILogger<CachedLlmService> _logger;

        public CachedLlmService(
            ILLMService inner,
            IDistributedCache cache,
            ITextNormalizer textNormalizer,
            IOptionsMonitor<ApiCacheOptions> optionsMonitor,
            ILogger<CachedLlmService> logger)
        {
            _inner = inner;
            _cache = cache;
            _textNormalizer = textNormalizer;
            _optionsMonitor = optionsMonitor;
            _logger = logger;
        }

        public async Task<LlmRawResponse> GenerateResponseAsync(LlmPromptPayload payload, CancellationToken cancellationToken = default)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var normalized = _textNormalizer.NormalizeForMatch(payload.promptText);
            var npcKey = payload.npcId ?? "";
            var contextKey = payload.contextKey ?? "";
            var conversationKey = payload.conversationId ?? "";
            var systemContextKey = payload.systemContext ?? "";
            var modelVersion = _optionsMonitor.CurrentValue.ModelVersion ?? "";
            var cacheKey = $"llm:{modelVersion}:{normalized}:{npcKey}:{contextKey}:{conversationKey}:{systemContextKey}:{payload.maxTokens}";

            try
            {
                var cachedValue = await _cache.GetStringAsync(cacheKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(cachedValue))
                {
                    _logger.LogDebug("LLM cache hit for key {CacheKey}", cacheKey);
                    return JsonSerializer.Deserialize<LlmRawResponse>(cachedValue) ?? new LlmRawResponse();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LLM cache get failed for key {CacheKey}. Falling back to LLM service.", cacheKey);
            }

            var response = await _inner.GenerateResponseAsync(payload, cancellationToken);
            var ttlSeconds = _optionsMonitor.CurrentValue.LlmTtlSeconds;

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
                    _logger.LogDebug("Caching LLM response for key {CacheKey} with TTL {TtlSeconds}s", cacheKey, ttlSeconds);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LLM cache set failed for key {CacheKey}. Returning LLM response.", cacheKey);
                }
            }

            return response;
        }
    }
}
