using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VectorIndexScenarioSuite.Tests
{
    [TestClass]
    public class ScenarioParsingTest
    {
        [TestMethod]
        public void YFCCScenarioParsingTest()
        {
            // Test that YFCC scenario name is parsed correctly
            var result = ScenarioParser.Parse("yfcc-embedding-only");
            Assert.AreEqual(Scenarios.YFCCEmbeddingOnly, result);
        }

        [TestMethod]
        public void ExistingScenariosStillParseTest()
        {
            // Test that existing scenarios still parse correctly
            var wikiResult = ScenarioParser.Parse("wiki-cohere-english-embedding-only");
            Assert.AreEqual(Scenarios.WikiCohereEnglishEmbeddingOnly, wikiResult);

            var msturingResult = ScenarioParser.Parse("ms-turing-embedding-only");
            Assert.AreEqual(Scenarios.MSTuringEmbeddingOnly, msturingResult);

            var automotiveResult = ScenarioParser.Parse("automotive-ecommerce");
            Assert.AreEqual(Scenarios.AutomotiveEcommerce, automotiveResult);
        }

        [TestMethod]
        public void BinaryDataTypeExtensionsTest()
        {
            // Test that new data types have correct sizes
            Assert.AreEqual(4, BinaryDataType.Float32.Size());
            Assert.AreEqual(1, BinaryDataType.UInt8.Size());
            Assert.AreEqual(1, BinaryDataType.Int8.Size());
            Assert.AreEqual(2, BinaryDataType.Int16.Size());
            Assert.AreEqual(2, BinaryDataType.Float16.Size());
        }
    }
}