using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace VectorIndexScenarioSuite
{
    internal class WikiCohereEnglishEmbeddingOnlyStreamingScenario : WikiCohereEnglishEmbeddingOnlyScenario
    {
       
        protected override string RunName => "wiki-cohere-english-embedding-only-streaming-" + guid;
        private const string RUNBOOK_PATH = "runbooks/wikipedia-35M_expirationtime_runbook.yaml";
        private const string GROUND_TRUTH_FILE_PREFIX_FOR_STEP = "step";

        /* This dataset comes with ground truth computed till 100 Nearest Neighbors. */
        private const string GROUND_TRUTH_FILE_EXTENSION_FOR_STEP = ".gt100";

        public WikiCohereEnglishEmbeddingOnlyStreamingScenario(IConfiguration configurations) : 
            base(configurations)
        { }

        public override void Setup()
        {
            this.CosmosContainer.ReplaceThroughputAsync(ComputeInitialAndFinalThroughput(this.Configurations).Item2).Wait();
        }

        public override async Task Run()
        {
            Runbook book = await Runbook.Parse(RUNBOOK_PATH);
            int startOperationId = Convert.ToInt32(this.Configurations["AppSettings:scenario:streaming:startOperationId"]);
            int stopOperationId = Convert.ToInt32(this.Configurations["AppSettings:scenario:streaming:stopOperationId"]);

            bool runIngestion = Convert.ToBoolean(this.Configurations["AppSettings:scenario:runIngestion"]);
            int totalNetVectorsToIngest = Convert.ToInt32(this.Configurations["AppSettings:scenario:streaming:totalNetVectorsToIngest"]);
            bool runQuery = Convert.ToBoolean(this.Configurations["AppSettings:scenario:runQuery"]);

            int insertSteps = 0;
            int searchSteps = 0;
            int deleteSteps = 0;
            int currentVectorCount = 0;

            int totalVectorsInserted = 0;
            int totalVectorsDeleted = 0;
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
                        if (runIngestion && (operationId >= startOperationId))
                        {
                            await PerformIngestion(IngestionOperationType.Insert, start, numVectors);
                        }

                        totalVectorsInserted += numVectors;

                        // Count insert step even if we skipped it as from runbook execution perspective, it was still done before.
                        insertSteps++;
                        break;
                    }
                    case "search":
                    {  
                        // No warmup logic added for now as this scenario is focused on recall.
                        if (runQuery && (operationId >= startOperationId))
                        {
                            int totalQueryVectors = BigANNBinaryFormat.GetBinaryDataHeader(GetQueryDataPath()).Item1;
                            for (int kI = 0; kI < K_VALS.Length; kI++)
                            {
                                Console.WriteLine($"Performing {totalQueryVectors} queries for Recall/RU/Latency stats for K: {K_VALS[kI]}.");
                                await PerformQuery(false /* isWarmup */, totalQueryVectors, K_VALS[kI] /*KVal*/, GetQueryDataPath());
                            }

                            // Compute Recall
                            bool computeRecall = Convert.ToBoolean(this.Configurations["AppSettings:scenario:computeRecall"]);
                            if (computeRecall)
                            {
                                Console.WriteLine("Computing Recall.");
                                GroundTruthValidator groundTruthValidator = new GroundTruthValidator(
                                    GroundTruthFileType.Binary,
                                    GetGroundTruthDataPath(operationId));

                                for (int kI = 0; kI < K_VALS.Length; kI++)
                                {
                                    int kVal = K_VALS[kI];
                                    float recall = groundTruthValidator.ComputeRecall(kVal, this.queryRecallResults[kVal]);

                                    Console.WriteLine($"Recall for K = {kVal} is {recall}.");
                                }
                            }
                        }

                        // Count search step even if we skipped it as from runbook execution perspective, it was still done before.
                        searchSteps++;
                        break;
                    }
                    case "delete":
                    {
                        int start = operation.Start ?? throw new MissingFieldException("Start missing for delete.");
                        int end = operation.End ?? throw new MissingFieldException("End missing for delete.");
                        int numVectors = (end - start);

                        if (runIngestion && (operationId >= startOperationId))
                        {
                            await PerformIngestion(IngestionOperationType.Delete, start, numVectors);
                        }
                        totalVectorsDeleted += numVectors;

                        // Count delete step even if we skipped it as from runbook execution perspective, it was still done before.
                        deleteSteps++;
                        break;
                    }
                    default:
                    {
                        throw new InvalidOperationException($"Invalid operation {operation.Name} in runbook.");
                    }
                }

                Console.WriteLine($"Executed Operation: {operation.Name} with OperationId: {operationId}");

                currentVectorCount = totalVectorsInserted - totalVectorsDeleted;
                if (currentVectorCount > totalNetVectorsToIngest || operationId > stopOperationId)
                {
                    Console.WriteLine($"Exiting after finishing Step {operationId}.");
                    break;
                }
            }

            Console.WriteLine($"Final vector count after ingestion in collection: {currentVectorCount}, total vectors to be ingested as per appSettings: {totalNetVectorsToIngest}. ");
            int totalSteps = insertSteps + deleteSteps + searchSteps;
            Console.WriteLine($"Executed {totalSteps} total steps with {insertSteps} insert steps, {deleteSteps} delete steps and {searchSteps} query steps.");
        }

        private string GetGroundTruthDataPath(int stepNumber)
        {
            string directory = this.Configurations["AppSettings:dataFilesBasePath"] ?? 
                throw new ArgumentNullException("AppSettings:dataFilesBasePath");
            string fileName = $"{GROUND_TRUTH_FILE_PREFIX_FOR_STEP}{stepNumber}{GROUND_TRUTH_FILE_EXTENSION_FOR_STEP}";
            return Path.Combine(directory, fileName);
        }

        private static (int, int) ComputeInitialAndFinalThroughput(IConfiguration configurations)
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
