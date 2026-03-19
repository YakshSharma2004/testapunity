using testapi1.Domain.Progression;

namespace testapi1.Application
{
    public interface IProgressionSessionStore
    {
        Task<ProgressionSessionState?> GetAsync(string sessionId, CancellationToken cancellationToken = default);
        Task SetAsync(ProgressionSessionState state, CancellationToken cancellationToken = default);
        Task<bool> RemoveAsync(string sessionId, CancellationToken cancellationToken = default);
    }
}
