using StackExchange.Redis;
using System.Text.Json;
using WebAPI.Models;

namespace WebAPI.Services
{
    public class RedisService : IRedisService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public RedisService(IConnectionMultiplexer redis, ILogger<RedisService> logger)
        {
            _redis = redis;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = null
            };
        }

        public async Task<List<VectorEmbedding>> GetEmbeddingsAsync(string sessionId)
        {
            var db = _redis.GetDatabase();
            _logger.LogInformation($"Fetching embeddings from Redis for sessionId: {sessionId}");

            var data = await db.StringGetAsync($"embeddings: {sessionId}").ConfigureAwait(false);

            if (data.IsNullOrEmpty)
            {
                _logger.LogWarning($"No embeddings found in Redis for sessionId: {sessionId}");
                return null;
            }

            _logger.LogInformation($"Embeddings found in Redis for sessionId {sessionId}, size: {data.Length} bytes");

            try
            {
                var embeddings = JsonSerializer.Deserialize<List<VectorEmbedding>>(
                    data,
                    _jsonOptions
                );

                _logger.LogInformation($"Successfully deserialized {embeddings.Count} embeddings for sessionId {sessionId}");
                return embeddings;
            }
            catch (JsonException ex)
            {
                _logger.LogError($"JSON deserialization error for sessionId {sessionId}: {ex.Message}");
                return null;
            }
        }

        public async Task StoreEmbeddingAsync(string sessionId, List<VectorEmbedding> embeddings)
        {
            var db = _redis.GetDatabase();
            var serializedData = JsonSerializer.Serialize(embeddings, _jsonOptions);
            await db.StringSetAsync($"embeddings: {sessionId}", serializedData, TimeSpan.FromHours(24));
        }
    }
}
