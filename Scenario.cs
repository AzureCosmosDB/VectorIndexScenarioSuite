﻿using Microsoft.Azure.Cosmos;
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

    internal abstract class Scenario
    {
        // The batches that the SDK creates to optimize throughput have a current maximum of 2Mb or 100 operations per batch.
        // Please see: https://devblogs.microsoft.com/cosmosdb/introducing-bulk-support-in-the-net-sdk/
        // Query can mirror the same batch size.
        public const int COSMOSDB_MAX_BATCH_SIZE = 100;

        protected IConfiguration Configurations { get; set; }

        protected Container CosmosContainerWithBulkClient { get; set; }

        protected Container CosmosContainer { get; set; }

        protected int[] K_VALS { get; set; } 

        public Scenario(IConfiguration configurations, int throughput)
        {
            this.K_VALS = null;
            this.Configurations = configurations;
            this.CosmosContainerWithBulkClient = CreateOrGetCollection(throughput, true /* bulkClient */);
            this.CosmosContainer = CreateOrGetCollection(throughput, false /* bulkClient */);
        }

        public abstract void Setup();

        public abstract Task Run();

        public abstract void Stop();

        protected abstract ContainerProperties GetContainerSpec(string containerName);

        protected Container CreateOrGetCollection(int throughput, bool bulkClient)
        {
            string containerId =
                this.Configurations["AppSettings:cosmosContainerId"] ?? throw new ArgumentNullException("cosmosContainerId");
            CosmosClient cosmosClient = CreateCosmosClient(bulkClient);

            ContainerProperties containerProperties = GetContainerSpec(containerId);
            Database database = cosmosClient.CreateDatabaseIfNotExistsAsync(this.Configurations["AppSettings:cosmosDatabaseId"]).Result;
            Container container = database.CreateContainerIfNotExistsAsync(containerProperties, throughput).Result;

            return container;
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
                MaxRetryAttemptsOnRateLimitedRequests = 9,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(60)
            };

            CosmosClient cosmosClient = new CosmosClient(
                accountEndpoint: this.Configurations["AppSettings:accountEndpoint"],
                authKeyOrResourceToken: this.Configurations["AppSettings:authKeyOrResourceTokenCredential"],
                clientOptions: cosmosClientOptions
                );

            return cosmosClient;
        }
    }
}
