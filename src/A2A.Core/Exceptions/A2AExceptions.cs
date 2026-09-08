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

    public TurnConflictException(string negotiationId, long turnIndex, string message, object? errorData = null)
        : base(JsonRpcConstants.TurnConflict, message, errorData ?? new { NegotiationId = negotiationId, TurnIndex = turnIndex })
    {
        NegotiationId = negotiationId;
        TurnIndex = turnIndex;
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
