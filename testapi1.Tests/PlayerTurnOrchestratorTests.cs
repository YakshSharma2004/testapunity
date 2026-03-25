using testapi1.ApiContracts;
using testapi1.Application;
using testapi1.Domain.Progression;
using testapi1.Services.Turns;
using testapi1.Tests.TestSupport;

namespace testapi1.Tests
{
    public sealed class PlayerTurnOrchestratorTests
    {
        [Fact]
        public async Task ApplyAsync_ClassifiesOnce_AndPasses_PersistedTurn_To_Dialogue()
        {
            var sessionStore = new InMemorySessionStore();
            await sessionStore.SetAsync(BuildSession());

            var classifier = new CountingIntentClassifier("ASK_OPEN_QUESTION", 0.87f);
            var resolver = new PlayerTurnResolver(new PassThroughNormalizer(), classifier);
            var progression = new StubResolvedProgressionTurnService();
            var dialogue = new StubResolvedNpcDialogueService();
            var orchestrator = new PlayerTurnOrchestrator(sessionStore, resolver, progression, dialogue);

            var response = await orchestrator.ApplyAsync(new ProgressionTurnRequest
            {
                sessionId = "ps_11111111111111111111111111111111",
                text = "Talk to me.",
                discussedClueIds = new List<string> { "elsa_email_draft" }
            });

            Assert.NotNull(response);
            Assert.Equal("Stay with the facts.", response!.replyText);
            Assert.Equal(1, classifier.CallCount);
            Assert.NotNull(progression.LastTurn);
            Assert.Equal("talk to me.", progression.LastTurn!.NormalizedText);
            Assert.NotNull(dialogue.LastPersistedTurn);
            Assert.Equal(42, dialogue.LastPersistedTurn!.InteractionId);
        }

        private static ProgressionSessionState BuildSession()
        {
            return new ProgressionSessionState(
                SessionId: "ps_11111111111111111111111111111111",
                PlayerId: 1,
                CaseId: "dylan-interrogation",
                NpcId: "dylan",
                State: ProgressionStateId.Intro,
                TurnCount: 0,
                TrustScore: 0,
                ShutdownScore: 0,
                IsTerminal: false,
                Ending: CaseEndingType.None,
                PresentedEvidence: Array.Empty<EvidenceId>(),
                DiscoveredClues: Array.Empty<ClueId>(),
                DiscussedClues: Array.Empty<ClueId>(),
                ClueClickHistory: Array.Empty<ClueClickHistoryEntry>(),
                ComposureState: ComposureState.Calm,
                ProofTier: ProofTier.None,
                CanConfess: false,
                History: Array.Empty<ProgressionHistoryEntry>(),
                LastTransitionReason: "seed",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                UpdatedAtUtc: DateTimeOffset.UtcNow);
        }

        private sealed class CountingIntentClassifier : IIntentClassifier
        {
            private readonly string _intent;
            private readonly float _confidence;

            public CountingIntentClassifier(string intent, float confidence)
            {
                _intent = intent;
                _confidence = confidence;
            }

            public int CallCount { get; private set; }

            public Task<IntentResponse> ClassifyAsync(IntentRequest request, CancellationToken cancellationToken = default)
            {
                CallCount++;
                return Task.FromResult(new IntentResponse
                {
                    intent = _intent,
                    confidence = _confidence,
                    notes = "counting-double",
                    modelVersion = "tests"
                });
            }
        }

        private sealed class StubResolvedProgressionTurnService : IResolvedProgressionTurnService
        {
            public ResolvedPlayerTurn? LastTurn { get; private set; }

            public Task<ProgressionTurnExecutionResult?> ApplyResolvedTurnAsync(
                ResolvedPlayerTurn turn,
                CancellationToken cancellationToken = default)
            {
                LastTurn = turn;
                return Task.FromResult<ProgressionTurnExecutionResult?>(new ProgressionTurnExecutionResult(
                    new ProgressionTurnResponse
                    {
                        sessionId = turn.SessionId,
                        snapshot = new ProgressionSnapshotResponse
                        {
                            sessionId = turn.SessionId,
                            state = "InformationGathering"
                        }
                    },
                    new PersistedTurnRecord(42, 1, 1, DateTimeOffset.UtcNow)));
            }
        }

        private sealed class StubResolvedNpcDialogueService : IResolvedNpcDialogueService
        {
            public PersistedTurnRecord? LastPersistedTurn { get; private set; }

            public Task<NpcDialogueResponse?> GenerateAsync(
                NpcDialogueRequest request,
                ResolvedPlayerTurn turn,
                PersistedTurnRecord? persistedTurn,
                CancellationToken cancellationToken = default)
            {
                LastPersistedTurn = persistedTurn;
                return Task.FromResult<NpcDialogueResponse?>(new NpcDialogueResponse
                {
                    sessionId = request.sessionId,
                    replyText = "Stay with the facts."
                });
            }
        }
    }
}
