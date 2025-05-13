# VectorIndexScenarioSuite
This repository contains a suite of scenarios (Full Space Search, Search on Streaming Workloads, Filtered Search, Sharded Index Search) to explore vector indexing capabilities in Azure Cosmos DB NoSQL.

We use datasets hosted at [BigANN](https://github.com/harsha-simhadri/big-ann-benchmarks/blob/main/benchmark/datasets.py) for demonstrative above scenarios. The dataset format used is the '*BigANNBinary*' format documented at [BigANNBenchmarks](https://big-ann-benchmarks.com/neurips21.html#bench-datasets)

## Blog Post Series
For a detailed explanation and walkthrough of each scenario, refer to our Cosmos DB Blog post series.

[Full Space Vector Search with DiskANN](https://devblogs.microsoft.com/cosmosdb/azure-cosmos-db-vector-search-with-diskann-part-1-full-space-search/)

[Billion Scale Vector Search with DiskANN](https://devblogs.microsoft.com/cosmosdb/azure-cosmos-db-with-diskann-part-2-scaling-to-1-billion-vectors-with/)

[Sharded DiskANN for Multi-tenant Vector Search](https://devblogs.microsoft.com/cosmosdb/sharded-diskann-focused-vector-search-for-better-performance-and-lower-cost/)

[Stable Vector Search Recall with Streaming Data](https://devblogs.microsoft.com/cosmosdb/azure-cosmos-db-with-diskann-part-4-stable-vector-search-recall-with-streaming-data/)

## Deep Dive

A technical deep dive into internals of Vector Search with Azure Cosmos DB can be found at [Arxiv](https://arxiv.org/abs/2505.05885).
