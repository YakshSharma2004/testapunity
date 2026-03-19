namespace testapi1.Application
{
    public sealed record RemoteDependencyStatus(
        string Name,
        string Target,
        bool Healthy,
        string Message,
        long DurationMs);

    public sealed record RemoteDependencyProbeReport(
        DateTimeOffset CheckedAtUtc,
        bool AllHealthy,
        IReadOnlyList<RemoteDependencyStatus> Dependencies);
}
