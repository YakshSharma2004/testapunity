using System.Net.Http.Json;
using System.Text.Json;
using testapi1.Application;
using testapi1.Infrastructure.VectorStores.Qdrant;

namespace testapi1.Infrastructure.VectorStores
{
    public sealed class VectorDbStore : IVectorStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly HttpClient _httpClient;
        private readonly QdrantOptions _options;

        public VectorDbStore(HttpClient httpClient, QdrantOptions options)
        {
            _httpClient = httpClient;
            _options = options;
        }

        public async Task UpsertEmbeddings(IReadOnlyCollection<VectorRecord> records, CancellationToken cancellationToken = default)
        {
            if (records.Count == 0)
            {
                return;
            }

            var payload = new QdrantUpsertRequest(
                records.Select(record => new QdrantPoint(
                    record.Id,
                    record.Embedding,
                    BuildPayload(record))).ToList());

            var response = await _httpClient.PostAsJsonAsync(
                $"collections/{_options.CollectionName}/points?wait=true",
                payload,
                JsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task<IReadOnlyList<VectorSearchResult>> QuerySimilar(float[] embedding, int limit, CancellationToken cancellationToken = default)
        {
            var payload = new QdrantSearchRequest(
                embedding,
                limit,
                true);

            var response = await _httpClient.PostAsJsonAsync(
                $"collections/{_options.CollectionName}/points/search",
                payload,
                JsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<QdrantSearchResponse>(JsonOptions, cancellationToken);
            if (data?.Result is null)
            {
                return Array.Empty<VectorSearchResult>();
            }

            return data.Result
                .Select(item => new VectorSearchResult(
                    item.Id,
                    ExtractIntentId(item.Payload),
                    item.Score,
                    item.Payload))
                .ToList();
        }

        public async Task DeleteByIntentId(string intentId, CancellationToken cancellationToken = default)
        {
            var payload = new QdrantDeleteRequest(
                new QdrantFilter(
                    new List<QdrantCondition>
                    {
                        new("intentId", new QdrantMatch(intentId))
                    }));

            var response = await _httpClient.PostAsJsonAsync(
                $"collections/{_options.CollectionName}/points/delete?wait=true",
                payload,
                JsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        private static Dictionary<string, object> BuildPayload(VectorRecord record)
        {
            var payload = record.Metadata is not null
                ? new Dictionary<string, object>(record.Metadata)
                : new Dictionary<string, object>();

            payload["intentId"] = record.IntentId;
            return payload;
        }

        private static string ExtractIntentId(IReadOnlyDictionary<string, object>? payload)
        {
            if (payload is null)
            {
                return string.Empty;
            }

            return payload.TryGetValue("intentId", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
        }
    }

}
