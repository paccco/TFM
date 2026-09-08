using A2A.Gateway.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace A2A.IntegrationTests;

public class GatewayFixture : WebApplicationFactory<Program>
{
    public MockNegotiationDispatcher MockDispatcher =>
        Services.GetRequiredService<MockNegotiationDispatcher>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
