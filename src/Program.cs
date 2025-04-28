using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using VectorIndexScenarioSuite.filtersearch;


namespace VectorIndexScenarioSuite
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Setup configuration builder
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddCommandLine(args);
        
            var configurations = builder.Build();
            TraceConfigKeyValues(configurations);

            string scenarioName = configurations["AppSettings:scenario:name"] ?? throw new ArgumentNullException("AppSettings:scenario:name");

            Console.WriteLine();
            Console.WriteLine($"Setting up {scenarioName}");
            Scenario scenario = CreateScenario(configurations);
            scenario.Setup();

            Console.WriteLine();
            Console.WriteLine($"Running {scenarioName} scenario. StartTime: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            await scenario.Run(); 
            stopwatch.Stop();

            Console.WriteLine($"Scenario run took: {stopwatch.Elapsed.TotalSeconds} seconds.");

            Console.WriteLine();
            Console.WriteLine($"Finishing {scenarioName} scenario. EndTime: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");
            scenario.Stop();

            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();

            bool waitForUserInputBeforeExit = Convert.ToBoolean(configurations["AppSettings:waitForUserInputBeforeExit"]);
            if(waitForUserInputBeforeExit)
            {
                Console.WriteLine("Press Enter to exit...");
                Console.ReadLine();
            }
        }

        public static void TraceConfigKeyValues(IConfiguration configurations)
        {
            Console.WriteLine("Executing VectorIndexScenarioSuite.");
            foreach (var configuration in configurations.AsEnumerable())
            {
                Console.WriteLine($"{configuration.Key} = {configuration.Value}");
            }
        }

        public static Scenario CreateScenario(IConfiguration configurations)
        {
            string scenarioName = configurations["AppSettings:scenario:name"] ?? throw new ArgumentNullException("AppSettings:scenario:name");
            Scenarios scenarios = ScenarioParser.Parse(scenarioName);

            switch (scenarios)
            {
                case Scenarios.AutomotiveEcommerce:
                    return new AutomotiveEcommerceScenario(configurations);
                case Scenarios.MSMarcoEmbeddingOnly:
                    return new MSMarcoEmbeddingOnlyScenario(configurations);
                case Scenarios.MSTuringEmbeddingOnly:
                    return new MSTuringEmbeddingOnlyScenario(configurations);
                case Scenarios.WikiCohereEnglishEmbeddingOnly:
                    return new WikiCohereEnglishEmbeddingOnlyScenario(configurations);
                case Scenarios.WikiCohereEnglishEmbeddingOnly1MDeleteStreaming:
                    return new WikiCohereEnglishEmbeddingOnly1MDeleteStreamingScenario(configurations);
                case Scenarios.WikiCohereEnglishEmbeddingOnly1MDeleteReplaceStreaming:
                    return new WikiCohereEnglishEmbeddingOnly1MDeleteReplaceStreamingScenario(configurations);
                case Scenarios.WikiCohereEnglishEmbeddingOnly1MReplaceStreaming:
                    return new WikiCohereEnglishEmbeddingOnly1MReplaceStreamingScenario(configurations);
                case Scenarios.WikiCohereEnglishEmbeddingOnly35MDeleteStreaming:
                    return new WikiCohereEnglishEmbeddingOnly35MDeleteStreamingScenario(configurations);
                case Scenarios.WikiCohereEnglishEmbeddingOnly35MDeleteReplaceStreaming:
                    return new WikiCohereEnglishEmbeddingOnly35MDeleteReplaceStreamingScenario(configurations);
                case Scenarios.WikiCohereEnglishEmbeddingOnly35MReplaceStreaming:
                    return new WikiCohereEnglishEmbeddingOnly35MReplaceStreamingScenario(configurations);
                default:
                    throw new System.Exception($"Scenario {scenarioName} is not supported.");
            }
        }
    }
}
