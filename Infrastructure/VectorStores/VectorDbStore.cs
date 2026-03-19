using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using testapi1.Application;
using testapi1.Infrastructure.VectorStores.Qdrant;

namespace testapi1.Infrastructure.VectorStores
{
    public sealed class VectorDbStore : IVectorStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly HttpClient _httpClient;
        private readonly QdrantOptions _options;

        public VectorDbStore(HttpClient httpClient, IOptions<QdrantOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;

            _httpClient.BaseAddress = _options.GetBaseUri();

            if (!string.IsNullOrEmpty(_options.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("api-key", _options.ApiKey);
            }
        }

        public async Task UpsertEmbeddings(IReadOnlyCollection<VectorRecord> records, CancellationToken cancellationToken = default)
        {
            if (records.Count == 0)
            {
                return;
            }

            var points = records.Select(record => new QdrantPoint(
                record.Id,
                record.Embedding,
                BuildPayload(record))).ToList();

            var payload = new QdrantUpsertRequest(points);

            var response = await _httpClient.PutAsJsonAsync(
                $"collections/{_options.CollectionName}/points?wait=true",
                payload,
                JsonOptions,
                cancellationToken);

            await EnsureSuccessOrThrow(response, "upsert", cancellationToken);
        }

        public async Task<IReadOnlyList<VectorSearchResult>> QuerySimilar(float[] embedding, int limit, CancellationToken cancellationToken = default)
        {
            var payload = new QdrantSearchRequest(embedding, limit, true);

            var response = await _httpClient.PostAsJsonAsync(
                $"collections/{_options.CollectionName}/points/search",
                payload,
                JsonOptions,
                cancellationToken);

            await EnsureSuccessOrThrow(response, "search", cancellationToken);

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
            var condition = new QdrantCondition("intentId", new QdrantMatch(intentId));
            var filter = new QdrantFilter(new[] { condition });
            var payload = new QdrantDeleteRequest(filter);

            var response = await _httpClient.PostAsJsonAsync(
                $"collections/{_options.CollectionName}/points/delete?wait=true",
                payload,
                JsonOptions,
                cancellationToken);

            await EnsureSuccessOrThrow(response, "delete", cancellationToken);
        }

        private static async Task EnsureSuccessOrThrow(
            HttpResponseMessage response,
            string operation,
            CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Qdrant {operation} failed: {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {body}");
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
            if (payload is null) return string.Empty;
            return payload.TryGetValue("intentId", out var value) ? value?.ToString() ?? string.Empty : string.Empty;
        }
    }

    // -------- Qdrant DTOs with proper JSON naming --------

    public sealed record QdrantPoint(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("vector")] float[] Vector,
        [property: JsonPropertyName("payload")] IReadOnlyDictionary<string, object> Payload);

    public sealed record QdrantUpsertRequest(
        [property: JsonPropertyName("points")] IReadOnlyCollection<QdrantPoint> Points);

    public sealed record QdrantSearchRequest(
        [property: JsonPropertyName("vector")] float[] Vector,
        [property: JsonPropertyName("limit")] int Limit,
        [property: JsonPropertyName("with_payload")] bool WithPayload);

    public sealed record QdrantSearchResponse(
        [property: JsonPropertyName("result")] IReadOnlyList<QdrantSearchResult> Result);

    public sealed record QdrantSearchResult(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("score")] double Score,
        [property: JsonPropertyName("payload")] IReadOnlyDictionary<string, object>? Payload);

    public sealed record QdrantDeleteRequest(
        [property: JsonPropertyName("filter")] QdrantFilter Filter);

    public sealed record QdrantFilter(
        [property: JsonPropertyName("must")] IReadOnlyList<QdrantCondition> Must);

    public sealed record QdrantCondition(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("match")] QdrantMatch Match);

    public sealed record QdrantMatch(
        [property: JsonPropertyName("value")] string Value);
}
