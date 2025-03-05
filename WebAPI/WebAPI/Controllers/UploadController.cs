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
                // store original JSON file in redis
                await _redisService.StoreOriginalContentAsync(sessionId, fileContent);
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

        [HttpPost("deduplicate/{sessionId}")]
        public async Task<IActionResult> DeduplicateFile(string sessionId)
        {
            try
            {
                _logger.LogInformation($"Starting deduplication for sessionId: {sessionId}");

                var duplicates = await _similarityService.FindDuplicatesAsync(await _redisService.GetEmbeddingsAsync(sessionId));

                if (duplicates == null || duplicates.DuplicateGroups == null || duplicates.DuplicateGroups.Count == 0)
                {
                    _logger.LogInformation($"No duplicates found for sessionId: {sessionId}");
                    return Ok(new { message = "No duplicates found to remove" });
                }

                var originalContent = await _redisService.GetOriginalContentAsync(sessionId);
                if (string.IsNullOrEmpty(originalContent))
                {
                    _logger.LogWarning($"Original content not found for sessionId: {sessionId}");
                    return NotFound("Original file content not found");
                }

                var deduplicationService = HttpContext.RequestServices.GetRequiredService<IDeduplicationService>();
                var deduplicatedContent = await deduplicationService.GetDeduplicatedJsonContentAsync(sessionId, duplicates, originalContent);

                // Save deduplicated content to redis
                await _redisService.StoreDeduplicatedContentAsync(sessionId, deduplicatedContent);

                return Ok(new
                {
                    message = "Deduplication completed successfully",
                    original_count = originalContent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length,
                    deduplicated_count = Encoding.UTF8.GetString(deduplicatedContent).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length,
                    removed_count = duplicates.TotalDuplicates - duplicates.TotalGroups
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during deduplication for sessionId: {sessionId}");
                return StatusCode(500, "An error occurred during deduplication");
            }
        }

        [HttpGet("deduplicated/{sessionId}")]
        public async Task<IActionResult> DownloadDeduplicated(string sessionId)
        {
            try
            {
                _logger.LogInformation($"Retrieving deduplicated content for sessionId: {sessionId}");

                // Get deduplicated content from Redis
                var deduplicatedContent = await _redisService.GetDeduplicatedContentAsync(sessionId);
                if (deduplicatedContent == null)
                {
                    _logger.LogWarning($"Deduplicated content not found for sessionId: {sessionId}");
                    return NotFound("Deduplicated content not found. Please run deduplication first.");
                }

                // Return the deduplicated file
                return File(deduplicatedContent, "application/json", $"deduplicated_{sessionId}.json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving deduplicated content for sessionId: {sessionId}");
                return StatusCode(500, "An error occurred while retrieving deduplicated content");
            }
        }
    }
}
