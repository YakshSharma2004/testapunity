namespace testapi1.Application
{
    public interface IProgressionRuntimeRepository
    {
        Task<bool> PlayerExistsAsync(int playerId, CancellationToken cancellationToken = default);

        Task<NpcRuntimeIdentity?> GetNpcByCodeAsync(
            string npcCode,
            CancellationToken cancellationToken = default);

        Task EnsurePlayerNpcStateAsync(
            int playerId,
            int npcId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default);

        Task PersistTurnAsync(
            TurnPersistenceRecord record,
            CancellationToken cancellationToken = default);

        Task TouchPlayerNpcStateAsync(
            int playerId,
            int npcId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default);
    }

    public sealed record NpcRuntimeIdentity(int NpcId, string NpcCode, string Name);

    public sealed record TurnPersistenceRecord(
        int PlayerId,
        int NpcId,
        int? ActionId,
        DateTimeOffset OccurredAtUtc,
        string IntentCode,
        string EventType,
        string PlayerText,
        string TransitionReason,
        string ComposureState,
        string ModelVersion,
        int TrustScore,
        int ShutdownScore,
        int TurnCount);
}
