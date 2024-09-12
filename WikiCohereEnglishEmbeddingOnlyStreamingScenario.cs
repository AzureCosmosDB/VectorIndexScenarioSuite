using Microsoft.Extensions.Configuration;

namespace VectorIndexScenarioSuite
{
    internal class WikiCohereEnglishEmbeddingOnlyStreamingScenario : WikiCohereEmbeddingOnlyBaseSceario
    {
        private const string RUNBOOK_PATH = "runbooks/wikipedia-35M_expirationtime_runbook.yaml";

        public WikiCohereEnglishEmbeddingOnlyStreamingScenario(IConfiguration configurations) : 
            base(configurations, ComputeInitialAndFinalThroughput(configurations).Item1)
        {

        }

        public override void Setup()
        {
            this.CosmosContainer.ReplaceThroughputAsync(ComputeInitialAndFinalThroughput(this.Configurations).Item2).Wait();
        }

        public override async Task Run()
        {
            Runbook book = await Runbook.Parse(RUNBOOK_PATH);
            int totalVectorsInserted = 0;
            int totalVectorsDeleted = 0;
            int startOperationId = Convert.ToInt32(this.Configurations["AppSettings:scenario:streaming::startOperationId"]);

            bool runIngestion = Convert.ToBoolean(this.Configurations["AppSettings:scenario:runIngestion"]);
            int totalVectors = Convert.ToInt32(this.Configurations["AppSettings:scenario:streaming:totalVectors"]);
            bool runQuery = Convert.ToBoolean(this.Configurations["AppSettings:scenario:runQuery"]);

            Console.WriteLine($"Executing Runbook from OperationId: {startOperationId}");
            foreach (var operationIdValue in book.RunbookData.Operation)
            {
                int operationId = Int32.Parse(operationIdValue.Key);
                Operation operation = operationIdValue.Value;

                switch (operation.Name)
                {
                    case "insert":
                    {
                        int start = operation.Start ?? throw new MissingFieldException("Start missing for insert.");
                        int end = operation.End ?? throw new MissingFieldException("End missing for insert.");
                        int numVectors = (end - start);
                        if (runIngestion && (operationId > startOperationId))
                        {
                            await PerformIngestion(IngestionOperationType.Insert, start, numVectors);
                            continue;
                        }

                        totalVectorsInserted += numVectors;
                        break;
                    }
                    case "search":
                    {  
                        // No warmup logic added for now as we are not concerned with latency and onyl recall.
                        if (runQuery && (operationId > startOperationId))
                        {
                            int totalQueryVectors = BigANNBinaryFormat.GetBinaryDataHeader(GetQueryDataPath()).Item1;
                            for (int kI = 0; kI < K_VALS.Length; kI++)
                            {
                                Console.WriteLine($"Performing {totalQueryVectors} queries for Recall/RU/Latency stats for K: {K_VALS[kI]}.");
                                await PerformQuery(false /* isWarmup */, totalQueryVectors, K_VALS[kI] /*KVal*/, GetQueryDataPath());
                            }

                            // Compute Recall
                        }
                        break;
                    }
                    case "delete":
                    {
                        int start = operation.Start ?? throw new MissingFieldException("Start missing for delete.");
                        int end = operation.End ?? throw new MissingFieldException("End missing for delete.");
                        int numVectors = (end - start);

                        if (runIngestion && (operationId > startOperationId))
                        {
                            await PerformIngestion(IngestionOperationType.Delete, start, numVectors);
                        }
                        totalVectorsDeleted += numVectors;
                        break;
                    }
                }

                int currentVectorCount = totalVectorsInserted - totalVectorsDeleted;
                if (currentVectorCount > totalVectors)
                {
                    Console.WriteLine($"Vectors ingested in collection: {currentVectorCount}, exceeded total vectors to be ingested {totalVectors}. " +
                        $"Exiting after finishing Step {operationId}.");
                    break;
                }
            }
        }

        private static (int, int) ComputeInitialAndFinalThroughput(IConfiguration configurations)
        {
            // Hardcoded for now.
            return (400, 10000);
        }

        public override void Stop()
        {
            // No Operation requried.
        }
    }
}
