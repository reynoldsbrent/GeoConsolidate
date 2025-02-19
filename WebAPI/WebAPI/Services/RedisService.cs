using StackExchange.Redis;
using System.Text.Json;
using WebAPI.Models;

namespace WebAPI.Services
{
    public class RedisService : IRedisService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisService> _logger;

        public RedisService(IConnectionMultiplexer redis, ILogger<RedisService> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public async Task<List<VectorEmbedding>> GetEmbeddingsAsync(string sessionId)
        {
            var db = _redis.GetDatabase();
            var data = await db.StringGetAsync($"embeddings:{sessionId}");
            return data.IsNull ? null : JsonSerializer.Deserialize<List<VectorEmbedding>>(data);
        }

        public async Task StoreEmbeddingAsync(string sessionId, List<VectorEmbedding> embeddings)
        {
            var db = _redis.GetDatabase();
            var serializedData = JsonSerializer.Serialize(embeddings);
            await db.StringSetAsync($"embeddings: {sessionId}", serializedData, TimeSpan.FromHours(24));
        }
    }
}
