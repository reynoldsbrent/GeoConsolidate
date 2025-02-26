using System.Text.Json.Serialization;

namespace WebAPI.Models
{
    public class LocationMetadata
    {
        [JsonPropertyName("Latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("Longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("LabeledLocation")]
        public string LabeledLocation { get; set; }
    }
}
