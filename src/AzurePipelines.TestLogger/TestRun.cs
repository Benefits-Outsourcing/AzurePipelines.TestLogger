using System;

namespace AzurePipelines.TestLogger
{
    internal class TestRun
    {
        public string Name { get; set; }

        public int BuildId { get; set; }
        public string ReleaseUri { get; set; }

        public DateTime StartedDate { get; set; }

        public bool IsAutomated { get; set; }
    }
}