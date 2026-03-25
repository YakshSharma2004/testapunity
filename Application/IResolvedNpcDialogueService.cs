using testapi1.ApiContracts;

namespace testapi1.Application
{
    public interface IResolvedNpcDialogueService
    {
        Task<NpcDialogueResponse?> GenerateAsync(
            NpcDialogueRequest request,
            ResolvedPlayerTurn turn,
            PersistedTurnRecord? persistedTurn,
            CancellationToken cancellationToken = default);
    }
}
