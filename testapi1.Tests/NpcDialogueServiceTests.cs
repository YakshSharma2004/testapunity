using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
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

        [Fact]
        public async Task GenerateAsync_UsesCompactPrompt_WhenLocalProviderIsConfigured()
        {
            var world = BuildWorldContext(
                ProgressionStateId.BuildingCase,
                new[] { "public_story", "topic_email", "topic_money" },
                publicStory: new string('P', 900),
                truthSummary: new string('T', 900),
                relationshipMemory: new string('M', 400),
                timeline: new[] { "one", "two", "three" },
                recentExchanges: Enumerable.Range(1, 5)
                    .Select(index => new ConversationExchange(
                        DateTimeOffset.UtcNow.AddMinutes(index),
                        $"player-{index}",
                        $"npc-{index}",
                        "ASK_OPEN_QUESTION",
                        "LOCAL_LLM"))
                    .ToList(),
                loreSnippets: Enumerable.Range(1, 4)
                    .Select(index => new LoreSnippet($"lore-{index}", $"title-{index}", $"body-{index}"))
                    .ToList());
            var retrieval = new StubRetrievalService(world);
            var llm = new StubLlmService(new LlmRawResponse
            {
                responseText = """{"replyText":"I already told you what I know.","allowedTopicsUsed":["public_story"]}""",
                modelName = "local-model",
                provider = "local",
                finishReason = "stop"
            });

            var service = new NpcDialogueService(
                retrieval,
                llm,
                new FixedIntentClassifier("ASK_OPEN_QUESTION", 0.82f),
                new PassThroughNormalizer(),
                new StaticOptionsMonitor<LlmOptions>(new LlmOptions
                {
                    Local = new LocalLlmOptions
                    {
                        Enabled = true,
                        BaseUrl = "http://localhost:11434",
                        Model = "qwen2.5:3b",
                        MaxRecentExchanges = 2,
                        MaxLoreSnippets = 2,
                        MaxTimelineItems = 1,
                        MaxPublicStoryChars = 120,
                        MaxTruthSummaryChars = 90,
                        MaxRelationshipMemoryChars = 50
                    }
                }),
                NullLogger<NpcDialogueService>.Instance);

            await service.GenerateAsync(new NpcDialogueRequest
            {
                sessionId = world.SessionId,
                text = "Tell me what happened."
            });

            Assert.NotNull(llm.LastPayload);
            using var document = JsonDocument.Parse(llm.LastPayload!.promptText);
            var root = document.RootElement;

            Assert.Equal(2, root.GetProperty("recentConversation").GetArrayLength());
            Assert.Equal(2, root.GetProperty("lore").GetArrayLength());
            Assert.Equal(1, root.GetProperty("worldview").GetProperty("timeline").GetArrayLength());
            Assert.True(root.GetProperty("worldview").GetProperty("publicStory").GetString()!.Length <= 120);
            Assert.True(root.GetProperty("worldview").GetProperty("internalTruth").GetString()!.Length <= 90);
            Assert.True(root.GetProperty("worldview").GetProperty("relationship").GetProperty("memory").GetString()!.Length <= 50);
        }

        private static NpcDialogueWorldContext BuildWorldContext(
            ProgressionStateId stateId,
            IReadOnlyList<string> allowedTopics,
            string? publicStory = null,
            string? truthSummary = null,
            string? relationshipMemory = null,
            IReadOnlyList<string>? timeline = null,
            IReadOnlyList<ConversationExchange>? recentExchanges = null,
            IReadOnlyList<LoreSnippet>? loreSnippets = null)
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
                PublicStory: publicStory ?? "I was barely around Elsa that night and I left before anything happened.",
                TruthSummary: truthSummary ?? "Dylan killed Elsa and concealed evidence.",
                Timeline: timeline ?? new[] { "Arrived shortly before 8:15 PM.", "Left after crossing paths with Elsa." },
                AllowedTopics: allowedTopics,
                TopicGuidance: allowedTopics.ToDictionary(item => item, item => $"guidance:{item}"),
                Relationship: new RelationshipSnapshot(0.4m, 0.5m, 0.5m, 0.3m, relationshipMemory ?? "tense"),
                RecentExchanges: recentExchanges ?? Array.Empty<ConversationExchange>(),
                LoreSnippets: loreSnippets ?? Array.Empty<LoreSnippet>());
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

            public LlmPromptPayload? LastPayload { get; private set; }

            public Task<LlmRawResponse> GenerateResponseAsync(
                LlmPromptPayload payload,
                CancellationToken cancellationToken = default)
            {
                LastPayload = payload;
                return Task.FromResult(_response);
            }
        }
    }
}
