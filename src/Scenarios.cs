namespace VectorIndexScenarioSuite
{
    internal enum Scenarios
    {
        AutomotiveEcommerce,
        MSMarcoEmbeddingOnly,
        MSTuringEmbeddingOnly,
        WikiCohereEnglishEmbeddingOnly,
        WikiCohereEnglishEmbeddingOnly1MDeleteStreaming,
        WikiCohereEnglishEmbeddingOnly1MDeleteReplaceStreaming,
        WikiCohereEnglishEmbeddingOnly1MReplaceStreaming,
        WikiCohereEnglishEmbeddingOnly35MDeleteStreaming,
        WikiCohereEnglishEmbeddingOnly35MDeleteReplaceStreaming,
        WikiCohereEnglishEmbeddingOnly35MReplaceStreaming
    }

    internal static class ScenarioParser
    {
        public static Scenarios Parse(string scenarioString)
        {
            switch (scenarioString.Trim().ToLower())
            {
                case "automotive-ecommerce":
                    return Scenarios.AutomotiveEcommerce;
                case "ms-marco-embedding-only":
                    return Scenarios.MSMarcoEmbeddingOnly;
                case "ms-turing-embedding-only":
                    return Scenarios.MSTuringEmbeddingOnly;
                case "wiki-cohere-english-embedding-only":
                    return Scenarios.WikiCohereEnglishEmbeddingOnly;
                case "wiki-cohere-english-embedding-only-1m-delete-streaming":
                    return Scenarios.WikiCohereEnglishEmbeddingOnly1MDeleteStreaming;
                case "wiki-cohere-english-embedding-only-1m-delete-replace-streaming":
                    return Scenarios.WikiCohereEnglishEmbeddingOnly1MDeleteReplaceStreaming;
                case "wiki-cohere-english-embedding-only-1m-replace-streaming":
                    return Scenarios.WikiCohereEnglishEmbeddingOnly1MReplaceStreaming;
                case "wiki-cohere-english-embedding-only-35m-delete-streaming":
                    return Scenarios.WikiCohereEnglishEmbeddingOnly35MDeleteStreaming;
                case "wiki-cohere-english-embedding-only-35m-delete-replace-streaming":
                    return Scenarios.WikiCohereEnglishEmbeddingOnly35MDeleteReplaceStreaming;
                case "wiki-cohere-english-embedding-only-35m-replace-streaming":
                    return Scenarios.WikiCohereEnglishEmbeddingOnly35MReplaceStreaming;
                default:
                    throw new ArgumentException("Invalid scenario value", scenarioString);
            }
        }
    }
}
