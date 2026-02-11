using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using testapi1.Application;
using testapi1.Infrastructure.VectorStores;

namespace testapi1.Services.Intent
{
    public sealed class IntentSeedHostedService : IHostedService
    {
        private readonly ILogger<IntentSeedHostedService> _logger;
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorStore _vectorStore;

        public IntentSeedHostedService(
            ILogger<IntentSeedHostedService> logger,
            IEmbeddingService embeddingService,
            IVectorStore vectorStore)
        {
            _logger = logger;
            _embeddingService = embeddingService;
            _vectorStore = vectorStore;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var records = new List<VectorRecord>(IntentSeed.Examples.Length);

            foreach (var (intent, text) in IntentSeed.Examples)
            {
                var embedding = await _embeddingService.EmbedAsync(text, cancellationToken);
                records.Add(new VectorRecord(
                    Id: BuildId(intent, text),
                    IntentId: intent,
                    Embedding: embedding,
                    Metadata: new Dictionary<string, object>
                    {
                        ["text"] = text,
                        ["seed"] = true
                    }));
            }

            await _vectorStore.UpsertEmbeddings(records, cancellationToken);
            _logger.LogInformation("Seeded {Count} intent examples for POC classification.", records.Count);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private static string BuildId(string intent, string text)
        {
            return $"seed::{intent}::{text.Trim().ToLowerInvariant().Replace(" ", "-")}";
        }
    }
}
