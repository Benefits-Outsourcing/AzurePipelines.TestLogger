using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AzurePipelines.TestLogger.Tests
{
    public class MockAzureDevOpsTestRunLogCollectorServer
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvcCore();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc(routes =>
            {
                routes.MapRoute(name: "default", template: "{controller=Home}/{action=Index}/{id?}");
            });

            app.UseMiddleware<CaptureRequestsMiddleware>();
        }
    }
}