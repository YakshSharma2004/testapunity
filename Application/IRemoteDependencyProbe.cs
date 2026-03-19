namespace testapi1.Application
{
    public interface IRemoteDependencyProbe
    {
        Task<RemoteDependencyProbeReport> ProbeAsync(CancellationToken cancellationToken = default);
    }
}
