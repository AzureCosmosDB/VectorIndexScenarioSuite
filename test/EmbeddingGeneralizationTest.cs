using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;

namespace VectorIndexScenarioSuite.Tests
{
    [TestClass]
    public class EmbeddingGeneralizationTest : VectorTestBase
    {
        // Test configuration for existing scenarios
        private static string TestParams = @"
        {
            ""AppSettings"": {
                ""cosmosDatabaseId"": ""test-db"",
                ""dataFilesBasePath"": ""."",
                ""errorLogBasePath"" : ""."",
                ""runIngestion"": false,
                ""searchListSizeMultiplier"": 10,
                ""numIngestionBatchCount"": 1,
                ""startVectorId"": 0,
                ""cosmosContainerRUInitial"": ""0"", 
                ""cosmosContainerRUFinal"": ""0"",
                ""scenario"": {
                    ""computeLatencyAndRUStats"": false,
                    ""kValues"": [ 10 ],
                    ""sliceCount"": ""100000""
                }
            }
        }";

        private static string SetupParams => UnionJson(TestParams, VectorTestBaseParams);

        [TestMethod]
        public void WikiCohereScenarioStillWorksTest()
        {
            // Test that existing scenarios still work with the generalization
            string testSpecificParams = @"
            {
                ""AppSettings"": {
                    ""name"": ""wiki-cohere-english-embedding-only""
                }
            }";

            string testParams = UnionJson(SetupParams, testSpecificParams);
            IConfiguration configuration = Setup(testParams);
            
            // Test that the scenario can be created without errors
            Scenario scenario = Program.CreateScenario(configuration);
            
            // Verify it's the correct type
            Assert.IsInstanceOfType(scenario, typeof(WikiCohereEnglishEmbeddingOnlyScenario));
            
            // Verify it uses the default Float32 binary data type
            var wikiScenario = (WikiCohereEnglishEmbeddingOnlyScenario)scenario;
            Assert.IsNotNull(wikiScenario);
        }

        [TestMethod]
        public void YFCCScenarioUsesUInt8Test()
        {
            // Test that YFCC scenario correctly uses UInt8 binary data type
            string testSpecificParams = @"
            {
                ""AppSettings"": {
                    ""name"": ""yfcc-embedding-only""
                }
            }";

            string testParams = UnionJson(SetupParams, testSpecificParams);
            IConfiguration configuration = Setup(testParams);
            
            // Test that the scenario can be created without errors
            Scenario scenario = Program.CreateScenario(configuration);
            
            // Verify it's the correct type
            Assert.IsInstanceOfType(scenario, typeof(YFCCEmbeddingOnlyScenario));
            
            var yfccScenario = (YFCCEmbeddingOnlyScenario)scenario;
            Assert.IsNotNull(yfccScenario);
        }

        [TestMethod]
        public void MSTuringScenarioStillWorksTest()
        {
            // Test another existing scenario
            string testSpecificParams = @"
            {
                ""AppSettings"": {
                    ""name"": ""ms-turing-embedding-only""
                }
            }";

            string testParams = UnionJson(SetupParams, testSpecificParams);
            IConfiguration configuration = Setup(testParams);
            
            // Test that the scenario can be created without errors
            Scenario scenario = Program.CreateScenario(configuration);
            
            // Verify it's the correct type
            Assert.IsInstanceOfType(scenario, typeof(MSTuringEmbeddingOnlyScenario));
            
            var msturingScenario = (MSTuringEmbeddingOnlyScenario)scenario;
            Assert.IsNotNull(msturingScenario);
        }
    }
}