using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;

namespace VectorIndexScenarioSuite.Tests
{
    [TestClass]
    public class WikiCohereTest : VectorTestBase
    {
        // Releveant configurations.
        private static string WikiTestParams = @"
        {
            ""AppSettings"": {
                ""cosmosDatabaseId"": ""wiki-cohere-test-db"",
                ""name"": ""wiki-cohere-english-embedding-only"",
                ""dataFilesBasePath"": ""."",
                ""errorLogBasePath"" : ""."",
                ""runIngestion"": true,
                ""searchListSizeMultiplier"": 10,
                ""numIngestionBatchCount"": 1,
                ""startVectorId"": 0,
                /* Will pick defaults based on sliceCount */
                ""cosmosContainerRUInitial"": ""0"", 
                ""cosmosContainerRUFinal"": ""0"",
                ""scenario"": {
                    ""computeLatencyAndRUStats"": false,
                    ""kValues"": [ 10 ]
                }
            }
        }";

        private static string SetupParams => UnionJson(WikiTestParams, VectorTestBaseParams);

        [TestMethod]
        public void WikiCohereCloudBulkIngestionOnlyTest()
        {
            string testSpecificParams = @"
            {
                ""AppSettings"": {
                    ""accountEndpoint"": ""https://your-account-endpoint.documents.azure.com:443/"",
                    ""cosmosContainerId"": ""WikiCohereCloudBulkIngestionOnlyTest"",
                    ""useAADAuth"": true,
                    ""authKey"": ""your-auth-key"",
                    ""useEmulator"": false,
                    ""scenario"": {
                        ""name"": ""wiki-cohere-english-embedding-only"",  
                        ""sliceCount"": ""10000"",
                        ""runIngestion"": true,
                        ""runQuery"": false,
                        ""ingestWithBulkExecution"": true,
                        ""computeRecall"": false
                    }
            }";

            string testParams = UnionJson(SetupParams, testSpecificParams);
            var configuration = Setup(testParams);

            Scenario scenario = Program.CreateScenario(configuration);
            scenario.Setup();
            scenario.Run().Wait();
            scenario.Stop();
        }

        [TestMethod]
        public void WikiCohereEmulatorIngestionAndQueryTest()
        {
            string testSpecificParams = @"
            {
                ""AppSettings"": {
                    ""useEmulator"": true,
                    ""cosmosContainerId"": ""WikiCohereEmulatorIngestionAndQueryTest"",
                    ""scenario"": {
                        ""name"": ""wiki-cohere-english-embedding-only"",
                        ""sliceCount"": ""10000"",
                        ""runIngestion"": true,
                        ""runQuery"": true,
                        ""numQueries"": 100,
                        ""ingestWithBulkExecution"": false,
                        ""computeRecall"": false
                    }
            }";

            string testParams = UnionJson(SetupParams, testSpecificParams);
            IConfiguration configuration = Setup(testParams);
        
            Scenario scenario = Program.CreateScenario(configuration);
            scenario.Setup();
            scenario.Run().Wait();
            scenario.Stop();
        }
    }
}