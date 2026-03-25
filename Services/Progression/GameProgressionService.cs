using Microsoft.Extensions.Options;
using testapi1.ApiContracts;
using testapi1.Application;
using testapi1.Domain.Progression;

namespace testapi1.Services.Progression
{
    public sealed class GameProgressionService : IGameProgressionService, IResolvedProgressionTurnService
    {
        private readonly IGameProgressionEngine _engine;
        private readonly IProgressionSessionStore _sessionStore;
        private readonly IPlayerTurnResolver _turnResolver;
        private readonly IIntentToProgressionEventMapper _eventMapper;
        private readonly IOptionsMonitor<ProgressionOptions> _progressionOptions;
        private readonly IProgressionRuntimeRepository _runtimeRepository;

        public GameProgressionService(
            IGameProgressionEngine engine,
            IProgressionSessionStore sessionStore,
            IPlayerTurnResolver turnResolver,
            IIntentToProgressionEventMapper eventMapper,
            IOptionsMonitor<ProgressionOptions> progressionOptions,
            IProgressionRuntimeRepository runtimeRepository)
        {
            _engine = engine;
            _sessionStore = sessionStore;
            _turnResolver = turnResolver;
            _eventMapper = eventMapper;
            _progressionOptions = progressionOptions;
            _runtimeRepository = runtimeRepository;
        }

        public async Task<StartProgressionResponse> StartSessionAsync(
            StartProgressionRequest request,
            CancellationToken cancellationToken = default)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var sessionId = string.IsNullOrWhiteSpace(request.sessionId)
                ? ProgressionSessionId.NewId()
                : request.sessionId.Trim().ToLowerInvariant();

            var playerId = request.playerId.GetValueOrDefault(1);
            if (playerId <= 0)
            {
                throw new InvalidOperationException("playerId must be a positive integer.");
            }

            var playerExists = await _runtimeRepository.PlayerExistsAsync(playerId, cancellationToken);
            if (!playerExists)
            {
                throw new InvalidOperationException($"Player '{playerId}' was not found.");
            }

            var caseId = string.IsNullOrWhiteSpace(request.caseId)
                ? "dylan-interrogation"
                : request.caseId.Trim();
            var npcCode = string.IsNullOrWhiteSpace(request.npcId)
                ? "dylan"
                : request.npcId.Trim().ToLowerInvariant();

            var npc = await _runtimeRepository.GetNpcByCodeAsync(npcCode, cancellationToken);
            if (npc is null)
            {
                throw new InvalidOperationException($"NPC '{npcCode}' was not found.");
            }

            await _runtimeRepository.EnsurePlayerNpcStateAsync(playerId, npc.NpcId, nowUtc, cancellationToken);

            var initialState = _engine.CreateInitialState(
                sessionId: sessionId,
                playerId: playerId,
                caseId: caseId,
                npcId: npc.NpcCode,
                nowUtc: nowUtc);

            initialState = CaseProgressionEvaluator.Recalculate(initialState, GetConfessionRequiredClues());
            await _sessionStore.SetAsync(initialState, cancellationToken);

            return new StartProgressionResponse
            {
                sessionId = sessionId,
                snapshot = await ToSnapshotAsync(initialState, cancellationToken)
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

            var resolvedTurn = await _turnResolver.ResolveAsync(request, session.NpcId, cancellationToken);
            var execution = await ApplyResolvedTurnAsync(resolvedTurn, cancellationToken);
            return execution?.Response;
        }

        public async Task<ProgressionTurnExecutionResult?> ApplyResolvedTurnAsync(
            ResolvedPlayerTurn turn,
            CancellationToken cancellationToken = default)
        {
            var session = await _sessionStore.GetAsync(turn.SessionId, cancellationToken);
            if (session is null)
            {
                return null;
            }

            var npc = await _runtimeRepository.GetNpcByCodeAsync(session.NpcId, cancellationToken);
            if (npc is null)
            {
                throw new InvalidOperationException($"NPC '{session.NpcId}' was not found.");
            }

            var intentRequest = new IntentRequest
            {
                Text = turn.Text,
                NpcId = turn.NpcId,
                ContextKey = turn.ContextKey
            };

            var mapped = await _eventMapper.MapAsync(
                intentRequest,
                turn.Intent,
                turn.NormalizedText,
                DateTimeOffset.UtcNow,
                cancellationToken);

            var sessionWithDiscussion = ApplyDiscussedClues(
                session,
                turn.DiscussedClueIds,
                mapped.Event.OccurredAtUtc);

            var sessionWithGateUpdates = CaseProgressionEvaluator.Recalculate(
                sessionWithDiscussion,
                GetConfessionRequiredClues());

            var transition = _engine.Apply(sessionWithGateUpdates, mapped.Event);
            var finalState = CaseProgressionEvaluator.Recalculate(transition.State, GetConfessionRequiredClues());

            await _sessionStore.SetAsync(finalState, cancellationToken);

            var persistedTurn = await _runtimeRepository.PersistTurnAsync(
                new TurnPersistenceRecord(
                    PlayerId: finalState.PlayerId,
                    NpcId: npc.NpcId,
                    ActionId: mapped.ActionId,
                    OccurredAtUtc: mapped.Event.OccurredAtUtc,
                    IntentCode: turn.Intent.intent ?? "unknown",
                    EventType: mapped.Event.EventType.ToString(),
                    PlayerText: turn.Text,
                    TransitionReason: transition.Reason,
                    ComposureState: finalState.ComposureState.ToString(),
                    ModelVersion: turn.Intent.modelVersion ?? "unknown",
                    TrustScore: finalState.TrustScore,
                    ShutdownScore: finalState.ShutdownScore,
                    TurnCount: finalState.TurnCount),
                cancellationToken);

            var response = new ProgressionTurnResponse
            {
                sessionId = finalState.SessionId,
                replyText = string.Empty,
                intent = turn.Intent.intent ?? "unknown",
                confidence = turn.Intent.confidence,
                eventType = mapped.Event.EventType.ToString(),
                evidenceId = mapped.Event.EvidenceId?.ToString() ?? string.Empty,
                transitioned = transition.Transitioned,
                transitionReason = transition.Reason,
                snapshot = await ToSnapshotAsync(finalState, cancellationToken)
            };

            return new ProgressionTurnExecutionResult(response, persistedTurn);
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

            var npc = await _runtimeRepository.GetNpcByCodeAsync(updatedState.NpcId, cancellationToken);
            if (npc is null)
            {
                throw new InvalidOperationException($"NPC '{updatedState.NpcId}' was not found.");
            }

            await _runtimeRepository.TouchPlayerNpcStateAsync(
                updatedState.PlayerId,
                npc.NpcId,
                nowUtc,
                cancellationToken);

            return new ProgressionClueClickResponse
            {
                sessionId = updatedState.SessionId,
                clueId = ClueCatalog.ToKey(clueId),
                unlockTopic = ClueCatalog.ToUnlockTopic(clueId),
                isFirstDiscovery = isFirstDiscovery,
                applied = isFirstDiscovery,
                reason = isFirstDiscovery ? "clue-discovered" : "duplicate-click",
                snapshot = await ToSnapshotAsync(updatedState, cancellationToken)
            };
        }

        public async Task<ProgressionSnapshotResponse?> GetSnapshotAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            var state = await _sessionStore.GetAsync(sessionId, cancellationToken);
            return state is null ? null : await ToSnapshotAsync(state, cancellationToken);
        }

        private async Task<ProgressionSnapshotResponse> ToSnapshotAsync(
            ProgressionSessionState state,
            CancellationToken cancellationToken)
        {
            var allowedIntents = await _eventMapper.GetAllowedIntentsAsync(state.State, cancellationToken);

            return new ProgressionSnapshotResponse
            {
                sessionId = state.SessionId,
                playerId = state.PlayerId,
                caseId = state.CaseId,
                npcId = state.NpcId,
                state = state.State.ToString(),
                turnCount = state.TurnCount,
                trustScore = state.TrustScore,
                shutdownScore = state.ShutdownScore,
                isTerminal = state.IsTerminal,
                ending = state.Ending.ToString(),
                allowedIntents = allowedIntents.ToList(),
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
