using System.Text.Json;
using A2A.Core.Abstractions;
using A2A.Core.Exceptions;
using A2A.Core.Models;

namespace A2A.Orchestrator;

/// <summary>
/// Motor de la Máquina de Estados Finitos (FSM) procedural y 100% stateless.
/// Rehidrata el estado desde IHotStateStore en cada turno, evalúa reglas duras de alternancia
/// y transiciones, y persiste atómicamente el nuevo snapshot de sesión.
/// </summary>
public sealed class NegotiationFsmEngine : INegotiationEngine
{
    private readonly IHotStateStore _hotStateStore;
    private readonly ITranscriptStore _transcriptStore;

    public NegotiationFsmEngine(
        IHotStateStore hotStateStore,
        ITranscriptStore? transcriptStore = null)
    {
        _hotStateStore = hotStateStore ?? throw new ArgumentNullException(nameof(hotStateStore));
        _transcriptStore = transcriptStore ?? NullTranscriptStore.Instance;
    }

    /// <summary>
    /// Procesa un turno de negociación determinista validando orden, identidad y matriz legal de estados.
    /// </summary>
    public async ValueTask<NegotiationTurnResult> ProcessTurnAsync(
        NegotiationTurnCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Rehidratación obligatoria del estado desde Track 2 (Hot State)
        var session = await _hotStateStore.GetAsync(command.NegotiationId, cancellationToken);
        if (session is null)
        {
            throw new NegotiationNotFoundException(command.NegotiationId);
        }

        // 2. Validación de Inmutabilidad de Estados Terminales
        if (session.CurrentState.IsTerminal())
        {
            throw new NegotiationTerminalException(session.NegotiationId, session.CurrentState);
        }

        // 3. Validación de Alternancia Estricta de Turnos
        if (!string.IsNullOrEmpty(session.ActiveTurn) &&
            !string.Equals(session.ActiveTurn, command.SenderId, StringComparison.OrdinalIgnoreCase))
        {
            throw new TurnConflictException(
                command.NegotiationId,
                session.ActiveTurn,
                command.SenderId,
                command.TurnIndex);
        }

        // 4. Validación de Secuencia Monótona de Turnos (turnIndex > lastTurnIndex)
        if (command.TurnIndex <= session.TurnIndex)
        {
            throw new TurnOutOfOrderException(
                command.NegotiationId,
                command.TurnIndex,
                session.TurnIndex + 1);
        }

        // 5. Evaluación procedural de la Matriz de Transiciones FSM
        var nextState = EvaluateTransition(session.CurrentState, command.Action, command.NegotiationId);

        // 6. Consolidación de hechos y cálculo del próximo participante activo
        var nextActiveTurn = nextState.IsTerminal() ? string.Empty : command.NextParticipantId;
        var consolidatedFacts = command.FactsDelta ?? session.ConsolidatedFacts;

        var updatedSession = new HotStateSession(
            NegotiationId: session.NegotiationId,
            CurrentState: nextState,
            ActiveTurn: nextActiveTurn,
            LastSenderId: command.SenderId,
            TurnIndex: command.TurnIndex,
            ConsolidatedFacts: consolidatedFacts,
            UpdatedAt: DateTimeOffset.UtcNow
        );

        // 7. Persistencia atómica en Hot State Store (Track 2)
        await _hotStateStore.SaveAsync(updatedSession, cancellationToken);

        // 8. Registro append-only en Raw Transcript Store (Track 1 - Out of band)
        var rawPayload = command.FactsDelta.HasValue
            ? command.FactsDelta.Value.GetRawText()
            : $"{{\"action\":\"{command.Action}\"}}";

        await _transcriptStore.AppendLogAsync(
            command.NegotiationId,
            command.SenderId,
            command.TurnIndex,
            rawPayload,
            cancellationToken);

        return new NegotiationTurnResult(
            Success: true,
            NegotiationId: session.NegotiationId,
            PreviousState: session.CurrentState,
            NewState: nextState,
            ActiveTurn: nextActiveTurn,
            LastSenderId: command.SenderId,
            TurnIndex: command.TurnIndex,
            ConsolidatedFacts: consolidatedFacts,
            Message: $"Transitioned from '{session.CurrentState.ToWireState()}' to '{nextState.ToWireState()}' via action '{command.Action}'."
        );
    }

    /// <summary>
    /// Evalúa la legalidad de la acción entrante según el estado actual de la FSM.
    /// </summary>
    private static NegotiationState EvaluateTransition(NegotiationState currentState, string action, string negotiationId)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new InvalidTransitionException(negotiationId, currentState, "<empty_action>");
        }

        var normalizedAction = action.Trim().ToLowerInvariant();

        return currentState switch
        {
            NegotiationState.OnBoard => normalizedAction switch
            {
                "propose" or "counter_propose" or "send_message" or "offer" or "counter_offer" => NegotiationState.OnBoard,
                "validate" or "request_validation" => NegotiationState.Validation,
                "report_format_error" or "format_error" => NegotiationState.BoardFormatError,
                "accept" or "complete" => NegotiationState.Ended,
                "freeze" or "pause" => NegotiationState.Frozen,
                "reject" or "abort" or "cancel" => NegotiationState.Frozen,
                _ => throw new InvalidTransitionException(negotiationId, currentState, action)
            },

            NegotiationState.BoardFormatError => normalizedAction switch
            {
                "fix_format" or "propose" or "counter_propose" or "send_message" or "retry" => NegotiationState.OnBoard,
                "freeze" or "abort" or "cancel" or "reject" => NegotiationState.Frozen,
                _ => throw new InvalidTransitionException(negotiationId, currentState, action)
            },

            NegotiationState.Validation => normalizedAction switch
            {
                "validation_passed" or "confirm" or "accept" or "sign" => NegotiationState.Ended,
                "validation_rejected" or "renegotiate" or "propose" or "counter_propose" => NegotiationState.OnBoard,
                "report_format_error" or "format_error" => NegotiationState.ValidationFormatError,
                "freeze" or "abort" or "reject" or "cancel" => NegotiationState.Frozen,
                _ => throw new InvalidTransitionException(negotiationId, currentState, action)
            },

            NegotiationState.ValidationFormatError => normalizedAction switch
            {
                "fix_format" or "validate" or "retry" => NegotiationState.Validation,
                "renegotiate" or "propose" => NegotiationState.OnBoard,
                "freeze" or "abort" or "cancel" or "reject" => NegotiationState.Frozen,
                _ => throw new InvalidTransitionException(negotiationId, currentState, action)
            },

            NegotiationState.Ended or NegotiationState.Frozen =>
                throw new NegotiationTerminalException(negotiationId, currentState),

            _ => throw new InvalidTransitionException(negotiationId, currentState, action)
        };
    }
}
