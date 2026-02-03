using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using testapi1.Application;
using testapi1.Contracts;

namespace testapi1.Services
{
    public class IntentClassifier : IIntentClassifier
    {
        private readonly ILogger<IntentClassifier> _logger;

        public IntentClassifier(ILogger<IntentClassifier> logger)
        {
            _logger = logger;
        }

        public Task<IntentResponse> ClassifyAsync(IntentRequest request, CancellationToken cancellationToken = default)
        {
            var text = request?.Text ?? "";
            _logger.LogInformation("Classifying intent for text length {Length}", text.Length);

            var response = new IntentResponse
            {
                intent = "unknown",
                confidence = 0.0f,
                notes = "placeholder-intent"
            };

            return Task.FromResult(response);
        }
    }
}
