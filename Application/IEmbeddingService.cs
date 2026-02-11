namespace testapi1.Application
{
    public interface IEmbeddingService
    {
        Task<float[]> CreateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    }
}
