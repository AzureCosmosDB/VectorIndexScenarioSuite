using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;

// idxctl: manage vector index policy for the "index-after-ingest" (r4) experiment.
//
// Commands:
//   create-noindex <endpoint> <db> <container> <ru>
//       Create container WITH the WikiCohere VectorEmbeddingPolicy (immutable, required)
//       but WITHOUT any VectorIndexes. IncludedPath "/", ExcludedPath "/embedding/*".
//   add-index    <endpoint> <db> <container> <quantizer> <byteSize>
//       ReplaceContainer to ADD a DiskANN vector index on /embedding carrying the
//       given quantizerType (product|spherical; "" = default) and quantizationByteSize
//       (0/"" = default). Then poll index-transformation-progress.
//   show-index   <endpoint> <db> <container>
//       Print current VectorEmbeddingPolicy + IndexingPolicy.VectorIndexes.
//
// WikiCohere embedding spec (must match the suite exactly):
//   PartitionKey /id ; EmbeddingPath /embedding ; Float32 ; DotProduct ; 768 dims.
class Program
{
    const string EmbeddingPath = "/embedding";
    const string PartitionKeyPath = "/id";
    const int Dimensions = 768;

    static async Task<int> Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("usage: idxctl <create-noindex|add-index|show-index> ...");
            return 2;
        }
        string cmd = args[0];
        try
        {
            switch (cmd)
            {
                case "create-noindex": return await CreateNoIndex(args);
                case "add-index":      return await AddIndex(args);
                case "show-index":     return await ShowIndex(args);
                default:
                    Console.Error.WriteLine($"unknown command '{cmd}'");
                    return 2;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IDXCTL_JSON: {{\"cmd\":\"{cmd}\",\"ok\":false,\"error\":\"{Escape(ex.Message)}\"}}");
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    static CosmosClient MakeClient(string endpoint)
    {
        var options = new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway, LimitToEndpoint = true };
        return new CosmosClient(endpoint, new DefaultAzureCredential(), options);
    }

    static VectorEmbeddingPolicy MakeEmbeddingPolicy()
    {
        return new VectorEmbeddingPolicy(new Collection<Embedding>(new List<Embedding>()
        {
            new Embedding()
            {
                Path = EmbeddingPath,
                DataType = VectorDataType.Float32,
                DistanceFunction = DistanceFunction.DotProduct,
                Dimensions = Dimensions,
            }
        }));
    }

    static VectorIndexPath MakeVectorIndexPath(string quantizer, string byteSize)
    {
        var vip = new VectorIndexPath() { Path = EmbeddingPath, Type = VectorIndexType.DiskANN };
        if (!string.IsNullOrEmpty(quantizer))
        {
            if (!Enum.TryParse<QuantizerType>(quantizer, ignoreCase: true, out var qt))
                throw new ArgumentException($"Invalid quantizerType '{quantizer}'.");
            vip.QuantizerType = qt;
        }
        if (!string.IsNullOrEmpty(byteSize) && byteSize != "0")
        {
            int bs = Convert.ToInt32(byteSize);
            if (bs <= 0) throw new ArgumentException("byteSize must be > 0");
            vip.QuantizationByteSize = bs;
        }
        return vip;
    }

    static async Task<int> CreateNoIndex(string[] a)
    {
        if (a.Length < 5) { Console.Error.WriteLine("usage: idxctl create-noindex <endpoint> <db> <container> <ru>"); return 2; }
        string endpoint = a[1], db = a[2], coll = a[3]; int ru = Convert.ToInt32(a[4]);
        using var client = MakeClient(endpoint);
        Database database = client.GetDatabase(db);

        var props = new ContainerProperties(id: coll, partitionKeyPath: PartitionKeyPath)
        {
            VectorEmbeddingPolicy = MakeEmbeddingPolicy(),
            IndexingPolicy = new IndexingPolicy()  // NOTE: no VectorIndexes
        };
        props.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/" });
        props.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = EmbeddingPath + "/*" });

        ContainerResponse resp = await database.CreateContainerIfNotExistsAsync(props, throughput: ru);
        int viCount = resp.Resource?.IndexingPolicy?.VectorIndexes?.Count ?? 0;
        bool hasEmbed = (resp.Resource?.VectorEmbeddingPolicy != null);
        Console.WriteLine($"IDXCTL_JSON: {{\"cmd\":\"create-noindex\",\"ok\":true,\"container\":\"{coll}\",\"status\":\"{(int)resp.StatusCode}\",\"vectorIndexes\":{viCount},\"hasEmbeddingPolicy\":{hasEmbed.ToString().ToLower()},\"ru\":{ru}}}");
        return 0;
    }

    static async Task<int> AddIndex(string[] a)
    {
        if (a.Length < 4) { Console.Error.WriteLine("usage: idxctl add-index <endpoint> <db> <container> [quantizer] [byteSize]"); return 2; }
        string endpoint = a[1], db = a[2], coll = a[3];
        string quantizer = a.Length > 4 ? a[4] : "";
        string byteSize  = a.Length > 5 ? a[5] : "";
        using var client = MakeClient(endpoint);
        Container container = client.GetContainer(db, coll);

        ContainerResponse cur = await container.ReadContainerAsync();
        ContainerProperties props = cur.Resource;
        int before = props.IndexingPolicy?.VectorIndexes?.Count ?? 0;

        // Ensure excluded path present, then add the vector index.
        bool hasExcluded = props.IndexingPolicy.ExcludedPaths.Any(p => p.Path == EmbeddingPath + "/*");
        if (!hasExcluded) props.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = EmbeddingPath + "/*" });
        if (props.IndexingPolicy.VectorIndexes == null) props.IndexingPolicy.VectorIndexes = new Collection<VectorIndexPath>();
        props.IndexingPolicy.VectorIndexes.Clear();
        props.IndexingPolicy.VectorIndexes.Add(MakeVectorIndexPath(quantizer, byteSize));

        DateTime t0 = DateTime.UtcNow;
        ContainerResponse repl = await container.ReplaceContainerAsync(props);
        int after = repl.Resource?.IndexingPolicy?.VectorIndexes?.Count ?? 0;
        string effQuant = (after > 0) ? repl.Resource.IndexingPolicy.VectorIndexes[0].QuantizerType.ToString() : "";
        var effBs = (after > 0) ? repl.Resource.IndexingPolicy.VectorIndexes[0].QuantizationByteSize : (int?)null;
        Console.WriteLine($"IDXCTL_JSON: {{\"cmd\":\"add-index\",\"ok\":true,\"container\":\"{coll}\",\"replaceStatus\":\"{(int)repl.StatusCode}\",\"vectorIndexesBefore\":{before},\"vectorIndexesAfter\":{after},\"effectiveQuantizer\":\"{effQuant}\",\"effectiveByteSize\":{(effBs.HasValue ? effBs.Value.ToString() : "null")}}}");

        // Poll scalar index-transformation-progress (DiskANN graph builds async in the backend).
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(3000);
            ContainerResponse r = await container.ReadContainerAsync();
            string prog = r.Headers?["x-ms-documentdb-collection-index-transformation-progress"];
            double secs = (DateTime.UtcNow - t0).TotalSeconds;
            Console.WriteLine($"IDXCTL_PROGRESS: {{\"container\":\"{coll}\",\"tSec\":{secs:F0},\"indexTransformationProgress\":\"{prog}\"}}");
            if (prog == "100" || prog == "-1" || string.IsNullOrEmpty(prog)) break;
        }
        return 0;
    }

    static async Task<int> ShowIndex(string[] a)
    {
        if (a.Length < 4) { Console.Error.WriteLine("usage: idxctl show-index <endpoint> <db> <container>"); return 2; }
        string endpoint = a[1], db = a[2], coll = a[3];
        using var client = MakeClient(endpoint);
        Container container = client.GetContainer(db, coll);
        ContainerResponse cur = await container.ReadContainerAsync();
        using ResponseMessage raw = await container.ReadContainerStreamAsync();
        raw.EnsureSuccessStatusCode();
        using var reader = new StreamReader(raw.Content);
        string resourceId = JObject.Parse(await reader.ReadToEndAsync())["_rid"]?.ToString() ?? "";
        var vis = cur.Resource?.IndexingPolicy?.VectorIndexes;
        int viCount = vis?.Count ?? 0;
        string details = "";
        if (viCount > 0)
        {
            var parts = vis.Select(v => $"{{\\\"path\\\":\\\"{v.Path}\\\",\\\"type\\\":\\\"{v.Type}\\\",\\\"quantizer\\\":\\\"{v.QuantizerType}\\\",\\\"byteSize\\\":{v.QuantizationByteSize}}}");
            details = string.Join(",", parts);
        }
        bool hasEmbed = (cur.Resource?.VectorEmbeddingPolicy != null);
        Console.WriteLine($"IDXCTL_JSON: {{\"cmd\":\"show-index\",\"ok\":true,\"container\":\"{coll}\",\"resourceId\":\"{Escape(resourceId)}\",\"hasEmbeddingPolicy\":{hasEmbed.ToString().ToLower()},\"vectorIndexes\":{viCount},\"details\":[{details}]}}");
        return 0;
    }

    static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
}
