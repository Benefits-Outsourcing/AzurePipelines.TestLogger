using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace AzurePipelines.TestLogger
{
    public interface IApiClient
    {
        bool Verbose { get; set; }

        string BuildRequestedFor { get; set; }

        IApiClient WithAccessToken(string accessToken);

        IApiClient WithDefaultCredentials();

        Task<int> AddTestRun(TestRun testRun, CancellationToken cancellationToken);


        Task AddTestCases(int testRunId, params ITestResult[] results);

        Task MarkTestRunCompleted(int testRunId, bool aborted, DateTime completedDate, CancellationToken cancellationToken);

        Task ReopenTestRun(int testRunId, CancellationToken cancellationToken);

        Task<List<TestCaseResult>> GetTestResults(int testRunId, CancellationToken cancellationToken);

        Task RemoveTestRun(int testRunId, CancellationToken cancellationToken);

        Task<List<Microsoft.TeamFoundation.TestManagement.WebApi.TestRun>> GetRuns(int buildId);
        Task<List<Microsoft.TeamFoundation.TestManagement.WebApi.TestRun>> GetRuns(int buildId, int releaseId);
        Task<Microsoft.TeamFoundation.TestManagement.WebApi.TestRun> GetRun(int runId);
        T GetClient<T>() where T : IVssHttpClient;
    }
}