﻿using Microsoft.Extensions.Configuration;

namespace VectorIndexScenarioSuite
{
    internal class WikiCohereEnglishEmbeddingOnly35MReplaceStreamingScenario : WikiCohereEnglishEmbeddingBase
    {
        protected override string RunName => "wiki-cohere-english-embedding-only-35M-replace-streaming-" + guid;

        public WikiCohereEnglishEmbeddingOnly35MReplaceStreamingScenario(IConfiguration configurations) : 
            base(configurations, DefaultInitialAndFinalThroughput(configurations).Item1)
        { }

        public override void Setup()
        {
            this.ReplaceFinalThroughput(DefaultInitialAndFinalThroughput(this.Configurations).Item2);
        }

        public override async Task Run()
        {
            await RunStreamingScenario("runbooks/wikipedia-35M_expirationtime_replace_only_runbook.yaml");
        }

        private static (int, int) DefaultInitialAndFinalThroughput(IConfiguration configurations)
        {
            // Setup the scenario with 10physical partitions and 100K RU/s.
            // Partition count = ceil(RUs / 6000)
            return (60000, 100000);
        }

        public override void Stop()
        {
            // No Operation required.
        }
    }
}
