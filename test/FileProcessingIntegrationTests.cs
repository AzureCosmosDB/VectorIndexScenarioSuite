using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace VectorIndexScenarioSuite.Tests
{
    [TestClass]
    [TestCategory("Integration")]
    public class FileProcessingIntegrationTests
    {
        [TestMethod]
        public void BigANNBinaryFormat_GetBinaryDataHeader_InvalidFile_ThrowsException()
        {
            // Arrange
            string nonExistentFile = "/tmp/non_existent_file.bin";
            
            // Act & Assert
            try
            {
                BigANNBinaryFormat.GetBinaryDataHeader(nonExistentFile);
                Assert.Fail("Expected exception was not thrown");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.Contains("Error reading metadata") || 
                             ex is FileNotFoundException || 
                             ex is DirectoryNotFoundException);
            }
        }
        
        [TestMethod]
        public void BigANNBinaryFormat_GetBinaryDataHeader_ValidEmptyFile_HandlesGracefully()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            
            try
            {
                // Create an empty file
                File.WriteAllBytes(tempFile, new byte[0]);
                
                // Act & Assert
                try
                {
                    BigANNBinaryFormat.GetBinaryDataHeader(tempFile);
                    Assert.Fail("Expected exception was not thrown for empty file");
                }
                catch (Exception ex)
                {
                    // Expected to fail reading header from empty file
                    Assert.IsTrue(ex is EndOfStreamException || ex.Message.Contains("Error reading"));
                }
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
        
        [TestMethod]
        public void BigANNBinaryFormat_GetBinaryDataHeader_ValidFileWithHeader_ReturnsCorrectData()
        {
            // Arrange
            string tempFile = Path.GetTempFileName();
            
            try
            {
                // Create a file with a valid header (8 bytes: 2 int32 values)
                using (var fs = new FileStream(tempFile, FileMode.Create))
                using (var writer = new BinaryWriter(fs))
                {
                    writer.Write(1000); // numberOfVectors
                    writer.Write(256);  // dimensions
                }
                
                // Act
                var (numberOfVectors, dimensions, headerSize) = BigANNBinaryFormat.GetBinaryDataHeader(tempFile);
                
                // Assert
                Assert.AreEqual(1000, numberOfVectors);
                Assert.AreEqual(256, dimensions);
                Assert.AreEqual(8, headerSize);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}