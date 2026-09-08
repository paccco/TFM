using System.Net;
using System.Text.Json;
using Xunit;

namespace A2A.IntegrationTests;

public class MetadataEndpointTests : IClassFixture<GatewayFixture>
{
    private readonly HttpClient _client;

    public MetadataEndpointTests(GatewayFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task GetAgentCard_ReturnsHttp200_WithValidMetadataAndCacheHeaders()
    {
        // Act
        var response = await _client.GetAsync("/.well-known/agent.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.Public);
        Assert.Equal(TimeSpan.FromSeconds(300), response.Headers.CacheControl?.MaxAge);

        string content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("id", out var idProp));
        Assert.NotEmpty(idProp.GetString()!);
        Assert.True(root.TryGetProperty("skills", out var skillsProp));
        Assert.Equal(JsonValueKind.Array, skillsProp.ValueKind);
        Assert.True(skillsProp.GetArrayLength() >= 4);
    }
}
