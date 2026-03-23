using Microsoft.AspNetCore.Mvc;
using testapi1.Application;
using testapi1.ApiContracts;
using testapi1.Domain.Progression;

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
            if (!string.IsNullOrWhiteSpace(payload.sessionId) && !ProgressionSessionId.IsValid(payload.sessionId))
            {
                return BadRequest("sessionId must match format ps_<32 lowercase hex characters>.");
            }

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

            if (!ProgressionSessionId.IsValid(request.sessionId))
            {
                return BadRequest("sessionId must match format ps_<32 lowercase hex characters>.");
            }

            var invalidDiscussed = request.discussedClueIds
                .Where(id => !ClueCatalog.TryParseKey(id, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (invalidDiscussed.Count > 0)
            {
                return BadRequest($"Unknown discussedClueIds: {string.Join(", ", invalidDiscussed)}.");
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

            if (!ProgressionSessionId.IsValid(sessionId))
            {
                return BadRequest("sessionId must match format ps_<32 lowercase hex characters>.");
            }

            var response = await _progressionService.GetSnapshotAsync(sessionId, cancellationToken);
            if (response is null)
            {
                return NotFound($"Session '{sessionId}' was not found or has expired.");
            }

            return Ok(response);
        }

        [HttpPost("clues/click")]
        public async Task<ActionResult<ProgressionClueClickResponse>> ClickClue(
            [FromBody] ProgressionClueClickRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.sessionId) || string.IsNullOrWhiteSpace(request.clueId))
            {
                return BadRequest("sessionId and clueId are required.");
            }

            if (!ProgressionSessionId.IsValid(request.sessionId))
            {
                return BadRequest("sessionId must match format ps_<32 lowercase hex characters>.");
            }

            if (!ClueCatalog.TryParseKey(request.clueId, out _))
            {
                return BadRequest($"Unknown clueId '{request.clueId}'.");
            }

            var response = await _progressionService.ApplyClueClickAsync(request, cancellationToken);
            if (response is null)
            {
                return NotFound($"Session '{request.sessionId}' was not found or has expired.");
            }

            return Ok(response);
        }
    }
}
