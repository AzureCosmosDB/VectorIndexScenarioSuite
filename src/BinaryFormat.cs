using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VectorIndexScenarioSuite
{
    /*
     * Parser for BigANNBinaryFormat.
     * Please see: https://big-ann-benchmarks.com/neurips21.html#bench-datasets for format details.
     */
    internal class BinaryFormat
    {
        // Return file header (number of vectors and dimensions) and header size (8 bytes)
        public static (int, int, int) GetBinaryDataHeader(string filePath)
        {
            try
            {
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(fileStream))
                {
                    // Read the first 4 bytes as an integer for the number of vectors
                    int numberOfVectors = reader.ReadInt32();

                    // Read the next 4 bytes as an integer for the dimensions
                    int dimensions = reader.ReadInt32();

                    return (numberOfVectors, dimensions, 8);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error reading metadata from {filePath}: ", ex);
            }
        }

        public static (int, int, int) GetGroundTruthDataHeader(string filePath)
        {
            try
            {
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(fileStream))
                {
                    // Read the first 4 bytes as an integer for the number of vectors
                    int numberOfVectors = reader.ReadInt32();

                    // Read the next 4 bytes as K value for the ground truth
                    int groundTruthK = reader.ReadInt32();

                    return (numberOfVectors, groundTruthK, 8);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error reading metadata from {filePath}: ", ex);
            }
        }

        public static async IAsyncEnumerable<(int, int[], float[])> GetGroundTruthDataAsync(string filePath)
        {
            // Read the header to get the number of vectors and dimensions
            (int numberOfVectors, int groundTruthK, int headerSize) = GetGroundTruthDataHeader(filePath);

            using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
            using (FileStream fileStream2 = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
            {
                // Seek to the start of the binary data
                int vectorIdOffset = headerSize;
                long distanceValueOffset = (long)headerSize + ((long)numberOfVectors * groundTruthK * sizeof(int));

                fileStream.Seek(headerSize, SeekOrigin.Begin);
                fileStream2.Seek(distanceValueOffset, SeekOrigin.Begin);

                for (int currentOffset = 0; currentOffset < numberOfVectors; currentOffset++)
                {
                    var buffer = new byte[sizeof(int)];

                    int[] nearestNeighborVectorIds = new int[groundTruthK];
                    for (int nIndex = 0; nIndex < groundTruthK; nIndex++)
                    {
                        await fileStream.ReadAsync(buffer, 0, sizeof(int));
                        nearestNeighborVectorIds[nIndex] = BitConverter.ToInt32(buffer, 0);
                    }

                    float[] nearestNeighborVectorDistances = new float[groundTruthK];
                    var bufferFloat = new byte[sizeof(float)];
                    for (int kIndex = 0; kIndex < groundTruthK; kIndex++)
                    {
                        await fileStream2.ReadAsync(bufferFloat, 0, sizeof(float));
                        nearestNeighborVectorDistances[kIndex] = BitConverter.ToSingle(bufferFloat, 0);
                    }

                    Trace.Assert(nearestNeighborVectorIds.Length == nearestNeighborVectorDistances.Length);

                    /* The ground-truth id corresponds to vector id in the corresponding query file */
                    yield return (currentOffset, nearestNeighborVectorIds, nearestNeighborVectorDistances);
                }
            }
        }

        internal static async IAsyncEnumerable<(int, T[])> GetBinaryDataAsync<T>(string filePath, int startVectorId, int numVectorsToRead) where T : unmanaged
        {
            // Read the header to get the number of vectors and dimensions
            (int totalNumberOfVectors, int dimensions, int headerSize) = GetBinaryDataHeader(filePath);
            int vectorSizeInBytes = dimensions * Marshal.SizeOf<T>();

            using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
            {
                // Seek to the start of the binary data
                long vectorFileOffset = (long)headerSize + ((long)startVectorId * vectorSizeInBytes);
                fileStream.Seek(vectorFileOffset, SeekOrigin.Begin);

                // If we start at vector 10 and want to read 2 vectors, we will read vectors 10 and 11 i.e < 12.
                int endVectorId = startVectorId + numVectorsToRead;
                int finalVectorId = Math.Min(endVectorId, totalNumberOfVectors);

                for (int currentId = startVectorId; currentId < finalVectorId; currentId++)
                {
                    T[] vector = new T[dimensions];
                    for (int d = 0; d < dimensions; d++)
                    {
                        var buffer = new byte[Marshal.SizeOf<T>()];
                        await fileStream.ReadAsync(buffer, 0, Marshal.SizeOf<T>());
                        vector[d] = MemoryMarshal.Read<T>(buffer); // Convert the buffer to the generic type T
                    }

                    yield return (currentId, vector);
                }
            }
        }

        internal static async IAsyncEnumerable<(int, T[], string)> GetBinaryDataWithLabelAsync<T>(string filePath, int startVectorId, int numVectorsToRead) where T : unmanaged
        {
            // Read the header to get the number of vectors and dimensions
            (int totalNumberOfVectors, int dimensions, int headerSize) = GetBinaryDataHeader(filePath);
            int vectorSizeInBytes = dimensions * Marshal.SizeOf<T>();

            using (FileStream labelFileStream = new FileStream(filePath + ".label", FileMode.Open, FileAccess.Read))
            using (StreamReader labelreader = new StreamReader(labelFileStream, bufferSize: 8192))
            using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
            {
                for (int i = 0; i < startVectorId; i++)
                {
                    if (labelreader.EndOfStream) break;
                    await labelreader.ReadLineAsync();
                }

                // Seek to the start of the binary data
                long vectorFileOffset = (long)headerSize + ((long)startVectorId * vectorSizeInBytes);
                fileStream.Seek(vectorFileOffset, SeekOrigin.Begin);

                // If we start at vector 10 and want to read 2 vectors, we will read vectors 10 and 11 i.e < 12.
                int endVectorId = startVectorId + numVectorsToRead;
                int finalVectorId = Math.Min(endVectorId, totalNumberOfVectors);

                for (int currentId = startVectorId; currentId < finalVectorId; currentId++)
                {
                    T[] vector = new T[dimensions];
                    for (int d = 0; d < dimensions; d++)
                    {
                        var buffer = new byte[Marshal.SizeOf<T>()];
                        await fileStream.ReadAsync(buffer, 0, Marshal.SizeOf<T>());
                        vector[d] = MemoryMarshal.Read<T>(buffer); // Convert the buffer to the generic type T
                    }
                    var line = await labelreader.ReadLineAsync();

                    yield return (currentId, vector, line);
                }
            }
        }
    }
}
