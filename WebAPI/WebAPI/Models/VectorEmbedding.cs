using System.Text.Json.Serialization;

namespace WebAPI.Models
{
    public class VectorEmbedding
    {
        [JsonPropertyName("LocationID")] 
        public string LocationID { get; set; }

        [JsonPropertyName("Embedding")]
        public float[] Embedding { get; set; }

        [JsonPropertyName("Metadata")]
        public LocationMetadata Metadata { get; set; }
    }
}
