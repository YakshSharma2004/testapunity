using Microsoft.AspNetCore.Mvc;
using testapi1.ApiContracts;
using testapi1.Application;
using testapi1.Controllers;

namespace testapi1.Tests
{
    public sealed class ProgressionControllerTests
    {
        [Fact]
        public async Task ClickClue_Returns_BadRequest_For_Invalid_SessionId_Format()
        {
            var controller = new ProgressionController(new StubProgressionService(), new StubTurnOrchestrator());

            var result = await controller.ClickClue(
                new ProgressionClueClickRequest
                {
                    sessionId = "bad-session",
                    clueId = "elsa_email_draft"
                },
                CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("sessionId", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ClickClue_Returns_BadRequest_For_Unknown_ClueId()
        {
            var controller = new ProgressionController(new StubProgressionService(), new StubTurnOrchestrator());

            var result = await controller.ClickClue(
                new ProgressionClueClickRequest
                {
                    sessionId = "ps_1234567890abcdef1234567890abcdef",
                    clueId = "not_a_real_clue"
                },
                CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("Unknown clueId", badRequest.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ClickClue_Returns_NotFound_For_Missing_Session()
        {
            var controller = new ProgressionController(new MissingSessionProgressionService(), new StubTurnOrchestrator());

            var result = await controller.ClickClue(
                new ProgressionClueClickRequest
                {
                    sessionId = "ps_1234567890abcdef1234567890abcdef",
                    clueId = "elsa_email_draft"
                },
                CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task Turn_Returns_Ok_With_ReplyText_And_Snapshot()
        {
            var controller = new ProgressionController(new StubProgressionService(), new SuccessfulTurnOrchestrator());

            var result = await controller.Turn(
                new ProgressionTurnRequest
                {
                    sessionId = "ps_1234567890abcdef1234567890abcdef",
                    text = "Tell me what happened."
                },
                CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<ProgressionTurnResponse>(ok.Value);
            Assert.Equal("Stay with the facts.", payload.replyText);
            Assert.Equal("ps_1234567890abcdef1234567890abcdef", payload.snapshot.sessionId);
        }

        private sealed class StubProgressionService : IGameProgressionService
        {
            public Task<StartProgressionResponse> StartSessionAsync(StartProgressionRequest request, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<ProgressionTurnResponse?> ApplyTurnAsync(ProgressionTurnRequest request, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<ProgressionClueClickResponse?> ApplyClueClickAsync(ProgressionClueClickRequest request, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<ProgressionSnapshotResponse?> GetSnapshotAsync(string sessionId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class StubTurnOrchestrator : IPlayerTurnOrchestrator
        {
            public Task<ProgressionTurnResponse?> ApplyAsync(ProgressionTurnRequest request, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class SuccessfulTurnOrchestrator : IPlayerTurnOrchestrator
        {
            public Task<ProgressionTurnResponse?> ApplyAsync(ProgressionTurnRequest request, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<ProgressionTurnResponse?>(new ProgressionTurnResponse
                {
                    sessionId = request.sessionId,
                    replyText = "Stay with the facts.",
                    snapshot = new ProgressionSnapshotResponse
                    {
                        sessionId = request.sessionId,
                        npcId = "dylan"
                    }
                });
            }
        }

        private sealed class MissingSessionProgressionService : IGameProgressionService
        {
            public Task<StartProgressionResponse> StartSessionAsync(StartProgressionRequest request, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<ProgressionTurnResponse?> ApplyTurnAsync(ProgressionTurnRequest request, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<ProgressionClueClickResponse?> ApplyClueClickAsync(
                ProgressionClueClickRequest request,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<ProgressionClueClickResponse?>(null);
            }

            public Task<ProgressionSnapshotResponse?> GetSnapshotAsync(string sessionId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }
    }
}
