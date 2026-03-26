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
        private readonly IPlayerTurnOrchestrator _turnOrchestrator;

        public ProgressionController(
            IGameProgressionService progressionService,
            IPlayerTurnOrchestrator turnOrchestrator)
        {
            _progressionService = progressionService;
            _turnOrchestrator = turnOrchestrator;
        }

        [HttpPost("start")]
        public async Task<ActionResult<StartProgressionResponse>> Start(
            [FromBody] StartProgressionRequest? request,
            CancellationToken cancellationToken)
        {
            var payload = request ?? new StartProgressionRequest();
            if (payload.playerId.HasValue && payload.playerId.Value <= 0)
            {
                return BadRequest("playerId must be a positive integer.");
            }

            if (!string.IsNullOrWhiteSpace(payload.sessionId) && !ProgressionSessionId.IsValid(payload.sessionId))
            {
                return BadRequest("sessionId must match format ps_<32 lowercase hex characters>.");
            }

            try
            {
                var response = await _progressionService.StartSessionAsync(payload, cancellationToken);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
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

            ProgressionTurnResponse? response;
            try
            {
                response = await _turnOrchestrator.ApplyAsync(request, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

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

            ProgressionClueClickResponse? response;
            try
            {
                response = await _progressionService.ApplyClueClickAsync(request, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            if (response is null)
            {
                return NotFound($"Session '{request.sessionId}' was not found or has expired.");
            }

            return Ok(response);
        }
    }
}
