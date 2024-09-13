using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace AzurePipelines.TestLogger.Tests
{
    public class RequestStore : List<HttpRequest>, IRequestStore
    {
    }
}
