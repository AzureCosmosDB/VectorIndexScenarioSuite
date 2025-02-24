using Newtonsoft.Json;

namespace VectorIndexScenarioSuite
{
    internal class EmbeddingWithAmazonLabelDocument
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; }

        [JsonProperty(PropertyName = "embedding")]
        private float[] Embedding { get; }

        [JsonProperty(PropertyName = "brand")]
        private string Brand { get; }

        //ratting 
        [JsonProperty(PropertyName = "rating")]
        private string Rating { get; }

        // category
        [JsonProperty(PropertyName = "category")]
        private string[] Category { get; }

        public EmbeddingWithAmazonLabelDocument(string id, float[] embedding, string brand, string rating, string[] category) 
        {
            this.Id = id;
            this.Embedding = embedding;
            this.Brand = brand;
            this.Rating = rating;
            this.Category = category;
        }
    }
}
