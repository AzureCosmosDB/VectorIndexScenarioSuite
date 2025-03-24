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

        // Function to create query_clause from query label
        public static List<List<string>> FromQuery(string queryLabel)
        {
            var queryClause = new List<List<string>>();
            var andClauses = queryLabel.Split('&');

            foreach (var token in andClauses)
            {
                var orClause = new List<string>();
                var orTokens = token.Split('|');

                foreach (var innerToken in orTokens)
                {
                    var trimmedToken = innerToken.Trim();
                    orClause.Add(trimmedToken);
                }

                queryClause.Add(orClause);
            }

            return queryClause;
        }

        public static string TryToWhereStatement(string queryLabel)
        {
            var queryClause = FromQuery(queryLabel);
            var whereClause = ToWhereStatement(queryClause);
            if (whereClause == string.Empty)
            { 
                return string.Empty; 
            }
            else
            {
                return "where " + whereClause;
            }
        }

        // Function to create a WHERE statement from query_clause
        public static string ToWhereStatement(List<List<string>> queryClause)
        {
            var whereClauses = new List<string>();

            foreach (var orClause in queryClause)
            {
                var orStatements = new List<string>();
                foreach (var condition in orClause)
                {
                    var parts = condition.Split('=');
                    if (parts.Length == 2)
                    {
                        var field = parts[0];
                        var value = parts[1];
                        if (field == "CAT")
                        {
                            orStatements.Add($"ARRAY_CONTAINS(c.category,\"{value}\")");
                        }
                        else if (field == "RATING")
                        {
                            orStatements.Add($"c.rating = \"{value}\"");
                        }
                        else if (field == "BRAND")
                        {
                            orStatements.Add($"c.brand = \"{value}\"");
                        }
                    }
                }
                if (orStatements.Count > 1)
                {
                    whereClauses.Add($"({string.Join(" OR ", orStatements)})");
                }
                else if (orStatements.Count == 1)
                {
                    whereClauses.Add(orStatements[0]);
                }
            }

            return string.Join(" AND ", whereClauses);
        }
    }
}
