using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using testapi1.Application;
using testapi1.ApiContracts;

namespace testapi1.Services
{
    public class LlmService : ILLMService
    {
        private readonly ILogger<LlmService> _logger;

        public LlmService(ILogger<LlmService> logger)
        {
            _logger = logger;
        }

        public Task<LlmRawResponse> GenerateResponseAsync(LlmPromptPayload payload, CancellationToken cancellationToken = default)
        {
            var prompt = payload?.promptText ?? "";
            _logger.LogInformation("Generating LLM response for prompt length {Length}", prompt.Length);

            var response = new LlmRawResponse
            {
                responseText = "placeholder-response",
                modelName = "stub",
                tokensUsed = 0,
                finishReason = "placeholder"
            };

            return Task.FromResult(response);
        }
    }
}
