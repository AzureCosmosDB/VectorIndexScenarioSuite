using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;

namespace VectorIndexScenarioSuite.Tests
{
    [TestClass]
    public class WikiCohere10K : VectorTestBase
    {
        // Releveant configurations.
        private static string WikiTestParams = @"
        {
            ""AppSettings"": {
                ""useEmulator"": true,
                ""cosmosDatabaseId"": ""wiki-cohere-test-db"",
                ""cosmosContainerId"": ""wiki-cohere-test-container"",
                ""name"": ""wiki-cohere-english-embedding-only"",
                ""dataFilesBasePath"": ""Q:\\dataset\\wiki10m"",
                ""errorLogBasePath"" : ""Q:\\dataset\\wiki10m"",
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

        private string SetupParams => UnionJson(WikiTestParams, BaseTestParams);

        [TestInitialize]
        public void Setup()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(WikiTestParams)));

            var configuration = builder.Build();
            base.SetupCommon(configuration);
        }

        [TestMethod]
        public void WikiCohereIngestionOnlyTest()
        {
            string testSpecificParams = @"
            {
                ""AppSettings"": {
                    ""scenario"": {
                        ""name"": ""wiki-cohere-english-embedding-only"",  
                        ""sliceCount"": ""10000"",
                        ""runIngestion"": true,
                        ""runQuery"": false,
                        ""ingestWithBulkExecution"": false,
                        ""computeRecall"": false
                    }
            }";

            string testParams = UnionJson(SetupParams, testSpecificParams);

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testParams)));
            var testConfig = builder.Build();

            Program.TraceConfigKeyValues(testConfig);
        
            Scenario scenario = Program.CreateScenario(testConfig);
            scenario.Setup();
            scenario.Run().Wait();
            scenario.Stop();
        }

        [TestMethod]
        public void WikiCohereBulkIngestionAndQueryTest()
        {
            string testSpecificParams = @"
            {
                ""AppSettings"": {
                    ""scenario"": {
                        ""name"": ""wiki-cohere-english-embedding-only"",
                        ""sliceCount"": ""10000"",
                        ""runIngestion"": true,
                        ""runQuery"": true,
                        ""numQueries"": 100,
                        ""ingestWithBulkExecution"": true,
                        ""computeRecall"": true
                    }
            }";

            string testParams = UnionJson(SetupParams, testSpecificParams);

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testParams)));
            var testConfig = builder.Build();

            Program.TraceConfigKeyValues(testConfig);
        
            Scenario scenario = Program.CreateScenario(testConfig);
            scenario.Setup();
            scenario.Run().Wait();
            scenario.Stop();
        }
    }
}