using System.Text.Json;
using A2A.Core.Abstractions;
using A2A.Core.Models;

namespace A2A.Gateway.Pipeline.Filters;

/// <summary>
/// Fase 4: Validación estructural de turno y ciclo de vida en el Gateway (Gateway Passthrough).
/// Valida presencia de negotiation_id y turn_index y correlaciona sender_id con AgentIdentityContext.
/// La evaluación de la FSM de negocio y el estado en Redis reside en el orquestador.
/// </summary>
public sealed class TurnGuardFilter
{
    public static async ValueTask<(bool IsValid, JsonRpcErrorResponse? Error, string NegotiationId, long TurnIndex)> ValidateAsync(
        string method,
        JsonElement paramsObj,
        object? id,
        AgentIdentityContext identityContext,
        ITurnGuardService turnGuardService,
        CancellationToken cancellationToken = default)
    {
        // Métodos que no gestionan turnos transaccionales (ej. create_task)
        if (method.Equals("create_task", StringComparison.OrdinalIgnoreCase))
        {
            string generatedNegId = paramsObj.TryGetProperty("negotiation_id", out var nProp) && nProp.ValueKind == JsonValueKind.String
                ? nProp.GetString()!
                : Guid.NewGuid().ToString("N");
            return (true, null, generatedNegId, 0);
        }

        // Para métodos con turnos explícitos (ej. send_message, get_task, cancel_task)
        string? negotiationId = paramsObj.TryGetProperty("negotiation_id", out var negProp) && negProp.ValueKind == JsonValueKind.String
            ? negProp.GetString()
            : null;

        string? senderId = paramsObj.TryGetProperty("sender_id", out var sendProp) && sendProp.ValueKind == JsonValueKind.String
            ? sendProp.GetString()
            : null;

        long? turnIndex = null;
        if (paramsObj.TryGetProperty("turn_index", out var tProp) && tProp.ValueKind == JsonValueKind.Number && tProp.TryGetInt64(out var val))
        {
            turnIndex = val;
        }
        else if (method.Equals("get_task", StringComparison.OrdinalIgnoreCase) || method.Equals("cancel_task", StringComparison.OrdinalIgnoreCase))
        {
            // Métodos de consulta o cancelación pueden no especificar turn_index estricto
            turnIndex = 0;
        }

        var validationResult = await turnGuardService.ValidateStructuralTurnAsync(
            negotiationId,
            senderId,
            turnIndex,
            identityContext,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            return (false, JsonRpcErrorResponse.InvalidParams(id, validationResult.ErrorMessage), string.Empty, 0);
        }

        return (true, null, negotiationId!, turnIndex ?? 0);
    }
}
