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
    

    internal abstract class Scenario
    {
        // The batches that the SDK creates to optimize throughput have a current maximum of 2Mb or 100 operations per batch.
        // Please see: https://devblogs.microsoft.com/cosmosdb/introducing-bulk-support-in-the-net-sdk/
        // Query can mirror the same batch size.
        public const int COSMOSDB_MAX_BATCH_SIZE = 100;

        /* Known Slices */
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

            bool bulkIngestion = Convert.ToBoolean(this.Configurations["AppSettings:scenario:ingestWithBulkExecution"]);
            this.CosmosContainerForIngestion = CreateOrGetCollection(throughput, bulkIngestion /* bulkClient */);

            //Always query with non-bulk client to measure latency appropriately
            this.CosmosContainerForQuery = CreateOrGetCollection(throughput, false /* bulkClient */);
        }

        public abstract void Setup();

        public abstract Task Run();

        public abstract void Stop();

        protected abstract ContainerProperties GetContainerSpec(string containerName);

        protected Container CreateOrGetCollection(int throughput, bool bulkClient)
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
            await this.CosmosContainerForIngestion.ReplaceThroughputAsync(throughput);
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
