using AzurePipelines.TestLogger;

ApiClientFactory apiClientFactory = new ApiClientFactory();
var baseUri = Environment.GetEnvironmentVariable(EnvironmentVariableNames.TeamFoundationCollectionUri);
var project = Environment.GetEnvironmentVariable(EnvironmentVariableNames.TeamProject);


IApiClient apiClient = apiClientFactory.CreateWithDefaultCredentials(baseUri, project, "7.0");

var numberOfAgentsString = Environment.GetEnvironmentVariable(EnvironmentVariableNames.NumberOfAgents);
if (!int.TryParse(numberOfAgentsString, out var numberOfAgents))
{
    numberOfAgents = 1; // Default to 1 if the environment variable is not present or not a valid number
}

var agentNumberString = Environment.GetEnvironmentVariable(EnvironmentVariableNames.AgentNumber);
if (!int.TryParse(agentNumberString, out int agentNumber))
{
    agentNumber = 1; // Default to 1 if the environment variable is not present or not a valid number
}

var buildId = int.Parse(Environment.GetEnvironmentVariable(EnvironmentVariableNames.BuildId)!);
var releaseUri = Environment.GetEnvironmentVariable(EnvironmentVariableNames.ReleaseUri);
var releaseId = int.Parse(Environment.GetEnvironmentVariable(EnvironmentVariableNames.ReleaseId)!);
int runId = 0;

if (agentNumber == 1)
{
    runId = await apiClient.AddTestRun(new TestRun()
    {
        Name = Environment.GetEnvironmentVariable(EnvironmentVariableNames.AgentJobName),
        BuildId = buildId,
        IsAutomated = true,
        ReleaseUri = releaseUri,
        StartedDate = DateTime.UtcNow,
    }, CancellationToken.None);
}
else
{
    while (runId == 0)
    {
        var runs = await apiClient.GetRuns(buildId, releaseId);
        if (runs.Count > 0)
        {
            runId = runs[0].Id;
        }
        Console.Write("Waiting for the first agent to create the test run...");
        // Wait for a few seconds before looping again
        Thread.Sleep(5000);
    }
}

Console.WriteLine($"##vso[task.setvariable variable=TestRunId]{runId}");