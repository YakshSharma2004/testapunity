using Microsoft.Extensions.Options;
using testapi1.ApiContracts;
using testapi1.Application;
using testapi1.Domain;
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

    internal sealed class FixedPlayerTurnResolver : IPlayerTurnResolver
    {
        private readonly string _intent;
        private readonly float _confidence;

        public FixedPlayerTurnResolver(string intent = "ASK_OPEN_QUESTION", float confidence = 0.91f)
        {
            _intent = intent;
            _confidence = confidence;
        }

        public Task<ResolvedPlayerTurn> ResolveAsync(
            ProgressionTurnRequest request,
            string npcId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ResolvedPlayerTurn(
                SessionId: request.sessionId ?? string.Empty,
                Text: request.text ?? string.Empty,
                NpcId: string.IsNullOrWhiteSpace(request.npcId) ? npcId : request.npcId,
                ContextKey: request.contextKey ?? string.Empty,
                DiscussedClueIds: request.discussedClueIds,
                NormalizedText: request.text?.Trim().ToLowerInvariant() ?? string.Empty,
                Intent: new IntentResponse
                {
                    intent = _intent,
                    confidence = _confidence,
                    notes = "test-double",
                    modelVersion = "tests"
                }));
        }
    }

    internal sealed class PassThroughNormalizer : ITextNormalizer
    {
        public string NormalizeForMatch(string input)
        {
            return input?.Trim().ToLowerInvariant() ?? string.Empty;
        }
    }

    internal sealed class FixedProgressionEventMapper : IIntentToProgressionEventMapper
    {
        private readonly ProgressionEventType _eventType;
        private readonly IReadOnlyList<string> _allowedIntents;
        private readonly int? _actionId;

        public FixedProgressionEventMapper(
            ProgressionEventType eventType = ProgressionEventType.AskOpenQuestion,
            IReadOnlyList<string>? allowedIntents = null,
            int? actionId = 1)
        {
            _eventType = eventType;
            _actionId = actionId;
            _allowedIntents = allowedIntents ?? new[] { "ASK_OPEN_QUESTION", "PRESENT_EVIDENCE" };
        }

        public Task<ProgressionMappedEvent> MapAsync(
            IntentRequest request,
            IntentResponse response,
            string normalizedText,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            var mapped = new ProgressionMappedEvent(
                Event: new ProgressionEvent(
                    EventType: _eventType,
                    Intent: response.intent,
                    Confidence: response.confidence,
                    RawText: request.Text,
                    NormalizedText: normalizedText,
                    EvidenceId: null,
                    OccurredAtUtc: nowUtc),
                ActionId: _actionId);

            return Task.FromResult(mapped);
        }

        public Task<IReadOnlyList<string>> GetAllowedIntentsAsync(
            ProgressionStateId state,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_allowedIntents);
        }
    }

    internal sealed class InMemoryRuntimeRepository : IProgressionRuntimeRepository
    {
        private readonly HashSet<int> _players = new() { 1 };
        private readonly Dictionary<string, NpcRuntimeIdentity> _npcs = new(StringComparer.OrdinalIgnoreCase)
        {
            ["dylan"] = new NpcRuntimeIdentity(1, "dylan", "Dylan Cross")
        };

        private readonly Dictionary<(int PlayerId, int NpcId), PlayerNpcState> _states = new();

        public List<TurnPersistenceRecord> TurnRecords { get; } = new();
        public List<PersistedTurnRecord> PersistedTurns { get; } = new();
        public List<(int PlayerId, int NpcId, DateTimeOffset AtUtc)> EnsureCalls { get; } = new();
        public List<(int PlayerId, int NpcId, DateTimeOffset AtUtc)> TouchCalls { get; } = new();
        private long _nextInteractionId = 1;

        public void AddPlayer(int playerId)
        {
            _players.Add(playerId);
        }

        public void RemovePlayer(int playerId)
        {
            _players.Remove(playerId);
        }

        public Task<bool> PlayerExistsAsync(int playerId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_players.Contains(playerId));
        }

        public Task<NpcRuntimeIdentity?> GetNpcByCodeAsync(string npcCode, CancellationToken cancellationToken = default)
        {
            _npcs.TryGetValue(npcCode, out var npc);
            return Task.FromResult(npc);
        }

        public Task EnsurePlayerNpcStateAsync(int playerId, int npcId, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            EnsureCalls.Add((playerId, npcId, nowUtc));
            var key = (playerId, npcId);
            if (!_states.ContainsKey(key))
            {
                _states[key] = new PlayerNpcState
                {
                    PlayerId = playerId,
                    NpcId = npcId,
                    Trust = 0.50m,
                    Patience = 0.50m,
                    Curiosity = 0.50m,
                    Openness = 0.50m,
                    Memory = "initialized",
                    LastInteractionAt = nowUtc.UtcDateTime
                };
            }

            return Task.CompletedTask;
        }

        public Task<PersistedTurnRecord> PersistTurnAsync(TurnPersistenceRecord record, CancellationToken cancellationToken = default)
        {
            TurnRecords.Add(record);
            _states[(record.PlayerId, record.NpcId)] = new PlayerNpcState
            {
                PlayerId = record.PlayerId,
                NpcId = record.NpcId,
                Trust = Math.Clamp(record.TrustScore, 0, 10) / 10m,
                Patience = Math.Clamp(10 - record.ShutdownScore, 0, 10) / 10m,
                Curiosity = 0.50m,
                Openness = 0.50m,
                Memory = record.TransitionReason,
                LastInteractionAt = record.OccurredAtUtc.UtcDateTime
            };

            var persisted = new PersistedTurnRecord(
                InteractionId: _nextInteractionId++,
                PlayerId: record.PlayerId,
                NpcId: record.NpcId,
                OccurredAtUtc: record.OccurredAtUtc);
            PersistedTurns.Add(persisted);
            return Task.FromResult(persisted);
        }

        public Task TouchPlayerNpcStateAsync(int playerId, int npcId, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            TouchCalls.Add((playerId, npcId, nowUtc));
            var key = (playerId, npcId);
            if (_states.TryGetValue(key, out var state))
            {
                state.LastInteractionAt = nowUtc.UtcDateTime;
            }
            else
            {
                _states[key] = new PlayerNpcState
                {
                    PlayerId = playerId,
                    NpcId = npcId,
                    Trust = 0.50m,
                    Patience = 0.50m,
                    Curiosity = 0.50m,
                    Openness = 0.50m,
                    Memory = "initialized",
                    LastInteractionAt = nowUtc.UtcDateTime
                };
            }

            return Task.CompletedTask;
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
