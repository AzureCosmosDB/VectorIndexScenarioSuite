using Newtonsoft.Json;

namespace VectorIndexScenarioSuite.filtersearch
{
    internal class AutomotiveEcommerceDocument : EmbeddingDocumentBase
    {
        [JsonProperty(PropertyName = "brand")]
        private string Brand { get; }

        [JsonProperty(PropertyName = "rating")]
        private string Rating { get; }

        [JsonProperty(PropertyName = "category")]
        private string[] Category { get; }

        // label format : 
        // BRAND=Caltric,CAT=Automotive,CAT=MotorcyclePowersports,CAT=Parts,CAT=Filters,CAT=OilFilters,RATING=5
        public AutomotiveEcommerceDocument(string id, float[] embedding, string label)
            : base(id, embedding) // Call the base class constructor
        {
            // Parse the label string into brand, rating, and category
            var labelJson = ParseAmazonLabelToJson(label);
            Brand = labelJson["brand"]?.ToString() ?? string.Empty;
            Rating = labelJson["rating"]?.ToString() ?? string.Empty;
            Category = ((List<string>?)labelJson["category"])?.ToArray() ?? Array.Empty<string>();
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

                    switch (key)
                    {
                        case "CAT":
                            ((List<string>)result["category"]).Add(value);
                            break;
                        case "BRAND":
                            result["brand"] = value;
                            break;
                        case "RATING":
                            result["rating"] = value;
                            break;
                    }
                }
            }

            return result;
        }

        // Function to create query_clause from query label
        // query label format : CAT=ExteriorAccessories&RATING=4|RATING=5
        private static List<List<string>> FromQuery(string queryLabel)
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
        private static string ToWhereStatement(List<List<string>> queryClause)
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
                        switch (field)
                        {
                            case "CAT":
                                orStatements.Add($"ARRAY_CONTAINS(c.category,\"{value}\")");
                                break;
                            case "RATING":
                                orStatements.Add($"c.rating = \"{value}\"");
                                break;
                            case "BRAND":
                                orStatements.Add($"c.brand = \"{value}\"");
                                break;
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
