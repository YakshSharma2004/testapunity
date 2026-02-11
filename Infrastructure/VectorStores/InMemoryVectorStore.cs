using testapi1.Application;

namespace testapi1.Infrastructure.VectorStores
{
    public sealed class InMemoryVectorStore : IVectorStore
    {
        private readonly List<VectorRecord> _records = new();

        public Task UpsertEmbeddings(IReadOnlyCollection<VectorRecord> records, CancellationToken cancellationToken = default)
        {
            foreach (var record in records)
            {
                var index = _records.FindIndex(item => item.Id == record.Id);
                if (index >= 0)
                {
                    _records[index] = record;
                }
                else
                {
                    _records.Add(record);
                }
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<VectorSearchResult>> QuerySimilar(float[] embedding, int limit, CancellationToken cancellationToken = default)
        {
            var results = _records
                .Select(record => new VectorSearchResult(
                    record.Id,
                    record.IntentId,
                    CosineSimilarity(record.Embedding, embedding),
                    record.Metadata))
                .OrderByDescending(result => result.Score)
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
        }

        public Task DeleteByIntentId(string intentId, CancellationToken cancellationToken = default)
        {
            _records.RemoveAll(record => record.IntentId == intentId);
            return Task.CompletedTask;
        }

        private static double CosineSimilarity(float[] left, float[] right)
        {
            if (left.Length != right.Length || left.Length == 0)
            {
                return 0d;
            }

            double dot = 0d;
            double normLeft = 0d;
            double normRight = 0d;

            for (var index = 0; index < left.Length; index++)
            {
                dot += left[index] * right[index];
                normLeft += left[index] * left[index];
                normRight += right[index] * right[index];
            }

            if (normLeft == 0d || normRight == 0d)
            {
                return 0d;
            }

            return dot / (Math.Sqrt(normLeft) * Math.Sqrt(normRight));
        }
    }
}
