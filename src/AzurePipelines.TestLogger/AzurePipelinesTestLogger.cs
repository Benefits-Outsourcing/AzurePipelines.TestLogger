using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace AzurePipelines.TestLogger
{
    [FriendlyName(AzurePipelinesTestLogger.FriendlyName)]
    [ExtensionUri(AzurePipelinesTestLogger.ExtensionUri)]
    public class AzurePipelinesTestLogger : ITestLoggerWithParameters
    {
        /// <summary>
        /// Uri used to uniquely identify the logger.
        /// </summary>
        public const string ExtensionUri = "logger://Microsoft/TestPlatform/AzurePiplinesTestLogger/v1";

        /// <summary>
        /// Alternate user friendly string to uniquely identify the logger.
        /// </summary>
        public const string FriendlyName = "AzurePipelines";

        private readonly IEnvironmentVariableProvider _environmentVariableProvider;
        private readonly IApiClientFactory _apiClientFactory;
        private IApiClient _apiClient;
        private LoggerQueue _queue;
        private bool _groupTestResultsByClassName = true;

        public AzurePipelinesTestLogger()
        {
            //while (!Debugger.IsAttached)
            //{
            //    Thread.Sleep(100); // Sleep for a short period to avoid busy-waiting
            //}

            // For debugging purposes
            // System.Diagnostics.Debugger.Launch();
            _environmentVariableProvider = new EnvironmentVariableProvider();
            _apiClientFactory = new ApiClientFactory();
        }

        // Used for testing
        public AzurePipelinesTestLogger(IEnvironmentVariableProvider environmentVariableProvider, IApiClientFactory apiClientFactory)
        {
            _environmentVariableProvider = environmentVariableProvider;
            _apiClientFactory = apiClientFactory;
        }

        public void Initialize(TestLoggerEvents events, string testRunDirectory)
        {
            Initialize(events, new Dictionary<string, string>());
        }

        public void Initialize(TestLoggerEvents events, Dictionary<string, string> parameters)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (!GetRequiredVariable(EnvironmentVariableNames.TeamFoundationCollectionUri, parameters, out string collectionUri)
                || !GetRequiredVariable(EnvironmentVariableNames.TeamProject, parameters, out string teamProject)
                || !GetRequiredVariable(EnvironmentVariableNames.AgentName, parameters, out string agentName)
                || !GetRequiredVariable(EnvironmentVariableNames.AgentJobName, parameters, out string jobName))
            {
                return;
            }

            var buildIdString = _environmentVariableProvider.GetEnvironmentVariable(EnvironmentVariableNames.BuildId);
            int? buildId = int.TryParse(buildIdString, out int parsedBuildId) ? parsedBuildId : null;
            var buildRequestedFor = _environmentVariableProvider.GetEnvironmentVariable(EnvironmentVariableNames.BuildRequestedFor);
            var releaseUri = _environmentVariableProvider.GetEnvironmentVariable(EnvironmentVariableNames.ReleaseUri);
            var releaseIdString = _environmentVariableProvider.GetEnvironmentVariable(EnvironmentVariableNames.ReleaseId);
            int? releaseId = int.TryParse(releaseIdString, out int parsedReleaseId) ? parsedReleaseId : null;
            var isReRun = parameters.ContainsKey("rerun");
            var runIdParameter = parameters.ContainsKey("TestRunId") ? parameters["TestRunId"] : null;

            var numberOfAgentsString = _environmentVariableProvider.GetEnvironmentVariable(EnvironmentVariableNames.NumberOfAgents);
            if (!int.TryParse(numberOfAgentsString, out int numberOfAgents))
            {
                numberOfAgents = 1; // Default to 1 if the environment variable is not present or not a valid number
            }

            var agentNumberString = _environmentVariableProvider.GetEnvironmentVariable(EnvironmentVariableNames.AgentNumber);
            if (!int.TryParse(agentNumberString, out int agentNumber))
            {
                agentNumber = 1; // Default to 1 if the environment variable is not present or not a valid number
            }

            if (_apiClient == null)
            {
                string apiVersion = "5.0";

                if (parameters.TryGetValue(TestLoggerParameters.ApiVersion, out string apiVersionParameterValue))
                {
                    apiVersion = apiVersionParameterValue;
                }

                if (parameters.TryGetValue(TestLoggerParameters.UseDefaultCredentials, out string useDefaultCredentialsString)
                    && bool.TryParse(useDefaultCredentialsString, out bool useDefaultCredentials)
                    && useDefaultCredentials)
                {
                    _apiClient = _apiClientFactory.CreateWithDefaultCredentials(collectionUri, teamProject, apiVersion);
                }
                else if (GetRequiredVariable(EnvironmentVariableNames.AccessToken, parameters, out string accessToken))
                {
                    _apiClient = _apiClientFactory.CreateWithAccessToken(accessToken, collectionUri, teamProject, apiVersion);
                }
                else
                {
                    throw new ArgumentException($"Expected environment variable {EnvironmentVariableNames.AccessToken} or {TestLoggerParameters.UseDefaultCredentials} parameter", nameof(parameters));
                }

                if (parameters.TryGetValue(TestLoggerParameters.Verbose, out string verboseParameterValue))
                {
                    if (!bool.TryParse(verboseParameterValue, out bool verbose))
                    {
                        throw new ArgumentException($"Expected {TestLoggerParameters.Verbose} parameter to be boolean.", nameof(parameters));
                    }

                    _apiClient.Verbose = verbose;
                }

                _apiClient.BuildRequestedFor = buildRequestedFor;
            }

            var runId = !string.IsNullOrWhiteSpace(runIdParameter) ? int.Parse(runIdParameter) : isReRun ? GetInProgressRunId() : 0;

            if (runId == 0 && agentNumber == 1 && numberOfAgents == 1)
            {
                // Single agent or local run, just create a run to log against
                runId = _apiClient.AddTestRun(new TestRun { Name = jobName, StartedDate = DateTime.UtcNow, BuildId = buildId, IsAutomated = true, ReleaseUri = releaseUri }, cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
                SaveInProgressRunId(runId);
            }

            if (parameters.TryGetValue(TestLoggerParameters.GroupTestResultsByClassName, out string groupTestResultsByClassNameString)
                && bool.TryParse(groupTestResultsByClassNameString, out bool groupTestResultsByClassName))
            {
                _groupTestResultsByClassName = groupTestResultsByClassName;
            }

            _queue = new LoggerQueue(_apiClient, runId, agentName, jobName, _groupTestResultsByClassName);

            // Register for the events
            events.TestRunMessage += TestMessageHandler;

            // when a single test has finished
            events.TestResult += TestResultHandler;

            // when the entire test run is finished
            events.TestRunComplete += TestRunCompleteHandler;
        }

        private int GetInProgressRunId()
        {
            if (File.Exists("testrunid.txt"))
            {
                return int.Parse(File.ReadAllText("testrunid.txt"));
            }
            return 0;
        }

        private void SaveInProgressRunId(int runId)
        {
            File.WriteAllText("testrunid.txt", runId.ToString());
        }

        private bool GetRequiredVariable(string name, IDictionary<string, string> parameters, out string value)
        {
            value = _environmentVariableProvider.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value) && parameters.TryGetValue(name, out value))
            {
                return true;
            }
            if (string.IsNullOrEmpty(value))
            {
                Console.WriteLine($"AzurePipelines.TestLogger: Not an Azure Pipelines test run, environment variable {name} not set.");
                return false;
            }
            return true;
        }

        private void TestMessageHandler(object sender, TestRunMessageEventArgs e)
        {
            // Add code to handle message
        }

        private void TestResultHandler(object sender, TestResultEventArgs e)
        {
            try
            {
                _queue.Enqueue(new VstpTestResult(e.Result));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void TestRunCompleteHandler(object sender, TestRunCompleteEventArgs e)
        {
            try
            {
                _queue.Flush(new VstpTestRunComplete(e.IsAborted || e.IsCanceled, e.AttachmentSets));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
