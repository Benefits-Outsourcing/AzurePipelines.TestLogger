﻿using AzurePipelines.AdoTools.PullRequests;
using AzurePipelines.TestLogger;

if (args.Length == 0)
{
    Console.WriteLine("No arguments provided.");
    return;
}

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
    case "create-run":
        runId = await apiClient.AddTestRun(new TestRun()
        {
            Name = $"{Environment.GetEnvironmentVariable("RELEASE_DEFINITIONNAME")}: {Environment.GetEnvironmentVariable("RELEASE_RELEASENAME")}: ATTEMPT {Environment.GetEnvironmentVariable(EnvironmentVariableNames.ReleaseAttempt)}",
            BuildId = buildId,
            IsAutomated = true,
            ReleaseUri = releaseUri,
            ReleaseEnvironmentUri = releaseEnvironmentUri,
            StartedDate = DateTime.UtcNow,
        }, CancellationToken.None);
        break;
    case "get-run":
        runId = (await apiClient.GetRuns(buildId, releaseId)).Last().Id;
        Console.WriteLine($"##vso[task.setvariable variable={EnvironmentVariableNames.TestRunId};isOutput=true]{runId}");
        break;
    case "complete-run":
        runId = (await apiClient.GetRuns(buildId, releaseId)).Last().Id;
        await apiClient.MarkTestRunCompleted(runId, false, DateTime.UtcNow, CancellationToken.None);
        break;
    case "pr-stats":
        if (args.Length < 2)
        {
            throw new ArgumentException("User email is required");
        }
        await new PRWorker(apiClient).Report(args[1], args.Length > 2 ? args[2] : "ESS");
        break;
    default:
        throw new ArgumentException("Invalid argument");
}

