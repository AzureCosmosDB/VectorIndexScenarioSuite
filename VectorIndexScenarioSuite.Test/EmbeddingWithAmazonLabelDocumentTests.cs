using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace VectorIndexScenarioSuite.Tests
{
    [TestClass]
    public class EmbeddingWithAmazonLabelDocumentTests
    {
        [TestMethod]
        public void Constructor_ShouldParseLabelCorrectly()
        {
            // Arrange
            string id = "test-id";
            float[] embedding = new float[] { 1.0f, 2.0f, 3.0f };
            string label = "CAT=Electronics,BRAND=Sony,RATING=5";

            // Act
            var document = new EmbeddingWithAmazonLabelDocument(id, embedding, label);

            // Assert
            document.Id.Should().Be(id);
            document.Embedding.Should().BeEquivalentTo(embedding);
            document.Brand.Should().Be("Sony");
            document.Rating.Should().Be("5");
            document.Category.Should().BeEquivalentTo(new string[] { "Electronics" });
        }

        [TestMethod]
        public void ParseAmazonLabelToJson_ShouldParseLabelCorrectly()
        {
            // Arrange
            string label = "CAT=Electronics,BRAND=Sony,RATING=5";

            // Act
            var result = EmbeddingWithAmazonLabelDocument.ParseAmazonLabelToJson(label);

            // Assert
            result["brand"].Should().Be("Sony");
            result["rating"].Should().Be("5");
            result["category"].Should().BeEquivalentTo(new List<string> { "Electronics" });
        }
    }
}
