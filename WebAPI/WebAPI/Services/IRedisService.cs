using WebAPI.Models;

namespace WebAPI.Services
{
    public interface IRedisService
    {
        Task StoreEmbeddingAsync(string sessionId, List<VectorEmbedding> embeddings);
        Task<List<VectorEmbedding>> GetEmbeddingsAsync(string sessionId);
    }
}
