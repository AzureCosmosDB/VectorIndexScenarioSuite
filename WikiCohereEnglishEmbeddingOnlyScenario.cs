using Microsoft.Extensions.Configuration;
namespace VectorIndexScenarioSuite
{ 
    internal class WikiCohereEnglishEmbeddingOnlyScenario : WikiCohereEmbeddingOnlyBaseSceario
    {
        private const string GROUND_TRUTH_FILE = "wikipedia_truth";

        public WikiCohereEnglishEmbeddingOnlyScenario(IConfiguration configurations) : 
            base(configurations, ComputeInitialAndFinalThroughput(configurations).Item1)
        {
        }

        public override void Setup()
        {
            this.CosmosContainer.ReplaceThroughputAsync(ComputeInitialAndFinalThroughput(this.Configurations).Item2).Wait();
        }

        public override async Task Run()
        {
            /* WikiCohereEnglishScenario is a simple scenario with following steps :
             * 1) Bulk Ingest 'scenario:slice' number of documents into Cosmos container.
             * 2) Query Cosmos container for a query-set and calculate recall for Nearest Neighbor search.
             */
            bool runIngestion = Convert.ToBoolean(this.Configurations["AppSettings:scenario:runIngestion"]);

            if(runIngestion) 
            {
                int totalVectors = Convert.ToInt32(this.Configurations["AppSettings:scenario:sliceCount"]);
                await PerformIngestion(IngestionOperationType.Insert, 0 /* startVectorId */, totalVectors);
            }

            bool runQuery = Convert.ToBoolean(this.Configurations["AppSettings:scenario:runQuery"]);

            if(runQuery)
            {
                bool performWarmup = Convert.ToBoolean(this.Configurations["AppSettings:scenario:warmup:enabled"]);
                if (performWarmup)
                {
                    int numWarmupQueries = Convert.ToInt32(this.Configurations["AppSettings:scenario:warmup:numWarmupQueries"]);
                    Console.WriteLine($"Performing {numWarmupQueries} queries for Warmup.");
                    await PerformQuery(true /* isWarmup */, numWarmupQueries, 10 /*KVal*/, GetBaseDataPath());
                }

                int totalQueryVectors = BigANNBinaryFormat.GetBinaryDataHeader(GetQueryDataPath()).Item1;
                for (int kI = 0; kI < K_VALS.Length; kI++)
                {
                    Console.WriteLine($"Performing {totalQueryVectors} queries for Recall/RU/Latency stats for K: {K_VALS[kI]}.");
                    await PerformQuery(false /* isWarmup */, totalQueryVectors, K_VALS[kI] /*KVal*/, GetQueryDataPath());
                }
            }
        }

        public override void Stop()
        {
            bool runQuery = Convert.ToBoolean(this.Configurations["AppSettings:scenario:runQuery"]);
            bool computeRecall = Convert.ToBoolean(this.Configurations["AppSettings:scenario:computeRecall"]);

            if (runQuery && computeRecall)
            {
                Console.WriteLine("Computing Recall.");
                GroundTruthValidator groundTruthValidator = new GroundTruthValidator(
                    GroundTruthFileType.Binary,
                    GetGroundTruthDataPath());

                for (int kI = 0; kI < K_VALS.Length; kI++)
                {
                    int kVal = K_VALS[kI];
                    float recall = groundTruthValidator.ComputeRecall(kVal, this.queryRecallResults[kVal]);

                    Console.WriteLine($"Recall for K = {kVal} is {recall}.");
                }
            }

            bool computeLatencyAndRUStats = Convert.ToBoolean(this.Configurations["AppSettings:scenario:computeLatencyAndRUStats"]);
            if (computeLatencyAndRUStats)
            {
                bool runIngestion = Convert.ToBoolean(this.Configurations["AppSettings:scenario:runIngestion"]);
                ComputeLatencyAndRUStats(runIngestion, runQuery);
            }
        }

        private void ComputeLatencyAndRUStats(bool runIngestion, bool runQuery)
        {
            if(runIngestion)
            {
                Console.WriteLine($"Ingestion Metrics:");
                Console.WriteLine($"RU Consumption: {this.ingestionMetrics.GetRequestUnitStatistics()}");
            }

            if (runQuery)
            {
                Console.WriteLine($"Query Metrics:");

                for(int kI = 0; kI < K_VALS.Length; kI++)
                {
                    int kVal = K_VALS[kI];
                    Console.WriteLine($"K = {kVal}");
                    ScenarioMetrics metrics = this.queryMetrics[kVal];

                    Console.WriteLine($"RU Consumption: {metrics.GetRequestUnitStatistics()}");
                    Console.WriteLine($"Client Latency Stats in Milliseconds: [ {metrics.GetClientLatencyStatistics()} ]");
                    Console.WriteLine($"Server Latency Stats in Milliseconds: [ {metrics.GetServerLatencyStatistics()} ]");
                }
            }
        }

        private string GetGroundTruthDataPath()
        {
            string directory = this.Configurations["AppSettings:dataFilesBasePath"] ?? 
                throw new ArgumentNullException("AppSettings:dataFilesBasePath");
            string fileName = $"{GROUND_TRUTH_FILE}_{this.Configurations["AppSettings:scenario:sliceCount"]}";
            return Path.Combine(directory, fileName);
        }

        private string GetBaseDataPath()
        {
            string directory = this.Configurations["AppSettings:dataFilesBasePath"] ?? 
                throw new ArgumentNullException("AppSettings:dataFilesBasePath");
            string fileName = $"{BASE_DATA_FILE}_{this.Configurations["AppSettings:scenario:sliceCount"]}.{BINARY_FILE_EXT}";
            return Path.Combine(directory, fileName);
        }

        private static (int, int) ComputeInitialAndFinalThroughput(IConfiguration configurations)
        {
             // For wiki-cohere scenario, we are starting with :
             // 1) For upto 1M embedding, Collection Create throughput of 400 RU, bumped to 10,000 RU.
             // 2) For 35M embedding, Collection Create throughput of 40,000 RU, bumped to 70,000 RU.
             // This is because we want 1 physical partition in scenario 1 and 7 physical partitions in scenario 2 (to reduce query fanout).
             int sliceCount = Convert.ToInt32(configurations["AppSettings:scenario:sliceCount"]);
             switch (sliceCount)
             {
                 case HUNDRED_THOUSAND:
                 case ONE_MILLION:
                     return (400, 10000);
                 case THIRTY_FIVE_MILLION:
                     return (40000, 70000);
                 default:
                     throw new ArgumentException("Invalid slice count.");
             }
        }
    }
}
