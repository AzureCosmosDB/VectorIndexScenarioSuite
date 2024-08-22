using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
namespace VectorIndexScenarioSuite
{
    internal class WikiCohereEmbeddingOnlyDocument
    {
        // This should be the same as the PARTITION_KEY_PATH in the WikiCohereEnglishEmbeddingOnlyScenario.
        [JsonProperty(PropertyName = "id")]
        public string Id { get; }

        // This should be the same as the EMBEDDING_PATH in the WikiCohereEnglishEmbeddingOnlyScenario.
        [JsonProperty(PropertyName = "embedding")]
        private float[] Embedding { get; }

        public WikiCohereEmbeddingOnlyDocument(string id, float[] embedding)
        {
            this.Id = id;
            this.Embedding = embedding;
        }
    }

    internal class WikiCohereEnglishEmbeddingOnlyScenario : Scenario
    {
        private const string PARTITION_KEY_PATH = "/id";
        private const string EMBEDDING_COLOUMN = "embedding";
        private const string EMBEDDING_PATH = $"/{EMBEDDING_COLOUMN}";
        private const VectorDataType EMBEDDING_DATA_TYPE = VectorDataType.Float32;
        private const DistanceFunction EMBEDDING_DISTANCE_FUNCTION = DistanceFunction.Cosine;
        private const int EMBEDDING_DIMENSIONS = 768;
        private const string BASE_DATA_FILE = "wikipedia_base";
        private const string QUERY_FILE = "wikipedia_query";
        private const string GROUND_TRUTH_FILE = "wikipedia_truth";
        private const string BINARY_FILE_EXT = "fbin";
        private static readonly string RUN_NAME = "wiki-cohere-en-embeddingonly-" + 
            Guid.NewGuid();

        /* Map 'K' -> Neighbor Results
         * Neighbor Results:
         * Map 'QueryId' -> List of neighbor IdWithSimilarityScore
         */
        private ConcurrentDictionary<int, ConcurrentDictionary<string, List<IdWithSimilarityScore>>> queryRecallResults;

        /* Map 'K' -> ScenarioMetrics (RU and Latency) */
        private ConcurrentDictionary<int, ScenarioMetrics> queryMetrics;

        private ScenarioMetrics ingestionMetrics;

        public WikiCohereEnglishEmbeddingOnlyScenario(IConfiguration configurations) : 
            base(configurations, ComputeInitialAndFinalThroughput(configurations).Item1)
        {
            this.queryRecallResults = new ConcurrentDictionary<int, ConcurrentDictionary<string, List<IdWithSimilarityScore>>>();
            this.queryMetrics = new ConcurrentDictionary<int, ScenarioMetrics>();

            this.K_VALS = configurations.GetSection("AppSettings:scenario:kValues").Get<int[]>() ?? 
                throw new ArgumentNullException("AppSettings:scenario:kValues");
            this.ingestionMetrics = new ScenarioMetrics(0);

            for(int kI = 0; kI < K_VALS.Length; kI++)
            {
                this.queryRecallResults.TryAdd(K_VALS[kI], new ConcurrentDictionary<string, List<IdWithSimilarityScore>>());
                this.queryMetrics.TryAdd(K_VALS[kI], new ScenarioMetrics(0));
            }
        }

        public override void Setup()
        {
            this.CosmosContainer.ReplaceThroughputAsync(ComputeInitialAndFinalThroughput(this.Configurations).Item2).Wait();
        }

        public override async Task Run()
        {
            /* WikiCohereEnglishScenario is a simple scenario with following steps :
             * 1) Bulk Ingest 'scenario:slice' number of documents into Cosmos container.
             * 2) Query Cosmos container for a query-set and calcualte recall for Nearest Neighbor search.
             */
            bool runIngestion = Convert.ToBoolean(this.Configurations["AppSettings:scenario:runIngestion"]);

            if(runIngestion) 
            {
                await PerformIngestion();
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
            bool computeRecall = Convert.ToBoolean(this.Configurations["AppSettings:scenario:computeRecall"]);
            if (computeRecall)
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
                bool runQuery = Convert.ToBoolean(this.Configurations["AppSettings:scenario:runQuery"]);

                ComputeLatencyAndRUStats(runIngestion, runQuery);
            }
        }

        private static (int, int) ComputeInitialAndFinalThroughput(IConfiguration configurations)
        {
             // For wiki-coherscenario, we are starting with :
             // 1) For upto 1M embedding, Collection Create throughput of 400 RU, bumped to 10,000 RU.
             // 2) For 35M embedding, Collection Create throughput of 40,000 RU, bumped to 70,000 RU.
             // This is because we want 1 physical partition in scenrio 1 and 7 physical partitions in scenario 2 (to reduce query fanout).
             int sliceCount = Convert.ToInt32(configurations["AppSettings:scenario:sliceCount"]);
             switch (sliceCount)
             {
                 case 100000:
                 case 1000000:
                     return (400, 10000);
                 case 35000000:
                     return (40000, 70000);
                 default:
                     throw new ArgumentException("Invalid slice count.");
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

        private async Task PerformIngestion()
        {
            int numBulkIngestionBatchCount = Convert.ToInt32(this.Configurations["AppSettings:scenario:numBulkIngestionBatchCount"]);
            int totalVectors = Convert.ToInt32(this.Configurations["AppSettings:scenario:sliceCount"]);
            if (totalVectors % numBulkIngestionBatchCount != 0)
            {
                throw new ArgumentException("Total vectors should be evenly divisible by numBulkIngestionBatchCount");
            }
            int numVectorsPerRange = totalVectors / numBulkIngestionBatchCount;

            this.ingestionMetrics = new ScenarioMetrics(totalVectors);

            var tasks = new List<Task>(numBulkIngestionBatchCount);
            for (int rangeIndex = 0; rangeIndex < numBulkIngestionBatchCount; rangeIndex++)
            {
                int startVectorId = rangeIndex * numVectorsPerRange ;
                Console.WriteLine(
                    $"Starting ingestion for range: {rangeIndex} with start vectorId: [{startVectorId}, " +
                    $"{startVectorId + numVectorsPerRange})");
                tasks.Add(BulkIngestDataForRange(startVectorId, numVectorsPerRange));
            }

            await Task.WhenAll(tasks);
        }

        private async Task PerformQuery(bool isWarmup, int numQueries, int KVal, string dataPath)
        {
            if(!isWarmup)
            {
                this.queryMetrics[KVal] = new ScenarioMetrics(numQueries);
            }

            await foreach ((int vectorId, float[] vector) in 
                BigANNBinaryFormat.GetBinaryDataAsync(dataPath, BinaryDataType.Float32, 0 /* startVectorId */, numQueries))
            {
                var queryDefinition = ConstructQueryDefinition(KVal, vector);

                FeedIterator<IdWithSimilarityScore> queryResultSetIterator = 
                    this.CosmosContainer.GetItemQueryIterator<IdWithSimilarityScore>(queryDefinition);

                while (queryResultSetIterator.HasMoreResults)
                {
                    var queryResponse = await queryResultSetIterator.ReadNextAsync();

                    if (!isWarmup)
                    {
                        if (queryResponse.Count > 0)
                        {
                            if (!this.queryRecallResults[KVal].ContainsKey(vectorId.ToString()))
                            {
                                this.queryRecallResults[KVal].TryAdd(vectorId.ToString(), new List<IdWithSimilarityScore>(KVal));
                            }
                            var results = this.queryRecallResults[KVal][vectorId.ToString()];

                            foreach (var idResponse in queryResponse)
                            {
                                results.Add(idResponse);
                            }

                            this.queryMetrics[KVal].AddRequestUnitMeasurement(
                                queryResponse.RequestCharge);
                            this.queryMetrics[KVal].AddClientLatencyMeasurement(
                                queryResponse.Diagnostics.GetClientElapsedTime().TotalMilliseconds);

                            // QueryMetrics is null for second and subsequent pages of query results.
                            if (queryResponse.Diagnostics.GetQueryMetrics() != null)
                            {
                                this.queryMetrics[KVal].AddServerLatencyMeasurement(
                                    queryResponse.Diagnostics.GetQueryMetrics().CumulativeMetrics.TotalTime.TotalMilliseconds);
                            }
                        }
                    }
                }

                int vectorCount = vectorId + 1;
                if (vectorCount % COSMOSDB_MAX_BATCH_SIZE == 0)
                {
                    double percentage = ((double)vectorId / numQueries) * 100;
                    Console.WriteLine($"Finished querying {percentage.ToString("F2")}% ");
                }
            }
        }

        private QueryDefinition ConstructQueryDefinition(int K, float[] queryVector)
        {
            string queryText = $"SELECT TOP {K} c.id, VectorDistance(c.{EMBEDDING_COLOUMN}, @vectorEmbedding) AS similarityScore " +
                $"FROM c ORDER BY VectorDistance(c.{EMBEDDING_COLOUMN}, @vectorEmbedding, false)";
;
            return new QueryDefinition(queryText).WithParameter("@vectorEmbedding", queryVector);
        }

        private async Task BulkIngestDataForRange(int startVectorId, int numVectorsToIngest)
        {
            // The batches that the SDK creates to optimize throughput have a current maximum of 2Mb or 100 operations per batch, 
            List<Task> ingestTasks = new List<Task>(COSMOSDB_MAX_BATCH_SIZE);
            string errorLogBasePath = this.Configurations["AppSettings:errorLogBasePath"] ?? 
                throw new ArgumentNullException("AppSettings:errorLogBasePath");
            string logFilePath = Path.Combine(errorLogBasePath, $"{RUN_NAME}-ingest.log");

            int totalVectorsIngested = 0;
            await foreach ((int vectorId, float[] vector) in BigANNBinaryFormat.GetBinaryDataAsync(GetBaseDataPath(), BinaryDataType.Float32, startVectorId, numVectorsToIngest))
            {
                var createTask = this.CosmosContainerWithBulkClient.CreateItemAsync<WikiCohereEmbeddingOnlyDocument>(
                    new WikiCohereEmbeddingOnlyDocument(vectorId.ToString(), vector), new PartitionKey(vectorId.ToString())).ContinueWith(async itemResponse =>
                {
                    if (!itemResponse.IsCompletedSuccessfully)
                    {
                        Console.WriteLine($"Insert failed for id: {vectorId}.");

                        // Log the error to a file
                        string errorLogMessage = $"Error ingesting vectorId: {vectorId}, " +
                            $"Error: {itemResponse.Exception.InnerException.Message}";
                        await LogErrorToFile(logFilePath, errorLogMessage);
                    }
                    else
                    {
                        // Given we are doing bulk ingestion which is optimized for throughput and not latency, we are not mesuring latency numbers. 
                        this.ingestionMetrics.AddRequestUnitMeasurement(itemResponse.Result.RequestCharge);
                    }
                }).Unwrap();
                ingestTasks.Add(createTask);

                if (ingestTasks.Count == COSMOSDB_MAX_BATCH_SIZE)
                {
                    await Task.WhenAll(ingestTasks);
                    ingestTasks.Clear();
                    totalVectorsIngested += COSMOSDB_MAX_BATCH_SIZE;
                    double percentage = ((double)totalVectorsIngested / numVectorsToIngest) * 100;
                    Console.WriteLine($"Finished ingesting {percentage.ToString("F2")}% " +
                        $"for Range [{startVectorId},{startVectorId + numVectorsToIngest}).");
                }
            }

            if (ingestTasks.Count > 0)
            {
                await Task.WhenAll(ingestTasks);
                totalVectorsIngested += ingestTasks.Count;
                ingestTasks.Clear();
                Console.WriteLine($"Ingested {totalVectorsIngested} documents for range with start vectorId {startVectorId}");
            }
        }

        private string GetQueryDataPath()
        {
            string directory = this.Configurations["AppSettings:dataFilesBasePath"] ?? 
                throw new ArgumentNullException("AppSettings:dataFilesBasePath");
            string fileName = $"{QUERY_FILE}.{BINARY_FILE_EXT}";
            return Path.Combine(directory, fileName);
        }

        private string GetBaseDataPath()
        {
            string directory = this.Configurations["AppSettings:dataFilesBasePath"] ?? 
                throw new ArgumentNullException("AppSettings:dataFilesBasePath");
            string fileName = $"{BASE_DATA_FILE}_{this.Configurations["AppSettings:scenario:sliceCount"]}.{BINARY_FILE_EXT}";
            return Path.Combine(directory, fileName);
        }

        private string GetGroundTruthDataPath()
        {
            string directory = this.Configurations["AppSettings:dataFilesBasePath"] ?? 
                throw new ArgumentNullException("AppSettings:dataFilesBasePath");
            string fileName = $"{GROUND_TRUTH_FILE}_{this.Configurations["AppSettings:scenario:sliceCount"]}";
            return Path.Combine(directory, fileName);
        }

        protected override ContainerProperties GetContainerSpec(string containerName)
        {
            ContainerProperties properties = new ContainerProperties(id: containerName, partitionKeyPath: PARTITION_KEY_PATH)
            {
                VectorEmbeddingPolicy = new VectorEmbeddingPolicy(new Collection<Embedding>(new List<Embedding>()
                {
                    new Embedding()
                    {
                        Path = EMBEDDING_PATH,
                        DataType = VectorDataType.Float32,
                        DistanceFunction = DistanceFunction.DotProduct,
                        Dimensions = 768,
                    }
                })),
                IndexingPolicy = new IndexingPolicy()
                {
                    VectorIndexes = new()
                    {
                        new VectorIndexPath()
                        {
                            Path = EMBEDDING_PATH,
                            Type = VectorIndexType.DiskANN,
                        }
                    }
                }
            };

            properties.IndexingPolicy.IncludedPaths.Add(new IncludedPath{ Path = "/" });

            // Add EMBEDDING_PATH to excluded paths for scalar indexing.
            properties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = EMBEDDING_PATH + "/*" });

            return properties;
        }
    }
}
