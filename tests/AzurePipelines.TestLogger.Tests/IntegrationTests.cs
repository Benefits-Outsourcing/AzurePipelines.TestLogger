using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SampleUnitTestProject;

namespace AzurePipelines.TestLogger.Tests
{
    [TestFixture]
    public class IntegrationTests
    {
        private string _vsTestExeFilePath;
        private string _sampleUnitTestProjectDllFilePath;
        private string _azurePipelinesTestLoggerAssemblyPath;

        [OneTimeSetUp]
        public void SetUpFixture()
        {
            _vsTestExeFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft Visual Studio",
                "2022",
                "Enterprise",
                "Common7",
                "IDE",
                "CommonExtensions",
                "Microsoft",
                "TestWindow",
                "vstest.console.exe");

            string configuration = "Debug";

#if RELEASE
            configuration = "Release";
#endif

            string rootRepositoryPath = GetRootRepositoryPath();
            _sampleUnitTestProjectDllFilePath = Path.Combine(rootRepositoryPath, "tests", "SampleUnitTestProject", "bin", configuration, "net8.0", "SampleUnitTestProject.dll");
            _azurePipelinesTestLoggerAssemblyPath = Path.Combine(rootRepositoryPath, "src", "AzurePipelines.TestLogger", "bin", configuration, "net8.0");
        }

        [Test]
        public void ExecuteTestWithInvalidAzureDevopsCollectionUriContinuesTestExecution()
        {
            // Given
            string fullyQualifiedTestMethodName = GetFullyQualifiedTestMethodName(
                typeof(UnitTest1),
                nameof(UnitTest1.TestMethod));

            const string collectionUri = "collectionUri";

            // When
            int exitCode = ExecuteUnitTestWithLogger(
                testMethod: fullyQualifiedTestMethodName,
                collectionUri: collectionUri);

            // Then
            Assert.Equals(0, exitCode);
        }

        [Test]
        public async Task ExecuteTestWithDataTestMethodLogsEachDataRow()
        {
            // Given
            string fullyQualifiedTestMethodName = GetFullyQualifiedTestMethodName(
                typeof(UnitTest1),
                nameof(UnitTest1.DataTestMethod));

            // When
            TestResults testResults = await StartServerAndExecuteUnitTestWithLoggerAsync(
                fullyQualifiedTestMethodName);

            // Then
            Assert.Equals(0, testResults.ExitCode);
            Assert.Equals(2, testResults.CapturedRequests.Count);
        }

        [Test]
        public async Task ExecuteEndToEndTest()
        {

            if (File.Exists("TestCaseResult_cache.json"))
            {
                File.Delete("TestCaseResult_cache.json");
            }

            if (File.Exists("testrunid.txt"))
            {
                File.Delete("testrunid.txt");
            }

            // Given
            string fullyQualifiedTestMethodName = GetFullyQualifiedTestMethodName(
            typeof(UnitTest1),
            nameof(UnitTest1.TestMethod));

            ApiClientFactory apiClientFactory = new ApiClientFactory();
            IApiClient apiClient = apiClientFactory.CreateWithDefaultCredentials("https://dev.azure.com/wtw-bda-outsourcing-product/", "BenefitConnect", "7.0");

            //var x_run = await apiClient.GetRun(1384264);
            //var x_results = await apiClient.GetTestResults(x_run.Id, CancellationToken.None);

            //var x_testcase = x_results.Single(item => item.TestCaseTitle == "Test_HSA_EligibilityQuestions_PageContentAndRelatedFunctionality");

            var testRuns = (await apiClient.GetRuns(193606)).ToList();
            foreach (var testRun in testRuns)
            {
                await apiClient.RemoveTestRun(testRun.Id, CancellationToken.None);
            }

            int exitCode = ExecuteUnitTestWithLogger(
                   testMethod: fullyQualifiedTestMethodName,
                   collectionUri: "https://dev.azure.com/wtw-bda-outsourcing-product/",
                   teamProject: "BenefitConnect",
                   buildId: "193606",
                   buildRequestedFor: "PW UNIT TEST",
                   filter: "ClassName=SampleUnitTestProject.UnitTest1",
                   agentName: "No AGENT",
                   agentJobName: "Job 1",
                   iteration: 1,
                   maxiteration: 3);

            //Assert.That(exitCode, Is.EqualTo(0));

            Environment.SetEnvironmentVariable("Flakey", "true");
            var runId = int.Parse(File.ReadAllText("testrunid.txt"));

            var result = await apiClient.GetRun(runId);

            Assert.That(result, Is.Not.Null);

            exitCode = ExecuteUnitTestWithLogger(
                   testMethod: fullyQualifiedTestMethodName,
                   collectionUri: "https://dev.azure.com/wtw-bda-outsourcing-product/",
                   teamProject: "BenefitConnect",
                   buildRequestedFor: "PW UNIT TEST",
                   buildId: "193606",
                   filter: "Name=TestMethodThatIsDeliberatelyFlakey|Name=TestMethodThatFails",
                   testRunId: runId.ToString(),
                   agentName: "No AGENT",
                   agentJobName: "Job 1");

            //Assert.That(exitCode, Is.EqualTo(0));

            exitCode = ExecuteUnitTestWithLogger(
                   testMethod: fullyQualifiedTestMethodName,
                   collectionUri: "https://dev.azure.com/wtw-bda-outsourcing-product/",
                   teamProject: "BenefitConnect",
                   buildRequestedFor: "PW UNIT TEST",
                   buildId: "193606",
                   filter: "Name=TestMethodThatFails",
                   testRunId: runId.ToString(),
                   agentName: "No AGENT",
                   agentJobName: "Job 1");

            //Assert.That(exitCode, Is.EqualTo(0));
        }


        private async Task<TestResults> StartServerAndExecuteUnitTestWithLoggerAsync(
            string fullyQualifiedTestMethodName)
        {
            // Create the Server
            IRequestStore requestStore = new RequestStore();

            IWebHost host = WebHost.CreateDefaultBuilder()
                .UseKestrel()
                .ConfigureServices(configureServices =>
                {
                    configureServices.AddSingleton(requestStore);
                })
                .UseUrls("http://127.0.0.1:0") // listen on a random available port
                .UseStartup<MockAzureDevOpsTestRunLogCollectorServer>()
                .Build();

            await host.StartAsync();

            try
            {
                // Get the server's listening address
                IServerAddressesFeature serverAddresses = host.Services
                    .GetRequiredService<IServer>()
                    .Features.Get<IServerAddressesFeature>();

                string serverUrl = serverAddresses.Addresses.First();

                Console.WriteLine($"Server is listening on: {serverUrl}");

                int exitCode = ExecuteUnitTestWithLogger(
                    testMethod: fullyQualifiedTestMethodName,
                    collectionUri: $"{serverUrl}/");

                List<HttpRequest> capturedRequests = (List<HttpRequest>)requestStore;

                return new TestResults
                {
                    ExitCode = exitCode,
                    CapturedRequests = capturedRequests,
                };
            }
            finally
            {
                await host.StopAsync();
            }
        }

        private class TestResults
        {
            public int ExitCode { get; set; }
            public List<HttpRequest> CapturedRequests { get; set; }
        }

        private static string GetFullyQualifiedTestMethodName(Type type, string methodName)
        {
            MethodInfo methodInfo = type.GetMethod(methodName);
            return $"{type.Namespace}.{type.Name}.{methodInfo.Name}";
        }

        private int ExecuteUnitTestWithLogger(
            bool verbose = false,
            bool useDefaultCredentials = true,
            string apiVersion = "7.0",
            bool groupTestResultsByClassName = false,
            string testMethod = "SampleUnitTestProject.UnitTest1.TestMethod1",
            string collectionUri = "collectionUri",
            string teamProject = "teamProject",
            string buildId = "buildId",
            string buildRequestedFor = "buildRequestedFor",
            string agentName = "agentName",
            string agentJobName = "jobName",
            string releaseUri = null,
            string filter = null,
            string testRunId = null,
            int maxiteration = 1,
            int iteration = 1)
        {
            List<string> loggerArguments = new List<string>
            {
                "AzurePipelines",
                $"Verbose={verbose}",
                $"UseDefaultCredentials={useDefaultCredentials}",
                $"ApiVersion={apiVersion}",
                $"maxiteration={maxiteration}",
                $"iteration={iteration}"
            };

            if (testRunId != null)
            {
                loggerArguments.Add($"TestRunId={testRunId}");
            }

            List<string> arguments = new List<string>
            {
                "test",
                $"\"{_sampleUnitTestProjectDllFilePath}\"",
                $"--filter {(filter ?? "\"FullyQualifiedName={testMethod}\"")}",
               // $"--logger \"trx;LogFileName=test_results_$repeat.trx\" --results-directory ./TestResults/",
                $"--logger \"{string.Join(";", loggerArguments)}\"",
                $"--test-adapter-path \"{_azurePipelinesTestLoggerAssemblyPath}\""
            };

            Dictionary<string, string> environmentVariables = new Dictionary<string, string>
            {
                { EnvironmentVariableNames.TeamFoundationCollectionUri, collectionUri },
                { EnvironmentVariableNames.TeamProject, teamProject },
                { EnvironmentVariableNames.BuildId, buildId },
                { EnvironmentVariableNames.BuildRequestedFor, buildRequestedFor },
                { EnvironmentVariableNames.AgentName, agentName },
                { EnvironmentVariableNames.AgentJobName, agentJobName },
                { EnvironmentVariableNames.ReleaseUri, releaseUri }
            };

            ProcessRunner processRunner = new ProcessRunner();
            return processRunner.Run("dotnet", arguments, environmentVariables);
        }

        private static string GetRootRepositoryPath()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string fileNameToFind = "root";

            // Start from the current directory and move up the directory tree
            while (!File.Exists(Path.Combine(currentDirectory, fileNameToFind)))
            {
                string parentDirectory = Directory.GetParent(currentDirectory)?.FullName;
                if (parentDirectory == null || parentDirectory == currentDirectory)
                {
                    throw new Exception($"Failed to find file '{fileNameToFind}' in the directory tree.");
                }

                currentDirectory = parentDirectory;
            }

            return currentDirectory;
        }
    }
}
