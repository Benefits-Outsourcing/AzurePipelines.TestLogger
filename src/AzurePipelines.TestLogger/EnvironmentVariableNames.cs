namespace AzurePipelines.TestLogger
{
    public static class EnvironmentVariableNames
    {
        public const string AccessToken = "SYSTEM_ACCESSTOKEN";
        public const string TeamFoundationCollectionUri = "SYSTEM_TEAMFOUNDATIONCOLLECTIONURI";
        public const string TeamProject = "SYSTEM_TEAMPROJECT";
        public const string BuildId = "BUILD_BUILDID";
        public const string BuildRequestedFor = "BUILD_REQUESTEDFOR";
        public const string AgentName = "AGENT_NAME";
        public const string AgentJobName = "AGENT_JOBNAME";
        public const string ReleaseUri = "RELEASE_RELEASEURI";
        public const string ReleaseId = "RELEASE_RELEASEID";
        public const string AgentNumber = "SYSTEM_JOBPOSITIONINPHASE";
        public const string NumberOfAgents = "SYSTEM_TOTALJOBSINPHASE";
    }
}
