using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        private readonly ILogger<UploadController> _logger;
        private readonly string _fastApiUrl;

        public UploadController(HttpClient httpClient, IRedisService redisService, ILogger<UploadController> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _redisService = redisService;
            _logger = logger;
            _fastApiUrl = configuration["FastApi:BaseUrl"];
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

                // Stream file to FastAPI endpoint
                using var streamContent = new StreamContent(file.OpenReadStream());
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                // Send file stream to FastAPI endpoint to process
                var response = await _httpClient.PostAsync($"{_fastApiUrl}/process-locations", streamContent);

                response.EnsureSuccessStatusCode();

                // Get embeddings from FastAPI response
                var embeddings = await response.Content.ReadFromJsonAsync<List<VectorEmbedding>>();

                // Store in Redis
                await _redisService.StoreEmbeddingAsync(sessionId, embeddings);

                return Ok(new { sessionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procession upload");
                return StatusCode(500, "An error occured while processing the file");
            }
        }

        [HttpGet("embeddings/{sessionId")]
        public async Task<IActionResult> GetEmbeddings(string sessionId)
        {
            try
            {
                var embeddings = await _redisService.GetEmbeddingsAsync(sessionId);
                if (embeddings == null)
                {
                    return NotFound("Embeddings not found");
                }

                return Ok(embeddings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving embeddings");
                return StatusCode(500, "An error occured while retrieving embeddings");
            }
        }
    }
}
