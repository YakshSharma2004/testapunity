using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using testapi1.Application;
using testapi1.Domain.Progression;

namespace testapi1.Services.Progression
{
    public sealed class InMemoryProgressionSessionStore : IProgressionSessionStore
    {
        private readonly ConcurrentDictionary<string, StoreEntry> _sessions = new(StringComparer.OrdinalIgnoreCase);
        private readonly IOptionsMonitor<ProgressionOptions> _optionsMonitor;

        public InMemoryProgressionSessionStore(IOptionsMonitor<ProgressionOptions> optionsMonitor)
        {
            _optionsMonitor = optionsMonitor;
        }

        public Task<ProgressionSessionState?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return Task.FromResult<ProgressionSessionState?>(null);
            }

            if (!_sessions.TryGetValue(sessionId, out var entry))
            {
                return Task.FromResult<ProgressionSessionState?>(null);
            }

            if (entry.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                _sessions.TryRemove(sessionId, out _);
                return Task.FromResult<ProgressionSessionState?>(null);
            }

            return Task.FromResult<ProgressionSessionState?>(entry.State);
        }

        public Task SetAsync(ProgressionSessionState state, CancellationToken cancellationToken = default)
        {
            var ttlMinutes = Math.Max(1, _optionsMonitor.CurrentValue.SessionTtlMinutes);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(ttlMinutes);

            _sessions[state.SessionId] = new StoreEntry(state, expiresAt);
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_sessions.TryRemove(sessionId, out _));
        }

        private sealed record StoreEntry(ProgressionSessionState State, DateTimeOffset ExpiresAtUtc);
    }
}
