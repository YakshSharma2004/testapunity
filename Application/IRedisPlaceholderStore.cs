using System.Threading;
using System.Threading.Tasks;

namespace testapi1.Application
{
    /// <summary>
    /// Placeholder abstraction for future Redis-backed features (session state, rate limiting, locks, etc).
    /// </summary>
    public interface IRedisPlaceholderStore
    {
        Task SetStringAsync(string key, string value, int ttlSeconds, CancellationToken cancellationToken = default);
        Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);
    }
}
