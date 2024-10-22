using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using Azure;
using System.Collections;
using YamlDotNet.Core;

namespace VectorIndexScenarioSuite
{
    internal class Operation
    {
        [YamlMember(Alias = "start")]
        public int? Start { get; set; }

        [YamlMember(Alias = "end")]
        public int? End { get; set; }

        [YamlMember(Alias = "operation")]
        public required string Name { get; set; }
    }

    internal class RunbookData
    {
        [YamlMember(Alias = "operations")]
        public Dictionary<string, Operation> Operation { get; set; }

        [YamlMember(Alias = "max_pts")]
        public int MaxPoints { get; set; }

        [YamlMember(Alias = "gt_url")]
        public required string GroundTruthURL { get; set; }
    }

    internal class Runbook
    {
        [YamlMember(Alias = "scenario")]
        public RunbookData RunbookData;

        [YamlMember(Alias = "version")]
        public int Version;

        // Default constructor is required for deserialization
        public Runbook()
        {
            this.Version = 0;
            this.RunbookData = new RunbookData() { 
                GroundTruthURL = string.Empty, MaxPoints = 0, Operation = new Dictionary<string, Operation>()
            };
        }

        public static async Task<Runbook> Parse(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            }

            using var reader = new StreamReader(filePath);
            var yamlContent = await reader.ReadToEndAsync();

            var deserializer = new DeserializerBuilder().Build();
            var runbook = deserializer.Deserialize<Runbook>(yamlContent);

            Console.WriteLine($"Runbook version: {runbook.Version}");
            return runbook;
        }
    }
}
