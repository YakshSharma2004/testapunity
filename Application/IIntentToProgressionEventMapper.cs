using testapi1.ApiContracts;
using testapi1.Domain.Progression;

namespace testapi1.Application
{
    public interface IIntentToProgressionEventMapper
    {
        Task<ProgressionMappedEvent> MapAsync(
            IntentRequest request,
            IntentResponse response,
            string normalizedText,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetAllowedIntentsAsync(
            ProgressionStateId state,
            CancellationToken cancellationToken = default);
    }
}
