namespace VectorIndexScenarioSuite
{
    internal enum Scenarios
    {
        WikiCohereEnglishEmbeddingOnly,
        WikiCohereEnglishEmnbeddingOnlyStreaming,
        MSTuringEmbeddingOnly
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
                case "ms-turing-embedding-only":
                    return Scenarios.MSTuringEmbeddingOnly;
                default:
                    throw new ArgumentException("Invalid scenario value", scenarioString);
            }
        }
    }
}
