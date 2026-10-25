using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace RequestTracker.IntegrationTests.TestHost;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // TestController lives in this assembly, so it never ships with the API.
        builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly));
    }
}
