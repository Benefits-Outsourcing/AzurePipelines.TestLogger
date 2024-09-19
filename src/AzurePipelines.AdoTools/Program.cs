using AzurePipelines.TestLogger;

ApiClientFactory apiClientFactory = new ApiClientFactory();
var baseUri = Environment.GetEnvironmentVariable(EnvironmentVariableNames.TeamFoundationCollectionUri);
var project = Environment.GetEnvironmentVariable(EnvironmentVariableNames.TeamProject);


IApiClient apiClient = apiClientFactory.CreateWithDefaultCredentials(baseUri, project, "7.0");

var buildId = int.Parse(Environment.GetEnvironmentVariable(EnvironmentVariableNames.BuildId)!);
var releaseUri = Environment.GetEnvironmentVariable(EnvironmentVariableNames.ReleaseUri);
var releaseEnvironmentUri = Environment.GetEnvironmentVariable(EnvironmentVariableNames.ReleaseEnvironmentUri);
var releaseId = int.Parse(Environment.GetEnvironmentVariable(EnvironmentVariableNames.ReleaseId)!);
int runId = 0;

switch (args[0])
{
    case "start":
        runId = await apiClient.AddTestRun(new TestRun()
        {
            Name = $"{Environment.GetEnvironmentVariable("RELEASE_DEFINITIONNAME")}: {Environment.GetEnvironmentVariable("RELEASE_RELEASENAME")}",
            BuildId = buildId,
            IsAutomated = true,
            ReleaseUri = releaseUri,
            ReleaseEnvironmentUri = releaseEnvironmentUri,
            StartedDate = DateTime.UtcNow,
        }, CancellationToken.None);
        break;
    case "get":
        runId = (await apiClient.GetRuns(buildId, releaseId)).Last().Id;
        Console.WriteLine($"##vso[task.setvariable variable={EnvironmentVariableNames.TestRunId};isOutput=true]{runId}");
        break;
    case "complete":
        runId = (await apiClient.GetRuns(buildId, releaseId)).Last().Id;
        await apiClient.MarkTestRunCompleted(runId, false, DateTime.UtcNow, CancellationToken.None);
        break;
    default:
        throw new ArgumentException("Invalid argument");
}

