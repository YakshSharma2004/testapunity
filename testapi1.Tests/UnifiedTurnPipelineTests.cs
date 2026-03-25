using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using testapi1.ApiContracts;
using testapi1.Application;
using testapi1.Domain.Progression;
using testapi1.Services.Dialogue;
using testapi1.Services.Llm;
using testapi1.Services.Progression;
using testapi1.Services.Turns;
using testapi1.Tests.TestSupport;

namespace testapi1.Tests
{
    public sealed class UnifiedTurnPipelineTests
    {
        [Fact]
        public async Task Start_ClueClick_Turn_Uses_Updated_DiscussedClues_For_SameTurn_Reply()
        {
            var sessionStore = new InMemorySessionStore();
            var runtimeRepository = new InMemoryRuntimeRepository();
            var classifier = new CountingIntentClassifier();
            var resolver = new PlayerTurnResolver(new PassThroughNormalizer(), classifier);
            var progressionService = new GameProgressionService(
                engine: new DylanProgressionEngine(),
                sessionStore: sessionStore,
                turnResolver: resolver,
                eventMapper: new FixedProgressionEventMapper(),
                progressionOptions: new StaticOptionsMonitor<ProgressionOptions>(new ProgressionOptions()),
                runtimeRepository: runtimeRepository);
            var retrieval = new SessionBackedRetrievalService(sessionStore);
            var llm = new RecordingLlmService();
            var dialogueService = new NpcDialogueService(
                retrieval,
                llm,
                resolver,
                new StaticOptionsMonitor<LlmOptions>(new LlmOptions()),
                NullLogger<NpcDialogueService>.Instance);
            var orchestrator = new PlayerTurnOrchestrator(sessionStore, resolver, progressionService, dialogueService);

            var start = await progressionService.StartSessionAsync(new StartProgressionRequest());
            var click = await progressionService.ApplyClueClickAsync(new ProgressionClueClickRequest
            {
                sessionId = start.sessionId,
                clueId = "elsa_email_draft",
                source = "desk"
            });

            Assert.NotNull(click);

            var turn = await orchestrator.ApplyAsync(new ProgressionTurnRequest
            {
                sessionId = start.sessionId,
                text = "Talk to me about Elsa's email.",
                discussedClueIds = new List<string> { "elsa_email_draft" }
            });

            Assert.NotNull(turn);
            Assert.Equal("Tell me exactly what you think the email proves.", turn!.replyText);
            Assert.Equal(1, classifier.CallCount);
            Assert.Contains("elsa_email_draft", turn.snapshot.discussedClueIds);
            Assert.Single(runtimeRepository.PersistedTurns);
            Assert.Single(retrieval.PersistedRecords);
            Assert.Equal(runtimeRepository.PersistedTurns[0].InteractionId, retrieval.PersistedRecords[0].InteractionId);

            Assert.NotNull(llm.LastPayload);
            using var document = JsonDocument.Parse(llm.LastPayload!.promptText);
            var allowedTopics = document.RootElement
                .GetProperty("progression")
                .GetProperty("allowedTopics")
                .EnumerateArray()
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();

            Assert.Contains("topic_email", allowedTopics);
        }

        private sealed class CountingIntentClassifier : IIntentClassifier
        {
            public int CallCount { get; private set; }

            public Task<IntentResponse> ClassifyAsync(IntentRequest request, CancellationToken cancellationToken = default)
            {
                CallCount++;
                return Task.FromResult(new IntentResponse
                {
                    intent = "ASK_OPEN_QUESTION",
                    confidence = 0.93f,
                    notes = "counting-double",
                    modelVersion = "tests"
                });
            }
        }

        private sealed class RecordingLlmService : ILLMService
        {
            public LlmPromptPayload? LastPayload { get; private set; }

            public Task<LlmRawResponse> GenerateResponseAsync(
                LlmPromptPayload payload,
                CancellationToken cancellationToken = default)
            {
                LastPayload = payload;
                return Task.FromResult(new LlmRawResponse
                {
                    responseText = """{"replyText":"Tell me exactly what you think the email proves.","allowedTopicsUsed":["topic_email"]}""",
                    modelName = "local-model",
                    provider = "local",
                    finishReason = "stop"
                });
            }
        }

        private sealed class SessionBackedRetrievalService : IRetrievalService
        {
            private readonly InMemorySessionStore _sessionStore;

            public SessionBackedRetrievalService(InMemorySessionStore sessionStore)
            {
                _sessionStore = sessionStore;
            }

            public List<NpcReplyPersistenceRecord> PersistedRecords { get; } = new();

            public async Task<NpcDialogueWorldContext?> GetNpcDialogueContextAsync(
                string sessionId,
                CancellationToken cancellationToken = default)
            {
                var session = await _sessionStore.GetAsync(sessionId, cancellationToken);
                if (session is null)
                {
                    return null;
                }

                var allowedTopics = ResolveAllowedTopics(session);
                return new NpcDialogueWorldContext(
                    SessionId: session.SessionId,
                    PlayerId: session.PlayerId,
                    PlayerName: "Casey",
                    NpcDbId: 1,
                    NpcId: session.NpcId,
                    NpcName: "Dylan Cross",
                    Progression: session,
                    PublicStory: "Dylan insists the night was uneventful.",
                    TruthSummary: "Dylan is lying about Elsa and the evidence trail.",
                    Timeline: new[] { "Arrived at 8:00 PM.", "Left after the argument." },
                    AllowedTopics: allowedTopics,
                    TopicGuidance: allowedTopics.ToDictionary(item => item, item => $"guidance:{item}"),
                    Relationship: new RelationshipSnapshot(0.4m, 0.5m, 0.5m, 0.3m, "tense"),
                    RecentExchanges: Array.Empty<ConversationExchange>(),
                    LoreSnippets: Array.Empty<LoreSnippet>());
            }

            public Task PersistNpcReplyAsync(
                NpcReplyPersistenceRecord record,
                CancellationToken cancellationToken = default)
            {
                PersistedRecords.Add(record);
                return Task.CompletedTask;
            }

            private static IReadOnlyList<string> ResolveAllowedTopics(ProgressionSessionState session)
            {
                var topics = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "public_story"
                };

                if (session.State != ProgressionStateId.Intro)
                {
                    foreach (var clueId in session.DiscussedClues)
                    {
                        topics.Add(ClueCatalog.ToUnlockTopic(clueId));
                    }
                }

                if (session.State is ProgressionStateId.BuildingCase or ProgressionStateId.ConfessionWindow or ProgressionStateId.Confession)
                {
                    foreach (var clueId in session.DiscoveredClues)
                    {
                        topics.Add(ClueCatalog.ToUnlockTopic(clueId));
                    }
                }

                if (session.State == ProgressionStateId.Confession)
                {
                    topics.Add("truth");
                }

                return topics.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
    }
}
