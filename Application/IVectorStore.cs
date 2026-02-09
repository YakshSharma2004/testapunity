using testapi1.Infrastructure.VectorStores;
using System.Threading;
using System.Threading.Tasks;
namespace testapi1.Infrastructure
{
    public interface IVectorStore
    {
        Task UpsertEmbeddings(IReadOnlyCollection<VectorRecord> records, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<VectorSearchResult>> QuerySimilar(float[] embedding, int limit, CancellationToken cancellationToken = default);
        Task DeleteByIntentId(string intentId, CancellationToken cancellationToken = default);
    }
}
