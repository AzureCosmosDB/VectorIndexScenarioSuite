using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
namespace VectorIndexScenarioSuite
{ 
    internal class MSMarcoEmbeddingOnlyScenario : EmbeddingOnlyScearioBase
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
        protected override ulong EmbeddingDimensions => 100;
        protected override int MaxPhysicalPartitionCount => 56;
        protected override string RunName => "msmarco-embeddingonly-" + guid;


 

        public MSMarcoEmbeddingOnlyScenario(IConfiguration configurations) : 
            base(configurations, ComputeInitialAndFinalThroughput(configurations).Item1)
        {
        }

        public override void Setup()
        {
            this.CosmosContainer.ReplaceThroughputAsync(ComputeInitialAndFinalThroughput(this.Configurations).Item2).Wait();
        }

        private static (int, int) ComputeInitialAndFinalThroughput(IConfiguration configurations)
        {
            // seting the throughput for the container initially for creation and then bumping it up
            int init_RU = Convert.ToInt32(configurations["AppSettings:cosmosContainerRUInitial"]);
            int final_RU = Convert.ToInt32(configurations["AppSettings:cosmosContainerRUInitial"]);

            return (init_RU, final_RU);
        }
    }
}
