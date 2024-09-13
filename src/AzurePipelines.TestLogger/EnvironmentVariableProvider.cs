using System;

namespace AzurePipelines.TestLogger
{
    public class EnvironmentVariableProvider : IEnvironmentVariableProvider
    {
        public string GetEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name);
        }

    }
}