using Microsoft.AspNetCore.Mvc;
using testapi1.Application;
using testapi1.Contracts;

namespace testapi1.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class LlmController : ControllerBase
    {
        private readonly ILLMService _llmService;

        public LlmController(ILLMService llmService)
        {
            _llmService = llmService;
        }

        [HttpPost("generate")]
        public async Task<ActionResult<LlmRawResponse>> Generate([FromBody] LlmPromptPayload payload, CancellationToken cancellationToken)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.promptText))
            {
                return BadRequest("promptText is required.");
            }

            var response = await _llmService.GenerateResponseAsync(payload, cancellationToken);
            return Ok(response);
        }
    }
}
