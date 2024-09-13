using AzurePipelines.TestLogger;

ApiClientFactory apiClientFactory = new ApiClientFactory();
var baseUri = Environment.GetEnvironmentVariable(EnvironmentVariableNames.TeamFoundationCollectionUri);
var project = Environment.GetEnvironmentVariable(EnvironmentVariableNames.TeamProject);
var runId = Environment.GetEnvironmentVariable("TestRunId");

IApiClient apiClient = apiClientFactory.CreateWithDefaultCredentials(baseUri, project, "7.0");

apiClient.MarkTestRunCompleted(int.Parse(runId), false, DateTime.UtcNow, CancellationToken.None).Wait();