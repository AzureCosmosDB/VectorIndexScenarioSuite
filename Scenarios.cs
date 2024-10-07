namespace VectorIndexScenarioSuite
{
    internal enum Scenarios
    {
        MSMarcoEmbeddingOnly,
        MSTuringEmbeddingOnly,
        WikiCohereEnglishEmbeddingOnly,
        WikiCohereEnglishEmnbeddingOnlyStreaming
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
