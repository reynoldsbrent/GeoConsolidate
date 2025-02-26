using System.Text.Json.Serialization;

namespace WebAPI.Models
{
    public class DuplicateResponse
    {
        [JsonPropertyName("duplicate_groups")]
        public List<DuplicateGroup> DuplicateGroups { get; set; }
        [JsonPropertyName("total_groups")]
        public int TotalGroups { get; set; }
        [JsonPropertyName("total_duplicates")]
        public int TotalDuplicates { get; set; }
    }
}
