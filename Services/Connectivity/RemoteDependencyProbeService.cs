using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Npgsql;
using testapi1.Application;
using testapi1.Infrastructure.VectorStores.Qdrant;
using testapi1.Services.Llm;

namespace testapi1.Services.Connectivity
{
    public sealed class RemoteDependencyProbeService : IRemoteDependencyProbe
    {
        private readonly IConfiguration _configuration;
        private readonly IOptionsMonitor<RemoteConnectivityOptions> _optionsMonitor;
        private readonly IOptionsMonitor<QdrantOptions> _qdrantOptionsMonitor;
        private readonly IOptionsMonitor<LlmOptions> _llmOptionsMonitor;
        private readonly IHttpClientFactory _httpClientFactory;

        public RemoteDependencyProbeService(
            IConfiguration configuration,
            IOptionsMonitor<RemoteConnectivityOptions> optionsMonitor,
            IOptionsMonitor<QdrantOptions> qdrantOptionsMonitor,
            IOptionsMonitor<LlmOptions> llmOptionsMonitor,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _optionsMonitor = optionsMonitor;
            _qdrantOptionsMonitor = qdrantOptionsMonitor;
            _llmOptionsMonitor = llmOptionsMonitor;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<RemoteDependencyProbeReport> ProbeAsync(CancellationToken cancellationToken = default)
        {
            var timeoutMs = Math.Max(250, _optionsMonitor.CurrentValue.TimeoutMs);
            var checks = await Task.WhenAll(
                ProbeRedisAsync(timeoutMs, cancellationToken),
                ProbeQdrantAsync(timeoutMs, cancellationToken),
                ProbePostgresAsync(timeoutMs, cancellationToken),
                ProbeOllamaAsync(timeoutMs, cancellationToken));

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

        private async Task<RemoteDependencyStatus> ProbeOllamaAsync(int timeoutMs, CancellationToken cancellationToken)
        {
            var local = _llmOptionsMonitor.CurrentValue.Local;
            var target = string.IsNullOrWhiteSpace(local.BaseUrl) ? "(not configured)" : local.BaseUrl.Trim();

            if (!local.Enabled)
            {
                return new RemoteDependencyStatus("ollama", target, true, "Disabled.", 0, local.Model, null);
            }

            if (string.IsNullOrWhiteSpace(local.BaseUrl) || string.IsNullOrWhiteSpace(local.Model))
            {
                return new RemoteDependencyStatus("ollama", target, false, "Llm:Local is enabled but BaseUrl or Model is missing.", 0, local.Model, false);
            }

            if (!Uri.TryCreate(BuildModelsUri(local.BaseUrl), UriKind.Absolute, out var probeUri))
            {
                return new RemoteDependencyStatus("ollama", target, false, "Llm:Local:BaseUrl is invalid.", 0, local.Model, false);
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var client = _httpClientFactory.CreateClient("llm-local");
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
                using var response = await client.GetAsync(probeUri, timeoutCts.Token);
                var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    return new RemoteDependencyStatus(
                        "ollama",
                        target,
                        false,
                        $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})",
                        stopwatch.ElapsedMilliseconds,
                        local.Model,
                        false);
                }

                var modelAvailable = TryModelListContains(body, local.Model);
                var message = modelAvailable
                    ? "Configured model found."
                    : "Configured model not found in /v1/models.";

                return new RemoteDependencyStatus(
                    "ollama",
                    target,
                    modelAvailable,
                    message,
                    stopwatch.ElapsedMilliseconds,
                    local.Model,
                    modelAvailable);
            }
            catch (OperationCanceledException)
            {
                return new RemoteDependencyStatus("ollama", target, false, $"Timeout after {timeoutMs}ms.", stopwatch.ElapsedMilliseconds, local.Model, false);
            }
            catch (Exception ex)
            {
                return new RemoteDependencyStatus("ollama", target, false, ex.Message, stopwatch.ElapsedMilliseconds, local.Model, false);
            }
        }

        private static string BuildModelsUri(string baseUrl)
        {
            var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            if (trimmed.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return $"{trimmed}/models";
            }

            return $"{trimmed}/v1/models";
        }

        private static bool TryModelListContains(string body, string configuredModel)
        {
            if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(configuredModel))
            {
                return false;
            }

            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(body);
                if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != System.Text.Json.JsonValueKind.Array)
                {
                    return false;
                }

                foreach (var item in data.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idElement) &&
                        idElement.ValueKind == System.Text.Json.JsonValueKind.String &&
                        string.Equals(idElement.GetString(), configuredModel, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (System.Text.Json.JsonException)
            {
                return false;
            }
        }
    }
}
