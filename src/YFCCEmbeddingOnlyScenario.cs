using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace VectorIndexScenarioSuite
{ 
    internal class YFCCEmbeddingOnlyScenario : BigANNBinaryEmbeddingScenarioBase
    {
        protected override string BaseDataFile => "base";
        protected override string BinaryFileExt => "u8bin"; // Different extension to indicate uint8 data
        protected override string QueryFile => "query";
        protected override string GetGroundTruthFileName => "ground_truth";
        protected override string PartitionKeyPath => "/id";
        protected override string EmbeddingColumn => "embedding";
        protected override string EmbeddingPath => $"/{EmbeddingColumn}";
        protected override VectorDataType EmbeddingDataType => VectorDataType.Float32; // Cosmos DB still expects Float32
        protected override DistanceFunction EmbeddingDistanceFunction => DistanceFunction.Euclidean;
        protected override ulong EmbeddingDimensions => 512;
        protected override int MaxPhysicalPartitionCount => 56;
        protected override string RunName => "yfcc-embeddingonly-" + guid;
        protected override BinaryDataType BinaryEmbeddingDataType => BinaryDataType.UInt8;

        public YFCCEmbeddingOnlyScenario(IConfiguration configurations) : 
            base(configurations, DefaultInitialAndFinalThroughput(configurations).Item1)
        {
        }

        public override void Setup()
        {
            this.ReplaceFinalThroughput(DefaultInitialAndFinalThroughput(this.Configurations).Item2);
        }

        public override async Task Run()
        {
            await RunScenario();
        }

        private static (int, int) DefaultInitialAndFinalThroughput(IConfiguration configurations)
        {
            // Default throughput for YFCCEmbeddingOnlyScenario
            int sliceCount = Convert.ToInt32(configurations["AppSettings:scenario:sliceCount"]);
            switch (sliceCount)
            {
                case HUNDRED_THOUSAND:
                case ONE_MILLION:
                    return (6000, 10000);
                case TEN_MILLION:
                    return (12000, 20000);
                case ONE_HUNDRED_MILLION:
                    return (48000, 80000);
                default:
                    throw new ArgumentException("Invalid slice count.");
            }
        }
    }
}