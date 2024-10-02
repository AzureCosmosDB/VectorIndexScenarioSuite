using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    abstract class WikiCohereEmbeddingOnlyBaseSceario : Scenario
    {
        protected const string BASE_DATA_FILE = "wikipedia_base";
        protected const string BINARY_FILE_EXT = "fbin";
        protected const string QUERY_FILE = "wikipedia_query";
        protected const string PARTITION_KEY_PATH = "/id";
        protected const string EMBEDDING_COLOUMN = "embedding";
        protected const string EMBEDDING_PATH = $"/{EMBEDDING_COLOUMN}";
        protected const VectorDataType EMBEDDING_DATA_TYPE = VectorDataType.Float32;
        protected const DistanceFunction EMBEDDING_DISTANCE_FUNCTION = DistanceFunction.Cosine;
        protected const int EMBEDDING_DIMENSIONS = 768;
        protected const int MAX_PHYSICAL_PARTITION_COUNT = 56;

        private static readonly string RUN_NAME = "wiki-cohere-en-embeddingonly-" + 
            Guid.NewGuid();

        /* Map 'K' -> Neighbor Results
         * Neighbor Results:
         * Map 'QueryId' -> List of neighbor IdWithSimilarityScore
         */
        protected ConcurrentDictionary<int, ConcurrentDictionary<string, List<IdWithSimilarityScore>>> queryRecallResults;

        /* Map 'K' -> ScenarioMetrics (RU and Latency) */
        protected ConcurrentDictionary<int, ScenarioMetrics> queryMetrics;

        protected ScenarioMetrics ingestionMetrics;

        public WikiCohereEmbeddingOnlyBaseSceario(IConfiguration configurations, int throughPut) : 
            base(configurations, throughPut)
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

        protected string GetQueryDataPath()
        {
            string directory = this.Configurations["AppSettings:dataFilesBasePath"] ?? 
                throw new ArgumentNullException("AppSettings:dataFilesBasePath");
            string fileName = $"{QUERY_FILE}.{BINARY_FILE_EXT}";
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

        protected async Task PerformIngestion(IngestionOperationType ingestionOperationType, int startVectorId, int totalVectors)
        {
            int numBulkIngestionBatchCount = Convert.ToInt32(this.Configurations["AppSettings:scenario:numBulkIngestionBatchCount"]);
            if (totalVectors % numBulkIngestionBatchCount != 0)
            {
                throw new ArgumentException("Total vectors should be evenly divisible by numBulkIngestionBatchCount");
            }
            int numVectorsPerRange = totalVectors / numBulkIngestionBatchCount;

            this.ingestionMetrics = new ScenarioMetrics(totalVectors);

            var tasks = new List<Task>(numBulkIngestionBatchCount);
            for (int rangeIndex = 0; rangeIndex < numBulkIngestionBatchCount; rangeIndex++)
            {
                int startVectorIdForRange = startVectorId + (rangeIndex * numVectorsPerRange) ;
                Console.WriteLine(
                    $"Starting ingestion for operation {ingestionOperationType.ToString()}, range: {rangeIndex} with start vectorId: [{startVectorIdForRange}, " +
                    $"{startVectorIdForRange + numVectorsPerRange})");
                tasks.Add(BulkIngestDataForRange(ingestionOperationType, startVectorIdForRange, numVectorsPerRange));
            }

            await Task.WhenAll(tasks);
        }

        protected async Task BulkIngestDataForRange(IngestionOperationType ingestionOperationType, int startVectorId, int numVectorsToIngest)
        {
            // The batches that the SDK creates to optimize throughput have a current maximum of 2Mb or 100 operations per batch, 
            List<Task> ingestTasks = new List<Task>(COSMOSDB_MAX_BATCH_SIZE);
            string errorLogBasePath = this.Configurations["AppSettings:errorLogBasePath"] ?? 
                throw new ArgumentNullException("AppSettings:errorLogBasePath");
            string logFilePath = Path.Combine(errorLogBasePath, $"{RUN_NAME}-ingest.log");

            int totalVectorsIngested = 0;
            await foreach ((int vectorId, float[] vector) in BigANNBinaryFormat.GetBinaryDataAsync(GetBaseDataPath(), BinaryDataType.Float32, startVectorId, numVectorsToIngest))
            {
                var createTask = CreateIngestionOperationTask(ingestionOperationType, vectorId, vector).ContinueWith(async itemResponse =>
                {
                    if (!itemResponse.IsCompletedSuccessfully)
                    {
                        Console.WriteLine($"Operation failed for id: {vectorId}.");

                        // Log the error to a file
                        string errorLogMessage = $"Error for vectorId: {vectorId}, " +
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
                    Console.WriteLine($"Finished ingestion {percentage.ToString("F2")}% " +
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

        private Task<ItemResponse<WikiCohereEmbeddingOnlyDocument>> CreateIngestionOperationTask(IngestionOperationType ingestionOperationType, int vectorId, float[] vector)
        {
            switch (ingestionOperationType)
            {
                case IngestionOperationType.Insert:
                    return this.CosmosContainerWithBulkClient.CreateItemAsync<WikiCohereEmbeddingOnlyDocument>(
                        new WikiCohereEmbeddingOnlyDocument(vectorId.ToString(), vector), new PartitionKey(vectorId.ToString()));
                case IngestionOperationType.Delete:
                    return this.CosmosContainerWithBulkClient.DeleteItemAsync<WikiCohereEmbeddingOnlyDocument>(
                        vectorId.ToString(), new PartitionKey(vectorId.ToString()));
                case IngestionOperationType.Replace:
                    // This needs APIs to be further enhanced before we support it.
                    throw new NotImplementedException("Replace not implemented yet");
                default:
                    throw new ArgumentException("Invalid IngestionOperationType");
            }
        }

        protected async Task PerformQuery(bool isWarmup, int numQueries, int KVal, string dataPath)
        {
            if(!isWarmup)
            {
                this.queryMetrics[KVal] = new ScenarioMetrics(numQueries);
            }

            await foreach ((int vectorId, float[] vector) in 
                BigANNBinaryFormat.GetBinaryDataAsync(dataPath, BinaryDataType.Float32, 0 /* startVectorId */, numQueries))
            {
                var queryDefinition = ConstructQueryDefinition(KVal, vector);

                bool retryQueryOnFailureForLatencyMeasurement;
                do
                {
                    FeedIterator<IdWithSimilarityScore> queryResultSetIterator = 
                        this.CosmosContainer.GetItemQueryIterator<IdWithSimilarityScore>(queryDefinition,
                        // Issue parallel queries to all partitions, capping this to MAX_PHYSICAL_PARTITION_COUNT but can be tuned based on change in setup.
                        requestOptions: new QueryRequestOptions { MaxConcurrency = (MAX_PHYSICAL_PARTITION_COUNT) });

                    retryQueryOnFailureForLatencyMeasurement = false;
                    while (queryResultSetIterator.HasMoreResults)
                    {
                        var queryResponse = await queryResultSetIterator.ReadNextAsync();

                        if (!isWarmup)
                        {
                            // If we are computing latency and RU stats, don't consider any query with failed requests (implies it was throttled).
                            bool computeLatencyAndRUStats = Convert.ToBoolean(this.Configurations["AppSettings:scenario:computeLatencyAndRUStats"]);
                            if (computeLatencyAndRUStats && queryResponse.Diagnostics.GetFailedRequestCount() > 0)
                            {
                                Console.WriteLine($"Retrying for vectorId : {vectorId}.");
                                retryQueryOnFailureForLatencyMeasurement = true;
                                break;
                            }

                            if (!retryQueryOnFailureForLatencyMeasurement && queryResponse.Count > 0)
                            {
                                // Get Client time before doing any more work. Validated this matches stopwatch time.
                                // The second iteration does not have meaningful RU and Latency numbers.
                                if (queryResponse.RequestCharge > 0)
                                {
                                    this.queryMetrics[KVal].AddRequestUnitMeasurement(
                                        queryResponse.RequestCharge);
                                    this.queryMetrics[KVal].AddClientLatencyMeasurement(
                                        queryResponse.Diagnostics.GetClientElapsedTime().TotalMilliseconds);
                                }

                                if (!this.queryRecallResults[KVal].ContainsKey(vectorId.ToString()))
                                {
                                    this.queryRecallResults[KVal].TryAdd(vectorId.ToString(), new List<IdWithSimilarityScore>(KVal));
                                }
                                var results = this.queryRecallResults[KVal][vectorId.ToString()];

                                foreach (var idWithScoreResponse in queryResponse)
                                {
                                    results.Add(idWithScoreResponse);
                                }

                                // Similarly, QueryMetrics is null for second and subsequent pages of query results.
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
                while ( retryQueryOnFailureForLatencyMeasurement );
            }
        }

        private QueryDefinition ConstructQueryDefinition(int K, float[] queryVector)
        {
            string queryText = $"SELECT TOP {K} c.id, VectorDistance(c.{EMBEDDING_COLOUMN}, @vectorEmbedding) AS similarityScore " +
                $"FROM c ORDER BY VectorDistance(c.{EMBEDDING_COLOUMN}, @vectorEmbedding, false)";
;
            return new QueryDefinition(queryText).WithParameter("@vectorEmbedding", queryVector);
        }

        private string GetBaseDataPath()
        {
            string directory = this.Configurations["AppSettings:dataFilesBasePath"] ?? 
                throw new ArgumentNullException("AppSettings:dataFilesBasePath");

            // For Streaming scenario, work of the full 35M data file.
            string fileName = $"{BASE_DATA_FILE}_35000000.{BINARY_FILE_EXT}";
            return Path.Combine(directory, fileName);
        }
    }
}
