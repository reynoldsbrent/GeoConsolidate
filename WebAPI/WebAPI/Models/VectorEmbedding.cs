namespace WebAPI.Models
{
    public class VectorEmbedding
    {
        public string LocationID { get; set; }
        public float[] Embedding { get; set; }
        public LocationMetadata Metadata { get; set; }
    }
}
