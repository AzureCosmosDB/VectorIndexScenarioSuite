﻿using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace VectorIndexScenarioSuite
{
    internal class MSTuringEmbeddingOnlyStreamingScenario : BigANNBinaryEmbeddingOnlyScearioBase
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
        protected override string RunName => "msturing-embeddingonly-streaming" + guid;

        public MSTuringEmbeddingOnlyStreamingScenario(IConfiguration configurations) : 
            base(configurations, DefaultInitialAndFinalThroughput(configurations).Item1)
        {
        }

        public override void Setup()
        {
            this.ReplaceFinalThroughput(DefaultInitialAndFinalThroughput(this.Configurations).Item2);
        }

        public override async Task Run()
        {
            await RunStreamingScenario("runbooks/msturing-10M_randomreplace_runbook.yml");
        }

        public override void Stop()
        {
            // No Operation required.
        }

        private static (int, int) DefaultInitialAndFinalThroughput(IConfiguration configurations)
        {
            // default throughput for MSTuringEmbeddingOnlyScenario
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