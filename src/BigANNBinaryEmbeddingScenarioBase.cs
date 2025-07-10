using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
namespace VectorIndexScenarioSuite
{

    abstract class BigANNBinaryEmbeddingScenarioBase : Scenario
    {
        protected abstract string BaseDataFile { get; }
        protected int SliceCount { get; set; }
        protected abstract string BinaryFileExt { get; }
        protected abstract string GetGroundTruthFileName { get; }
        protected abstract string QueryFile { get; }
        protected abstract string PartitionKeyPath { get; }
        protected abstract string EmbeddingColumn { get; }
        protected abstract string EmbeddingPath { get; }
        protected abstract VectorDataType EmbeddingDataType { get; }
        protected abstract DistanceFunction EmbeddingDistanceFunction { get; }
        protected abstract ulong EmbeddingDimensions { get; }
        protected abstract int MaxPhysicalPartitionCount { get; }
        protected abstract string RunName { get; }
        protected static Guid guid = Guid.NewGuid();
        protected virtual bool IsFilterSearch {  get; } = false;

        /* Map 'K' -> Neighbor Results
         * Neighbor Results:
         * Map 'QueryId' -> List of neighbor IdWithSimilarityScore
         */
        protected ConcurrentDictionary<int, ConcurrentDictionary<string, List<IdWithSimilarityScore>>> queryRecallResults;

        /* Map 'K' -> ScenarioMetrics (RU and Latency) */
        protected ConcurrentDictionary<int, ScenarioMetrics> queryMetrics;

        protected ScenarioMetrics ingestionMetrics;

        public BigANNBinaryEmbeddingScenarioBase(IConfiguration configurations, int throughPut) : 
            base(configurations, throughPut)
        {
            this.SliceCount = Convert.ToInt32(configurations["AppSettings:scenario:sliceCount"]);
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
            string fileName = $"{this.QueryFile}.{this.BinaryFileExt}";
            return Path.Combine(directory, fileName);
        }

        protected override ContainerProperties GetContainerSpec(string containerName)
        {
            VectorIndexPath vectorIndexPath = new VectorIndexPath()
            {
                Path = this.EmbeddingPath,
                Type = VectorIndexType.DiskANN,
            };

            bool enableShardedDiskAnn = Convert.ToBoolean(this.Configurations["AppSettings:scenario:sharded:enableShardedDiskANN"]);
            if (enableShardedDiskAnn)
            {
                string shardKey = this.Configurations["AppSettings:scenario:sharded:vectorIndexShardKey"]  ?? throw new ArgumentNullException("AppSettings:scenario:sharded:vectorIndexShardKey");
                vectorIndexPath.VectorIndexShardKey = [shardKey];
            }

            ContainerProperties properties = new ContainerProperties(id: containerName, partitionKeyPath: this.PartitionKeyPath)
            {
                VectorEmbeddingPolicy = new VectorEmbeddingPolicy(new Collection<Embedding>(new List<Embedding>()
                {
                    new Embedding()
                    {
                        Path = this.EmbeddingPath,
                        DataType = this.EmbeddingDataType,
                        DistanceFunction = this.EmbeddingDistanceFunction,
                        Dimensions = (int) this.EmbeddingDimensions,
                    }
                })),
                IndexingPolicy = new IndexingPolicy()
                {
                    VectorIndexes = new()
                    {
                        vectorIndexPath
                    }
                }
            };

            properties.IndexingPolicy.IncludedPaths.Add(new IncludedPath{ Path = "/" });

            // Add EMBEDDING_PATH to excluded paths for scalar indexing.
            properties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = this.EmbeddingPath + "/*" });

            return properties;
        }

        protected async Task PerformIngestion(IngestionOperationType ingestionOperationType, int? startTagId, int startVectorId, int totalVectors)
        {
            int numIngestionBatchCount = Convert.ToInt32(this.Configurations["AppSettings:scenario:numIngestionBatchCount"]);
            numIngestionBatchCount = (numIngestionBatchCount == 0) ? 1 : numIngestionBatchCount;

            if (totalVectors % numIngestionBatchCount != 0)
            {
                throw new ArgumentException("Total vectors should be evenly divisible by numIngestionBatchCount");
            }
            int numVectorsPerRange = totalVectors / numIngestionBatchCount;

            var tasks = new List<Task>(numIngestionBatchCount);

            for (int rangeIndex = 0; rangeIndex < numIngestionBatchCount; rangeIndex++)
            {
                int startVectorIdForRange = startVectorId + (rangeIndex * numVectorsPerRange);
                Console.WriteLine(
                    $"Starting ingestion for operation {ingestionOperationType.ToString()}, range: {rangeIndex} with start vectorId: [{startVectorIdForRange}, " +
                    $"{startVectorIdForRange + numVectorsPerRange})");
                tasks.Add(BulkIngestDataForRange(ingestionOperationType, startTagId, startVectorIdForRange, numVectorsPerRange));
            }
            await Task.WhenAll(tasks);
        }

        protected async Task BulkIngestDataForRange(IngestionOperationType ingestionOperationType, int? startTagId, int startVectorId, int numVectorsToIngest)
        {
            // The batches that the SDK creates to optimize throughput have a current maximum of 2Mb or 100 operations per batch, 
            List<Task> ingestTasks = new List<Task>(COSMOSDB_MAX_BATCH_SIZE);
            string errorLogBasePath = this.Configurations["AppSettings:errorLogBasePath"] ?? 
                throw new ArgumentNullException("AppSettings:errorLogBasePath");
            string logFilePath = Path.Combine(errorLogBasePath, $"{this.RunName}-ingest.log");

            string failedIdsPath = Path.Combine(errorLogBasePath, $"{this.RunName}-failedIds.csv");

            int totalVectorsIngested = 0;
            await foreach (var document in JsonDocumentFactory.GetDocumentAsync(GetBaseDataPath(), BinaryDataType.Float32, startVectorId, numVectorsToIngest, this.IsFilterSearch))
            {
                int vectorId = Convert.ToInt32(document.Id);

                // For Replace scenario, the vectorId here is for the new image but we need original id to replace which is based on the tag.
                if (ingestionOperationType == IngestionOperationType.Replace)
                {
                    int startTagIdValue = startTagId.HasValue ? startTagId.Value : throw new ArgumentNullException("StartId is null");
                    document.Id = (startTagIdValue + (vectorId - startVectorId)).ToString();
                }

                var createTask = CreateIngestionOperationTask(ingestionOperationType,document).ContinueWith(async itemResponse =>
                {
                    if (!itemResponse.IsCompletedSuccessfully)
                    {
                        Console.WriteLine($"Operation failed for id: {vectorId}");
                        await LogErrorToFile(failedIdsPath, vectorId.ToString());

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

        private async Task RetryFailedIds(IngestionOperationType ingestionOperationType, int? startTagId)
        {
            List<int> failedIds = PopulateFailedIdsFromFile();
            if (failedIds.Count == 0)
            {
                Console.WriteLine("No failed ids to retry.");
                return;
            }
            Console.WriteLine("Retrying failed ids for ingestion...");
            foreach (var failedId in failedIds)
            {
                // Since the failedIds might not be consecutive we need to try them individually hence setting numVectorsToIngest to 1
                await BulkIngestDataForRange(ingestionOperationType, startTagId, failedId, 1);
            }
        }

        private List<int> PopulateFailedIdsFromFile()
        {
            List<int> failedIds = new List<int>();
            string failedIdsFilePath = this.Configurations["AppSettings:failedIdsFilePath"] ??
                throw new ArgumentNullException("AppSettings:failedIdsFilePath");
            if (!File.Exists(failedIdsFilePath))
            {
                Console.WriteLine($"No failed ids file found at {failedIdsFilePath}. Skipping population of failed ids.");
                return failedIds;
            }
            string[] lines = File.ReadAllLines(failedIdsFilePath);
            foreach (var line in lines)
            {
                var fields = line.Split(',');
                foreach (var field in fields)
                {
                    if (int.TryParse(field.Trim(), out int id))
                    {
                        failedIds.Add(id);
                    }
                }
            }
            return failedIds;
        }

        private Task<ItemResponse<EmbeddingDocumentBase>> CreateIngestionOperationTask(IngestionOperationType ingestionOperationType, EmbeddingDocumentBase document)
        {
            switch (ingestionOperationType)
            {
                case IngestionOperationType.Insert:
                    return this.CosmosContainerForIngestion.CreateItemAsync<EmbeddingDocumentBase>(
                        document, new PartitionKey(document.Id));
                case IngestionOperationType.Delete:
                    return this.CosmosContainerForIngestion.DeleteItemAsync<EmbeddingDocumentBase>(
                        document.Id, new PartitionKey(document.Id));
                case IngestionOperationType.Replace:
                    return this.CosmosContainerForIngestion.ReplaceItemAsync<EmbeddingDocumentBase>(
                        document, document.Id, new PartitionKey(document.Id));
                    throw new NotImplementedException("Replace not implemented yet");
                default:
                    throw new ArgumentException("Invalid IngestionOperationType");
            }
        }

        protected async Task PerformQuery(bool isWarmup, int numQueries, int KVal, string dataPath)
        {
            // Issue parallel queries to all partitions, capping this to MAX_PHYSICAL_PARTITION_COUNT but can be override through config.
            int overrideMaxConcurrancy = Convert.ToInt32(this.Configurations["AppSettings:scenario:MaxPhysicalPartitionCount"]);
            int maxConcurrancy = overrideMaxConcurrancy == 0 ? this.MaxPhysicalPartitionCount : overrideMaxConcurrancy;
            await foreach ((int vectorId, float[] vector, string whereClause) in
JsonDocumentFactory.GetQueryAsync(dataPath, BinaryDataType.Float32, 0 /* startVectorId */, numQueries, this.IsFilterSearch))
            {

                var queryDefinition = ConstructQueryDefinition(KVal, vector, whereClause);
                bool retryQueryOnFailureForLatencyMeasurement;
                do
                {
                    FeedIterator<IdWithSimilarityScore> queryResultSetIterator =
                        this.CosmosContainerForQuery.GetItemQueryIterator<IdWithSimilarityScore>(queryDefinition,
                requestOptions: new QueryRequestOptions { MaxConcurrency = maxConcurrancy });

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
                                    // add per partition server latency
                                    foreach (var serverMetrics in queryResponse.Diagnostics.GetQueryMetrics().PartitionedMetrics)
                                    {
                                        this.queryMetrics[KVal].AddServerLatencyMeasurement(serverMetrics.ServerSideMetrics.TotalTime.TotalMilliseconds);
                                    }

                                    // this is total server latency
                                    //this.queryMetrics[KVal].AddServerLatencyMeasurement(
                                    //    queryResponse.Diagnostics.GetQueryMetrics().CumulativeMetrics.TotalTime.TotalMilliseconds);
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

        private QueryDefinition ConstructQueryDefinition(int K, float[] queryVector, string whereClause)
        {
            int searchListSizeMultiplier = Convert.ToInt32(this.Configurations["AppSettings:scenario:searchListSizeMultiplier"]);

            // empty json object for using default value if multiplier is 0
            string obj_expr = searchListSizeMultiplier == 0 ? "{}" : $"{{ 'searchListSizeMultiplier': {searchListSizeMultiplier} }}";
            string queryText = $"SELECT TOP {K} c.id, VectorDistance(c.{this.EmbeddingColumn}, @vectorEmbedding) AS similarityScore " +
                $"FROM c {whereClause} ORDER BY VectorDistance(c.{this.EmbeddingColumn}, @vectorEmbedding, false, {obj_expr})";
            return new QueryDefinition(queryText).WithParameter("@vectorEmbedding", queryVector);

        }

        private string GetBaseDataPath()
        {
            string directory = this.Configurations["AppSettings:dataFilesBasePath"] ?? 
                throw new ArgumentNullException("AppSettings:dataFilesBasePath");

            string fileName = $"{this.BaseDataFile}_{this.SliceCount}.{this.BinaryFileExt}";
            return Path.Combine(directory, fileName);
        }

        protected virtual string GetGroundTruthDataPath()
        {
            string directory = this.Configurations["AppSettings:dataFilesBasePath"] ??
                throw new ArgumentNullException("AppSettings:dataFilesBasePath");
            string fileName = $"{this.GetGroundTruthFileName}_{this.SliceCount}";
            return Path.Combine(directory, fileName);
        }

        /* Allow this to be override so we can run multiple streaming scenarios in parallel
         * while sharing the base files.
         */
        protected virtual string GetGroundTruthDataPath(int stepNumber)
        {
            string directory = this.Configurations["AppSettings:dataFilesBasePath"] ??
                throw new ArgumentNullException("AppSettings:dataFilesBasePath");

            string fileName = $"step{stepNumber}.gt100";
            return Path.Combine(directory, fileName);
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

        protected async Task RunScenario()
        {
            bool onlyIngestFailedIds = Convert.ToBoolean(this.Configurations["AppSettings:onlyIngestFailedIds"]);
            if (onlyIngestFailedIds)
            {
                await RetryFailedIds(IngestionOperationType.Insert, null /* startTagId */);
            }
            else
            {
                /* Default with following steps :
                 * 1) Bulk Ingest 'scenario:slice' number of documents into Cosmos container.
                 * 2) Query Cosmos container for a query-set and calculate recall for Nearest Neighbor search.
                 */
                bool runIngestion = Convert.ToBoolean(this.Configurations["AppSettings:scenario:runIngestion"]);

                if (runIngestion)
                {
                    int sliceCount = Convert.ToInt32(this.Configurations["AppSettings:scenario:sliceCount"]);
                    int startVectorId = Convert.ToInt32(this.Configurations["AppSettings:scenario:startVectorId"]);
                    int endVectorId = Convert.ToInt32(this.Configurations["AppSettings:scenario:endVectorId"]);
                    int totalVectors = ((endVectorId == 0 || endVectorId < startVectorId) ? sliceCount : endVectorId) - startVectorId;
                    await PerformIngestion(IngestionOperationType.Insert, null /* startTagId */, startVectorId /* startVectorId */, totalVectors);
                }

                bool runQuery = Convert.ToBoolean(this.Configurations["AppSettings:scenario:runQuery"]);

                if (runQuery)
                {
                    bool performWarmup = Convert.ToBoolean(this.Configurations["AppSettings:scenario:warmup:enabled"]);
                    if (performWarmup)
                    {
                        int numWarmupQueries = Convert.ToInt32(this.Configurations["AppSettings:scenario:warmup:numWarmupQueries"]);
                        Console.WriteLine($"Performing {numWarmupQueries} queries for Warmup.");
                        await PerformQuery(true /* isWarmup */, numWarmupQueries, 10 /*KVal*/, GetBaseDataPath());
                    }

                    int totalQueryVectors = BigANNBinaryFormat.GetBinaryDataHeader(GetQueryDataPath()).Item1;

                    // only query specific number of point if specific by the config
                    int numQueries = Convert.ToInt32(this.Configurations["AppSettings:scenario:numQueries"]);
                    numQueries = numQueries == 0 ? totalQueryVectors : numQueries;

                    for (int kI = 0; kI < K_VALS.Length; kI++)
                    {
                        Console.WriteLine($"Performing {numQueries} queries for Recall/RU/Latency stats for K: {K_VALS[kI]}.");
                        await PerformQuery(false /* isWarmup */, numQueries, K_VALS[kI] /*KVal*/, GetQueryDataPath());
                    }
                }
            }
        }

        protected async Task RunStreamingScenario(string runbookPath)
        {
            bool onlyIngestFailedIds = Convert.ToBoolean(this.Configurations["AppSettings:onlyIngestFailedIds"]);
            if (onlyIngestFailedIds)
            {
                await RetryFailedIds(IngestionOperationType.Insert, null /* startTagId */);
            }
            else
            {
                Runbook book = await Runbook.Parse(runbookPath);

                int startOperationId = Convert.ToInt32(this.Configurations["AppSettings:scenario:streaming:startOperationId"]);
                int stopOperationId = Convert.ToInt32(this.Configurations["AppSettings:scenario:streaming:stopOperationId"]);

                bool runIngestion = Convert.ToBoolean(this.Configurations["AppSettings:scenario:runIngestion"]);
                int totalNetVectorsToIngest = Convert.ToInt32(this.Configurations["AppSettings:scenario:streaming:totalNetVectorsToIngest"]);
                bool runQuery = Convert.ToBoolean(this.Configurations["AppSettings:scenario:runQuery"]);

                int insertSteps = 0;
                int searchSteps = 0;
                int deleteSteps = 0;
                int replaceSteps = 0;
                int currentVectorCount = 0;

                int totalVectorsInserted = 0;
                int totalVectorsDeleted = 0;
                int totalVectorsReplaced = 0;
                foreach (var operationIdValue in book.RunbookData.Operation)
                {
                    int operationId = Int32.Parse(operationIdValue.Key);
                    Operation operation = operationIdValue.Value;

                    switch (operation.Name)
                    {
                        case "insert":
                            {
                                int startVectorId = operation.Start ?? throw new MissingFieldException("Start missing for insert.");
                                int endVectorId = operation.End ?? throw new MissingFieldException("End missing for insert.");
                                int numVectors = (endVectorId - startVectorId);
                                if (runIngestion && (operationId >= startOperationId))
                                {
                                    await PerformIngestion(IngestionOperationType.Insert, null /*startTagId */, startVectorId, numVectors);
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
                                    // Reset queryRecallResults for each step.
                                    // Query metrics are not reset as they are cumulative across steps.
                                    this.queryRecallResults =
                                            new ConcurrentDictionary<int, ConcurrentDictionary<string, List<IdWithSimilarityScore>>>();

                                    int totalQueryVectors = BigANNBinaryFormat.GetBinaryDataHeader(GetQueryDataPath()).Item1;
                                    for (int kI = 0; kI < K_VALS.Length; kI++)
                                    {
                                        Console.WriteLine($"Performing {totalQueryVectors} queries for Recall/RU/Latency stats for K: {K_VALS[kI]}.");
                                        this.queryRecallResults.TryAdd(K_VALS[kI], new ConcurrentDictionary<string, List<IdWithSimilarityScore>>());
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
                                    await PerformIngestion(IngestionOperationType.Delete, null /* startTagId */, start, numVectors);
                                }
                                totalVectorsDeleted += numVectors;

                                // Count delete step even if we skipped it as from runbook execution perspective, it was still done before.
                                deleteSteps++;
                                break;
                            }
                        case "replace":
                            {
                                int tagsStart = operation.TagsStart ?? throw new MissingFieldException("TagStart missing for replace.");
                                int tagsEnd = operation.TagsEnd ?? throw new MissingFieldException("TagEnd missing for replace.");

                                int vectorIdsStart = operation.IdsStart ?? throw new MissingFieldException("IdsStart missing for replace.");
                                int vectorIdsEnd = operation.IdsEnd ?? throw new MissingFieldException("IdsEnd missing for replace.");

                                int numVectors = (vectorIdsEnd - vectorIdsStart);
                                int numTags = (tagsEnd - tagsStart);

                                if (numTags != numVectors)
                                {
                                    throw new ArgumentException("Number of tags and vectors should be equal for replace operation.");
                                }

                                if (runIngestion && (operationId >= startOperationId))
                                {
                                    await PerformIngestion(IngestionOperationType.Replace, tagsStart, vectorIdsStart, numVectors);
                                }
                                totalVectorsReplaced += numVectors;

                                // Count replace step even if we skipped it as from runbook execution perspective, it was still done before.
                                replaceSteps++;
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

                Console.WriteLine($"Final vector count after ingestion in collection: {currentVectorCount}, " +
                    $"inserts {totalVectorsInserted}, deletes {totalVectorsDeleted}, replaces {totalVectorsReplaced}," +
                    $"total vectors to be ingested as per appSettings: {totalNetVectorsToIngest}. ");
                int totalSteps = insertSteps + deleteSteps + searchSteps;
                Console.WriteLine($"Executed {totalSteps} total steps with {insertSteps} insert steps, {deleteSteps} delete steps, {replaceSteps} replace steps" +
                    $" and {searchSteps} query steps.");
                Console.WriteLine($"Experiment End time in UTC: {DateTime.Now.ToUniversalTime()}");
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
    }
}
