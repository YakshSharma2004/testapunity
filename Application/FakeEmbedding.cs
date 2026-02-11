using System.Security.Cryptography;
using System.Text;

namespace testapi1.Application
{
    public sealed class FakeEmbeddingService : IEmbeddingService
    {
        private const int Dim = 384;

        public Task<float[]> CreateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Task.FromResult(new float[Dim]);
            }

            // Deterministic hash-based embedding: same text -> same vector
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
            var result = new float[Dim];

            for (int i = 0; i < Dim; i++)
            {
                byte b = hash[i % hash.Length];
                result[i] = (b / 127.5f) - 1.0f; // map [0,255] -> [-1,1]
            }

            return Task.FromResult(result);
        }
    }
}
