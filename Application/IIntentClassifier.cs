using System.Threading;
using System.Threading.Tasks;
using testapi1.ApiContracts;

namespace testapi1.Application
{
    public interface IIntentClassifier
    {
        Task<IntentResponse> ClassifyAsync(IntentRequest request, CancellationToken cancellationToken = default);
    }
}
