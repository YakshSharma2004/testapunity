namespace testapi1.Infrastructure.VectorStores.Qdrant
{
    public sealed record QdrantOptions(string BaseUrl, string CollectionName)
    {
        public Uri GetBaseUri() => new(BaseUrl.TrimEnd('/') + "/");
    }

    public sealed record QdrantPoint(string Id, float[] Vector, IReadOnlyDictionary<string, object> Payload);

    public sealed record QdrantUpsertRequest(IReadOnlyCollection<QdrantPoint> Points);

    public sealed record QdrantSearchRequest(float[] Vector, int Limit, bool WithPayload);

    public sealed record QdrantSearchResponse(IReadOnlyList<QdrantSearchResult> Result);

    public sealed record QdrantSearchResult(string Id, double Score, IReadOnlyDictionary<string, object>? Payload);

    public sealed record QdrantDeleteRequest(QdrantFilter Filter);

    public sealed record QdrantFilter(IReadOnlyList<QdrantCondition> Must);

    public sealed record QdrantCondition(string Key, QdrantMatch Match);

    public sealed record QdrantMatch(string Value);
}
