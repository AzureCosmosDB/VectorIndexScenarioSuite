using Newtonsoft.Json;

namespace VectorIndexScenarioSuite
{
    internal class EmbeddingOnlyDocument
    {
         [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

         [JsonProperty(PropertyName = "embedding")]
        public float[] Embedding { get; }

        public EmbeddingOnlyDocument(string id, float[] embedding)
        {
            this.Id = id;
            this.Embedding = embedding;
        }
    }
}
