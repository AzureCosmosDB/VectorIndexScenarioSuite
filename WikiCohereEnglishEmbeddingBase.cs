using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
namespace VectorIndexScenarioSuite
{ 
    abstract class WikiCohereEnglishEmbeddingBase : BigANNBinaryEmbeddingOnlyScearioBase
    {
        protected override string BaseDataFile => "wikipedia_base";
        protected override string BinaryFileExt => "fbin";
        protected override string QueryFile => "wikipedia_query";
        protected override string GetGroundTruthFileName => "wikipedia_truth";
        protected override string PartitionKeyPath => "/id";
        protected override string EmbeddingColumn => "embedding";
        protected override string EmbeddingPath => $"/{EmbeddingColumn}";
        protected override VectorDataType EmbeddingDataType => VectorDataType.Float32;
        protected override DistanceFunction EmbeddingDistanceFunction => DistanceFunction.DotProduct;
        protected override ulong EmbeddingDimensions => 768;
        protected override int MaxPhysicalPartitionCount => 56;
 
        public WikiCohereEnglishEmbeddingBase(IConfiguration configurations, int throughPut) : 
            base(configurations, throughPut)
        {
        }
    }
}
