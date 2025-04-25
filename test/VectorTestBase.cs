using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace VectorIndexScenarioSuite.Tests
{
    public class VectorTestBase
    {
        private const string EmulatorBootScriptPath = "bootemulator.ps1";

        protected static string BaseTestParams = @"
        {
            ""AppSettings"": {
                ""deleteContainerOnStart"": true,
                ""emulatorSettings"": {
                  ""emulatorEndPoint"": ""https://localhost:8081"",
                  // Emulator auth key is publicly available and okay to be hardcoded.
                  ""emulatorKey"": 
                        ""C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="",
                },
            }
        }";

        public void SetupCommon(IConfiguration configuration)
        {
            bool useEmulator = Convert.ToBoolean(configuration["AppSettings:useEmulator"]);
            if (useEmulator)
            {
                StartEmulator();
            }
        }

        public static string UnionJson(string json1, string json2)
        {
            if (string.IsNullOrEmpty(json1) || string.IsNullOrEmpty(json2))
            {
                throw new ArgumentException("Both JSON strings must be non-empty.");
            }

            var obj1 = JsonConvert.DeserializeObject<JObject>(json1);
            var obj2 = JsonConvert.DeserializeObject<JObject>(json2);

            if (obj1 == null || obj2 == null)
            {
                throw new InvalidOperationException("Failed to deserialize one or both JSON strings into JObject.");
            }

            obj1.Merge(obj2, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Union,
                MergeNullValueHandling = MergeNullValueHandling.Merge
            });

            return JsonConvert.SerializeObject(obj1, Formatting.Indented);
        }

        private void StartEmulator()
        {
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{EmulatorBootScriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = System.Diagnostics.Process.Start(processInfo))
            {
                if (process == null)
                {
                    throw new System.Exception("Failed to start the emulator process.");
                }

                process.WaitForExit();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();

                System.Console.WriteLine(output);
                if (process.ExitCode != 0)
                {
                    throw new System.Exception($"Script execution failed: {error}");
                }
            }
        }
    }
}