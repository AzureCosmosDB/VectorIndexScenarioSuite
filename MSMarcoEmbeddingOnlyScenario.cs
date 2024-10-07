using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
namespace VectorIndexScenarioSuite
{ 
    internal class MSMarcoEmbeddingOnlyScenario : BigANNBinaryEmbeddingOnlyScearioBase
    {
        protected override string BaseDataFile => "base";
        protected override string BinaryFileExt => "fbin";
        protected override string QueryFile => "query";
        protected override string GetGroundTruthFileName => "ground_truth";
        protected override string PartitionKeyPath => "/id";
        protected override string EmbeddingColumn => "embedding";
        protected override string EmbeddingPath => $"/{EmbeddingColumn}";
        protected override VectorDataType EmbeddingDataType => VectorDataType.Float32;
        protected override DistanceFunction EmbeddingDistanceFunction => DistanceFunction.DotProduct;
        protected override ulong EmbeddingDimensions => 768;
        protected override int MaxPhysicalPartitionCount => 56;
        protected override string RunName => "msmarco-embeddingonly-" + guid;

        public MSMarcoEmbeddingOnlyScenario(IConfiguration configurations) : 
            base(configurations, defaultInitialAndFinalThroughput(configurations).Item1)
        {
        }

        public override void Setup()
        {
            this.replaceFinalThroughput(defaultInitialAndFinalThroughput(this.Configurations).Item2);
        }

        private static (int, int) defaultInitialAndFinalThroughput(IConfiguration configurations)
        {
            // default throughput for MSMarcoEmbeddingOnlyScenario
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
