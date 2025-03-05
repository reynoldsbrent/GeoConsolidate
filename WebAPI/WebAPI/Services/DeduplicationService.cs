using System.Text;
using System.Text.Json;
using WebAPI.Models;

namespace WebAPI.Services
{
    public class DeduplicationService : IDeduplicationService
    {
        private readonly ILogger<DeduplicationService> _logger;
        public DeduplicationService(ILogger<DeduplicationService> logger)
        {
            _logger = logger;
        }
        public async Task<byte[]> GetDeduplicatedJsonContentAsync(string sessionId, DuplicateResponse duplicates, string originalContent)
        {
            _logger.LogInformation($"Deduplicating JSON content for session: {sessionId}");

            var originalLines = originalContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var originalCount = originalLines.Length;

            // Hashset of ids to remove
            var idsToRemove = new HashSet<string>();

            foreach (var group in duplicates.DuplicateGroups)
            {
                // Skip the first location to keep it
                // The rest of the locations will be marked for removal
                for (int i = 1; i < group.Locations.Count; i++)
                {
                    idsToRemove.Add(group.Locations[i].Id);
                }
            }

            _logger.LogInformation($"Identified {idsToRemove.Count} duplicate entries to remove");

            // Entries not in the removal list will be kept
            var deduplicatedLines = new List<string>();
            foreach (var line in originalLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var entry = JsonSerializer.Deserialize<LocationData>(line);
                    if (!idsToRemove.Contains(entry.id.ToString()))
                    {
                        deduplicatedLines.Add(line);
                    }
                } catch (JsonException ex)
                {
                    _logger.LogWarning($"Error parsing JSON line: {ex.Message}. Keeping line in output");
                    deduplicatedLines.Add(line);
                }
            }

            var removeCount = originalCount - deduplicatedLines.Count;
            _logger.LogInformation($"Deduplication complete. Removed {removeCount} duplicates.");

            // convert deduplicated lines to JSON
            var deduplicatedContent = string.Join('\n', deduplicatedLines);
            return Encoding.UTF8.GetBytes( deduplicatedContent );
        }
    }
}
