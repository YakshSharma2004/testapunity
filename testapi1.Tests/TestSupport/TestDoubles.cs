using Microsoft.Extensions.Options;
using testapi1.ApiContracts;
using testapi1.Application;
using testapi1.Domain.Progression;

namespace testapi1.Tests.TestSupport
{
    internal sealed class InMemorySessionStore : IProgressionSessionStore
    {
        private readonly Dictionary<string, ProgressionSessionState> _states = new(StringComparer.OrdinalIgnoreCase);

        public Task<ProgressionSessionState?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            _states.TryGetValue(sessionId, out var state);
            return Task.FromResult(state);
        }

        public Task SetAsync(ProgressionSessionState state, CancellationToken cancellationToken = default)
        {
            _states[state.SessionId] = state;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_states.Remove(sessionId));
        }
    }

    internal sealed class FixedIntentClassifier : IIntentClassifier
    {
        private readonly string _intent;
        private readonly float _confidence;

        public FixedIntentClassifier(string intent = "ASK_OPEN_QUESTION", float confidence = 0.91f)
        {
            _intent = intent;
            _confidence = confidence;
        }

        public Task<IntentResponse> ClassifyAsync(IntentRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IntentResponse
            {
                intent = _intent,
                confidence = _confidence,
                notes = "test-double",
                modelVersion = "tests"
            });
        }
    }

    internal sealed class PassThroughNormalizer : ITextNormalizer
    {
        public string NormalizeForMatch(string input)
        {
            return input?.Trim().ToLowerInvariant() ?? string.Empty;
        }
    }

    internal sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
