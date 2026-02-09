using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using testapi1.Infrastructure;

namespace testapi1.Application
{
    public interface IVectorStore
    {
        Task UpsertEmbeddings(IReadOnlyCollection<VectorRecord> records, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<VectorSearchResult>> QuerySimilar(float[] embedding, int limit, CancellationToken cancellationToken = default);
        Task DeleteByIntentId(string intentId, CancellationToken cancellationToken = default);
    }
}
