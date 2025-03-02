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

        public EmbeddingWithAmazonLabelDocument(string id, float[] embedding, string label)
            : base(id, embedding) // Call the base class constructor
        {
            // Parse the label string into brand, rating, and category
            var labelJson = ParseAmazonLabelToJson(label);
            this.Brand = labelJson["brand"]?.ToString() ?? string.Empty;
            this.Rating = labelJson["rating"]?.ToString() ?? string.Empty;
            this.Category = ((List<string>?)labelJson["category"])?.ToArray() ?? Array.Empty<string>();
        }

        public static Dictionary<string, object> ParseAmazonLabelToJson(string line)
        {
            var result = new Dictionary<string, object>();
            var parts = line.Split(',');
            result["category"] = new List<string>();

            foreach (var part in parts)
            {
                var keyValue = part.Split('=');
                if (keyValue.Length == 2)
                {
                    var key = keyValue[0];
                    var value = keyValue[1];

                    if (key == "CAT")
                    {
                        if (!result.ContainsKey("category"))
                        {
                            result["category"] = new List<string>();
                        }
                        ((List<string>)result["category"]).Add(value);
                    }
                    else if (key == "BRAND")
                    {
                        result["brand"] = value;
                    }
                    else if (key == "RATING")
                    {
                        result["rating"] = value;
                    }
                }
            }

            return result;
        }
    }
}
