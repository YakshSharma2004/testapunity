using Microsoft.AspNetCore.Mvc;
using testapi1.Application;
using testapi1.ApiContracts;
using testapi1.Domain.Progression;

namespace testapi1.Controllers
{
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/v1/[controller]")]
    public class LlmController : ControllerBase
    {
        private readonly INpcDialogueService _npcDialogueService;

        public LlmController(INpcDialogueService npcDialogueService)
        {
            _npcDialogueService = npcDialogueService;
        }

        [HttpPost("generate")]
        public async Task<ActionResult<NpcDialogueResponse>> Generate(
            [FromBody] NpcDialogueRequest payload,
            CancellationToken cancellationToken)
        {
            if (payload is null || string.IsNullOrWhiteSpace(payload.sessionId) || string.IsNullOrWhiteSpace(payload.text))
            {
                return BadRequest("sessionId and text are required.");
            }

            if (!ProgressionSessionId.IsValid(payload.sessionId))
            {
                return BadRequest("sessionId must match format ps_<32 lowercase hex characters>.");
            }

            var response = await _npcDialogueService.GenerateAsync(payload, cancellationToken);
            if (response is null)
            {
                return NotFound($"Session '{payload.sessionId}' was not found or has expired.");
            }

            return Ok(response);
        }
    }
}
