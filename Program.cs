using Microsoft.Extensions.Configuration;

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
            Console.WriteLine($"Running {scenarioName} scenario.");
            await scenario.Run(); 

            Console.WriteLine();
            Console.WriteLine($"Finishing {scenarioName} scenario.");
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
                default:
                    throw new System.Exception($"Scenario {scenarioName} is not supported.");
            }
        }
    }
}
