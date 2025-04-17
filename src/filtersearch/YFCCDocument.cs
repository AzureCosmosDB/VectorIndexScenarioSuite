using Newtonsoft.Json;

namespace VectorIndexScenarioSuite.filtersearch
{
    internal class YFCCDocument<T> : EmbeddingDocumentBase<T>
    {
        [JsonProperty(PropertyName = "year")]
        private string Year { get; }

        [JsonProperty(PropertyName = "month")]
        private string Month { get; }

        [JsonProperty(PropertyName = "model")]
        private string Model { get; }

        [JsonProperty(PropertyName = "country")]
        private string Country { get; }

        // label format : 
        // BRAND=Caltric,CAT=Automotive,CAT=MotorcyclePowersports,CAT=Parts,CAT=Filters,CAT=OilFilters,RATING=5
        public YFCCDocument(string id, T[] embedding, string label)
            : base(id, embedding) // Call the base class constructor
        {
            var labelJson = ParseLabelToJson(label);
            Year = labelJson.TryGetValue("year", out var year) ? year?.ToString() ?? string.Empty : string.Empty;
            Month = labelJson.TryGetValue("month", out var month) ? month?.ToString() ?? string.Empty : string.Empty;
            Model = labelJson.TryGetValue("model", out var model) ? model?.ToString() ?? string.Empty : string.Empty;
            Country = labelJson.TryGetValue("country", out var country) ? country?.ToString() ?? string.Empty : string.Empty;
        }

        public static Dictionary<string, object> ParseLabelToJson(string line)
        {
            var result = new Dictionary<string, object>();
            var parts = line.Split(',');
 
            foreach (var part in parts)
            {
                var keyValue = part.Split('=');
                if (keyValue.Length == 2)
                {
                    var key = keyValue[0];
                    var value = keyValue[1];

                    switch (key.ToLower())
                    {
                        case "year":
                            result["year"] = value;
                            break;
                        case "month":
                            result["brand"] = value;
                            break;
                        case "model":
                            result["rating"] = value;
                            break;
                        case "country":
                            result["country"] = value;
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
                        switch (field.ToLower())
                        {
                            case "year":
                                orStatements.Add($"c.year = \"{value}\"");
                                break;
                            case "month":
                                orStatements.Add($"c.month = \"{value}\"");
                                break;
                            case "model":
                                orStatements.Add($"c.model = \"{value}\"");
                                break;
                            case "country":
                                orStatements.Add($"c.country = \"{value}\"");
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
