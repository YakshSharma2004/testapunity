using testapi1.ApiContracts;
using testapi1.Domain.Progression;
using testapi1.Services.Progression;
using testapi1.Tests.TestSupport;

namespace testapi1.Tests
{
    public sealed class GameProgressionServiceTests
    {
        [Fact]
        public async Task StartSession_Defaults_PlayerId_To_Seeded_Player_One()
        {
            var runtime = new InMemoryRuntimeRepository();
            var service = CreateService(new InMemorySessionStore(), runtime);

            var start = await service.StartSessionAsync(new StartProgressionRequest());

            Assert.Equal(1, start.snapshot.playerId);
            Assert.Contains(runtime.EnsureCalls, call => call.PlayerId == 1 && call.NpcId == 1);
        }

        [Fact]
        public async Task StartSession_Throws_When_Player_Is_Missing()
        {
            var runtime = new InMemoryRuntimeRepository();
            runtime.RemovePlayer(1);
            var service = CreateService(new InMemorySessionStore(), runtime);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.StartSessionAsync(new StartProgressionRequest()));
        }

        [Fact]
        public async Task Turn_Persists_Runtime_Record()
        {
            var runtime = new InMemoryRuntimeRepository();
            var service = CreateService(new InMemorySessionStore(), runtime);
            var start = await service.StartSessionAsync(new StartProgressionRequest());

            var response = await service.ApplyTurnAsync(new ProgressionTurnRequest
            {
                sessionId = start.sessionId,
                text = "Walk me through your timeline."
            });

            Assert.NotNull(response);
            var record = Assert.Single(runtime.TurnRecords);
            Assert.Equal(1, record.PlayerId);
            Assert.Equal(1, record.NpcId);
            Assert.Equal("ASK_OPEN_QUESTION", record.IntentCode);
        }

        [Fact]
        public async Task ClueClick_FirstClick_Applies_Repeat_Is_NoOp_But_Logged()
        {
            var store = new InMemorySessionStore();
            var runtime = new InMemoryRuntimeRepository();
            var service = CreateService(store, runtime);
            var start = await service.StartSessionAsync(new StartProgressionRequest());

            var first = await service.ApplyClueClickAsync(new ProgressionClueClickRequest
            {
                sessionId = start.sessionId,
                clueId = "elsa_email_draft",
                source = "desk"
            });

            var second = await service.ApplyClueClickAsync(new ProgressionClueClickRequest
            {
                sessionId = start.sessionId,
                clueId = "elsa_email_draft",
                source = "desk"
            });

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.True(first!.isFirstDiscovery);
            Assert.True(first.applied);
            Assert.False(second!.isFirstDiscovery);
            Assert.False(second.applied);

            var state = await store.GetAsync(start.sessionId);
            Assert.NotNull(state);
            Assert.Single(state!.DiscoveredClues);
            Assert.Equal(2, state.ClueClickHistory.Count);
            Assert.Equal(2, runtime.TouchCalls.Count);
        }

        [Fact]
        public async Task ClueClick_Allows_Any_Order()
        {
            var service = CreateService(new InMemorySessionStore(), new InMemoryRuntimeRepository());
            var start = await service.StartSessionAsync(new StartProgressionRequest());

            var response = await service.ApplyClueClickAsync(new ProgressionClueClickRequest
            {
                sessionId = start.sessionId,
                clueId = "weapon_found",
                source = "storage"
            });

            Assert.NotNull(response);
            Assert.True(response!.applied);
            Assert.Contains("weapon_found", response.snapshot.discoveredClueIds);
        }

        [Fact]
        public async Task CanConfess_Requires_Discovered_And_Discussed_Required_Clues()
        {
            var required = new[]
            {
                "elsa_email_draft",
                "payroll_report",
                "meeting_note",
                "entry_log",
                "pin_note",
                "cleanup_item",
                "weapon_found",
                "flash_drive"
            };

            var service = CreateService(
                new InMemorySessionStore(),
                new InMemoryRuntimeRepository(),
                new ProgressionOptions { ConfessionRequiredClues = required.ToList() });

            var start = await service.StartSessionAsync(new StartProgressionRequest());

            foreach (var clue in required)
            {
                var click = await service.ApplyClueClickAsync(new ProgressionClueClickRequest
                {
                    sessionId = start.sessionId,
                    clueId = clue
                });
                Assert.NotNull(click);
            }

            var snapshotBeforeDiscussion = await service.GetSnapshotAsync(start.sessionId);
            Assert.NotNull(snapshotBeforeDiscussion);
            Assert.False(snapshotBeforeDiscussion!.canConfess);

            var turn = await service.ApplyTurnAsync(new ProgressionTurnRequest
            {
                sessionId = start.sessionId,
                text = "Let's discuss all evidence.",
                discussedClueIds = required.ToList()
            });

            Assert.NotNull(turn);
            Assert.True(turn!.snapshot.canConfess);
        }

        [Fact]
        public async Task ClueClick_Returns_Null_When_Session_Does_Not_Exist()
        {
            var service = CreateService(new InMemorySessionStore(), new InMemoryRuntimeRepository());

            var response = await service.ApplyClueClickAsync(new ProgressionClueClickRequest
            {
                sessionId = "ps_1234567890abcdef1234567890abcdef",
                clueId = "elsa_email_draft"
            });

            Assert.Null(response);
        }

        private static GameProgressionService CreateService(
            InMemorySessionStore store,
            InMemoryRuntimeRepository runtimeRepository,
            ProgressionOptions? options = null)
        {
            return new GameProgressionService(
                engine: new DylanProgressionEngine(),
                sessionStore: store,
                intentClassifier: new FixedIntentClassifier(),
                eventMapper: new FixedProgressionEventMapper(),
                normalizer: new PassThroughNormalizer(),
                progressionOptions: new StaticOptionsMonitor<ProgressionOptions>(options ?? new ProgressionOptions()),
                runtimeRepository: runtimeRepository);
        }
    }
}
