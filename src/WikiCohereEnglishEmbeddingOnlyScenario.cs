using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
namespace VectorIndexScenarioSuite
{ 
    internal class WikiCohereEnglishEmbeddingOnlyScenario : WikiCohereEnglishEmbeddingBase
    {
        protected override string RunName => "wiki-cohere-en-embeddingonly-" + guid;

        public WikiCohereEnglishEmbeddingOnlyScenario(IConfiguration configurations) : 
            base(configurations, DefaultInitialAndFinalThroughput(configurations).Item1)
        {
        }

        public override void Setup()
        {
            this.ReplaceFinalThroughput(DefaultInitialAndFinalThroughput(this.Configurations).Item2);
        }

        public override async Task Run()
        {
            await RunScenario();
        }

        private static (int, int) DefaultInitialAndFinalThroughput(IConfiguration configurations)
        {
            // For wiki-cohere scenario, we are starting with :
            // 1) For upto 1M embedding, Collection Create throughput of 400 RU, bumped to 10,000 RU.
            // 2) For 35M embedding, Collection Create throughput of 40,000 RU, bumped to 70,000 RU.
            // This is because we want 1 physical partition in scenario 1 and 7 physical partitions in scenario 2 (to reduce query fanout).

            int sliceCount = Convert.ToInt32(configurations["AppSettings:scenario:sliceCount"]);
            switch (sliceCount)
            {
                case TEN_THOUSAND:
                case HUNDRED_THOUSAND:
                case ONE_MILLION:
                    return (400, 10000);
                case TEN_MILLION:
                    return (12000, 20000);
                case THIRTY_FIVE_MILLION:
                    return (40000, 70000);
                default:
                    throw new ArgumentException("Invalid slice count.");
            }
        }
    }
}
