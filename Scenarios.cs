namespace VectorIndexScenarioSuite
{
    internal enum Scenarios
    {
        WikiCohereEnglishEmbeddingOnly,
        WikiCohereEnglishFull
    }

    internal static class ScenarioParser
    {
        public static Scenarios Parse(string scenarioString)
        {
            switch (scenarioString.Trim().ToLower())
            {
                case "wiki-cohere-english":
                    return Scenarios.WikiCohereEnglishFull;
                case "wiki-cohere-english-embedding-only":
                    return Scenarios.WikiCohereEnglishEmbeddingOnly;
                default:
                    throw new ArgumentException("Invalid scenario value", scenarioString);
            }
        }
    }
}
