using testapi1.ApiContracts;

namespace testapi1.Application
{
    public interface IPlayerTurnResolver
    {
        Task<ResolvedPlayerTurn> ResolveAsync(
            ProgressionTurnRequest request,
            string npcId,
            CancellationToken cancellationToken = default);
    }
}
