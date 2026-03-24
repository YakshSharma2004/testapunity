using testapi1.ApiContracts;
using testapi1.Application;
using testapi1.Domain.Progression;
using testapi1.Services.Progression;

namespace testapi1.Tests
{
    public sealed class IntentToProgressionEventMapperTests
    {
        [Fact]
        public async Task MapAsync_Uses_DbCatalog_EventType_And_ActionId()
        {
            var repo = new StubCatalogRepository
            {
                Action = new ProgressionCatalogAction(
                    ActionId: 7,
                    Code: "INTIMIDATE",
                    IntentTag: "INTIMIDATE",
                    ProgressionEventType: ProgressionEventType.Intimidate,
                    IsEnabled: true)
            };
            var mapper = new IntentToProgressionEventMapper(repo);

            var mapped = await mapper.MapAsync(
                request: new IntentRequest { Text = "talk now", NpcId = "dylan", ContextKey = "" },
                response: new IntentResponse { intent = "INTIMIDATE", confidence = 0.88f, modelVersion = "tests" },
                normalizedText: "talk now",
                nowUtc: DateTimeOffset.UtcNow);

            Assert.Equal(ProgressionEventType.Intimidate, mapped.Event.EventType);
            Assert.Equal(7, mapped.ActionId);
        }

        [Fact]
        public async Task GetAllowedIntentsAsync_Delegates_To_Repository()
        {
            var repo = new StubCatalogRepository
            {
                Allowed = new[] { "ASK_OPEN_QUESTION", "PRESENT_EVIDENCE" }
            };
            var mapper = new IntentToProgressionEventMapper(repo);

            var allowed = await mapper.GetAllowedIntentsAsync(ProgressionStateId.Intro);

            Assert.Equal(2, allowed.Count);
            Assert.Contains("ASK_OPEN_QUESTION", allowed);
            Assert.Contains("PRESENT_EVIDENCE", allowed);
        }

        private sealed class StubCatalogRepository : IProgressionCatalogRepository
        {
            public ProgressionCatalogAction? Action { get; set; }
            public IReadOnlyList<string> Allowed { get; set; } = Array.Empty<string>();

            public Task<ProgressionCatalogAction?> FindActionByIntentAsync(
                string intentCode,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Action);
            }

            public Task<IReadOnlyList<string>> GetAllowedIntentCodesAsync(
                ProgressionStateId state,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Allowed);
            }
        }
    }
}
