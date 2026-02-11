using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using testapi1.Application;
using Tokenizers.HuggingFace.Tokenizer;

namespace testapi1.Services
{
    public sealed class MpnetOnnxEmbeddingService : IEmbeddingService, IDisposable
    {
        private readonly ILogger<MpnetOnnxEmbeddingService> _logger;
        private readonly InferenceSession _session;
        private readonly Tokenizer _tokenizer;

        // MPNet sentence-transformers typically uses max length 384 (model card). :contentReference[oaicite:2]{index=2}
        private const int MaxLen = 384;

        public MpnetOnnxEmbeddingService(ILogger<MpnetOnnxEmbeddingService> logger, IWebHostEnvironment env)
        {
            _logger = logger;

            var modelPath = Path.Combine(env.ContentRootPath, "Models", "mpnet", "model.onnx");
            var tokPath = Path.Combine(env.ContentRootPath, "Models", "mpnet", "tokenizer.json");

            _tokenizer = Tokenizer.FromFile(tokPath);

            // Basic runtime options; keep default for now
            _session = new InferenceSession(modelPath);

            // Log input/output names once (very useful because ONNX models vary)
            _logger.LogInformation("ONNX inputs: {Inputs}", string.Join(", ", _session.InputMetadata.Keys));
            _logger.LogInformation("ONNX outputs: {Outputs}", string.Join(", ", _session.OutputMetadata.Keys));
        }

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            // Tokenize
            var enc = _tokenizer.Encode(text ?? string.Empty, addSpecialTokens: true).FirstOrDefault();

            // Get ids, truncate, and build attention mask
            var ids = enc.Ids.ToArray();
            if (ids.Length > MaxLen) ids = ids.Take(MaxLen).ToArray();

            var attention = new long[MaxLen];
            var inputIds = new long[MaxLen];

            for (int i = 0; i < MaxLen; i++)
            {
                if (i < ids.Length)
                {
                    inputIds[i] = (long)ids[i];
                    attention[i] = 1;
                }
                else
                {
                    inputIds[i] = 0;     // pad id often 0 in BERT-like tokenizers
                    attention[i] = 0;
                }
            }

            // Build tensors: shape [1, MaxLen]
            var inputIdsTensor = new DenseTensor<long>(new[] { 1, MaxLen });
            var attnTensor = new DenseTensor<long>(new[] { 1, MaxLen });

            for (int i = 0; i < MaxLen; i++)
            {
                inputIdsTensor[0, i] = inputIds[i];
                attnTensor[0, i] = attention[i];
            }

            // Prepare ONNX inputs.
            // Different repos name inputs differently. We'll map common names:
            // - input_ids
            // - attention_mask
            // Some also include token_type_ids (MPNet usually doesn't need it).
            var inputs = new List<NamedOnnxValue>();

            string? inputIdsName = FindFirst(_session.InputMetadata.Keys, "input_ids", "input_ids:0", "ids");
            string? attnName = FindFirst(_session.InputMetadata.Keys, "attention_mask", "attention_mask:0", "mask");

            if (inputIdsName == null || attnName == null)
            {
                throw new InvalidOperationException(
                    $"Could not find expected ONNX inputs. Available: {string.Join(", ", _session.InputMetadata.Keys)}");
            }

            inputs.Add(NamedOnnxValue.CreateFromTensor(inputIdsName, inputIdsTensor));
            inputs.Add(NamedOnnxValue.CreateFromTensor(attnName, attnTensor));

            // Run inference
            using var results = _session.Run(inputs);

            // Most sentence-transformer ONNX ports output a sentence embedding directly (pooling + normalize included). :contentReference[oaicite:3]{index=3}
            // But output name varies, so take the first output tensor<float>.
            foreach (var r in results)
            {
                if (r.Value is Tensor<float> t)
                {
                    // Expect shape [1, 768]
                    var embedding = MeanPoolAndNormalize(t, attention, MaxLen, 768);
                    return Task.FromResult(embedding);
                }
            }

            throw new InvalidOperationException("No float tensor output found from ONNX session.");
        }
        private static float[] MeanPoolAndNormalize(Tensor<float> hidden, long[] attentionMask, int maxLen, int hiddenSize = 768)
        {
            var pooled = new float[hiddenSize];
            float count = 0;

            // hidden shape: [1, maxLen, hiddenSize]
            for (int t = 0; t < maxLen; t++)
            {
                if (attentionMask[t] == 0) continue;

                for (int j = 0; j < hiddenSize; j++)
                {
                    pooled[j] += hidden[0, t, j];
                }
                count += 1f;
            }

            if (count > 0)
            {
                for (int j = 0; j < hiddenSize; j++)
                    pooled[j] /= count;
            }

            // L2 normalize
            double norm = 0;
            for (int j = 0; j < hiddenSize; j++)
                norm += pooled[j] * pooled[j];

            norm = Math.Sqrt(norm);
            if (norm > 0)
            {
                for (int j = 0; j < hiddenSize; j++)
                    pooled[j] = (float)(pooled[j] / norm);
            }

            return pooled;
        }

        private static string? FindFirst(IEnumerable<string> keys, params string[] candidates)
        {
            var set = new HashSet<string>(keys);
            foreach (var c in candidates)
                if (set.Contains(c)) return c;

            // fallback: partial match
            foreach (var k in keys)
            {
                foreach (var c in candidates)
                {
                    if (k.Contains(c, StringComparison.OrdinalIgnoreCase))
                        return k;
                }
            }
            return null;
        }

        public void Dispose()
        {
            _session.Dispose();
        }
    }

}
