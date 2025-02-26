using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;
using WebAPI.Models;
using WebAPI.Services;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IRedisService _redisService;
        private readonly ISimilarityService _similarityService;
        private readonly ILogger<UploadController> _logger;
        private readonly string _fastApiUrl;
        private readonly IConnectionMultiplexer _redis;

        public UploadController(HttpClient httpClient, IRedisService redisService, ILogger<UploadController> logger, IConfiguration configuration, IConnectionMultiplexer redis, ISimilarityService similarityService)
        {
            _httpClient = httpClient;
            _redisService = redisService;
            _logger = logger;
            _fastApiUrl = configuration["FastApi:BaseUrl"];
            _redis = redis;
            _similarityService = similarityService;

        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file uploaded");
                }

                if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Only JSON files are accepted");
                }

                var sessionId = Guid.NewGuid().ToString();

                using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
                var fileContent = await reader.ReadToEndAsync();
                _logger.LogInformation($"File content: {fileContent.Substring(0, Math.Min(fileContent.Length, 500))}..."); // first 500 chars or if the file is shorter, get the whole length

                // Reset file stream position to 0
                file.OpenReadStream().Position = 0;

                // Stream file to FastAPI endpoint
                using var content = new MultipartFormDataContent();
                var streamContent = new StreamContent(file.OpenReadStream());
                content.Add(streamContent, "file", file.FileName);

                var response = await _httpClient.PostAsync($"{_fastApiUrl}/process-locations", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"FastAPI error: {response.StatusCode} - {errorMessage}");
                    return StatusCode((int)response.StatusCode, $"FastAPI error: {errorMessage}");
                }

                response.EnsureSuccessStatusCode();

                // Get embeddings from FastAPI response
                var embeddings = await response.Content.ReadFromJsonAsync<List<VectorEmbedding>>();

                // Store in Redis
                await _redisService.StoreEmbeddingAsync(sessionId, embeddings);

                return Ok(new { sessionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing upload");
                return StatusCode(500, "An error occured while processing the file");
            }
        }

        [HttpGet("embeddings/{sessionId}")]
        public async Task<IActionResult> GetEmbeddings(string sessionId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 50)
        {
            try
            {
                _logger.LogInformation($"Fetching embeddings for sessionId: {sessionId}, page: {page}, pageSize: {pageSize}");

                var embeddings = await _redisService.GetEmbeddingsAsync(sessionId);

                if (embeddings == null || embeddings.Count == 0)
                {
                    _logger.LogWarning($"No embeddings found for sessionId: {sessionId}");
                    return NotFound("Embeddings not found");
                }

                // Pagination calculation
                var totalItems = embeddings.Count;
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                var skip = (page - 1) * pageSize;

                var pagedEmbeddings = embeddings
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList();

                var response = new
                {
                    Data = pagedEmbeddings,
                    Pagination = new
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalItems = totalItems,
                        TotalPages = totalPages
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving embeddings for sessionId: {sessionId}");
                return StatusCode(500, "An error occurred while retrieving embeddings");
            }
        }

        [HttpGet("embeddings/{sessionId}/all")]
        public async Task<IActionResult> GetAllEmbeddings(string sessionId)
        {
            try
            {
                _logger.LogInformation($"Fetching all embeddings for sessionId: {sessionId}");

                var embeddings = await _redisService.GetEmbeddingsAsync(sessionId);

                if (embeddings == null || embeddings.Count == 0)
                {
                    _logger.LogWarning($"No embeddings found for sessionId: {sessionId}");
                    return NotFound("Embeddings not found");
                }

                // Set response header - total number of embeddings returned
                Response.Headers.Add("X-Total-Count", embeddings.Count.ToString());

                return Ok(embeddings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving embeddings for sessionId: {sessionId}");
                return StatusCode(500, "An error occurred while retrieving embeddings");
            }
        }

        [HttpGet("debug-redis/{sessionId}")]
        public async Task<IActionResult> DebugRedis(string sessionId)
        {
            var db = _redis.GetDatabase();
            var rawData = await db.StringGetAsync($"embeddings: {sessionId}");

            if (rawData.IsNullOrEmpty)
                return NotFound("No data found");

            return Ok(new { rawData = rawData.ToString() });
        }

        [HttpGet("redis-check")]
        public async Task<IActionResult> CheckRedis()
        {
            try
            {
                var db = _redis.GetDatabase();
                await db.PingAsync();
                return Ok("Redis connection successful");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Redis connection failed: {ex.Message}");
                return StatusCode(500, "Cannot connect to Redis");
            }
        }

        [HttpGet("embeddings/raw/{sessionId}")]
        public async Task<IActionResult> GetRawEmbeddings(string sessionId)
        {
            var db = _redis.GetDatabase();
            _logger.LogInformation($"Fetching raw embeddings from Redis for sessionId: {sessionId}");

            var rawData = await db.StringGetAsync($"embeddings: {sessionId}");

            if (rawData.IsNullOrEmpty)
            {
                _logger.LogWarning($"No raw embeddings found for sessionId: {sessionId}");
                return NotFound("No data found");
            }

            _logger.LogInformation($"Returning raw Redis data for sessionId {sessionId}, size: {rawData.ToString().Length} bytes");
            var rawJson = rawData.ToString();
            var parsedEmbeddings = JsonSerializer.Deserialize<List<VectorEmbedding>>(rawJson);
            var limitedEmbeddings = parsedEmbeddings?.Take(5).ToList();

            return Ok(limitedEmbeddings);
        }

        [HttpGet("embeddings/{sessionId}/duplicates")]
        public async Task<IActionResult> FindDuplicates(string sessionId)
        {
            try
            {
                _logger.LogInformation($"Finding duplicates for sessionId: {sessionId}");

                // Get all embeddings
                var embeddings = await _redisService.GetEmbeddingsAsync(sessionId);

                if (embeddings == null || embeddings.Count == 0)
                {
                    _logger.LogWarning($"No embeddings found for sessionId: {sessionId}");
                    return NotFound("No embeddings found");
                }

                _logger.LogInformation($"Found {embeddings.Count} embeddings, sending to similarity service");

                // Find duplicates using similarity service
                var duplicates = await _similarityService.FindDuplicatesAsync(embeddings);

                return Ok(duplicates);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"FastAPI error for sessionId: {sessionId}");
                return StatusCode(500, "Error communicating with similarity service");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding duplicates for sessionId: {sessionId}");
                return StatusCode(500, "An error occurred while processing duplicates");
            }
        }
    }
}
