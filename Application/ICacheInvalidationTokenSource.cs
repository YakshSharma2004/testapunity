using Microsoft.Extensions.Primitives;

namespace testapi1.Application
{
    public interface ICacheInvalidationTokenSource
    {
        IChangeToken GetChangeToken();
    }
}
