using A2A.Core.Models;
using A2A.Gateway.Services;
using Xunit;

namespace A2A.UnitTests;

public class GatewayTurnGuardServiceTests
{
    private readonly GatewayTurnGuardService _guardService = new();

    [Fact]
    public async Task ValidateStructuralTurnAsync_WhenValidParameters_ReturnsSuccess()
    {
        // Arrange
        var context = new AgentIdentityContext
        {
            Current = new AgentIdentity("agent-buyer-01")
        };

        // Act
        var result = await _guardService.ValidateStructuralTurnAsync(
            negotiationId: "neg-100",
            senderId: "agent-buyer-01",
            turnIndex: 1L,
            identityContext: context);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(1L, result.TurnIndex);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateStructuralTurnAsync_WhenNegotiationIdMissing_ReturnsFailure(string? negId)
    {
        // Arrange
        var context = new AgentIdentityContext();

        // Act
        var result = await _guardService.ValidateStructuralTurnAsync(
            negotiationId: negId,
            senderId: "agent-01",
            turnIndex: 0L,
            identityContext: context);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Missing or empty 'negotiation_id'", result.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1L)]
    [InlineData(-10L)]
    public async Task ValidateStructuralTurnAsync_WhenTurnIndexInvalid_ReturnsFailure(object? turnIndexObj)
    {
        // Arrange
        var context = new AgentIdentityContext();
        long? turnIndex = turnIndexObj switch
        {
            long l => l,
            int i => (long)i,
            _ => null
        };

        // Act
        var result = await _guardService.ValidateStructuralTurnAsync(
            negotiationId: "neg-100",
            senderId: "agent-01",
            turnIndex: turnIndex,
            identityContext: context);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Invalid or negative 'turn_index'", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateStructuralTurnAsync_WhenSenderIdDoesNotMatchAuthenticatedIdentity_ReturnsFailure()
    {
        // Arrange
        var context = new AgentIdentityContext
        {
            Current = new AgentIdentity("authenticated-agent")
        };

        // Act
        var result = await _guardService.ValidateStructuralTurnAsync(
            negotiationId: "neg-100",
            senderId: "impersonated-agent",
            turnIndex: 1L,
            identityContext: context);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Identity mismatch", result.ErrorMessage);
    }
}
