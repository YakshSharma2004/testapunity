using testapi1.ApiContracts;

namespace testapi1.Application
{
    public interface IPlayerTurnOrchestrator
    {
        Task<ProgressionTurnResponse?> ApplyAsync(
            ProgressionTurnRequest request,
            CancellationToken cancellationToken = default);
    }
}
