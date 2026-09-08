using System.Collections.Concurrent;
using System.Text.Json;
using A2A.Core.Abstractions;
using A2A.Core.Exceptions;
using A2A.Core.Models;

namespace A2A.Gateway.Services;

/// <summary>
/// Stub de prueba para INegotiationDispatcher que simula el orquestador y la FSM.
/// Registra los sobres despachados y permite simular conflictos de turno y validaciones de dominio.
/// </summary>
public sealed class MockNegotiationDispatcher : INegotiationDispatcher
{
    private readonly ConcurrentBag<A2ACommandEnvelope> _dispatchedEnvelopes = new();
    private readonly ConcurrentDictionary<string, long> _activeNegotiationsLastTurn = new();

    /// <summary>
    /// Permite forzar un conflicto de turno para un negotiation_id específico en escenarios de test.
    /// </summary>
    public HashSet<string> ConflictNegotiationIds { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Historial de comandos despachados para aserciones de prueba.
    /// </summary>
    public IReadOnlyCollection<A2ACommandEnvelope> DispatchedEnvelopes => _dispatchedEnvelopes;

    public ValueTask<NegotiationDispatchResult> DispatchAsync(
        A2ACommandEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        // 1. Simulación de conflicto inducido para tests
        if (ConflictNegotiationIds.Contains(envelope.NegotiationId))
        {
            throw new TurnOutOfOrderException(
                envelope.NegotiationId,
                envelope.TurnIndex,
                expectedTurnIndex: envelope.TurnIndex + 1);
        }

        // 2. Simulación de verificación de secuencia monótona en orquestador
        if (envelope.Method.Equals("send_message", StringComparison.OrdinalIgnoreCase))
        {
            var lastTurn = _activeNegotiationsLastTurn.GetOrAdd(envelope.NegotiationId, -1);
            if (envelope.TurnIndex <= lastTurn)
            {
                throw new TurnOutOfOrderException(
                    envelope.NegotiationId,
                    envelope.TurnIndex,
                    expectedTurnIndex: lastTurn + 1);
            }

            _activeNegotiationsLastTurn[envelope.NegotiationId] = envelope.TurnIndex;
        }

        _dispatchedEnvelopes.Add(envelope);

        var result = new NegotiationDispatchResult(
            Success: true,
            NegotiationId: envelope.NegotiationId,
            TurnIndex: envelope.TurnIndex,
            Status: "acknowledged",
            Output: envelope.Payload,
            Message: $"Command '{envelope.Method}' successfully dispatched to orchestration perimeter."
        );

        return ValueTask.FromResult(result);
    }

    public void Reset()
    {
        _dispatchedEnvelopes.Clear();
        _activeNegotiationsLastTurn.Clear();
        ConflictNegotiationIds.Clear();
    }
}
