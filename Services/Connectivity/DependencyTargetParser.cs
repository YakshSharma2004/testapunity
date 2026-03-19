using Npgsql;

namespace testapi1.Services.Connectivity
{
    public static class DependencyTargetParser
    {
        public static string GetRedisTarget(string? redisConnection)
        {
            if (TryGetRedisHostPort(redisConnection, out var host, out var port))
            {
                return $"{host}:{port}";
            }

            return "not-configured";
        }

        public static string GetQdrantTarget(string? baseUrl)
        {
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            {
                var port = uri.IsDefaultPort
                    ? (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80)
                    : uri.Port;
                return $"{uri.Host}:{port}";
            }

            return "not-configured";
        }

        public static string GetPostgresTarget(string? postgresConnection)
        {
            if (TryGetPostgresHostPort(postgresConnection, out var host, out var port))
            {
                return $"{host}:{port}";
            }

            return "not-configured";
        }

        public static bool TryGetRedisHostPort(string? redisConnection, out string host, out int port)
        {
            host = string.Empty;
            port = 6379;

            if (string.IsNullOrWhiteSpace(redisConnection))
            {
                return false;
            }

            var endpoint = redisConnection
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => segment.Trim())
                .FirstOrDefault(segment => !segment.Contains('='));

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return false;
            }

            endpoint = endpoint.Trim();

            if (endpoint.StartsWith("[", StringComparison.Ordinal))
            {
                var closeBracket = endpoint.IndexOf(']');
                if (closeBracket > 1)
                {
                    host = endpoint[1..closeBracket];
                    var remainder = endpoint[(closeBracket + 1)..];
                    if (remainder.StartsWith(":", StringComparison.Ordinal) &&
                        int.TryParse(remainder[1..], out var parsedV6Port))
                    {
                        port = parsedV6Port;
                    }

                    return !string.IsNullOrWhiteSpace(host);
                }
            }

            var lastColon = endpoint.LastIndexOf(':');
            if (lastColon > 0 && lastColon < endpoint.Length - 1 && int.TryParse(endpoint[(lastColon + 1)..], out var parsedPort))
            {
                host = endpoint[..lastColon];
                port = parsedPort;
                return true;
            }

            host = endpoint;
            return true;
        }

        public static bool TryGetPostgresHostPort(string? postgresConnection, out string host, out int port)
        {
            host = string.Empty;
            port = 5432;

            if (string.IsNullOrWhiteSpace(postgresConnection))
            {
                return false;
            }

            try
            {
                var builder = new NpgsqlConnectionStringBuilder(postgresConnection);
                host = string.IsNullOrWhiteSpace(builder.Host) ? string.Empty : builder.Host;
                port = builder.Port > 0 ? builder.Port : 5432;
                return !string.IsNullOrWhiteSpace(host);
            }
            catch
            {
                return false;
            }
        }
    }
}
