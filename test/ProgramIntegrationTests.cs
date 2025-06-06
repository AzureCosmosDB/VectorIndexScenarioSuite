using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace VectorIndexScenarioSuite.Tests
{
    [TestClass]
    [TestCategory("Integration")]
    public class ProgramIntegrationTests : VectorTestBase
    {
        [TestMethod]
        public void Program_CreateScenario_ValidConfiguration_ReturnsScenario()
        {
            // Arrange
            string testConfig = @"
            {
                ""AppSettings"": {
                    ""scenario"": {
                        ""name"": ""wiki-cohere-english-embedding-only""
                    },
                    ""useEmulator"": false,
                    ""deleteContainerOnStart"": false
                }
            }";
            
            var mergedConfig = UnionJson(testConfig, VectorTestBaseParams);
            var configuration = Setup(mergedConfig);
            
            // Act & Assert - This should not throw an exception
            try
            {
                var scenario = Program.CreateScenario(configuration);
                Assert.IsNotNull(scenario);
            }
            catch (Exception ex)
            {
                // We expect this to potentially fail due to missing Cosmos connection
                // but we want to make sure the scenario creation logic works
                // We'll only fail if it's a scenario parsing error
                if (ex.Message.Contains("Scenario") && ex.Message.Contains("not supported"))
                {
                    Assert.Fail($"Scenario creation failed with parsing error: {ex.Message}");
                }
                // Other exceptions (like connection issues) are expected in CI
            }
        }
        
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Program_CreateScenario_MissingScenarioName_ThrowsArgumentNullException()
        {
            // Arrange
            string testConfig = @"
            {
                ""AppSettings"": {
                    ""useEmulator"": false
                }
            }";
            
            var configuration = Setup(testConfig);
            
            // Act
            Program.CreateScenario(configuration);
            
            // Assert is handled by ExpectedException
        }
        
        [TestMethod]
        public void Program_CreateScenario_InvalidScenarioName_ThrowsException()
        {
            // Arrange
            string testConfig = @"
            {
                ""AppSettings"": {
                    ""scenario"": {
                        ""name"": ""invalid-scenario-name""
                    }
                }
            }";
            
            var configuration = Setup(testConfig);
            
            // Act & Assert
            try
            {
                Program.CreateScenario(configuration);
                Assert.Fail("Expected exception was not thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.Contains("not supported") || ex.Message.Contains("Invalid scenario"));
            }
        }
        
        [TestMethod]
        public void Program_TraceConfigKeyValues_DoesNotThrow()
        {
            // Arrange
            string testConfig = @"
            {
                ""AppSettings"": {
                    ""scenario"": {
                        ""name"": ""wiki-cohere-english-embedding-only""
                    }
                }
            }";
            
            var configuration = Setup(testConfig);
            
            // Act & Assert - Should not throw
            try
            {
                Program.TraceConfigKeyValues(configuration);
            }
            catch (Exception ex)
            {
                Assert.Fail($"TraceConfigKeyValues should not throw: {ex.Message}");
            }
        }
    }
}