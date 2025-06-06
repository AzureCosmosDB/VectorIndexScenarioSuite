using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace VectorIndexScenarioSuite.Tests
{
    [TestClass]
    [TestCategory("Integration")]
    public class ConfigurationIntegrationTests : VectorTestBase
    {
        [TestMethod]
        public void Configuration_Setup_CreatesValidConfiguration()
        {
            // Arrange
            string testConfig = @"
            {
                ""AppSettings"": {
                    ""cosmosDatabaseId"": ""test-db"",
                    ""useEmulator"": false,
                    ""scenario"": {
                        ""name"": ""wiki-cohere-english-embedding-only"",
                        ""sliceCount"": ""1000""
                    }
                }
            }";
            
            // Act
            var configuration = Setup(testConfig);
            
            // Assert
            Assert.IsNotNull(configuration);
            Assert.AreEqual("test-db", configuration["AppSettings:cosmosDatabaseId"]);
            Assert.AreEqual("False", configuration["AppSettings:useEmulator"]);
            Assert.AreEqual("wiki-cohere-english-embedding-only", configuration["AppSettings:scenario:name"]);
            Assert.AreEqual("1000", configuration["AppSettings:scenario:sliceCount"]);
        }
        
        [TestMethod]
        public void Configuration_UnionJson_MergesConfigurationsCorrectly()
        {
            // Arrange
            string json1 = @"{""AppSettings"": {""prop1"": ""value1"", ""prop2"": ""value2""}}";
            string json2 = @"{""AppSettings"": {""prop2"": ""overridden"", ""prop3"": ""value3""}}";
            
            // Act
            string result = UnionJson(json1, json2);
            
            // Assert
            Assert.IsTrue(result.Contains("\"prop1\": \"value1\""));
            Assert.IsTrue(result.Contains("\"prop2\": \"overridden\""));
            Assert.IsTrue(result.Contains("\"prop3\": \"value3\""));
        }
        
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Configuration_UnionJson_EmptyJson1_ThrowsArgumentException()
        {
            // Arrange
            string json1 = "";
            string json2 = @"{""AppSettings"": {""prop1"": ""value1""}}";
            
            // Act
            UnionJson(json1, json2);
            
            // Assert is handled by ExpectedException
        }
        
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Configuration_UnionJson_EmptyJson2_ThrowsArgumentException()
        {
            // Arrange
            string json1 = @"{""AppSettings"": {""prop1"": ""value1""}}";
            string json2 = "";
            
            // Act
            UnionJson(json1, json2);
            
            // Assert is handled by ExpectedException
        }
    }
}