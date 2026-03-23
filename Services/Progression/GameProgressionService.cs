using testapi1.Application;
using testapi1.ApiContracts;
using testapi1.Domain.Progression;
using Microsoft.Extensions.Options;

namespace testapi1.Services.Progression
{
    public sealed class GameProgressionService : IGameProgressionService
    {
        private readonly IGameProgressionEngine _engine;
        private readonly IProgressionSessionStore _sessionStore;
        private readonly IIntentClassifier _intentClassifier;
        private readonly IIntentToProgressionEventMapper _eventMapper;
        private readonly ITextNormalizer _normalizer;
        private readonly IOptionsMonitor<ProgressionOptions> _progressionOptions;

        public GameProgressionService(
            IGameProgressionEngine engine,
            IProgressionSessionStore sessionStore,
            IIntentClassifier intentClassifier,
            IIntentToProgressionEventMapper eventMapper,
            ITextNormalizer normalizer,
            IOptionsMonitor<ProgressionOptions> progressionOptions)
        {
            _engine = engine;
            _sessionStore = sessionStore;
            _intentClassifier = intentClassifier;
            _eventMapper = eventMapper;
            _normalizer = normalizer;
            _progressionOptions = progressionOptions;
        }

        public async Task<StartProgressionResponse> StartSessionAsync(
            StartProgressionRequest request,
            CancellationToken cancellationToken = default)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var sessionId = string.IsNullOrWhiteSpace(request.sessionId)
                ? ProgressionSessionId.NewId()
                : request.sessionId.Trim().ToLowerInvariant();

            var caseId = string.IsNullOrWhiteSpace(request.caseId)
                ? "dylan-interrogation"
                : request.caseId.Trim();
            var npcId = string.IsNullOrWhiteSpace(request.npcId)
                ? "dylan"
                : request.npcId.Trim();

            var initialState = _engine.CreateInitialState(sessionId, caseId, npcId, nowUtc);
            initialState = CaseProgressionEvaluator.Recalculate(initialState, GetConfessionRequiredClues());
            await _sessionStore.SetAsync(initialState, cancellationToken);

            return new StartProgressionResponse
            {
                sessionId = sessionId,
                snapshot = ToSnapshot(initialState)
            };
        }

        public async Task<ProgressionTurnResponse?> ApplyTurnAsync(
            ProgressionTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            var session = await _sessionStore.GetAsync(request.sessionId, cancellationToken);
            if (session is null)
            {
                return null;
            }

            var intentRequest = new IntentRequest
            {
                Text = request.text ?? string.Empty,
                NpcId = string.IsNullOrWhiteSpace(request.npcId) ? session.NpcId : request.npcId,
                ContextKey = request.contextKey ?? string.Empty
            };

            var intent = await _intentClassifier.ClassifyAsync(intentRequest, cancellationToken);
            var normalizedText = _normalizer.NormalizeForMatch(intentRequest.Text);
            var progressionEvent = _eventMapper.Map(intentRequest, intent, normalizedText, DateTimeOffset.UtcNow);
            var sessionWithDiscussion = ApplyDiscussedClues(session, request.discussedClueIds, progressionEvent.OccurredAtUtc);
            var sessionWithGateUpdates = CaseProgressionEvaluator.Recalculate(sessionWithDiscussion, GetConfessionRequiredClues());
            var transition = _engine.Apply(sessionWithGateUpdates, progressionEvent);
            var finalState = CaseProgressionEvaluator.Recalculate(transition.State, GetConfessionRequiredClues());

            await _sessionStore.SetAsync(finalState, cancellationToken);

            return new ProgressionTurnResponse
            {
                sessionId = finalState.SessionId,
                intent = intent.intent,
                confidence = intent.confidence,
                eventType = progressionEvent.EventType.ToString(),
                evidenceId = progressionEvent.EvidenceId?.ToString() ?? string.Empty,
                transitioned = transition.Transitioned,
                transitionReason = transition.Reason,
                snapshot = ToSnapshot(finalState)
            };
        }

        public async Task<ProgressionClueClickResponse?> ApplyClueClickAsync(
            ProgressionClueClickRequest request,
            CancellationToken cancellationToken = default)
        {
            var session = await _sessionStore.GetAsync(request.sessionId, cancellationToken);
            if (session is null)
            {
                return null;
            }

            if (!ClueCatalog.TryParseKey(request.clueId, out var clueId))
            {
                throw new InvalidOperationException($"Unknown clueId '{request.clueId}'.");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var discovered = session.DiscoveredClues.ToHashSet();
            var isFirstDiscovery = discovered.Add(clueId);

            var clueClicks = session.ClueClickHistory.ToList();
            clueClicks.Add(new ClueClickHistoryEntry(
                ClueId: clueId,
                IsFirstDiscovery: isFirstDiscovery,
                Source: request.source ?? string.Empty,
                ClueName: string.IsNullOrWhiteSpace(request.clueName)
                    ? ClueCatalog.ToDisplayName(clueId)
                    : request.clueName.Trim(),
                OccurredAtUtc: nowUtc));

            var updatedState = session with
            {
                DiscoveredClues = discovered.ToList(),
                ClueClickHistory = clueClicks,
                LastTransitionReason = isFirstDiscovery ? "clue-discovered" : "duplicate-click",
                UpdatedAtUtc = nowUtc
            };

            updatedState = CaseProgressionEvaluator.Recalculate(updatedState, GetConfessionRequiredClues());
            await _sessionStore.SetAsync(updatedState, cancellationToken);

            return new ProgressionClueClickResponse
            {
                sessionId = updatedState.SessionId,
                clueId = ClueCatalog.ToKey(clueId),
                unlockTopic = ClueCatalog.ToUnlockTopic(clueId),
                isFirstDiscovery = isFirstDiscovery,
                applied = isFirstDiscovery,
                reason = isFirstDiscovery ? "clue-discovered" : "duplicate-click",
                snapshot = ToSnapshot(updatedState)
            };
        }

        public async Task<ProgressionSnapshotResponse?> GetSnapshotAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            var state = await _sessionStore.GetAsync(sessionId, cancellationToken);
            return state is null ? null : ToSnapshot(state);
        }

        private ProgressionSnapshotResponse ToSnapshot(ProgressionSessionState state)
        {
            return new ProgressionSnapshotResponse
            {
                sessionId = state.SessionId,
                caseId = state.CaseId,
                npcId = state.NpcId,
                state = state.State.ToString(),
                turnCount = state.TurnCount,
                trustScore = state.TrustScore,
                shutdownScore = state.ShutdownScore,
                isTerminal = state.IsTerminal,
                ending = state.Ending.ToString(),
                allowedIntents = _engine.GetAllowedIntents(state).ToList(),
                evidencePresented = state.PresentedEvidence.Select(item => item.ToString()).OrderBy(value => value).ToList(),
                discoveredClueIds = state.DiscoveredClues.Select(ClueCatalog.ToKey).OrderBy(value => value).ToList(),
                discussedClueIds = state.DiscussedClues.Select(ClueCatalog.ToKey).OrderBy(value => value).ToList(),
                canConfess = state.CanConfess,
                proofTier = state.ProofTier.ToString(),
                composureState = state.ComposureState.ToString(),
                lastTransitionReason = state.LastTransitionReason
            };
        }

        private IReadOnlyCollection<ClueId> GetConfessionRequiredClues()
        {
            var configured = _progressionOptions.CurrentValue.ConfessionRequiredClues ?? new List<string>();
            var parsed = configured
                .Select(value => ClueCatalog.TryParseKey(value, out var clueId) ? clueId : (ClueId?)null)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .Distinct()
                .ToList();

            if (parsed.Count > 0)
            {
                return parsed;
            }

            return new[]
            {
                ClueId.ElsaEmailDraft,
                ClueId.PayrollReport,
                ClueId.MeetingNote,
                ClueId.EntryLog,
                ClueId.PinNote,
                ClueId.CleanupItem,
                ClueId.WeaponFound,
                ClueId.FlashDrive
            };
        }

        private static ProgressionSessionState ApplyDiscussedClues(
            ProgressionSessionState state,
            IReadOnlyCollection<string>? discussedClueIds,
            DateTimeOffset nowUtc)
        {
            if (discussedClueIds is null || discussedClueIds.Count == 0)
            {
                return state;
            }

            var discussed = state.DiscussedClues.ToHashSet();
            var changed = false;

            foreach (var token in discussedClueIds)
            {
                if (ClueCatalog.TryParseKey(token, out var clueId))
                {
                    changed |= discussed.Add(clueId);
                }
            }

            if (!changed)
            {
                return state;
            }

            return state with
            {
                DiscussedClues = discussed.ToList(),
                UpdatedAtUtc = nowUtc
            };
        }
    }
}
