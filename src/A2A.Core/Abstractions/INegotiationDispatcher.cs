using System.Text.Json;
using System.Text.Json.Serialization;
using A2A.Core.Models;

namespace A2A.Core.Abstractions;

/// <summary>
/// Resultado emitido por el despachador de negociación tras procesar el comando validado.
/// </summary>
public sealed record NegotiationDispatchResult(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("negotiationId")] string NegotiationId,
    [property: JsonPropertyName("turnIndex")] long TurnIndex,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("output"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? Output = null,
    [property: JsonPropertyName("message"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message = null
);

/// <summary>
/// Contrato desacoplado para despachar comandos A2A validados hacia la capa de orquestación y FSM.
/// </summary>
public interface INegotiationDispatcher
{
    ValueTask<NegotiationDispatchResult> DispatchAsync(
        A2ACommandEnvelope envelope,
        CancellationToken cancellationToken = default);
}
