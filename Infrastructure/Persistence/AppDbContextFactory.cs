using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace testapi1.Infrastructure.Persistence
{
    public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var connectionString = TryGetConnectionFromArgs(args);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                var root = Directory.GetCurrentDirectory();
                LoadDotEnv(root);

                var configuration = new ConfigurationBuilder()
                    .SetBasePath(root)
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile("appsettings.Development.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                connectionString = configuration.GetConnectionString("Postgres");
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Missing Postgres connection string for design-time EF commands. " +
                    "Set CONNECTIONSTRINGS__POSTGRES in your environment or .env.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(connectionString);
            return new AppDbContext(optionsBuilder.Options);
        }

        private static string? TryGetConnectionFromArgs(string[] args)
        {
            if (args is null || args.Length == 0)
            {
                return null;
            }

            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index];
                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                if (arg.StartsWith("--connection=", StringComparison.OrdinalIgnoreCase))
                {
                    return arg["--connection=".Length..].Trim();
                }

                if (string.Equals(arg, "--connection", StringComparison.OrdinalIgnoreCase) &&
                    index + 1 < args.Length)
                {
                    return args[index + 1].Trim();
                }
            }

            return null;
        }

        private static void LoadDotEnv(string rootPath)
        {
            var filePath = Path.Combine(rootPath, ".env");
            if (!File.Exists(filePath))
            {
                return;
            }

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

                // Design-time commands should use the repo's current .env deterministically.
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
