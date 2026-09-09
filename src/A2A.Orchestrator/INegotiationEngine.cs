using System.Text.Json;
using A2A.Core.Models;

namespace A2A.Orchestrator;

/// <summary>
/// Comando de turno para invocar la evaluación determinista de la FSM.
/// </summary>
public sealed record NegotiationTurnCommand(
    string NegotiationId,
    string SenderId,
    string NextParticipantId,
    string Action,
    JsonElement? FactsDelta,
    long TurnIndex
);

/// <summary>
/// Resultado emitido por el motor FSM tras procesar un turno determinista.
/// </summary>
public sealed record NegotiationTurnResult(
    bool Success,
    string NegotiationId,
    NegotiationState PreviousState,
    NegotiationState NewState,
    string ActiveTurn,
    string LastSenderId,
    long TurnIndex,
    JsonElement? ConsolidatedFacts,
    string? Message = null
);

/// <summary>
/// Contrato del motor procedural y stateless de gobernanza FSM.
/// </summary>
public interface INegotiationEngine
{
    ValueTask<NegotiationTurnResult> ProcessTurnAsync(
        NegotiationTurnCommand command,
        CancellationToken cancellationToken = default);
}
