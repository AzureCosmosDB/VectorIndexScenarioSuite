using Newtonsoft.Json;

namespace VectorIndexScenarioSuite
{
    internal class EmbeddingDocumentBase
    {
         [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

         [JsonProperty(PropertyName = "embedding")]
        public float[] Embedding { get; }

        public EmbeddingDocumentBase(string id, float[] embedding)
        {
            this.Id = id;
            this.Embedding = embedding;
        }
    }
}
