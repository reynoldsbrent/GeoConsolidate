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

        public async Task<byte[]> GetDeduplicatedContentAsync(string sessionId)
        {
            var db = _redis.GetDatabase();
            var content = await db.StringGetAsync($"deduplicated_content: {sessionId}");

            if (content.IsNullOrEmpty)
            {
                _logger.LogWarning($"Deduplicated content not found for sessionId: {sessionId}");
                return null;
            }

            _logger.LogInformation($"Retrieved deduplicated content for sessionId: {sessionId}, size: {content.Length} bytes");
            return content;
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

        public async Task<string> GetOriginalContentAsync(string sessionId)
        {
            var db = _redis.GetDatabase();
            var content = await db.StringGetAsync($"original_content: {sessionId}");

            if (content.IsNullOrEmpty)
            {
                _logger.LogWarning($"Original content not found for sessionId: {sessionId}");
                return null;
            }

            _logger.LogInformation($"Retrieved original content for sessionId: {sessionId}, size: {content.ToString().Length} bytes");
            return content.ToString();
        }

        public async Task StoreDeduplicatedContentAsync(string sessionId, byte[] content)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync($"deduplicated_content: {sessionId}", content, TimeSpan.FromHours(24));
            _logger.LogInformation($"Stored deduplicated content for sessionId: {sessionId}, size: {content.Length} bytes");
        }

        public async Task StoreEmbeddingAsync(string sessionId, List<VectorEmbedding> embeddings)
        {
            var db = _redis.GetDatabase();
            var serializedData = JsonSerializer.Serialize(embeddings, _jsonOptions);
            await db.StringSetAsync($"embeddings: {sessionId}", serializedData, TimeSpan.FromHours(24));
        }

        public async Task StoreOriginalContentAsync(string sessionId, string content)
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync($"original_content: {sessionId}", content, TimeSpan.FromHours(24));
            _logger.LogInformation($"Stored original content for sessionId: {sessionId}, size: {content.Length} bytes");
        }
    }
}
