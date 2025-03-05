using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using System.Text.Json;
using WebAPI.Services;
using WebAPI.Models;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeduplicationController : ControllerBase
    {
        private readonly IRedisService _redisService;
        private readonly ILogger<DeduplicationController> _logger;

        public DeduplicationController(IRedisService redisService, ILogger<DeduplicationController> logger)
        {
            _redisService = redisService;
            _logger = logger;
        }

        [HttpGet("{sessionId}")]
        public async Task<IActionResult> GetDeduplicationData(string sessionId)
        {
            try
            {
                _logger.LogInformation($"Fetching deduplication visualization data for sessionId: {sessionId}");

                // Get original content
                var originalContent = await _redisService.GetOriginalContentAsync(sessionId);
                if (string.IsNullOrEmpty(originalContent))
                {
                    _logger.LogWarning($"Original content not found for sessionId: {sessionId}");
                    return NotFound("Original file content not found");
                }

                // Get deduplicated content
                var deduplicatedContentBytes = await _redisService.GetDeduplicatedContentAsync(sessionId);
                if (deduplicatedContentBytes == null)
                {
                    _logger.LogWarning($"Deduplicated content not found for sessionId: {sessionId}");
                    return NotFound("Deduplicated content not found. Please run deduplication first.");
                }

                // Parse original and deduplicated JSON
                var originalLocations = ParseLocations(originalContent);
                var deduplicatedLocations = ParseLocations(System.Text.Encoding.UTF8.GetString(deduplicatedContentBytes));

                // Return data for visualization
                return Ok(new
                {
                    originalLocations,
                    deduplicatedLocations
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving deduplication data for sessionId: {sessionId}");
                return StatusCode(500, "An error occurred while retrieving deduplication data");
            }
        }

        // Helper method to parse locations from JSON
        private List<Models.Location> ParseLocations(string jsonContent)
        {
            var locations = new List<Models.Location>();

            try
            {
                var lines = jsonContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var locationObj = JsonSerializer.Deserialize<JsonElement>(line);

                    if (locationObj.TryGetProperty("data", out var data))
                    {
                        var location = new Models.Location();

                        if (data.TryGetProperty("label", out var label))
                        {
                            location.name = label.GetString() ?? "";
                        }

                        if (data.TryGetProperty("lat", out var lat) && lat.ValueKind != JsonValueKind.Null)
                        {
                            location.latitude = lat.GetDouble();
                        }

                        if (data.TryGetProperty("lon", out var lon) && lon.ValueKind != JsonValueKind.Null)
                        {
                            location.longitude = lon.GetDouble();
                        }

                        // Only add if valid coordinates
                        if (location.latitude != 0 || location.longitude != 0)
                        {
                            locations.Add(location);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error parsing location data: {ex.Message}");
            }

            return locations;
        }
    }
}
