using A2A.Core.Abstractions;
using A2A.Core.Models;

namespace A2A.Gateway.Services;

/// <summary>
/// Implementación perimetral de ITurnGuardService para el Gateway (Gateway Passthrough).
/// Valida estructuralmente la presencia y tipo de negotiation_id y turn_index, y correlaciona
/// el sender_id declarado en el mensaje con la identidad autenticada en AgentIdentityContext.
/// La evaluación de la FSM de negocio reside exclusivamente en el orquestador.
/// </summary>
public sealed class GatewayTurnGuardService : ITurnGuardService
{
    public ValueTask<TurnValidationResult> ValidateStructuralTurnAsync(
        string? negotiationId,
        string? senderId,
        long? turnIndex,
        AgentIdentityContext identityContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(negotiationId))
        {
            return ValueTask.FromResult(TurnValidationResult.Failure("Missing or empty 'negotiation_id'."));
        }

        if (turnIndex is null or < 0)
        {
            return ValueTask.FromResult(TurnValidationResult.Failure("Invalid or negative 'turn_index'."));
        }

        // Correlación de identidad: si el emisor se autenticó como agente, verificar coincidencia con sender_id
        if (identityContext.IsAuthenticated && !string.IsNullOrWhiteSpace(senderId))
        {
            var authenticatedAgentId = identityContext.Current!.AgentId;
            if (!string.Equals(senderId, authenticatedAgentId, StringComparison.OrdinalIgnoreCase))
            {
                return ValueTask.FromResult(TurnValidationResult.Failure(
                    $"Identity mismatch: sender_id '{senderId}' does not match authenticated identity '{authenticatedAgentId}'.",
                    turnIndex.Value));
            }
        }

        return ValueTask.FromResult(TurnValidationResult.Success(turnIndex.Value));
    }
}
