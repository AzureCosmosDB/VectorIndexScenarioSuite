using Microsoft.Extensions.Configuration;

namespace VectorIndexScenarioSuite
{
    internal class WikiCohereEnglishEmbeddingOnly1MDeleteStreamingScenario : WikiCohereEnglishEmbeddingBase
    {
        protected override string RunName => "wiki-cohere-english-embedding-only-1M-delete-streaming-" + guid;

        public WikiCohereEnglishEmbeddingOnly1MDeleteStreamingScenario(IConfiguration configurations) : 
            base(configurations, DefaultInitialAndFinalThroughput(configurations).Item1)
        { }

        public override void Setup()
        {
            this.ReplaceFinalThroughput(DefaultInitialAndFinalThroughput(this.Configurations).Item2);
        }

        public override async Task Run()
        {
            await RunStreamingScenario("runbooks/wikipedia-1M_expirationtime_runbook.yaml");
        }

        private static (int, int) DefaultInitialAndFinalThroughput(IConfiguration configurations)
        {
            // Setup the scenario with 1physical partitions and 10K RU/s.
            return (400, 10000);
        }

        protected override string GetGroundTruthDataPath(int stepNumber)
        {
            string directory = this.Configurations["AppSettings:dataFilesBasePath"] ?? 
                throw new ArgumentNullException("AppSettings:dataFilesBasePath");

            string fileName = $"\\wikipedia-1M_expirationtime_runbook_data\\step{stepNumber}.gt100";
            return Path.Combine(directory, fileName);
        }

        public override void Stop()
        {
            // No Operation required.
        }
    }
}
