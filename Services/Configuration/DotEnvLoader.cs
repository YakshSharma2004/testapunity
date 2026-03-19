namespace testapi1.Services.Configuration
{
    public static class DotEnvLoader
    {
        public static int LoadIfDevelopment(string contentRootPath)
        {
            var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? "Production";

            if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var filePath = Path.Combine(contentRootPath, ".env");
            if (!File.Exists(filePath))
            {
                return 0;
            }

            var loadedCount = 0;
            foreach (var rawLine in File.ReadAllLines(filePath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if ((value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)) ||
                    (value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal)))
                {
                    value = value[1..^1];
                }

                if (Environment.GetEnvironmentVariable(key) is null)
                {
                    Environment.SetEnvironmentVariable(key, value);
                    loadedCount++;
                }
            }

            return loadedCount;
        }
    }
}
