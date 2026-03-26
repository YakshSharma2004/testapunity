using testapi1.ApiContracts;

namespace testapi1.Application
{
    public interface IResolvedProgressionTurnService
    {
        Task<ProgressionTurnExecutionResult?> ApplyResolvedTurnAsync(
            ResolvedPlayerTurn turn,
            CancellationToken cancellationToken = default);
    }

    public sealed record ProgressionTurnExecutionResult(
        ProgressionTurnResponse Response,
        PersistedTurnRecord PersistedTurn);
}
