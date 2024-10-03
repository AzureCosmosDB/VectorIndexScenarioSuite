﻿using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
namespace VectorIndexScenarioSuite
{ 
    internal class MSTuringEmbeddingOnlyScenario : EmbeddingOnlyScearioBase
    {
        protected override string BaseDataFile => "base";
        protected override string BinaryFileExt => "fbin";
        protected override string QueryFile => "query10k";
        protected override string GetGroundTruthFileName => "msturing-gt";
        protected override string PartitionKeyPath => "/id";
        protected override string EmbeddingColumn => "embedding";
        protected override string EmbeddingPath => $"/{EmbeddingColumn}";
        protected override VectorDataType EmbeddingDataType => VectorDataType.Float32;
        protected override DistanceFunction EmbeddingDistanceFunction => DistanceFunction.Euclidean;
        protected override ulong EmbeddingDimensions => 100;
        protected override int MaxPhysicalPartitionCount => 56;
        protected override string RunName => "msturing-embeddingonly-" + Guid.NewGuid();


 

        public MSTuringEmbeddingOnlyScenario(IConfiguration configurations) : 
            base(configurations, ComputeInitialAndFinalThroughput(configurations).Item1)
        {
        }

        public override void Setup()
        {
            this.CosmosContainer.ReplaceThroughputAsync(ComputeInitialAndFinalThroughput(this.Configurations).Item2).Wait();
        }

        private static (int, int) ComputeInitialAndFinalThroughput(IConfiguration configurations)
        {
             // 1) For upto 1M embedding, Collection Create throughput of 400 RU, bumped to 10,000 RU.
            // 2) For 35M embedding, Collection Create throughput of 40,000 RU, bumped to 70,000 RU.
            // This is because we want 1 physical partition in scenario 1 and 7 physical partitions in scenario 2 (to reduce query fanout).
            int sliceCount = Convert.ToInt32(configurations["AppSettings:scenario:sliceCount"]);
            switch (sliceCount)
            {
                case HUNDRED_THOUSAND:
                case ONE_MILLION:
                    return (400, 10000);
                case THIRTY_FIVE_MILLION:
                    return (40000, 70000);
                case ONE_BILLION:
                    return (40000, 70000);
                default:
                    throw new ArgumentException("Invalid slice count.");
            }
        }
    }
}