using testapi1.ApiContracts;

namespace testapi1.Application
{
    public interface INpcDialogueService
    {
        Task<NpcDialogueResponse?> GenerateAsync(
            NpcDialogueRequest request,
            CancellationToken cancellationToken = default);
    }
}
