using Microsoft.EntityFrameworkCore;
using testapi1.Application;
using testapi1.Domain;
using testapi1.Infrastructure.Persistence;
using testapi1.Services;
using testapi1.Tests.TestSupport;

namespace testapi1.Tests
{
    public sealed class RetrievalServicePersistenceTests
    {
        [Fact]
        public async Task PersistNpcReplyAsync_Uses_Exact_InteractionId_When_Provided()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;

            await using var db = new AppDbContext(options);
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
            db.PlayerNpcStates.Add(new PlayerNpcState
            {
                PlayerId = 1,
                NpcId = 1,
                Trust = 0.5m,
                Patience = 0.5m,
                Curiosity = 0.5m,
                Openness = 0.5m,
                Memory = "initialized",
                LastInteractionAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var runtimeRepository = new PostgresProgressionRuntimeRepository(db);
            var retrievalService = new RetrievalService(db, new InMemorySessionStore());

            var first = await runtimeRepository.PersistTurnAsync(new TurnPersistenceRecord(
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
                SessionId: "ps_1234567890abcdef1234567890abcdef",
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

            Assert.Equal("reason-2", updatedSecond.ResponseText);
            Assert.Equal("PROGRESSION_ENGINE", updatedSecond.ResponseSource);
        }
    }
}
