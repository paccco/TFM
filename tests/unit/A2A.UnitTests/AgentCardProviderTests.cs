using System.Text.Json;
using A2A.Gateway.Services;
using Xunit;

namespace A2A.UnitTests;

public class AgentCardProviderTests
{
    [Fact]
    public void GetAgentCard_ReturnsConfiguredCardWithStandardSkills()
    {
        // Arrange
        var provider = new AgentCardProvider();

        // Act
        var card = provider.GetAgentCard();

        // Assert
        Assert.NotNull(card);
        Assert.NotEmpty(card.Id);
        Assert.NotEmpty(card.Skills);
        Assert.True(provider.HasSkill("create_task"));
        Assert.True(provider.HasSkill("send_message"));
        Assert.True(provider.HasSkill("get_task"));
        Assert.True(provider.HasSkill("cancel_task"));
        Assert.False(provider.HasSkill("unsupported_skill"));
    }

    [Fact]
    public void GetPrecompiledAgentCardUtf8_ReturnsValidJsonBytes()
    {
        // Arrange
        var provider = new AgentCardProvider();

        // Act
        var utf8Bytes = provider.GetPrecompiledAgentCardUtf8();

        // Assert
        Assert.False(utf8Bytes.IsEmpty);
        using var jsonDoc = JsonDocument.Parse(utf8Bytes.ToArray());
        Assert.NotNull(jsonDoc.RootElement.GetProperty("id").GetString());
        Assert.True(jsonDoc.RootElement.TryGetProperty("skills", out var skillsProp));
        Assert.Equal(JsonValueKind.Array, skillsProp.ValueKind);
    }
}
