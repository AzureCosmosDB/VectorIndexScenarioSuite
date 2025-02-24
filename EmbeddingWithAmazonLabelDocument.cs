using Newtonsoft.Json;

namespace VectorIndexScenarioSuite
{
    internal class EmbeddingWithAmazonLabelDocument : EmbeddingOnlyDocument
    {
        [JsonProperty(PropertyName = "brand")]
        private string Brand { get; }

        //ratting 
        [JsonProperty(PropertyName = "rating")]
        private string Rating { get; }

        // category
        [JsonProperty(PropertyName = "category")]
        private string[] Category { get; }

        public EmbeddingWithAmazonLabelDocument(string id, float[] embedding, string brand, string rating, string[] category)
            : base(id, embedding) // Call the base class constructor
        {
            this.Brand = brand;
            this.Rating = rating;
            this.Category = category;
        }
    }
}
