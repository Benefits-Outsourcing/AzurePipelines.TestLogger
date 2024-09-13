namespace AzurePipelines.TestLogger
{
    public interface IEnvironmentVariableProvider
    {
        string GetEnvironmentVariable(string name);
    }
}