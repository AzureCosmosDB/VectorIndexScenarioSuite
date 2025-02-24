

namespace VectorIndexScenarioSuite
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class QueryParser
    {
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
                        else if (field=="BRAND")
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
