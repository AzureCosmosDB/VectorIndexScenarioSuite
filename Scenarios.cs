namespace VectorIndexScenarioSuite
{
    internal enum Scenarios
    {
        WikiCohereEnglishEmbeddingOnly,
        WikiCohereEnglishEmnbeddingOnlyStreaming
    }

    internal static class ScenarioParser
    {
        public static Scenarios Parse(string scenarioString)
        {
            switch (scenarioString.Trim().ToLower())
            {
                case "wiki-cohere-english-embedding-only":
                    return Scenarios.WikiCohereEnglishEmbeddingOnly;
                case "wiki-cohere-english-embedding-only-streaming":
                    return Scenarios.WikiCohereEnglishEmnbeddingOnlyStreaming;
                default:
                    throw new ArgumentException("Invalid scenario value", scenarioString);
            }
        }
    }
}
