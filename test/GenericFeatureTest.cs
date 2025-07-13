using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;

namespace VectorIndexScenarioSuite.Tests
{
    [TestClass]
    public class GenericFeatureTest : VectorTestBase
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
        public void CustomEndVectorIdTest()
        {
            // Please note this is not a test specific to wiki cohere scenario alone, but a test to validate the endVectorId functionality.
            string testSpecificParams = @"
            {
                ""AppSettings"": {
                    ""useEmulator"": true,
                    ""cosmosContainerId"": ""CustomEndVectorIdTest"",
                    ""scenario"": {
                        ""name"": ""wiki-cohere-english-embedding-only"",
                        ""sliceCount"": ""100000"",
                        ""runIngestion"": true,
                        ""ingestWithBulkExecution"": false,
                        ""computeRecall"": false,
                        ""endVectorId"": 1000
                    }
            }";
            string testParams = UnionJson(SetupParams, testSpecificParams);
            IConfiguration configuration = Setup(testParams);

            Scenario scenario = Program.CreateScenario(configuration);
            scenario.Setup();
            scenario.Run().Wait();
            scenario.Stop();
        }

        [TestMethod]
        public void CustomFailedIdsInsertionTest()
        {
            // Please note this is not a test specific to wiki cohere scenario alone, but a test to validate the failedIds insertion functionality.
            string testSpecificParams = @"
            {
                ""AppSettings"": {
                    ""onlyIngestFailedIds"": true,
                    ""failedIdsFilePath"": ""."",
                    ""useEmulator"": true,
                    ""cosmosContainerId"": ""CustomFailedIdsInsertionTest"",
                    ""scenario"": {
                        ""name"": ""wiki-cohere-english-embedding-only"",
                        ""sliceCount"": ""100000"",
                        ""runIngestion"": true,
                        ""ingestWithBulkExecution"": false,
                        ""computeRecall"": false
                    }
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
