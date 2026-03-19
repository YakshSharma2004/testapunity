using System.Security.Cryptography;
using System.Text;

namespace testapi1.Services.Caching
{
    public static class CacheKeyHasher
    {
        public static string BuildIntentCacheKey(
            string modelVersion,
            string embeddingModel,
            string normalizedText,
            string npcKey,
            string contextKey)
        {
            var raw = $"intent|{modelVersion}|{embeddingModel}|{normalizedText}|{npcKey}|{contextKey}";
            var hash = HashToLowerHex(raw);
            return $"intent:{modelVersion}:{embeddingModel}:{hash}";
        }

        private static string HashToLowerHex(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
