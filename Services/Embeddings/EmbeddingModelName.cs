namespace testapi1.Services.Embeddings
{
    public static class EmbeddingModelName
    {
        public static string Normalize(string? value)
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "mpnet"
                : value.Trim().ToLowerInvariant();

            return normalized == "current" ? "mpnet" : normalized;
        }
    }
}
