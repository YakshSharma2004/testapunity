using Microsoft.Extensions.Logging.Abstractions;
using testapi1.ApiContracts;
using testapi1.Application;
using testapi1.Domain.Progression;
using testapi1.Services.Dialogue;
using testapi1.Services.Llm;
using testapi1.Tests.TestSupport;

namespace testapi1.Tests
{
    public sealed class NpcDialogueServiceTests
    {
        [Fact]
        public async Task GenerateAsync_ReturnsGuardrailFallback_WhenModelConfessesOutsideConfession()
        {
            var world = BuildWorldContext(ProgressionStateId.BuildingCase, new[] { "public_story", "topic_email" });
            var retrieval = new StubRetrievalService(world);
            var llm = new StubLlmService(new LlmRawResponse
            {
                responseText = """{"replyText":"I killed Elsa and hid the evidence.","allowedTopicsUsed":["topic_email"]}""",
                modelName = "local-model",
                provider = "local",
                finishReason = "stop"
            });

            var service = new NpcDialogueService(
                retrieval,
                llm,
                new FixedIntentClassifier("PRESENT_EVIDENCE", 0.94f),
                new PassThroughNormalizer(),
                new StaticOptionsMonitor<LlmOptions>(new LlmOptions()),
                NullLogger<NpcDialogueService>.Instance);

            var response = await service.GenerateAsync(new NpcDialogueRequest
            {
                sessionId = world.SessionId,
                text = "The email makes you look guilty."
            });

            Assert.NotNull(response);
            Assert.Equal("application_fallback", response!.provider);
            Assert.Equal("policy_fallback", response.finishReason);
            Assert.DoesNotContain("i killed", response.replyText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("topic_email", response.allowedTopicsUsed);
            Assert.Single(retrieval.PersistedRecords);
            Assert.Equal("The email makes you look guilty.", retrieval.PersistedRecords[0].PlayerText);
        }

        [Fact]
        public async Task GenerateAsync_AllowsExplicitAdmission_InConfessionState()
        {
            var world = BuildWorldContext(ProgressionStateId.Confession, new[] { "public_story", "truth", "topic_weapon" });
            var retrieval = new StubRetrievalService(world);
            var llm = new StubLlmService(new LlmRawResponse
            {
                responseText = """{"replyText":"I killed Elsa when she threatened to expose me.","allowedTopicsUsed":["truth","topic_weapon","locked_topic"]}""",
                modelName = "local-model",
                provider = "local",
                finishReason = "stop"
            });

            var service = new NpcDialogueService(
                retrieval,
                llm,
                new FixedIntentClassifier("CONTRADICTION", 0.88f),
                new PassThroughNormalizer(),
                new StaticOptionsMonitor<LlmOptions>(new LlmOptions()),
                NullLogger<NpcDialogueService>.Instance);

            var response = await service.GenerateAsync(new NpcDialogueRequest
            {
                sessionId = world.SessionId,
                text = "The weapon ties back to you."
            });

            Assert.NotNull(response);
            Assert.Equal("local", response!.provider);
            Assert.Contains("I killed Elsa", response.replyText, StringComparison.Ordinal);
            Assert.Contains("truth", response.allowedTopicsUsed);
            Assert.DoesNotContain("locked_topic", response.allowedTopicsUsed);
            Assert.Single(retrieval.PersistedRecords);
            Assert.Equal("LOCAL_LLM", retrieval.PersistedRecords[0].ResponseSource);
        }

        private static NpcDialogueWorldContext BuildWorldContext(
            ProgressionStateId stateId,
            IReadOnlyList<string> allowedTopics)
        {
            var progression = new ProgressionSessionState(
                SessionId: "ps_11111111111111111111111111111111",
                PlayerId: 1,
                CaseId: "dylan-interrogation",
                NpcId: "dylan",
                State: stateId,
                TurnCount: 4,
                TrustScore: 5,
                ShutdownScore: 2,
                IsTerminal: false,
                Ending: CaseEndingType.None,
                PresentedEvidence: Array.Empty<EvidenceId>(),
                DiscoveredClues: new[] { ClueId.ElsaEmailDraft, ClueId.WeaponFound },
                DiscussedClues: new[] { ClueId.ElsaEmailDraft },
                ClueClickHistory: Array.Empty<ClueClickHistoryEntry>(),
                ComposureState: ComposureState.Guarded,
                ProofTier: ProofTier.Minimum,
                CanConfess: stateId == ProgressionStateId.Confession,
                History: Array.Empty<ProgressionHistoryEntry>(),
                LastTransitionReason: "test",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                UpdatedAtUtc: DateTimeOffset.UtcNow);

            return new NpcDialogueWorldContext(
                SessionId: progression.SessionId,
                PlayerId: 1,
                PlayerName: "Casey",
                NpcDbId: 1,
                NpcId: "dylan",
                NpcName: "Dylan Cross",
                Progression: progression,
                PublicStory: "I was barely around Elsa that night and I left before anything happened.",
                TruthSummary: "Dylan killed Elsa and concealed evidence.",
                Timeline: new[] { "Arrived shortly before 8:15 PM.", "Left after crossing paths with Elsa." },
                AllowedTopics: allowedTopics,
                TopicGuidance: allowedTopics.ToDictionary(item => item, item => $"guidance:{item}"),
                Relationship: new RelationshipSnapshot(0.4m, 0.5m, 0.5m, 0.3m, "tense"),
                RecentExchanges: Array.Empty<ConversationExchange>(),
                LoreSnippets: Array.Empty<LoreSnippet>());
        }

        private sealed class StubRetrievalService : IRetrievalService
        {
            private readonly NpcDialogueWorldContext _world;

            public StubRetrievalService(NpcDialogueWorldContext world)
            {
                _world = world;
            }

            public List<NpcReplyPersistenceRecord> PersistedRecords { get; } = new();

            public Task<NpcDialogueWorldContext?> GetNpcDialogueContextAsync(
                string sessionId,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<NpcDialogueWorldContext?>(_world.SessionId == sessionId ? _world : null);
            }

            public Task PersistNpcReplyAsync(
                NpcReplyPersistenceRecord record,
                CancellationToken cancellationToken = default)
            {
                PersistedRecords.Add(record);
                return Task.CompletedTask;
            }
        }

        private sealed class StubLlmService : ILLMService
        {
            private readonly LlmRawResponse _response;

            public StubLlmService(LlmRawResponse response)
            {
                _response = response;
            }

            public Task<LlmRawResponse> GenerateResponseAsync(
                LlmPromptPayload payload,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_response);
            }
        }
    }
}
