namespace testapi1.Infrastructure.VectorStores
{
    public sealed record VectorRecord(
        string Id,
        string IntentId,
        float[] Embedding,
        IReadOnlyDictionary<string, object>? Metadata = null);

    public sealed record VectorSearchResult(
        string Id,
        string IntentId,
        double Score,
        IReadOnlyDictionary<string, object>? Metadata = null);
}
