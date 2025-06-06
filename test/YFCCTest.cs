using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;

namespace VectorIndexScenarioSuite.Tests
{
    [TestClass]
    public class YFCCTest : VectorTestBase
    {
        // Relevant configurations for YFCC scenario
        private static string YFCCTestParams = @"
        {
            ""AppSettings"": {
                ""cosmosDatabaseId"": ""yfcc-test-db"",
                ""name"": ""yfcc-embedding-only"",
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
                    ""kValues"": [ 10 ],
                    ""sliceCount"": ""100000""
                }
            }
        }";

        private static string SetupParams => UnionJson(YFCCTestParams, VectorTestBaseParams);

        [TestMethod]
        public void YFCCScenarioCreationTest()
        {
            IConfiguration configuration = Setup(SetupParams);
            
            // Test that the scenario can be created without errors
            Scenario scenario = Program.CreateScenario(configuration);
            
            // Verify it's the correct type
            Assert.IsInstanceOfType(scenario, typeof(YFCCEmbeddingOnlyScenario));
            
            // Verify basic properties are set correctly
            var yfccScenario = (YFCCEmbeddingOnlyScenario)scenario;
            Assert.IsNotNull(yfccScenario);
        }
    }
}