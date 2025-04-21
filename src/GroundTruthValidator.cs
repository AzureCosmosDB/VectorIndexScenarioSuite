namespace VectorIndexScenarioSuite
{
    internal enum GroundTruthFileType
    {
        Binary
    }

    internal class GroundTruthValidator
    {
        private IDictionary<string, List<IdWithSimilarityScore>> groundTruth; 
        private int groundTruthKValue;

        public GroundTruthValidator(GroundTruthFileType fileType, string filePath)
        {
            this.groundTruth = new Dictionary<string, List<IdWithSimilarityScore>>();
            LoadGroundTruthData(fileType, filePath);
        }

        public float ComputeRecall(int queryKValue, IDictionary<string, List<IdWithSimilarityScore>> queryResults)
        {
            if (this.groundTruthKValue < queryKValue)
            {
                throw new ArgumentException("Query K value is greater than the ground truth K value");
            }

            float recall = 0;
            HashSet<string> groundTruthIdsForQuery = new HashSet<string>();
            HashSet<string> resultIdsForQuery = new HashSet<string>();

            int cumulativeTruePositive = 0;
            var queryIds = queryResults.Keys;
            foreach(string queryId in queryResults.Keys)
            {
                groundTruthIdsForQuery.Clear();
                resultIdsForQuery.Clear();

                // for filter search, there might have less than K results
                for (int i = 0; i < queryResults[queryId].Count; i++)
                {
                    resultIdsForQuery.Add(queryResults[queryId][i].Id);
                }

                // for filter search, if the ground truth is -1, means there is no result
                for (int i = queryResults[queryId].Count; i < queryKValue; i++)
                {
                    if (this.groundTruth[queryId][i].Id == "-1")
                    {
                        // -1 filled for non reuslt.
                        Console.WriteLine($"Ground truth id is -1 for queryId: {queryId} at {i}/K");
                        //cumulativeTruePositive++;
                    }
                }

                /* Compute valid ground truth ids for the query 
                 * Handle scenario where multiple vectors have the same similarity score as the kth vector
                 */
                int tieBreaker = queryKValue - 1;
                while (this.groundTruth[queryId][tieBreaker].Id != "-1" &&tieBreaker < this.groundTruth[queryId].Count &&
                    this.groundTruth[queryId][tieBreaker].SimilarityScore == this.groundTruth[queryId][queryKValue - 1].SimilarityScore)
                {
                    tieBreaker++;
                }
                for (int i = 0; i < tieBreaker; i++)
                {
                    groundTruthIdsForQuery.Add(this.groundTruth[queryId][i].Id);
                }

                int truePositive = 0;
                foreach(string queryResultId in resultIdsForQuery)
                {
                    if(groundTruthIdsForQuery.Contains(queryResultId))
                    {
                        cumulativeTruePositive++;
                        truePositive++;
                    }
                }
            }

            Console.WriteLine($"Recall Stats: " +
                $"Cumulative True Positive: {cumulativeTruePositive}, " +
                $"NumQueries: {queryResults.Count}, KValue: {queryKValue}, GroundTruthKValue: {groundTruthKValue}");

            float averageHitsAcrossQueries = ((cumulativeTruePositive * 1.0f) / queryResults.Count);
            recall = (averageHitsAcrossQueries / queryKValue) * 100.0f;
            return recall;
        }

        private void LoadGroundTruthData(GroundTruthFileType fileType, string filePath)
        {
            switch(fileType)
            {
                case GroundTruthFileType.Binary:
                    LoadGroundTruthDataFromBinaryFile(filePath).Wait();
                    break;
                default:
                    throw new ArgumentException("Invalid GroundTruthFileType: ", nameof(fileType));
            }
        }
        private async Task LoadGroundTruthDataFromBinaryFile(string filePath)
        {
            await foreach ((int vectorId, int[] groundTruthNeighborIds, float[] groundTruthNeighborDistances) in BinaryFormat.GetGroundTruthDataAsync(filePath))
            {
                List<IdWithSimilarityScore> idWithSimilarityScores = new List<IdWithSimilarityScore>();
                for (int i = 0; i < groundTruthNeighborIds.Length; i++)
                {
                    idWithSimilarityScores.Add(new IdWithSimilarityScore(groundTruthNeighborIds[i].ToString(), groundTruthNeighborDistances[i]));
                }

                if (vectorId == 0)
                {
                    this.groundTruthKValue = groundTruthNeighborIds.Length;
                }
                else if (this.groundTruthKValue != groundTruthNeighborIds.Length)
                {
                   Console.WriteLine($"Ground truth K value mismatch for vectorId: {vectorId}");
                }

                this.groundTruth.Add(vectorId.ToString(), idWithSimilarityScores);
            }
        }
    }
}
