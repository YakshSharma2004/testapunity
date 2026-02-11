using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using testapi1.Application;

namespace testapi1.Services.Redis
{
    /// <summary>
    /// Lightweight placeholder implementation that routes through IDistributedCache.
    /// Works with Redis when available and safely degrades when Redis is offline.
    /// </summary>
    public class DistributedCacheRedisPlaceholderStore : IRedisPlaceholderStore
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<DistributedCacheRedisPlaceholderStore> _logger;

        public DistributedCacheRedisPlaceholderStore(
            IDistributedCache cache,
            ILogger<DistributedCacheRedisPlaceholderStore> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task SetStringAsync(string key, string value, int ttlSeconds, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key) || ttlSeconds <= 0)
            {
                return;
            }

            try
            {
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
                };

                await _cache.SetStringAsync(key, value, options, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis placeholder write failed for key {CacheKey}", key);
            }
        }

        public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            try
            {
                return await _cache.GetStringAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis placeholder read failed for key {CacheKey}", key);
                return null;
            }
        }
    }
}
