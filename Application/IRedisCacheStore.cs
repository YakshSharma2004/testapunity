using System.Threading;
using System.Threading.Tasks;

namespace testapi1.Application
{
    /// <summary>
    /// Redis cache store abstraction used by runtime services.
    /// </summary>
    public interface IRedisCacheStore
    {
        Task SetStringAsync(string key, string value, int ttlSeconds, CancellationToken cancellationToken = default);
        Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);
    }
}
