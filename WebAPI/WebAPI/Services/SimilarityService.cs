using System.Text.Json;
using WebAPI.Models;

namespace WebAPI.Services
{
    public class SimilarityService : ISimilarityService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SimilarityService> _logger;
        private readonly string _fastApiUrl;
        private readonly JsonSerializerOptions _jsonOptions;

        public SimilarityService(HttpClient httpClient, ILogger<SimilarityService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _fastApiUrl = configuration["FastApi:BaseUrl"];
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = null  
            };
        }
        public async Task<DuplicateResponse> FindDuplicatesAsync(List<VectorEmbedding> embeddings)
        {
            try
            {
                _logger.LogInformation($"Sending {embeddings.Count} embeddings to similarity service");

                // Log first embedding
                if (embeddings.Any())
                {
                    var first = embeddings.First();
                    _logger.LogInformation(
                        $"Sample: ID={first.LocationID}, " +
                        $"Location={first.Metadata.LabeledLocation}, " +
                        $"VectorLength={first.Embedding.Length}"
                    );
                }

                var response = await _httpClient.PostAsJsonAsync(
                    $"{_fastApiUrl}/process-similarity",
                    embeddings
                );

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"FastAPI error: {response.StatusCode} - {error}");
                    throw new HttpRequestException($"FastAPI error: {error}");
                }

                var result = await response.Content.ReadFromJsonAsync<DuplicateResponse>();

                _logger.LogInformation($"Found {result.TotalGroups} duplicate groups with {result.TotalDuplicates} total duplicates");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding duplicates");
                throw;
            }
        }
    }
}
