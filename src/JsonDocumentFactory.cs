using System.Diagnostics;
using VectorIndexScenarioSuite.filtersearch;

namespace VectorIndexScenarioSuite
{
    internal static class JsonDocumentFactory
    {
        public static async IAsyncEnumerable<EmbeddingDocumentBase<T>> GetDocumentAsync<T>(string filePath, int startVectorId, int numVectorsToRead, bool includeLabel)
        {
            if (includeLabel)
            {
                await foreach (var item in BigANNBinaryFormat.GetBinaryDataWithLabelAsync<T>(filePath, startVectorId, numVectorsToRead))
                {
                    yield return new AutomotiveEcommerceDocument(item.Item1.ToString(), item.Item2, item.Item3);
                }
            }
            else
            {
                await foreach (var item in BigANNBinaryFormat.GetBinaryDataAsync<T>(filePath, startVectorId, numVectorsToRead))
                {
                    yield return new EmbeddingDocumentBase<T>(item.Item1.ToString(), item.Item2);
                }
            }
        }

        public static async IAsyncEnumerable<(int, T[], string)> GetQueryAsync<T>(string filePath, int startVectorId, int numVectorsToRead, bool includeLabel)
        {
            if (includeLabel)
            {
                await foreach (var item in BigANNBinaryFormat.GetBinaryDataWithLabelAsync<T>(filePath, startVectorId, numVectorsToRead))
                {
                    string where = AutomotiveEcommerceDocument.TryToWhereStatement(item.Item3);

                    yield return (item.Item1, item.Item2, where);
                }
            }
            else
            {
                await foreach (var item in BigANNBinaryFormat.GetBinaryDataAsync<T>(filePath, startVectorId, numVectorsToRead))
                {
                    yield return (item.Item1, item.Item2, string.Empty);
                }
            }
        }
    }
}
