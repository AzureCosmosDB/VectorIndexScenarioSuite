using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;

namespace VectorIndexScenarioSuite.Tests
{
    [TestClass]
    [TestCategory("Unit")]
    public class BinaryDataTypeTests
    {
        [TestMethod]
        public void BinaryDataType_Float32_ReturnsCorrectSize()
        {
            // Arrange
            var dataType = BinaryDataType.Float32;
            
            // Act
            int size = dataType.Size();
            
            // Assert
            Assert.AreEqual(sizeof(float), size);
            Assert.AreEqual(4, size);
        }
    }
}