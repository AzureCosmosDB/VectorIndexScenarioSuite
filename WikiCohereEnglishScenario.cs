using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace VectorIndexScenarioSuite
{
    /*
     * Wiki Cohere English dataset: https://huggingface.co/datasets/Cohere/wikipedia-22-12-en-embeddings
     * TBD: This scenario is not implemented at the moment and will be implemented in future.
     */
    internal sealed class WikiCohereEnglishScenario : Scenario
    {
        private const string PARTITION_KEY_PATH = "";
        private const string EMBEDDING_PATH = "/embedding";
        private const VectorDataType EMBEDDING_DATA_TYPE = VectorDataType.Float32;
        private const DistanceFunction EMBEDDING_DISTANCE_FUNCTION = DistanceFunction.DotProduct;
        private const int EMBEDDING_DIMENSIONS = 768;
        private const int THROUGHPUT = 0;

        public WikiCohereEnglishScenario(IConfiguration configurations) : base(configurations, THROUGHPUT)
        { }

        public override void Setup()
        {
            throw new NotImplementedException();
        }

        public override async Task Run()
        {
            // No-Op
            // Cannot throw NotImplementedException as it is not allowed in async method
            await Task.CompletedTask;
        }

        public override void Stop()
        {
            throw new NotImplementedException();
        }

        protected override ContainerProperties GetContainerSpec(string containerName)
        {
            throw new NotImplementedException();
        }
    }
}
