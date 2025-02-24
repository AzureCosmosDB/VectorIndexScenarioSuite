namespace VectorIndexScenarioSuite
{
    internal static class LabelParser
    {
        public static Dictionary<string, object> ParseLineToJson(string line)
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
