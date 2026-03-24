using testapi1.Domain.Progression;

namespace testapi1.Application
{
    public interface IProgressionCatalogRepository
    {
        Task<ProgressionCatalogAction?> FindActionByIntentAsync(
            string intentCode,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetAllowedIntentCodesAsync(
            ProgressionStateId state,
            CancellationToken cancellationToken = default);
    }

    public sealed record ProgressionCatalogAction(
        int ActionId,
        string Code,
        string IntentTag,
        ProgressionEventType ProgressionEventType,
        bool IsEnabled);
}
