namespace testapi1.Services.Configuration
{
    public static class ExternalDependencyEnvValidator
    {
        private static readonly string[][] RequiredEnvAliases =
        {
            new[] { "CONNECTIONSTRINGS__REDIS", "ConnectionStrings__Redis" },
            new[] { "CONNECTIONSTRINGS__POSTGRES", "ConnectionStrings__Postgres" },
            new[] { "QDRANT__BASEURL", "Qdrant__BaseUrl" },
            new[] { "QDRANT__COLLECTIONNAME", "Qdrant__CollectionName" },
            new[] { "VECTORSTORE__PROVIDER", "VectorStore__Provider" }
        };

        public static void ValidateOrThrow()
        {
            var errors = new List<string>();

            foreach (var aliases in RequiredEnvAliases)
            {
                if (TryGetFirstNonEmptyEnvValue(aliases, out _))
                {
                    continue;
                }

                errors.Add($"Missing required environment variable ({string.Join(" or ", aliases)}).");
            }

            if (TryGetFirstNonEmptyEnvValue(new[] { "VECTORSTORE__PROVIDER", "VectorStore__Provider" }, out var provider) &&
                !string.Equals(provider, "Qdrant", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("VECTORSTORE__PROVIDER must be set to 'Qdrant'.");
            }

            if (errors.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "External dependency configuration must be provided through environment variables. " +
                string.Join(" ", errors));
        }

        private static bool TryGetFirstNonEmptyEnvValue(IEnumerable<string> aliases, out string value)
        {
            foreach (var alias in aliases)
            {
                var candidate = Environment.GetEnvironmentVariable(alias);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    value = candidate;
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }
    }
}
