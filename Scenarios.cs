namespace VectorIndexScenarioSuite
{
    internal enum Scenarios
    {
        Amazon,
        MSMarcoEmbeddingOnly,
        MSTuringEmbeddingOnly,
        WikiCohereEnglishEmbeddingOnly,
        WikiCohereEnglishEmbeddingOnly1MStreaming,
        WikiCohereEnglishEmbeddingOnly35MStreaming
    }

    internal static class ScenarioParser
    {
        public static Scenarios Parse(string scenarioString)
        {
            switch (scenarioString.Trim().ToLower())
            {
                case "amazon":
                    return Scenarios.Amazon;
                case "ms-marco-embedding-only":
                    return Scenarios.MSMarcoEmbeddingOnly;
                case "ms-turing-embedding-only":
                    return Scenarios.MSTuringEmbeddingOnly;
                case "wiki-cohere-english-embedding-only":
                    return Scenarios.WikiCohereEnglishEmbeddingOnly;
                case "wiki-cohere-english-embedding-only-1m-streaming":
                    return Scenarios.WikiCohereEnglishEmbeddingOnly1MStreaming;
                case "wiki-cohere-english-embedding-only-35m-streaming":
                    return Scenarios.WikiCohereEnglishEmbeddingOnly35MStreaming;
                default:
                    throw new ArgumentException("Invalid scenario value", scenarioString);
            }
        }
    }
}
