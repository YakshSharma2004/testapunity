using testapi1.Application;
using testapi1.Contracts;
using testapi1.Domain.Progression;

namespace testapi1.Services.Progression
{
    public sealed class GameProgressionService : IGameProgressionService
    {
        private readonly IGameProgressionEngine _engine;
        private readonly IProgressionSessionStore _sessionStore;
        private readonly IIntentClassifier _intentClassifier;
        private readonly IIntentToProgressionEventMapper _eventMapper;
        private readonly ITextNormalizer _normalizer;

        public GameProgressionService(
            IGameProgressionEngine engine,
            IProgressionSessionStore sessionStore,
            IIntentClassifier intentClassifier,
            IIntentToProgressionEventMapper eventMapper,
            ITextNormalizer normalizer)
        {
            _engine = engine;
            _sessionStore = sessionStore;
            _intentClassifier = intentClassifier;
            _eventMapper = eventMapper;
            _normalizer = normalizer;
        }

        public async Task<StartProgressionResponse> StartSessionAsync(
            StartProgressionRequest request,
            CancellationToken cancellationToken = default)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var sessionId = string.IsNullOrWhiteSpace(request.sessionId)
                ? Guid.NewGuid().ToString("N")
                : request.sessionId.Trim();

            var caseId = string.IsNullOrWhiteSpace(request.caseId)
                ? "dylan-interrogation"
                : request.caseId.Trim();
            var npcId = string.IsNullOrWhiteSpace(request.npcId)
                ? "dylan"
                : request.npcId.Trim();

            var initialState = _engine.CreateInitialState(sessionId, caseId, npcId, nowUtc);
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
            var transition = _engine.Apply(session, progressionEvent);

            await _sessionStore.SetAsync(transition.State, cancellationToken);

            return new ProgressionTurnResponse
            {
                sessionId = transition.State.SessionId,
                intent = intent.intent,
                confidence = intent.confidence,
                eventType = progressionEvent.EventType.ToString(),
                evidenceId = progressionEvent.EvidenceId?.ToString() ?? string.Empty,
                transitioned = transition.Transitioned,
                transitionReason = transition.Reason,
                snapshot = ToSnapshot(transition.State)
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
                lastTransitionReason = state.LastTransitionReason
            };
        }
    }
}
