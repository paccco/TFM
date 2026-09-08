using System.Text.Json;
using A2A.Core.Abstractions;
using A2A.Core.Models;
using A2A.Core.Serialization;
using Xunit;

namespace A2A.UnitTests;

public class JsonRpcSerializationTests
{
    [Fact]
    public void Serialize_JsonRpcResponse_MatchesStandardFormat()
    {
        // Arrange
        var result = new NegotiationDispatchResult(
            Success: true,
            NegotiationId: "neg-123",
            TurnIndex: 3,
            Status: "acknowledged",
            Message: "OK");
        var response = JsonRpcResponse<NegotiationDispatchResult>.Success(result, 1);

        // Act
        string json = JsonSerializer.Serialize(response, A2AJsonSerializerContext.Default.JsonRpcResponseNegotiationDispatchResult);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal(1, root.GetProperty("id").GetInt32());
        var resObj = root.GetProperty("result");
        Assert.True(resObj.GetProperty("success").GetBoolean());
        Assert.Equal("neg-123", resObj.GetProperty("negotiationId").GetString());
        Assert.Equal(3, resObj.GetProperty("turnIndex").GetInt64());
    }

    [Fact]
    public void Serialize_JsonRpcErrorResponse_MatchesStandardFormat()
    {
        // Arrange
        var errorResp = JsonRpcErrorResponse.TurnConflict(42, "Conflict detail");

        // Act
        string json = JsonSerializer.Serialize(errorResp, A2AJsonSerializerContext.Default.JsonRpcErrorResponse);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal(42, root.GetProperty("id").GetInt32());
        var errObj = root.GetProperty("error");
        Assert.Equal(-32001, errObj.GetProperty("code").GetInt32());
        Assert.Contains("Conflict detail", errObj.GetProperty("message").GetString());
    }
}
