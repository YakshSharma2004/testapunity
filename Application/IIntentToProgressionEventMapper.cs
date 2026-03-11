using testapi1.Contracts;
using testapi1.Domain.Progression;

namespace testapi1.Application
{
    public interface IIntentToProgressionEventMapper
    {
        ProgressionEvent Map(IntentRequest request, IntentResponse response, string normalizedText, DateTimeOffset nowUtc);
    }
}
