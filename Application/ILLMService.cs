using System.Threading;
using System.Threading.Tasks;
using testapi1.Contracts;

namespace testapi1.Application
{
    public interface ILLMService
    {
        Task<LlmRawResponse> GenerateResponseAsync(LlmPromptPayload payload, CancellationToken cancellationToken = default);
    }
}
