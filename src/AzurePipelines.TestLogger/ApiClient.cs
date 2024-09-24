using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using AzurePipelines.TestLogger.Json;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using TestOutcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome;

namespace AzurePipelines.TestLogger
{
    public abstract class ApiClient : IApiClient
    {
        private readonly string _runsUrl;
        private readonly string _buildsUrl;
        private readonly string _apiVersionString;
        private readonly string _organizationUrl;
        private readonly string _teamProject;
        private VssConnection _connection;
        private HttpClient _client;

        protected const string _dateFormatString = "yyyy-MM-ddTHH:mm:ss.FFFZ";

        private readonly LocalCache<TestCaseResult> TestCaseResultCache = new LocalCache<TestCaseResult>();

        protected ApiClient(string organizationUrl, string teamProject, string apiVersionString)
        {
            if (organizationUrl == null)
            {
                throw new ArgumentNullException(nameof(organizationUrl));
            }

            if (teamProject == null)
            {
                throw new ArgumentNullException(nameof(teamProject));
            }

            _teamProject = teamProject;
            _organizationUrl = organizationUrl;
            _runsUrl = $"{organizationUrl}{teamProject}/_apis/test/runs";
            _buildsUrl = $"{organizationUrl}{teamProject}/_apis/build/builds/";
            _apiVersionString = apiVersionString ?? throw new ArgumentNullException(nameof(apiVersionString));
        }

        public bool Verbose { get; set; }

        public string BuildRequestedFor { get; set; }

        public IApiClient WithAccessToken(string accessToken)
        {
            // The : character delimits username (which should be empty here) and password in basic auth headers
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Authorization
                 = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{accessToken}")));
            return this;
        }

        public IApiClient WithDefaultCredentials()
        {
            var token = Environment.GetEnvironmentVariable(EnvironmentVariableNames.AccessToken);

            if (string.IsNullOrEmpty(token))
            {
                DefaultAzureCredential credential = new(new DefaultAzureCredentialOptions { ExcludeManagedIdentityCredential = false });
                string[] scopes = { "499b84ac-1321-427f-aa17-267ca6975798/.default" };

                var handler = new AzureAuthenticationHandler(credential, scopes)
                {
                    InnerHandler = new HttpClientHandler()
                };

                _client = new HttpClient(handler);
                token = credential.GetToken(new TokenRequestContext(scopes)).Token;
            }
            else
            {
                _client = new HttpClient();
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}")));
            }

            _connection = new VssConnection(new Uri(_organizationUrl), new VssBasicCredential(string.Empty, token));
            return this;
        }

        public T GetClient<T>() where T : IVssHttpClient
        {
            return _connection.GetClient<T>();
        }

        public async Task<int> AddTestRun(TestRun testRun, CancellationToken cancellationToken)
        {
            // string requestBody = new Dictionary<string, object>
            // {
            //     { "name", testRun.Name },
            //     { "build", new Dictionary<string, object> { { "id", testRun.BuildId } } },
            //     { "startedDate", testRun.StartedDate.ToString(_dateFormatString) },
            //     { "isAutomated", true }
            // }.ToJson();

            // string responseString = await SendAsync(HttpMethod.Post, null, requestBody, cancellationToken).ConfigureAwait(false);
            // using (StringReader reader = new StringReader(responseString))
            // {
            //     JsonObject response = JsonDeserializer.Deserialize(reader) as JsonObject;
            //     return response.ValueAsInt("id");
            // }
            var testClient = _connection.GetClient<TestManagementHttpClient>();
            var createdTestRun = await testClient.CreateTestRunAsync(
                new RunCreateModel(
                    name: testRun.Name,
                    startedDate: testRun.StartedDate.ToString(_dateFormatString),
                    buildId: testRun.BuildId.GetValueOrDefault(),
                    isAutomated: true,
                    releaseUri: testRun.ReleaseUri,
                    releaseEnvironmentUri: testRun.ReleaseEnvironmentUri
                    ),
                _teamProject,
                cancellationToken).ConfigureAwait(false);

            // var newTestRun = new RunCreateModel
            // {
            //     Name = testRun.Name,
            //     Build = new ShallowReference { Id = testRun.BuildId.ToString() },
            //     StartedDate = testRun.StartedDate,
            //     IsAutomated = true
            // };

            // var createdTestRun = await testClient.CreateTestRunAsync(newTestRun, _teamProject, cancellationToken: cancellationToken);

            return createdTestRun.Id;
        }

        public async Task<List<TestCaseResult>> GetTestResults(int testRunId, CancellationToken cancellationToken)
        {
            TestManagementHttpClient testClient = _connection.GetClient<TestManagementHttpClient>();
            return await testClient.GetTestResultsAsync(_teamProject, testRunId, cancellationToken: cancellationToken);

            //string responseString = await SendAsync(HttpMethod.Get, $"/{testRunId}/results", null, cancellationToken).ConfigureAwait(false);
            //using StringReader reader = new(responseString);
            //return JsonDeserializer.Deserialize(reader);
        }

        public async Task RemoveTestRun(int testRunId, CancellationToken cancellationToken)
        {
            TestManagementHttpClient testClient = _connection.GetClient<TestManagementHttpClient>();
            await testClient.DeleteTestRunAsync(_teamProject, testRunId, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<List<Microsoft.TeamFoundation.TestManagement.WebApi.TestRun>> GetRuns(int buildId)
        {
            TestManagementHttpClient testClient = _connection.GetClient<TestManagementHttpClient>();
            return (await testClient.GetTestRunsAsync(_teamProject, buildUri: $"vstfs:///Build/Build/{buildId}", includeRunDetails: true).ConfigureAwait(false)).Where(x => x.State != "255").ToList();
        }

        public async Task<Microsoft.TeamFoundation.TestManagement.WebApi.TestRun> GetRun(int runId)
        {
            TestManagementHttpClient testClient = _connection.GetClient<TestManagementHttpClient>();
            return await testClient.GetTestRunByIdAsync(_teamProject, runId).ConfigureAwait(false);
        }

        public async Task<List<Microsoft.TeamFoundation.TestManagement.WebApi.TestRun>> GetRuns(int buildId, int releaseId)
        {
            var allBuildRuns = await GetRuns(buildId);
            return allBuildRuns.Where(x => x.Release != null && x.Release.Id == releaseId).ToList();
        }

        public async Task AddTestCases(int testRunId, params ITestResult[] results)
        {
            // use VssConnection
            var testClient = _connection.GetClient<TestManagementHttpClient>();

            var previousTestResults = TestCaseResultCache.Items.Where(cached => results.Any(result => cached.AutomatedTestName.Equals(result.DisplayName))).ToList();

            if (previousTestResults.Any())
            {
                var updatedTestCaseResults = await testClient.UpdateTestResultsAsync(
                    results.Select(result =>
                    {
                        var parent = previousTestResults.SingleOrDefault(cached => cached.AutomatedTestName.Equals(result.DisplayName));
                        var subresults = new List<TestSubResult>();
                        //if (parent.ResultGroupType != ResultGroupType.Rerun)
                        //{
                        //    subresults.Add(
                        //        // copy the parent result to the subresult so that we keep the first failed test as a subresult
                        //        new TestSubResult
                        //        {
                        //            ParentId = parent.Id,
                        //            DisplayName = $"#1 {result.DisplayName}",
                        //            Outcome = parent.Outcome,
                        //            DurationInMs = (long)parent.DurationInMs,
                        //            ErrorMessage = parent.ErrorMessage,
                        //            StackTrace = parent.StackTrace,
                        //            ResultGroupType = ResultGroupType.None,
                        //            ComputerName = Environment.MachineName,
                        //            CompletedDate = parent.CompletedDate,
                        //            StartedDate = parent.StartedDate,
                        //            CustomFields = new List<CustomTestField> { new CustomTestField { FieldName = "AttemptId", Value = 1 } }

                        //        });
                        //}
                        // add the latest test result
                        subresults.Add(new TestSubResult
                        {
                            ParentId = parent.Id,
                            DisplayName = $"#{parent.Revision + 1} {result.DisplayName}",
                            Outcome = result.Outcome.ToString(),
                            DurationInMs = Convert.ToInt64(result.Duration.TotalMilliseconds),
                            ErrorMessage = result.ErrorMessage,
                            StackTrace = result.ErrorStackTrace,
                            ResultGroupType = ResultGroupType.None,
                            ComputerName = Environment.MachineName,
                            CompletedDate = result.StartTime.UtcDateTime,
                            StartedDate = result.StartTime.UtcDateTime,
                            CustomFields = new List<CustomTestField> { new CustomTestField { FieldName = "AttemptId", Value = parent.Revision + 1 } }
                        });

                        var newParent = new TestCaseResult
                        {
                            Id = parent.Id,
                            Outcome = result.Outcome.ToString(),
                            AutomatedTestName = result.DisplayName,
                            // FailingSince TODO
                            ResultGroupType = ResultGroupType.Rerun,
                            SubResults = subresults,
                            ErrorMessage = result.ErrorMessage ?? string.Empty,
                            StackTrace = result.ErrorStackTrace ?? string.Empty,
                            ComputerName = Environment.MachineName,
                            StartedDate = result.StartTime.UtcDateTime,
                            CompletedDate = result.StartTime.UtcDateTime,
                            CustomFields = new List<CustomTestField> { new CustomTestField { FieldName = "AttemptId", Value = parent.Revision + 1 }, new CustomTestField { FieldName = "IsTestResultFlaky", Value = result.Outcome == TestOutcome.Passed } }
                        };

                        parent.ResultGroupType = ResultGroupType.Rerun;
                        parent.Revision++;
                        TestCaseResultCache.WriteCache();

                        return newParent;

                    }).ToArray(),
                    project: _teamProject,
                    runId: testRunId,
                    cancellationToken: CancellationToken.None
                );

                foreach (var updated in updatedTestCaseResults)
                {
                    var result = results.Single(r => r.DisplayName.Equals(updated.AutomatedTestName));
                    //// serialized update to json and output to console (using newtonsoft json)
                    //Console.WriteLine(JsonConvert.SerializeObject(updated));
                    UploadConsoleOutputsAndErrors(testRunId, result.Messages, updated.Id, updated.SubResults.Single().Id).ConfigureAwait(false).GetAwaiter().GetResult();
                    UploadTestResultFiles(testRunId, updated.Id, updated.SubResults.Single().Id, result.Attachments, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();

                }
            }
            else
            {
                var testCases = results.Select(result => new TestCaseResult
                {
                    TestCaseTitle = result.DisplayName,
                    ComputerName = Environment.MachineName,
                    AutomatedTestName = result.DisplayName,
                    AutomatedTestType = "UnitTest",
                    CompletedDate = result.StartTime.UtcDateTime,
                    StartedDate = result.StartTime.UtcDateTime,
                    Outcome = result.Outcome.ToString(),
                    // FailingSince TODO
                    DurationInMs = Convert.ToInt64(result.Duration.TotalMilliseconds),
                    ErrorMessage = result.ErrorMessage,
                    StackTrace = result.ErrorStackTrace,
                    ResultGroupType = result.Outcome == TestOutcome.Passed ? ResultGroupType.None : ResultGroupType.Rerun,
                    SubResults = result.Outcome == TestOutcome.Passed ? null : new List<TestSubResult> {
                        new TestSubResult { Outcome = result.Outcome.ToString(),
                            ErrorMessage = result.ErrorMessage,
                            StackTrace = result.ErrorStackTrace,
                            DisplayName = $"#1 {result.DisplayName}",
                            ComputerName = Environment.MachineName,
                            StartedDate = result.StartTime.UtcDateTime,
                            CompletedDate = result.StartTime.UtcDateTime,
                            CustomFields = new List<CustomTestField> { new CustomTestField { FieldName = "AttemptId", Value = 1 } }
                        } },
                }).ToArray();

                var savedCases = await testClient.AddTestResultsToTestRunAsync(
                    testCases,
                    project: _teamProject,
                    runId: testRunId,
                    cancellationToken: CancellationToken.None
                );

                testCases.ForEach(testCase =>
                {
                    testCase.Revision = 1;
                    var savedTestCase = savedCases.Single(saved => saved.AutomatedTestName.Equals(testCase.AutomatedTestName));
                    var result = results.Single(r => r.DisplayName.Equals(testCase.AutomatedTestName));
                    testCase.Id = savedTestCase.Id;
                    if (testCase.Outcome == "Failed")
                    {
                        UploadConsoleOutputsAndErrors(testRunId, result.Messages, testCase.Id, savedTestCase.SubResults.Single().Id).ConfigureAwait(false).GetAwaiter().GetResult();
                        UploadTestResultFiles(testRunId, testCase.Id, savedTestCase.SubResults.Single().Id, result.Attachments, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
                    }

                });
                TestCaseResultCache.AddRange(testCases);
            }

        }

        public async Task MarkTestRunCompleted(int testRunId, bool aborted, DateTime completedDate, CancellationToken cancellationToken)
        {
            var testClient = _connection.GetClient<TestManagementHttpClient>();

            await testClient.UpdateTestRunAsync(
                    new RunUpdateModel(state: aborted ? "Aborted" : "Completed", completedDate: completedDate.ToString(_dateFormatString)),
                    project: _teamProject,
                    runId: testRunId,
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }

        public async Task ReopenTestRun(int testRunId, CancellationToken cancellationToken)
        {
            var testClient = _connection.GetClient<TestManagementHttpClient>();

            await testClient.UpdateTestRunAsync(
                    new RunUpdateModel(state: "InProgress", completedDate: ""),
                    project: _teamProject,
                    runId: testRunId,
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }

        protected Dictionary<string, object> GetTestResultProperties(ITestResult testResult)
        {
            // https://docs.microsoft.com/en-us/rest/api/azure/devops/test/results/list?view=azure-devops-rest-6.0#testcaseresult
            // outcome valid values = (Unspecified, None, Passed, Failed, Inconclusive, Timeout, Aborted, Blocked, NotExecuted, Warning, Error, NotApplicable, Paused, InProgress, NotImpacted)
            string testOutcome;
            switch (testResult.Outcome)
            {
                case TestOutcome.None:
                    testOutcome = "None";
                    break;
                case TestOutcome.Passed:
                    testOutcome = "Passed";
                    break;
                case TestOutcome.Failed:
                    testOutcome = "Failed";
                    break;
                case TestOutcome.Skipped:
                    testOutcome = "Inconclusive";
                    break;
                case TestOutcome.NotFound:
                    testOutcome = "NotExecuted";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(testResult.Outcome), testResult.Outcome.ToString());
            }

            Dictionary<string, object> properties = new Dictionary<string, object>
            {
                { "outcome", testOutcome },
                { "computerName", testResult.ComputerName },
                { "runBy", new Dictionary<string, object> { { "displayName", BuildRequestedFor } } }
            };

            AddAdditionalTestResultProperties(testResult, properties);

            if (testResult.Outcome == TestOutcome.Passed || testResult.Outcome == TestOutcome.Failed)
            {
                properties.Add("startedDate", testResult.StartTime.ToString(_dateFormatString));
                properties.Add("completedDate", testResult.EndTime.ToString(_dateFormatString));

                long duration = Convert.ToInt64(testResult.Duration.TotalMilliseconds);
                properties.Add("durationInMs", duration.ToString(CultureInfo.InvariantCulture));

                string errorStackTrace = testResult.ErrorStackTrace;
                if (!string.IsNullOrEmpty(errorStackTrace))
                {
                    properties.Add("stackTrace", errorStackTrace);
                }

                string errorMessage = testResult.ErrorMessage;

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    properties.Add("errorMessage", errorMessage);
                }
            }
            else
            {
                // Handle output type skip, NotFound and None
            }

            return properties;
        }

        public abstract string GetTestResults(
            Dictionary<string, TestResultParent> testCaseTestResults,
            IEnumerable<IGrouping<string, ITestResult>> testResultsByParent,
            DateTime completedDate);

        public virtual void AddAdditionalTestResultProperties(ITestResult testResult, Dictionary<string, object> properties)
        {
        }


        private async Task UploadConsoleOutputsAndErrors(int testRunId, IList<TestResultMessage> messages, int testResultId, int? testSubResultId)
        {
            StringBuilder stdErr = new StringBuilder();
            StringBuilder stdOut = new StringBuilder();
            foreach (TestResultMessage m in messages)
            {
                if (TestResultMessage.StandardOutCategory.Equals(m.Category, StringComparison.OrdinalIgnoreCase))
                {
                    stdOut.AppendLine(m.Text);
                }
                else if (TestResultMessage.StandardErrorCategory.Equals(m.Category, StringComparison.OrdinalIgnoreCase))
                {
                    stdErr.AppendLine(m.Text);
                }
            }

            if (stdOut.Length > 0)
            {
                await AttachTextAsFile(testRunId, testResultId, testSubResultId, stdOut.ToString(), "console output.txt", null);
            }

            if (stdErr.Length > 0)
            {
                await AttachTextAsFile(testRunId, testResultId, testSubResultId, stdErr.ToString(), "console error.txt", null);
            }
        }

        private string NormalizePathForLinux(string path)
        {
            // Replace backslashes with forward slashes
            path = path.Replace('\\', '/');

            // If the path is relative, convert it to an absolute path
            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(path);
            }

            return path;
        }

        private async Task UploadTestResultFiles(int testRunId, int? testResultId, int? testSubResultId, ICollection<AttachmentSet> attachmentSets, CancellationToken cancellationToken)
        {
            if (attachmentSets.Count > 0)
            {
                string message = $"Attaching files to test run {testRunId}";

                if (testResultId != null)
                {
                    message += $" and test result {testResultId}";
                }

                message += "...";

                Console.WriteLine(message);
            }

            foreach (AttachmentSet attachmentSet in attachmentSets)
            {

                foreach (UriDataAttachment attachment in attachmentSet.Attachments)
                {

                    string localPath = $"TestResults/{attachment.Uri}";

                    // 

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        localPath = NormalizePathForLinux(localPath);
                    }

                    Console.WriteLine($"Attaching file {attachment.Description} {attachment.Uri.ToString()}...");

                    //if (attachment.Uri.IsAbsoluteUri)
                    //{
                    //    localPath = attachment.Uri.LocalPath;
                    //}
                    //else
                    //{
                    //    // Handle relative URI case
                    //    // You might need to resolve the relative URI to an absolute path based on your application's context
                    //    localPath = Path.GetFullPath(attachment.Uri.ToString());
                    //}

                    //Console.WriteLine($"Attaching file {attachment.Description} {attachment.Uri.LocalPath}...");

                    await AttachFile(testRunId, testResultId, testSubResultId, localPath, attachment.Description);
                }
            }
        }

        private async Task AttachTextAsFile(int testRunId, int testResultId, int? testSubResultId, string fileContents, string fileName, string comment)
        {
            byte[] contentAsBytes = Encoding.UTF8.GetBytes(fileContents);
            await AttachFile(testRunId, testResultId, testSubResultId, contentAsBytes, fileName, comment);
        }

        private async Task AttachFile(int testRunId, int? testResultId, int? testSubResultId, string filePath, string comment)
        {
            byte[] contentAsBytes = File.ReadAllBytes(filePath);
            string fileName = Path.GetFileName(filePath);
            await AttachFile(testRunId, testResultId, testSubResultId, contentAsBytes, fileName, comment);
        }

        private async Task AttachFile(int testRunId, int? testResultId, int? testSubResultId, byte[] fileContents, string fileName, string comment)
        {
            string contentAsBase64 = Convert.ToBase64String(fileContents);

            string attachmentType = "GeneralAttachment";

            if (fileName.EndsWith(".coverage", StringComparison.OrdinalIgnoreCase))
            {
                attachmentType = "CodeCoverage";
            }

            Dictionary<string, object> props = new Dictionary<string, object>
            {
                { "stream", contentAsBase64 },
                { "fileName", fileName },
                { "comment", comment },
                { "attachmentType", attachmentType }
            };

            string requestBody = props.ToJson();

            var testClient = _connection.GetClient<TestManagementHttpClient>();

            if (testResultId == null)
            {
                await testClient.CreateTestRunAttachmentAsync(new TestAttachmentRequestModel(stream: contentAsBase64, fileName, comment, attachmentType), _teamProject, testRunId, null, CancellationToken.None);
            }
            else
            {
                if (testSubResultId.HasValue)
                {
                    await testClient.CreateTestSubResultAttachmentAsync(new TestAttachmentRequestModel(stream: contentAsBase64, fileName, comment, attachmentType), _teamProject, testRunId, testResultId.Value, testSubResultId.Value, CancellationToken.None);
                }
                else
                {
                    await testClient.CreateTestResultAttachmentAsync(new TestAttachmentRequestModel(stream: contentAsBase64, fileName, comment, attachmentType), _teamProject, testRunId, testResultId.Value, CancellationToken.None);
                }

            }
        }
    }
}