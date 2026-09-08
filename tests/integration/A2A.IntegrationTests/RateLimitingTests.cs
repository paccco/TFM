using System.Net;
using System.Text;
using A2A.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace A2A.IntegrationTests;

public class RateLimitingFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.Configure<AgentRateLimitConfig>(options =>
            {
                options.PermitLimit = 2;
                options.WindowSeconds = 60;
            });
        });
    }
}

public class RateLimitingTests : IClassFixture<RateLimitingFixture>
{
    private readonly HttpClient _client;

    public RateLimitingTests(RateLimitingFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    private static HttpRequestMessage CreateRequest(string agentId, int id)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/rpc")
        {
            Content = new StringContent($$"""
            {
                "jsonrpc": "2.0",
                "method": "get_task",
                "params": { "negotiation_id": "neg-rl-{{id}}" },
                "id": {{id}}
            }
            """, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Agent-ID", agentId);
        return request;
    }

    [Fact]
    public async Task RateLimiting_WhenQuotaExceeded_Returns429_AndIsolatesPartitions()
    {
        // Act: Agent A envía 2 peticiones (permitLimit = 2)
        var respA1 = await _client.SendAsync(CreateRequest("agent-alpha", 1));
        Assert.Equal(HttpStatusCode.OK, respA1.StatusCode);

        var respA2 = await _client.SendAsync(CreateRequest("agent-alpha", 2));
        Assert.Equal(HttpStatusCode.OK, respA2.StatusCode);

        // 3ª petición de Agent A -> Debe ser bloqueada con 429 Too Many Requests
        var respA3 = await _client.SendAsync(CreateRequest("agent-alpha", 3));
        Assert.Equal(HttpStatusCode.TooManyRequests, respA3.StatusCode);

        // Petición de Agent B (partición independiente) -> Debe pasar con 200 OK
        var respB1 = await _client.SendAsync(CreateRequest("agent-beta", 1));
        Assert.Equal(HttpStatusCode.OK, respB1.StatusCode);
    }
}
