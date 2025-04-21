using Newtonsoft.Json;

namespace VectorIndexScenarioSuite
{
    internal class EmbeddingDocumentBase<T>
    {
         [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "embedding")]
        public int[] Embedding { get; }

        public EmbeddingDocumentBase(string id, T[] embedding)
        {
            this.Id = id;
            this.Embedding = Array.ConvertAll(embedding, item => Convert.ToInt32(item));
        }
    }
}
