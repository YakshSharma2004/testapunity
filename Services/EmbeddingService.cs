using Microsoft.ML.OnnxRuntime.Tensors;
using System.Linq;

namespace testapi1.Application
{
  /*  public interface IEmbeddingService
    {
        Task<float[]> CreateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    }*/

    public sealed class EmbeddingService : IEmbeddingService
    {
        private readonly IOnnxModelRunner _onnxModelRunner;

        // Adjust these names to match your ONNX model's inputs/outputs
        private const string InputName = "input_ids";
        private const string OutputName = "sentence_embedding"; // or "last_hidden_state", etc.

        public EmbeddingService(IOnnxModelRunner onnxModelRunner)
        {
            _onnxModelRunner = onnxModelRunner;
        }

        public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new float[384];
            }

            // TODO: replace with a real tokenizer for your model.
            // For now we assume you already have token IDs computed somehow.
            // This stub uses a single fake token just so the pipeline compiles.
            // You MUST plug in your actual tokenization logic here.
            var tokenIds = FakeTokenize(text);

            // Build ONNX input tensor: shape [1, seq_len]
            var inputTensor = new DenseTensor<float>(new[] { 1, tokenIds.Length });
            for (int i = 0; i < tokenIds.Length; i++)
            {
                inputTensor[0, i] = tokenIds[i];
            }

            var inputs = new Dictionary<string, Tensor<float>>
            {
                [InputName] = inputTensor
            };

            var outputs = await _onnxModelRunner.RunAsync(inputs, cancellationToken);
            if (!outputs.TryGetValue(OutputName, out var outputTensor))
            {
                throw new InvalidOperationException($"ONNX output '{OutputName}' not found.");
            }

            // If the model already returns [1, 384], just flatten.
            if (outputTensor.Dimensions.Length == 2 &&
                outputTensor.Dimensions[0] == 1 &&
                outputTensor.Dimensions[1] == 384)
            {
                var result = new float[384];
                for (int i = 0; i < 384; i++)
                {
                    result[i] = outputTensor[0, i];
                }
                return result;
            }

            // If it returns [1, seq_len, hidden] (e.g., last_hidden_state),
            // average-pool over seq_len to get a 384-d sentence embedding.
            if (outputTensor.Dimensions.Length == 3 &&
                outputTensor.Dimensions[0] == 1 &&
                outputTensor.Dimensions[2] == 384)
            {
                int seqLen = outputTensor.Dimensions[1];
                var result = new float[384];

                for (int t = 0; t < seqLen; t++)
                {
                    for (int h = 0; h < 384; h++)
                    {
                        result[h] += outputTensor[0, t, h];
                    }
                }

                for (int h = 0; h < 384; h++)
                {
                    result[h] /= seqLen;
                }

                return result;
            }

            throw new InvalidOperationException(
                $"Unexpected embedding tensor shape: [{string.Join(", ", outputTensor.Dimensions.ToArray())}].");
        }

        // STUB: replace with your real tokenizer output (int token IDs).
        private static int[] FakeTokenize(string text)
        {
            // You will later replace this with actual tokenizer logic that
            // produces the correct IDs for your ONNX model.
            return new[] { 1 }; // placeholder "CLS" token id as int
        }
    }
}
