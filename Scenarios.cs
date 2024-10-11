namespace VectorIndexScenarioSuite
{
    internal enum Scenarios
    {
        MSMarcoEmbeddingOnly,
        MSTuringEmbeddingOnly,
        MSTuringEmbeddingOnlyStreaming,
        WikiCohereEnglishEmbeddingOnly,
        WikiCohereEnglishEmbeddingOnlyStreaming
    }

    internal static class ScenarioParser
    {
        public static Scenarios Parse(string scenarioString)
        {
            switch (scenarioString.Trim().ToLower())
            {
                case "ms-marco-embedding-only":
                    return Scenarios.MSMarcoEmbeddingOnly;
                case "ms-turing-embedding-only":
                    return Scenarios.MSTuringEmbeddingOnly;
                case "ms-turing-embedding-only-streaming":
                    return Scenarios.MSTuringEmbeddingOnlyStreaming;
                case "wiki-cohere-english-embedding-only":
                    return Scenarios.WikiCohereEnglishEmbeddingOnly;
                case "wiki-cohere-english-embedding-only-streaming":
                    return Scenarios.WikiCohereEnglishEmbeddingOnlyStreaming;
                default:
                    throw new ArgumentException("Invalid scenario value", scenarioString);
            }
        }
    }
}
