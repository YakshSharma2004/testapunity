namespace testapi1.Infrastructure.VectorStores.Qdrant
{
    public sealed class QdrantOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string CollectionName { get; set; } = string.Empty;
        public string? ApiKey { get; set; }

        public Uri GetBaseUri() => new(BaseUrl.TrimEnd('/') + "/");
    }
}
