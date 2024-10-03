using Microsoft.Extensions.Configuration;
using System.Diagnostics;


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

        static void TraceConfigKeyValues(IConfiguration configurations)
        {
            Console.WriteLine("Executing VectorIndexScenarioSuite.");
            foreach (var configuration in configurations.AsEnumerable())
            {
                Console.WriteLine($"{configuration.Key} = {configuration.Value}");
            }
        }

        static Scenario CreateScenario(IConfiguration configurations)
        {
            string scenarioName = configurations["AppSettings:scenario:name"] ?? throw new ArgumentNullException("AppSettings:scenario:name");
            Scenarios scenarios = ScenarioParser.Parse(scenarioName);

            switch (scenarios)
            {
                case Scenarios.WikiCohereEnglishEmbeddingOnly:
                    return new WikiCohereEnglishEmbeddingOnlyScenario(configurations);
                case Scenarios.WikiCohereEnglishEmnbeddingOnlyStreaming:
                    return new WikiCohereEnglishEmbeddingOnlyStreamingScenario(configurations);
                case Scenarios.MSTuringEmbeddingOnly:
                    return new MSTuringEmbeddingOnlyScenario(configurations);
                default:
                    throw new System.Exception($"Scenario {scenarioName} is not supported.");
            }
        }
    }
}
