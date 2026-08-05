using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace VectorIndexScenarioSuite
{
    public class IdWithSimilarityScore  
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get;}

        [JsonProperty(PropertyName = "similarityScore")]
        public double SimilarityScore   { get; }

        public IdWithSimilarityScore(string id, double similarityScore)
        {
            this.Id = id;
            this.SimilarityScore = similarityScore;
        }

        public override string ToString()
        {
            return $"(Id: {this.Id}, SimilarityScore: {this.SimilarityScore})";
        }
    }

    enum IngestionOperationType
    {
        Insert,
        Delete,
        Replace
    }
    

    public abstract class Scenario
    {
        // The batches that the SDK creates to optimize throughput have a current maximum of 2Mb or 100 operations per batch.
        // Please see: https://devblogs.microsoft.com/cosmosdb/introducing-bulk-support-in-the-net-sdk/
        // Query can mirror the same batch size.
        public const int COSMOSDB_MAX_BATCH_SIZE = 100;

        /* Known Slices */
        protected const int FIVE_THOUSAND = 5000;
        protected const int TEN_THOUSAND = 10000;
        protected const int HUNDRED_THOUSAND = 100000;
        protected const int ONE_MILLION =  1000000;
        protected const int TEN_MILLION = 10000000;
        protected const int THIRTY_FIVE_MILLION = 35000000;
        protected const int ONE_HUNDRED_MILLION = 100000000;
        protected const int ONE_BILLION = 1000000000;
        protected IConfiguration Configurations { get; set; }

        protected Container CosmosContainerForIngestion { get; set; }

        protected Container CosmosContainerForQuery { get; set; }

        protected int[] K_VALS { get; set; } 

        public Scenario(IConfiguration configurations, int throughput)
        {
            this.K_VALS = Array.Empty<int>();
            this.Configurations = configurations;

            bool deleteContainer = Convert.ToBoolean(this.Configurations["AppSettings:deleteContainerOnStart"]);
            if (deleteContainer)
            {
                DeleteContainer();
            }

            bool ingestWithBulkExecution = Convert.ToBoolean(this.Configurations["AppSettings:scenario:ingestWithBulkExecution"]);
            this.CosmosContainerForIngestion = CreateOrGetCollection(throughput, ingestWithBulkExecution /* bulkClient */);

            //Always query with non-bulk client to measure latency appropriately
            this.CosmosContainerForQuery = CreateOrGetCollection(throughput, false /* bulkClient */);
        }

        public abstract void Setup();

        public abstract Task Run();

        public abstract void Stop();

        public abstract ContainerProperties GetContainerSpec(string containerName);

        private void DeleteContainer()
        {
            string containerId = 
                this.Configurations["AppSettings:cosmosContainerId"] ?? throw new ArgumentNullException("cosmosContainerId");
            string databaseId =
                this.Configurations["AppSettings:cosmosDatabaseId"] ?? throw new ArgumentNullException("cosmosDatabaseId");

            CosmosClient deleteCosmosClient = CreateCosmosClient(false /* bulkClient */);

            try
            {
                Database database = deleteCosmosClient.GetDatabase(databaseId);

                // Check if the container exists else it will throw an exception.
                database.GetContainer(containerId).ReadContainerAsync().Wait();
        
                // If it exists, delete it
                database.GetContainer(containerId).DeleteContainerAsync().Wait();

                Console.WriteLine($"Database-Container {databaseId}-{containerId} deleted.");
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException is CosmosException cosmosEx && cosmosEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"Container '{containerId}' does not exist.");
                }
                else
                {
                    Console.WriteLine($"An error occurred when deleting collection: {ex.InnerException?.Message}");
                }
            }
        }

        private Container CreateOrGetCollection(int throughput, bool bulkClient)
        {
            string init_RU = this.Configurations["AppSettings:cosmosContainerRUInitial"] ?? throw new ArgumentNullException("cosmosContainerRUInitial");
            int init_RUValue = Convert.ToInt32(init_RU);
            if (init_RUValue > 0)
            {
                throughput = init_RUValue; // override the throughput value from the config file
            }

            string containerId =
                this.Configurations["AppSettings:cosmosContainerId"] ?? throw new ArgumentNullException("cosmosContainerId");
            CosmosClient cosmosClient = CreateCosmosClient(bulkClient);

            ContainerProperties containerProperties = GetContainerSpec(containerId);
            Database database = cosmosClient.CreateDatabaseIfNotExistsAsync(this.Configurations["AppSettings:cosmosDatabaseId"]).Result;
            Container container = database.CreateContainerIfNotExistsAsync(containerProperties, throughput).Result;

            return container;
        }

        protected async void ReplaceFinalThroughput(int throughput)
        {
            string final_RU = this.Configurations["AppSettings:cosmosContainerRUFinal"] ?? throw new ArgumentNullException("cosmosContainerRUFinal");
            int final_RUValue = Convert.ToInt32(final_RU);
            if (final_RUValue > 0)
            {
                throughput = final_RUValue; // override the throughput value from the config file
            }
            try
            {
                await this.CosmosContainerForIngestion.ReplaceThroughputAsync(throughput);
            }
            catch (Exception ex)
            {
                // Throughput management may be unavailable when using AAD auth without control-plane
                // permissions (or on pre-provisioned containers). Don't let this crash the run.
                Console.WriteLine($"Warning: could not replace throughput ({throughput} RU/s): {ex.Message}");
            }
        }

        /// <summary>
        /// Polls the container's index transformation progress until the index (including the
        /// DiskANN vector index) is fully built, i.e. the "lazy catch-up" completes.
        /// Returns the elapsed time in milliseconds. Returns -1 if the progress header is
        /// unavailable, or the elapsed time so far if the timeout is hit.
        /// </summary>
        protected async Task<double> WaitForIndexTransformationAsync(
            TimeSpan pollInterval, TimeSpan timeout)
        {
            const string ProgressHeader = "x-ms-documentdb-collection-index-transformation-progress";
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int lastProgress = -1;
            bool headerSeen = false;

            while (true)
            {
                int progress = -1;
                try
                {
                    ContainerResponse response = await this.CosmosContainerForIngestion.ReadContainerAsync(
                        requestOptions: new ContainerRequestOptions { PopulateQuotaInfo = true });
                    string? raw = response.Headers[ProgressHeader];
                    if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out int parsed))
                    {
                        progress = parsed;
                        headerSeen = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: could not read index transformation progress: {ex.Message}");
                }

                if (progress != lastProgress)
                {
                    Console.WriteLine($"Index transformation progress: {progress}% (elapsed {stopwatch.Elapsed.TotalSeconds:F1}s)");
                    lastProgress = progress;
                }

                // progress == 100 => fully built. If the header is never returned (e.g. progress is
                // reported as -1 meaning "already up to date"), treat a non-in-progress state as done.
                if (progress >= 100)
                {
                    stopwatch.Stop();
                    return stopwatch.Elapsed.TotalMilliseconds;
                }

                if (stopwatch.Elapsed >= timeout)
                {
                    Console.WriteLine($"Warning: index transformation did not reach 100% within {timeout.TotalSeconds}s (last {lastProgress}%).");
                    stopwatch.Stop();
                    return stopwatch.Elapsed.TotalMilliseconds;
                }

                await Task.Delay(pollInterval);

                if (!headerSeen && stopwatch.Elapsed > TimeSpan.FromSeconds(30))
                {
                    // Header never surfaced; nothing to wait on.
                    Console.WriteLine("Index transformation progress header unavailable; skipping catch-up wait.");
                    stopwatch.Stop();
                    return -1;
                }
            }
        }

        protected async Task LogErrorToFile(string filePath, string message)
        {
            string formattedMessage = message + Environment.NewLine;
            using (var stream = new FileStream(filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite))
            using (var writer = new StreamWriter(stream))
            {
                await writer.WriteLineAsync(message);
            }
        }

        private CosmosClient CreateCosmosClient(bool bulkExecution)
        {
            CosmosClientOptions cosmosClientOptions = new()
            {
                ConnectionMode = ConnectionMode.Direct,
                AllowBulkExecution = bulkExecution,
                // SDK will handle throttles and also wait for the amount of time the service tells it to wait and retry after the time has elapsed.
                // Please see : https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/how-to-migrate-from-bulk-executor-library
                MaxRetryAttemptsOnRateLimitedRequests = 100,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(600)
            };

            bool useEmulator = Convert.ToBoolean(this.Configurations["AppSettings:useEmulator"]);
            if (useEmulator)
            {
                    return new CosmosClient(
                        accountEndpoint: this.Configurations["AppSettings:emulatorSettings:emulatorEndPoint"],
                        authKeyOrResourceToken: this.Configurations["AppSettings:emulatorSettings:emulatorKey"],
                        clientOptions: cosmosClientOptions
                    );
            }
            else
            {
                bool useAADAuth = Convert.ToBoolean(this.Configurations["AppSettings:useAADAuth"]);
                if (useAADAuth) 
                {
                    return new CosmosClient(
                        accountEndpoint: this.Configurations["AppSettings:accountEndpoint"],
                        tokenCredential: new DefaultAzureCredential(),
                        clientOptions: cosmosClientOptions
                   );
                }
                else
                {
                    return new CosmosClient(
                        accountEndpoint: this.Configurations["AppSettings:accountEndpoint"],
                        authKeyOrResourceToken: this.Configurations["AppSettings:authKey"],
                        clientOptions: cosmosClientOptions
                    );
                }
            }
        }
    }
}
