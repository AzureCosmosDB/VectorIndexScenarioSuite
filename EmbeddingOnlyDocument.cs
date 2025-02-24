using Newtonsoft.Json;

namespace VectorIndexScenarioSuite
{
    internal class EmbeddingOnlyDocument
    {
         [JsonProperty(PropertyName = "id")]
        public string Id { get; }

         [JsonProperty(PropertyName = "embedding")]
        private float[] Embedding { get; }

        public EmbeddingOnlyDocument(string id, float[] embedding)
        {
            this.Id = id;
            this.Embedding = embedding;
        }
    }
}
