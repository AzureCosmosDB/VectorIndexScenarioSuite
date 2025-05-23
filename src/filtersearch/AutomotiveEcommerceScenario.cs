﻿using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
namespace VectorIndexScenarioSuite.filtersearch
{ 
    internal class AutomotiveEcommerceScenario : BigANNBinaryEmbeddingScenarioBase
    {
        protected override string BaseDataFile => "base";
        protected override string BinaryFileExt => "fbin";
        protected override string QueryFile => "query";
        protected override string GetGroundTruthFileName => "ground_truth";
        protected override string PartitionKeyPath => "/id";
        protected override string EmbeddingColumn => "embedding";
        protected override string EmbeddingPath => $"/{EmbeddingColumn}";
        protected override VectorDataType EmbeddingDataType => VectorDataType.Float32;
        protected override DistanceFunction EmbeddingDistanceFunction => DistanceFunction.Euclidean;
        protected override ulong EmbeddingDimensions => 384;
        protected override int MaxPhysicalPartitionCount => 56;
        protected override string RunName => "Automotive-" + guid;
        protected override bool IsFilterSearch => true;

        public AutomotiveEcommerceScenario(IConfiguration configurations) : 
            base(configurations, DefaultInitialAndFinalThroughput(configurations).Item1)
        {
        }

        public override async Task Run()
        {
            await RunScenario();
        }

        public override void Setup()
        {
            ReplaceFinalThroughput(DefaultInitialAndFinalThroughput(Configurations).Item2);
        }

        private static (int, int) DefaultInitialAndFinalThroughput(IConfiguration configurations)
        {
            int sliceCount = Convert.ToInt32(configurations["AppSettings:scenario:sliceCount"]);
            switch (sliceCount)
            {
                case HUNDRED_THOUSAND:
                case ONE_MILLION:
                    return (6000, 10000);
                default:
                    throw new ArgumentException("Invalid slice count.");
            }
        }
    }
}
