using Microsoft.AspNetCore.Mvc;
using testapi1.Application;
using testapi1.ApiContracts;

namespace testapi1.Controllers
{
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/v1/[controller]")]
    public class IntentController : ControllerBase
    {
        private readonly IIntentClassifier _intentClassifier;

        public IntentController(IIntentClassifier intentClassifier)
        {
            _intentClassifier = intentClassifier;
        }

        [HttpPost("classify")]
        public async Task<ActionResult<IntentResponse>> Classify([FromBody] IntentRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest("Text is required.");
            }

            var response = await _intentClassifier.ClassifyAsync(request, cancellationToken);
            return Ok(response);
        }
    }
}
