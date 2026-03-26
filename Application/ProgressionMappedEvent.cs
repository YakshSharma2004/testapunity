using testapi1.Domain.Progression;

namespace testapi1.Application
{
    public sealed record ProgressionMappedEvent(
        ProgressionEvent Event,
        int? ActionId);
}
