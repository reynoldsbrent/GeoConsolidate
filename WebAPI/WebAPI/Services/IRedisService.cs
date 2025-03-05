using WebAPI.Models;

namespace WebAPI.Services
{
    public interface IRedisService
    {
        Task StoreEmbeddingAsync(string sessionId, List<VectorEmbedding> embeddings);
        Task<List<VectorEmbedding>> GetEmbeddingsAsync(string sessionId);
        Task StoreOriginalContentAsync(string sessionId, string content);
        Task<string> GetOriginalContentAsync(string sessionId);
        Task StoreDeduplicatedContentAsync(string sessionId, byte[] content);
        Task<byte[]> GetDeduplicatedContentAsync(string sessionId);
    }
}
