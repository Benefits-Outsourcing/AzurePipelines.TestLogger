using Microsoft.AspNetCore.Http;

namespace AzurePipelines.TestLogger.Tests
{
    public interface IRequestStore
    {
        void Add(HttpRequest item);
    }
}