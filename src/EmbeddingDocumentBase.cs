using Newtonsoft.Json;

namespace VectorIndexScenarioSuite
{
    internal class EmbeddingDocumentBase<T>
    {
         [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

         [JsonProperty(PropertyName = "embedding")]
        public T[] Embedding { get; }

        public EmbeddingDocumentBase(string id, T[] embedding)
        {
            this.Id = id;
            this.Embedding = embedding;
        }
    }
}
