using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;

namespace VectorIndexScenarioSuite.Tests
{
    [TestClass]
    [TestCategory("Unit")]
    public class ScenarioParsingTests
    {
        [TestMethod]
        public void ScenarioParser_ValidScenarioName_ReturnsCorrectEnum()
        {
            // Arrange
            string scenarioName = "wiki-cohere-english-embedding-only";
            
            // Act
            var result = ScenarioParser.Parse(scenarioName);
            
            // Assert
            Assert.AreEqual(Scenarios.WikiCohereEnglishEmbeddingOnly, result);
        }
        
        [TestMethod]
        public void ScenarioParser_CaseInsensitive_ReturnsCorrectEnum()
        {
            // Arrange
            string scenarioName = "WIKI-COHERE-ENGLISH-EMBEDDING-ONLY";
            
            // Act
            var result = ScenarioParser.Parse(scenarioName);
            
            // Assert
            Assert.AreEqual(Scenarios.WikiCohereEnglishEmbeddingOnly, result);
        }
        
        [TestMethod]
        public void ScenarioParser_WithWhitespace_ReturnsCorrectEnum()
        {
            // Arrange
            string scenarioName = "  wiki-cohere-english-embedding-only  ";
            
            // Act
            var result = ScenarioParser.Parse(scenarioName);
            
            // Assert
            Assert.AreEqual(Scenarios.WikiCohereEnglishEmbeddingOnly, result);
        }
        
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ScenarioParser_InvalidScenarioName_ThrowsArgumentException()
        {
            // Arrange
            string invalidScenarioName = "invalid-scenario-name";
            
            // Act
            ScenarioParser.Parse(invalidScenarioName);
            
            // Assert is handled by ExpectedException
        }
        
        [TestMethod]
        public void ScenarioParser_AllValidScenarios_ParseCorrectly()
        {
            // Arrange & Act & Assert
            Assert.AreEqual(Scenarios.AutomotiveEcommerce, ScenarioParser.Parse("automotive-ecommerce"));
            Assert.AreEqual(Scenarios.MSMarcoEmbeddingOnly, ScenarioParser.Parse("ms-marco-embedding-only"));
            Assert.AreEqual(Scenarios.MSTuringEmbeddingOnly, ScenarioParser.Parse("ms-turing-embedding-only"));
            Assert.AreEqual(Scenarios.WikiCohereEnglishEmbeddingOnly, ScenarioParser.Parse("wiki-cohere-english-embedding-only"));
            Assert.AreEqual(Scenarios.WikiCohereEnglishEmbeddingOnly1MDeleteStreaming, ScenarioParser.Parse("wiki-cohere-english-embedding-only-1m-delete-streaming"));
            Assert.AreEqual(Scenarios.WikiCohereEnglishEmbeddingOnly1MDeleteReplaceStreaming, ScenarioParser.Parse("wiki-cohere-english-embedding-only-1m-delete-replace-streaming"));
            Assert.AreEqual(Scenarios.WikiCohereEnglishEmbeddingOnly1MReplaceStreaming, ScenarioParser.Parse("wiki-cohere-english-embedding-only-1m-replace-streaming"));
            Assert.AreEqual(Scenarios.WikiCohereEnglishEmbeddingOnly35MDeleteStreaming, ScenarioParser.Parse("wiki-cohere-english-embedding-only-35m-delete-streaming"));
            Assert.AreEqual(Scenarios.WikiCohereEnglishEmbeddingOnly35MDeleteReplaceStreaming, ScenarioParser.Parse("wiki-cohere-english-embedding-only-35m-delete-replace-streaming"));
            Assert.AreEqual(Scenarios.WikiCohereEnglishEmbeddingOnly35MReplaceStreaming, ScenarioParser.Parse("wiki-cohere-english-embedding-only-35m-replace-streaming"));
        }
    }
}