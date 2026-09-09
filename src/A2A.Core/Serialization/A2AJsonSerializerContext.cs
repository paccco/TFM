using System.Text.Json;
using System.Text.Json.Serialization;
using A2A.Core.Abstractions;
using A2A.Core.Models;

namespace A2A.Core.Serialization;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(JsonRpcRequest<JsonElement>))]
[JsonSerializable(typeof(JsonRpcResponse<NegotiationDispatchResult>))]
[JsonSerializable(typeof(JsonRpcResponse<JsonElement>))]
[JsonSerializable(typeof(JsonRpcResponse<object>))]
[JsonSerializable(typeof(JsonRpcErrorResponse))]
[JsonSerializable(typeof(JsonRpcError))]
[JsonSerializable(typeof(AgentCard))]
[JsonSerializable(typeof(AgentSkill))]
[JsonSerializable(typeof(AgentSecurityConfig))]
[JsonSerializable(typeof(AgentRateLimitConfig))]
[JsonSerializable(typeof(A2ACommandEnvelope))]
[JsonSerializable(typeof(NegotiationDispatchResult))]
[JsonSerializable(typeof(AgentIdentity))]
[JsonSerializable(typeof(NegotiationState))]
[JsonSerializable(typeof(HotStateSession))]
public partial class A2AJsonSerializerContext : JsonSerializerContext
{
}
