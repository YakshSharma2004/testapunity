using Microsoft.EntityFrameworkCore;
using testapi1.Application;
using testapi1.Domain;
using testapi1.Domain.Progression;
using testapi1.Infrastructure.Persistence;
using testapi1.Services;
using testapi1.Tests.TestSupport;

namespace testapi1.Tests
{
    public sealed class RetrievalServicePersistenceTests
    {
        [Fact]
        public async Task PersistTurnAsync_Stores_SessionId_On_Interaction()
        {
            var options = CreateOptions();

            await using var db = new AppDbContext(options);
            await SeedCoreEntitiesAsync(db);

            var runtimeRepository = new PostgresProgressionRuntimeRepository(db);
            await runtimeRepository.EnsurePlayerNpcStateAsync(1, 1, DateTimeOffset.UtcNow);

            var persisted = await runtimeRepository.PersistTurnAsync(new TurnPersistenceRecord(
                SessionId: "ps_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                PlayerId: 1,
                NpcId: 1,
                ActionId: 7,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IntentCode: "ASK_OPEN_QUESTION",
                EventType: "AskOpenQuestion",
                PlayerText: "first text",
                TransitionReason: "reason-1",
                ComposureState: "Calm",
                ModelVersion: "tests",
                TrustScore: 1,
                ShutdownScore: 0,
                TurnCount: 1));

            var interaction = await db.Interactions.FirstAsync(item => item.InteractionId == persisted.InteractionId);

            Assert.Equal("ps_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", persisted.SessionId);
            Assert.Equal("ps_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", interaction.SessionId);
        }

        [Fact]
        public async Task PersistNpcReplyAsync_Uses_Exact_InteractionId_When_Provided()
        {
            var options = CreateOptions();

            await using var db = new AppDbContext(options);
            await SeedCoreEntitiesAsync(db);

            var runtimeRepository = new PostgresProgressionRuntimeRepository(db);
            var retrievalService = new RetrievalService(db, new InMemorySessionStore());
            await runtimeRepository.EnsurePlayerNpcStateAsync(1, 1, DateTimeOffset.UtcNow);

            var first = await runtimeRepository.PersistTurnAsync(new TurnPersistenceRecord(
                SessionId: "ps_1234567890abcdef1234567890abcdef",
                PlayerId: 1,
                NpcId: 1,
                ActionId: 7,
                OccurredAtUtc: DateTimeOffset.UtcNow,
                IntentCode: "ASK_OPEN_QUESTION",
                EventType: "AskOpenQuestion",
                PlayerText: "first text",
                TransitionReason: "reason-1",
                ComposureState: "Calm",
                ModelVersion: "tests",
                TrustScore: 1,
                ShutdownScore: 0,
                TurnCount: 1));
            var second = await runtimeRepository.PersistTurnAsync(new TurnPersistenceRecord(
                SessionId: "ps_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                PlayerId: 1,
                NpcId: 1,
                ActionId: 8,
                OccurredAtUtc: DateTimeOffset.UtcNow.AddMinutes(1),
                IntentCode: "ASK_OPEN_QUESTION",
                EventType: "AskOpenQuestion",
                PlayerText: "second text",
                TransitionReason: "reason-2",
                ComposureState: "Calm",
                ModelVersion: "tests",
                TrustScore: 2,
                ShutdownScore: 0,
                TurnCount: 2));

            await retrievalService.PersistNpcReplyAsync(new NpcReplyPersistenceRecord(
                SessionId: first.SessionId,
                PlayerId: 1,
                NpcDbId: 1,
                PlayerText: "mismatched text",
                IntentCode: "ASK_OPEN_QUESTION",
                ResponseText: "Exact interaction update",
                ResponseSource: "LOCAL_LLM",
                ModelVersion: "model-x",
                OccurredAtUtc: DateTimeOffset.UtcNow.AddHours(4),
                InteractionId: first.InteractionId));

            var updatedFirst = await db.Interactions.FirstAsync(item => item.InteractionId == first.InteractionId);
            var updatedSecond = await db.Interactions.FirstAsync(item => item.InteractionId == second.InteractionId);

            Assert.Equal("Exact interaction update", updatedFirst.ResponseText);
            Assert.Equal("LOCAL_LLM", updatedFirst.ResponseSource);
            Assert.Contains("npc_reply_generated", updatedFirst.OutcomeFlags, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(first.SessionId, updatedFirst.SessionId);

            Assert.Equal("reason-2", updatedSecond.ResponseText);
            Assert.Equal("PROGRESSION_ENGINE", updatedSecond.ResponseSource);
            Assert.Equal(second.SessionId, updatedSecond.SessionId);
        }

        [Fact]
        public async Task PersistNpcReplyAsync_DoesNotReuse_Candidate_From_Different_Session()
        {
            var options = CreateOptions();

            await using var db = new AppDbContext(options);
            await SeedCoreEntitiesAsync(db);

            var runtimeRepository = new PostgresProgressionRuntimeRepository(db);
            var retrievalService = new RetrievalService(db, new InMemorySessionStore());
            var occurredAt = DateTimeOffset.UtcNow;
            await runtimeRepository.EnsurePlayerNpcStateAsync(1, 1, occurredAt);

            var original = await runtimeRepository.PersistTurnAsync(new TurnPersistenceRecord(
                SessionId: "ps_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                PlayerId: 1,
                NpcId: 1,
                ActionId: 7,
                OccurredAtUtc: occurredAt,
                IntentCode: "ASK_OPEN_QUESTION",
                EventType: "AskOpenQuestion",
                PlayerText: "same text",
                TransitionReason: "reason-1",
                ComposureState: "Calm",
                ModelVersion: "tests",
                TrustScore: 1,
                ShutdownScore: 0,
                TurnCount: 1));

            await retrievalService.PersistNpcReplyAsync(new NpcReplyPersistenceRecord(
                SessionId: "ps_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                PlayerId: 1,
                NpcDbId: 1,
                PlayerText: "same text",
                IntentCode: "ASK_OPEN_QUESTION",
                ResponseText: "new reply for new session",
                ResponseSource: "LOCAL_LLM",
                ModelVersion: "model-y",
                OccurredAtUtc: occurredAt.AddMinutes(1),
                InteractionId: null));

            var interactions = await db.Interactions
                .OrderBy(item => item.InteractionId)
                .ToListAsync();

            Assert.Equal(2, interactions.Count);
            Assert.Equal(original.InteractionId, interactions[0].InteractionId);
            Assert.Equal("ps_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", interactions[0].SessionId);
            Assert.Equal("reason-1", interactions[0].ResponseText);

            Assert.Equal("ps_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", interactions[1].SessionId);
            Assert.Equal("new reply for new session", interactions[1].ResponseText);
            Assert.Equal("LOCAL_LLM", interactions[1].ResponseSource);
        }

        [Fact]
        public async Task GetNpcDialogueContextAsync_Returns_RecentExchanges_From_Current_Session_Only()
        {
            var options = CreateOptions();

            await using var db = new AppDbContext(options);
            await SeedCoreEntitiesAsync(db);

            var sessionStore = new InMemorySessionStore();
            await sessionStore.SetAsync(BuildSessionState("ps_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
            await sessionStore.SetAsync(BuildSessionState("ps_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));

            var runtimeRepository = new PostgresProgressionRuntimeRepository(db);
            var retrievalService = new RetrievalService(db, sessionStore);
            await runtimeRepository.EnsurePlayerNpcStateAsync(1, 1, DateTimeOffset.UtcNow);

            await runtimeRepository.PersistTurnAsync(new TurnPersistenceRecord(
                SessionId: "ps_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                PlayerId: 1,
                NpcId: 1,
                ActionId: 1,
                OccurredAtUtc: DateTimeOffset.UtcNow.AddMinutes(-3),
                IntentCode: "ASK_OPEN_QUESTION",
                EventType: "AskOpenQuestion",
                PlayerText: "session-a-one",
                TransitionReason: "reply-a-one",
                ComposureState: "Calm",
                ModelVersion: "tests",
                TrustScore: 1,
                ShutdownScore: 0,
                TurnCount: 1));
            await runtimeRepository.PersistTurnAsync(new TurnPersistenceRecord(
                SessionId: "ps_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                PlayerId: 1,
                NpcId: 1,
                ActionId: 1,
                OccurredAtUtc: DateTimeOffset.UtcNow.AddMinutes(-2),
                IntentCode: "ASK_OPEN_QUESTION",
                EventType: "AskOpenQuestion",
                PlayerText: "session-b-one",
                TransitionReason: "reply-b-one",
                ComposureState: "Calm",
                ModelVersion: "tests",
                TrustScore: 1,
                ShutdownScore: 0,
                TurnCount: 1));
            await runtimeRepository.PersistTurnAsync(new TurnPersistenceRecord(
                SessionId: "ps_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                PlayerId: 1,
                NpcId: 1,
                ActionId: 1,
                OccurredAtUtc: DateTimeOffset.UtcNow.AddMinutes(-1),
                IntentCode: "ASK_OPEN_QUESTION",
                EventType: "AskOpenQuestion",
                PlayerText: "session-a-two",
                TransitionReason: "reply-a-two",
                ComposureState: "Calm",
                ModelVersion: "tests",
                TrustScore: 1,
                ShutdownScore: 0,
                TurnCount: 2));

            var context = await retrievalService.GetNpcDialogueContextAsync("ps_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

            Assert.NotNull(context);
            Assert.Collection(
                context!.RecentExchanges,
                exchange => Assert.Equal("session-a-one", exchange.PlayerText),
                exchange => Assert.Equal("session-a-two", exchange.PlayerText));
        }

        private static DbContextOptions<AppDbContext> CreateOptions()
        {
            return new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        }

        private static async Task SeedCoreEntitiesAsync(AppDbContext db)
        {
            db.Players.Add(new Player
            {
                PlayerId = 1,
                DisplayName = "Casey",
                CreatedAt = DateTime.UtcNow,
                NpcStates = new List<PlayerNpcState>(),
                Interactions = new List<Interaction>()
            });
            db.Npcs.Add(new Npc
            {
                NpcId = 1,
                NpcCode = "dylan",
                Name = "Dylan Cross",
                Archetype = "suspect",
                BaseFriendliness = 0.4m,
                BasePatience = 0.5m,
                BaseCuriosity = 0.4m,
                BaseOpenness = 0.3m,
                BaseConfidence = 0.7m,
                CreatedAt = DateTime.UtcNow,
                PlayerStates = new List<PlayerNpcState>(),
                Interactions = new List<Interaction>(),
                DialogueTemplates = new List<DialogueTemplate>(),
                LoreDocs = new List<LoreDoc>()
            });

            await db.SaveChangesAsync();
        }

        private static ProgressionSessionState BuildSessionState(string sessionId)
        {
            return new ProgressionSessionState(
                SessionId: sessionId,
                PlayerId: 1,
                CaseId: "dylan-interrogation",
                NpcId: "dylan",
                State: ProgressionStateId.InformationGathering,
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
    }
}
