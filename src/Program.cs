    using global::OpenTelemetry;
    using global::OpenTelemetry.Trace;
    using global::OpenTelemetry.Resources;
    using System;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Configuration;
    using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
    using System.Diagnostics;
using OpenTelemetry.Logs;
using Microsoft.Extensions.Azure;
namespace VectorIndexScenarioSuite
{
    public class Program
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

                            // <SetUpOpenTelemetry>
                ResourceBuilder resource = ResourceBuilder.CreateDefault().AddService(
                            serviceName: "VectorIndexScenarioSuite",
                            serviceVersion: "1.0.0");

                // Set up logging to forward logs to chosen exporter
                using ILoggerFactory loggerFactory
                    = LoggerFactory.Create(builder => builder
                                                        .AddOpenTelemetry(options =>
                                                        {
                                                            options.IncludeFormattedMessage = true;
                                                            options.SetResourceBuilder(resource);
                                                            options.AddConsoleExporter();
                                                        }));
                /*.AddFilter(level => level == LogLevel.Error) // Filter  is irrespective of event type or event name*/

                AzureEventSourceLogForwarder logforwader = new AzureEventSourceLogForwarder(loggerFactory);
                logforwader.Start();

                // Configure OpenTelemetry trace provider
                AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
                var _traceProvider = Sdk.CreateTracerProviderBuilder()
                    .AddSource("Azure.Cosmos.Operation", // Cosmos DB source for operation level telemetry
                               "Sample.Application") 
                    .AddHttpClientInstrumentation() // Added to capture HTTP telemetry
                    .SetResourceBuilder(resource)
                    .Build();
                // </SetUpOpenTelemetry>

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
                case Scenarios.BigANNEmbeddingOnly:
                    return new BigANNSiftEmbeddingOnlyScenario(configurations);
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
