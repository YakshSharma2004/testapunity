using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Npgsql;
using testapi1.Application;
using testapi1.Infrastructure.VectorStores.Qdrant;

namespace testapi1.Services.Connectivity
{
    public sealed class RemoteDependencyProbeService : IRemoteDependencyProbe
    {
        private readonly IConfiguration _configuration;
        private readonly IOptionsMonitor<RemoteConnectivityOptions> _optionsMonitor;
        private readonly IOptionsMonitor<QdrantOptions> _qdrantOptionsMonitor;
        private readonly IHttpClientFactory _httpClientFactory;

        public RemoteDependencyProbeService(
            IConfiguration configuration,
            IOptionsMonitor<RemoteConnectivityOptions> optionsMonitor,
            IOptionsMonitor<QdrantOptions> qdrantOptionsMonitor,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _optionsMonitor = optionsMonitor;
            _qdrantOptionsMonitor = qdrantOptionsMonitor;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<RemoteDependencyProbeReport> ProbeAsync(CancellationToken cancellationToken = default)
        {
            var timeoutMs = Math.Max(250, _optionsMonitor.CurrentValue.TimeoutMs);
            var checks = await Task.WhenAll(
                ProbeRedisAsync(timeoutMs, cancellationToken),
                ProbeQdrantAsync(timeoutMs, cancellationToken),
                ProbePostgresAsync(timeoutMs, cancellationToken));

            var dependencyChecks = checks.ToList();
            var allHealthy = dependencyChecks.All(item => item.Healthy);

            return new RemoteDependencyProbeReport(
                CheckedAtUtc: DateTimeOffset.UtcNow,
                AllHealthy: allHealthy,
                Dependencies: dependencyChecks);
        }

        private async Task<RemoteDependencyStatus> ProbeRedisAsync(int timeoutMs, CancellationToken cancellationToken)
        {
            var redisConnection = _configuration.GetConnectionString("Redis");
            var target = DependencyTargetParser.GetRedisTarget(redisConnection);

            if (!DependencyTargetParser.TryGetRedisHostPort(redisConnection, out var host, out var port))
            {
                return new RemoteDependencyStatus("redis", target, false, "ConnectionStrings:Redis missing or invalid.", 0);
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var tcpClient = new TcpClient();
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
                await tcpClient.ConnectAsync(host, port, timeoutCts.Token);

                return new RemoteDependencyStatus("redis", target, true, "Connected.", stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                return new RemoteDependencyStatus("redis", target, false, $"Timeout after {timeoutMs}ms.", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return new RemoteDependencyStatus("redis", target, false, ex.Message, stopwatch.ElapsedMilliseconds);
            }
        }

        private async Task<RemoteDependencyStatus> ProbeQdrantAsync(int timeoutMs, CancellationToken cancellationToken)
        {
            var qdrantBaseUrl = _qdrantOptionsMonitor.CurrentValue.BaseUrl;
            var target = DependencyTargetParser.GetQdrantTarget(qdrantBaseUrl);

            if (!Uri.TryCreate(qdrantBaseUrl, UriKind.Absolute, out var baseUri))
            {
                return new RemoteDependencyStatus("qdrant", target, false, "Qdrant:BaseUrl missing or invalid.", 0);
            }

            var probeUri = new Uri(baseUri, "collections");
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var client = _httpClientFactory.CreateClient("remote-dependency-probe");
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
                using var response = await client.GetAsync(probeUri, timeoutCts.Token);

                var healthy = response.IsSuccessStatusCode;
                var message = healthy
                    ? $"HTTP {(int)response.StatusCode}"
                    : $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";

                return new RemoteDependencyStatus("qdrant", target, healthy, message, stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                return new RemoteDependencyStatus("qdrant", target, false, $"Timeout after {timeoutMs}ms.", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return new RemoteDependencyStatus("qdrant", target, false, ex.Message, stopwatch.ElapsedMilliseconds);
            }
        }

        private async Task<RemoteDependencyStatus> ProbePostgresAsync(int timeoutMs, CancellationToken cancellationToken)
        {
            var postgresConnection = _configuration.GetConnectionString("Postgres");
            var target = DependencyTargetParser.GetPostgresTarget(postgresConnection);

            if (string.IsNullOrWhiteSpace(postgresConnection))
            {
                return new RemoteDependencyStatus("postgres", target, false, "ConnectionStrings:Postgres missing.", 0);
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await using var connection = new NpgsqlConnection(postgresConnection);
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
                await connection.OpenAsync(timeoutCts.Token);

                await using var command = new NpgsqlCommand("select 1", connection);
                await command.ExecuteScalarAsync(timeoutCts.Token);

                return new RemoteDependencyStatus("postgres", target, true, "Connected.", stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                return new RemoteDependencyStatus("postgres", target, false, $"Timeout after {timeoutMs}ms.", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return new RemoteDependencyStatus("postgres", target, false, ex.Message, stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
