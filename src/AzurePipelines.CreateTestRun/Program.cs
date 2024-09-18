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

runId = await apiClient.AddTestRun(new TestRun()
{
    Name = $"{Environment.GetEnvironmentVariable("RELEASE_DEFINITIONNAME")}: {Environment.GetEnvironmentVariable("RELEASE_RELEASENAME")}",
    BuildId = buildId,
    IsAutomated = true,
    ReleaseUri = releaseUri,
    ReleaseEnvironmentUri = releaseEnvironmentUri,
    StartedDate = DateTime.UtcNow,
}, CancellationToken.None);

Console.WriteLine($"##vso[task.setvariable variable={EnvironmentVariableNames.TestRunId};isOutput=true]{runId}");