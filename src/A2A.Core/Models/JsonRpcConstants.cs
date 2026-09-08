namespace A2A.Core.Models;

/// <summary>
/// Constantes y códigos de error estándar para JSON-RPC 2.0 y extensiones A2A.
/// </summary>
public static class JsonRpcConstants
{
    public const string JsonRpcVersion = "2.0";

    // Códigos estándar JSON-RPC 2.0
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;

    // Código de servidor reservado para conflicto / orden de turno en A2A
    public const int TurnConflict = -32001;

    // Mensajes estándar
    public const string ParseErrorMessage = "Parse error: Invalid JSON payload.";
    public const string InvalidRequestMessage = "Invalid Request: Missing required JSON-RPC 2.0 fields.";
    public const string MethodNotFoundMessage = "Method not found: Skill not offered in Agent Card.";
    public const string InvalidParamsMessage = "Invalid params: Parameters do not conform to schema.";
    public const string TurnConflictMessage = "Turn Conflict / Out of Order.";
}
