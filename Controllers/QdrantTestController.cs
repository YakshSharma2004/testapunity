using Microsoft.AspNetCore.Mvc;
using testapi1.Infrastructure;
using testapi1.Application;

namespace testapi1.Controllers
{
    [ApiController]
    [Route("qdrant-test")]
    public class QdrantTestController : ControllerBase
    {
        private readonly IVectorStore _store;
        private readonly IEmbeddingService _embedding;

        public QdrantTestController(IVectorStore store, IEmbeddingService embedding)
        {
            _store = store;
            _embedding = embedding;
        }

        [HttpPost("upsert")]
        public async Task<IActionResult> Upsert([FromBody] string text, CancellationToken ct)
        {
            var vector = await _embedding.CreateEmbeddingAsync(text, ct);

            var record = new VectorRecord(
                Id: Guid.NewGuid().ToString(),
                IntentId: "test-intent",
                Embedding: vector,
                Metadata: new Dictionary<string, object> { ["text"] = text });

            await _store.UpsertEmbeddings(new[] { record }, ct);
            return Ok("Upserted");
        }

        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] string query, CancellationToken ct)
        {
            var vector = await _embedding.CreateEmbeddingAsync(query, ct);
            var results = await _store.QuerySimilar(vector, 5, ct);
            return Ok(results);
        }
    }
}
