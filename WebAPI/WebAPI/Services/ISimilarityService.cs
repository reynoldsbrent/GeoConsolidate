using WebAPI.Models;

namespace WebAPI.Services
{
    public interface ISimilarityService
    {
        Task<DuplicateResponse> FindDuplicatesAsync(List<VectorEmbedding> embeddings);
    }
}
