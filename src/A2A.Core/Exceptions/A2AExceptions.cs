using A2A.Core.Models;

namespace A2A.Core.Exceptions;

/// <summary>
/// Excepción base para errores formalmente mapeables al estándar JSON-RPC 2.0 en el A2A Gateway.
/// </summary>
public class A2AGatewayException : Exception
{
    public int ErrorCode { get; }
    public object? ErrorData { get; }

    public A2AGatewayException(int errorCode, string message, object? errorData = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ErrorData = errorData;
    }
}

/// <summary>
/// Error emitido ante petición malformada o cabeceras/campos base inválidos (JSON-RPC -32600).
/// </summary>
public sealed class InvalidRequestException : A2AGatewayException
{
    public InvalidRequestException(string message, object? errorData = null)
        : base(JsonRpcConstants.InvalidRequest, message, errorData) { }
}

/// <summary>
/// Error emitido cuando la habilidad o método solicitado no existe en la Agent Card activa (JSON-RPC -32601).
/// </summary>
public sealed class MethodNotFoundException : A2AGatewayException
{
    public MethodNotFoundException(string method)
        : base(JsonRpcConstants.MethodNotFound, $"Method '{method}' not found in Agent Card skills catalog.", new { Method = method }) { }
}

/// <summary>
/// Error emitido cuando los parámetros de la petición no satisfacen el esquema esperado (JSON-RPC -32602).
/// </summary>
public sealed class InvalidParamsException : A2AGatewayException
{
    public InvalidParamsException(string message, object? errorData = null)
        : base(JsonRpcConstants.InvalidParams, message, errorData) { }
}

/// <summary>
/// Excepción de dominio emitida ante conflicto de turno, desincronización de secuencia o concurrencia (JSON-RPC -32001).
/// </summary>
public class TurnConflictException : A2AGatewayException
{
    public string NegotiationId { get; }
    public long TurnIndex { get; }
    public string? ExpectedSender { get; }
    public string? ActualSender { get; }

    public TurnConflictException(string negotiationId, long turnIndex, string message, object? errorData = null)
        : base(JsonRpcConstants.TurnConflict, message, errorData ?? new { NegotiationId = negotiationId, TurnIndex = turnIndex })
    {
        NegotiationId = negotiationId;
        TurnIndex = turnIndex;
    }

    public TurnConflictException(string negotiationId, string expectedSender, string actualSender, long turnIndex)
        : base(JsonRpcConstants.TurnConflict,
               $"Turn conflict in negotiation '{negotiationId}': expected sender '{expectedSender}', but received message from '{actualSender}' at turn {turnIndex}.",
               new { NegotiationId = negotiationId, ExpectedSender = expectedSender, ActualSender = actualSender, TurnIndex = turnIndex })
    {
        NegotiationId = negotiationId;
        TurnIndex = turnIndex;
        ExpectedSender = expectedSender;
        ActualSender = actualSender;
    }
}

/// <summary>
/// Conflicto específico por turno fuera de orden o desincronización en la secuencia monótona.
/// </summary>
public sealed class TurnOutOfOrderException : TurnConflictException
{
    public long ExpectedTurnIndex { get; }

    public TurnOutOfOrderException(string negotiationId, long receivedTurnIndex, long expectedTurnIndex)
        : base(negotiationId, receivedTurnIndex, 
               $"Turn conflict: Received turn index {receivedTurnIndex} does not match expected sequence ({expectedTurnIndex}).",
               new { NegotiationId = negotiationId, ReceivedTurn = receivedTurnIndex, ExpectedTurn = expectedTurnIndex })
    {
        ExpectedTurnIndex = expectedTurnIndex;
    }
}

/// <summary>
/// Excepción emitida ante intento de transición no permitida por la matriz legal de la FSM.
/// </summary>
public sealed class InvalidTransitionException : A2AGatewayException
{
    public string NegotiationId { get; }
    public NegotiationState FromState { get; }
    public string Action { get; }

    public InvalidTransitionException(string negotiationId, NegotiationState from, string action)
        : base(JsonRpcConstants.InvalidParams, 
               $"Invalid FSM transition in negotiation '{negotiationId}': action '{action}' is not allowed from state '{from.ToWireState()}'.",
               new { NegotiationId = negotiationId, FromState = from.ToWireState(), Action = action })
    {
        NegotiationId = negotiationId;
        FromState = from;
        Action = action;
    }
}

/// <summary>
/// Excepción emitida cuando no se encuentra la sesión de negociación en el almacén de estado.
/// </summary>
public sealed class NegotiationNotFoundException : A2AGatewayException
{
    public string NegotiationId { get; }

    public NegotiationNotFoundException(string negotiationId)
        : base(JsonRpcConstants.InvalidParams,
               $"Negotiation session '{negotiationId}' was not found.",
               new { NegotiationId = negotiationId })
    {
        NegotiationId = negotiationId;
    }
}

/// <summary>
/// Excepción emitida cuando se intenta ejecutar una acción sobre una sesión en estado terminal.
/// </summary>
public sealed class NegotiationTerminalException : A2AGatewayException
{
    public string NegotiationId { get; }
    public NegotiationState State { get; }

    public NegotiationTerminalException(string negotiationId, NegotiationState state)
        : base(JsonRpcConstants.InvalidParams,
               $"Negotiation session '{negotiationId}' is in terminal state '{state.ToWireState()}' and cannot accept further actions.",
               new { NegotiationId = negotiationId, State = state.ToWireState() })
    {
        NegotiationId = negotiationId;
        State = state;
    }
}
