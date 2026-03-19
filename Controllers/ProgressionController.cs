using Microsoft.AspNetCore.Mvc;
using testapi1.Application;
using testapi1.Contracts;

namespace testapi1.Controllers
{
    [ApiController]
    [Route("api/v1/progression")]
    public sealed class ProgressionController : ControllerBase
    {
        private readonly IGameProgressionService _progressionService;

        public ProgressionController(IGameProgressionService progressionService)
        {
            _progressionService = progressionService;
        }

        [HttpPost("start")]
        public async Task<ActionResult<StartProgressionResponse>> Start(
            [FromBody] StartProgressionRequest? request,
            CancellationToken cancellationToken)
        {
            var payload = request ?? new StartProgressionRequest();
            var response = await _progressionService.StartSessionAsync(payload, cancellationToken);
            return Ok(response);
        }

        [HttpPost("turn")]
        public async Task<ActionResult<ProgressionTurnResponse>> Turn(
            [FromBody] ProgressionTurnRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.sessionId) || string.IsNullOrWhiteSpace(request.text))
            {
                return BadRequest("sessionId and text are required.");
            }

            var response = await _progressionService.ApplyTurnAsync(request, cancellationToken);
            if (response is null)
            {
                return NotFound($"Session '{request.sessionId}' was not found or has expired.");
            }

            return Ok(response);
        }

        [HttpGet("{sessionId}")]
        public async Task<ActionResult<ProgressionSnapshotResponse>> Snapshot(
            [FromRoute] string sessionId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return BadRequest("sessionId is required.");
            }

            var response = await _progressionService.GetSnapshotAsync(sessionId, cancellationToken);
            if (response is null)
            {
                return NotFound($"Session '{sessionId}' was not found or has expired.");
            }

            return Ok(response);
        }
    }
}
