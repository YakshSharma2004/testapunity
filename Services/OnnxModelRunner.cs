using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using testapi1.Application;

namespace testapi1.Services
{
    public sealed class OnnxModelRunner : IOnnxModelRunner, IDisposable
    {
        private readonly ILogger<OnnxModelRunner> _logger;
        private readonly OnnxModelOptions _options;
        private readonly Lazy<InferenceSession> _session;

        public OnnxModelRunner(ILogger<OnnxModelRunner> logger, IOptions<OnnxModelOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            _session = new Lazy<InferenceSession>(CreateSession);
        }

        public Task<IReadOnlyDictionary<string, Tensor<float>>> RunAsync(
            IReadOnlyDictionary<string, Tensor<float>> inputs,
            CancellationToken cancellationToken = default)
        {
            if (inputs.Count == 0)
            {
                throw new ArgumentException("At least one input tensor is required.", nameof(inputs));
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var results = _session.Value.Run((IReadOnlyCollection<NamedOnnxValue>)CreateInputs(inputs));
            var outputMap = new Dictionary<string, Tensor<float>>(StringComparer.OrdinalIgnoreCase);

            foreach (var result in results)
            {
                if (result.Value is not Tensor<float> tensor)
                {
                    throw new InvalidOperationException(
                        $"Output '{result.Name}' is not a float tensor. Update the runner for the output type.");
                }

                outputMap[result.Name] = CopyTensor(tensor);
            }

            return Task.FromResult<IReadOnlyDictionary<string, Tensor<float>>>(outputMap);
        }

        public void Dispose()
        {
            if (_session.IsValueCreated)
            {
                _session.Value.Dispose();
            }
        }

        private InferenceSession CreateSession()
        {
            if (string.IsNullOrWhiteSpace(_options.ModelPath))
            {
                throw new InvalidOperationException("Onnx:ModelPath must be configured.");
            }

            if (!File.Exists(_options.ModelPath))
            {
                throw new FileNotFoundException($"ONNX model not found at '{_options.ModelPath}'.");
            }

            _logger.LogInformation("Loading ONNX model from {ModelPath}", _options.ModelPath);
            var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            return new InferenceSession(_options.ModelPath, sessionOptions);
        }

        private IEnumerable<NamedOnnxValue> CreateInputs(IReadOnlyDictionary<string, Tensor<float>> inputs)
        {
            foreach (var entry in inputs)
            {
                yield return NamedOnnxValue.CreateFromTensor(entry.Key, entry.Value);
            }
        }

        private static DenseTensor<float> CopyTensor(Tensor<float> tensor)
        {
            var data = tensor.ToArray();
            return new DenseTensor<float>(data, tensor.Dimensions.ToArray());
        }
    }
}
