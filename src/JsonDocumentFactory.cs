using System.Diagnostics;
using VectorIndexScenarioSuite.filtersearch;

namespace VectorIndexScenarioSuite
{
    internal static class JsonDocumentFactory
    {

        public static async IAsyncEnumerable<EmbeddingDocumentBase<float>> GetDocumentAsync(string filePath, BinaryDataType dataType, int startVectorId, int numVectorsToRead, bool includeLabel)
        {
            if (includeLabel)
            {
                await foreach (var item in BigANNBinaryFormat.GetBinaryDataWithLabelAsync(filePath, dataType, startVectorId, numVectorsToRead))
                {
                    yield return new AutomotiveEcommerceDocument(item.Item1.ToString(), item.Item2, item.Item3);
                }
            }
            else
            {
                await foreach (var item in BigANNBinaryFormat.GetBinaryDataAsync(filePath, dataType, startVectorId, numVectorsToRead))
                {
                    yield return new EmbeddingDocumentBase<float>(item.Item1.ToString(), item.Item2);
                }
            }
        }

        public static async IAsyncEnumerable<(int, float[], string)> GetQueryAsync(string filePath, BinaryDataType dataType, int startVectorId, int numVectorsToRead, bool includeLabel)
        {
            if (includeLabel)
            {
                await foreach (var item in BigANNBinaryFormat.GetBinaryDataWithLabelAsync(filePath, dataType, startVectorId, numVectorsToRead))
                {
                    string where = AutomotiveEcommerceDocument.TryToWhereStatement(item.Item3);

                    yield return (item.Item1, item.Item2, where);
                }
            }
            else
            {
                await foreach (var item in BigANNBinaryFormat.GetBinaryDataAsync(filePath, dataType, startVectorId, numVectorsToRead))
                {
                    yield return (item.Item1, item.Item2, string.Empty);
                }
            }
        }
    }
}
