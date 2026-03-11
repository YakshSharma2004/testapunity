using testapi1.Contracts;

namespace testapi1.Application
{
    public interface IGameProgressionService
    {
        Task<StartProgressionResponse> StartSessionAsync(StartProgressionRequest request, CancellationToken cancellationToken = default);
        Task<ProgressionTurnResponse?> ApplyTurnAsync(ProgressionTurnRequest request, CancellationToken cancellationToken = default);
        Task<ProgressionSnapshotResponse?> GetSnapshotAsync(string sessionId, CancellationToken cancellationToken = default);
    }
}
