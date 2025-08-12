using Newtonsoft.Json;

namespace VectorIndexScenarioSuite
{
    internal class EmbeddingDocumentBase<T>
    {
         [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "embedding")]
        public Array Embedding { get; }

        public EmbeddingDocumentBase(string id, T[] embedding)
        {
            this.Id = id;
            this.Embedding = ConvertEmbeddingForSerialization(embedding);
        }

        // Shared helper used by ingestion + query path to ensure Uint8/Int8 are represented as int[] for JSON (avoid base64)
        internal static Array ConvertEmbeddingForSerialization<TEmbedding>(TEmbedding[] embedding)
        {
            if (embedding == null)
            {
                throw new ArgumentNullException(nameof(embedding));
            }

            // Convert byte/sbyte -> int[] so Newtonsoft serializes as numeric array instead of base64 string
            if (typeof(TEmbedding) == typeof(byte))
            {
                return Array.ConvertAll((byte[])(object)embedding, b => (int)b);
            }
            if (typeof(TEmbedding) == typeof(sbyte))
            {
                return Array.ConvertAll((sbyte[])(object)embedding, sb => (int)sb);
            }

            // Other numeric types (float, double, int, etc.) can be emitted directly
            return embedding; // still an Array
        }
    }
}
