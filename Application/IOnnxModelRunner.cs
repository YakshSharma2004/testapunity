using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace testapi1.Application
{
    public interface IOnnxModelRunner
    {
        Task<IReadOnlyDictionary<string, Tensor<float>>> RunAsync(
            IReadOnlyDictionary<string, Tensor<float>> inputs,
            CancellationToken cancellationToken = default);
    }
}
