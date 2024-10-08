# VectorIndexScenarioSuite
This repository contains a suite of scenarios designed to explore vector indexing capabilities in CosmosDB NoSQL.

## Blog Post Series
For a detailed explanation and walkthrough of each scenario, refer to our [Cosmos DB Blog post series](https://aka.ms/CosmosDiskANNBlogPart1).

## List of supported Scenarios :
1. Wiki-Cohere-English-EmbeddingOnly Scenario :
   
    The Wiki Cohere English Embedding Only Scenario contains 768 dimensional embeddings of English Wikipedia articles (without corresponding passage text).
    The embeddings have been generated using Cohere’s multilingual-22-12 model. 
    
    For simplicity, we use pre-processed version of the dataset hosted at [BigANN](https://github.com/harsha-simhadri/big-ann-benchmarks/blob/main/benchmark/datasets.py).
    This dataset contains :
    - Base data slices of sizes [100K, 10Million and 35Million].
    - Query vectors and corresponding ground truth neighbor identifiers / distances for 5000 vectors not in the base dataset.

    The dataset uses the BigANNBinary format documented at [BigANNBenchmarks](https://big-ann-benchmarks.com/neurips21.html#bench-datasets)

Please Watch / Star this repository as we will be adding multiple new scenarios in the near future.
