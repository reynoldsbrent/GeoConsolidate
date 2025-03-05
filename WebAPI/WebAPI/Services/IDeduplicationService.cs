using WebAPI.Models;

namespace WebAPI.Services
{
    public interface IDeduplicationService
    {
        Task<byte[]> GetDeduplicatedJsonContentAsync(string sessionId, DuplicateResponse duplicates, string originalContent);
    }
}
