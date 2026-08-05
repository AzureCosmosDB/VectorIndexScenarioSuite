using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure.Identity;
using Microsoft.Azure.Cosmos;

// pcheck: authoritative physical-partition count + throughput for a container.
// Usage: pcheck <endpoint> <database> <container>
// Prints one line: PCHECK_JSON: {"container":...,"feedRanges":N,"docCount":D}
class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: pcheck <endpoint> <database> <container>");
            return 2;
        }
        string endpoint = args[0], db = args[1], coll = args[2];
        var options = new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway, LimitToEndpoint = true };
        using var client = new CosmosClient(endpoint, new DefaultAzureCredential(), options);
        Container container = client.GetContainer(db, coll);

        // One FeedRange per physical partition = authoritative partition count.
        IReadOnlyList<FeedRange> ranges = await container.GetFeedRangesAsync();
        int feedRanges = ranges.Count;

        long docCount = -1;
        try
        {
            using var it = container.GetItemQueryIterator<long>("SELECT VALUE COUNT(1) FROM c");
            if (it.HasMoreResults)
            {
                var resp = await it.ReadNextAsync();
                docCount = resp.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"docCount query failed: {ex.Message}");
        }

        Console.WriteLine($"PCHECK_JSON: {{\"container\":\"{coll}\",\"feedRanges\":{feedRanges},\"docCount\":{docCount}}}");
        return 0;
    }
}
