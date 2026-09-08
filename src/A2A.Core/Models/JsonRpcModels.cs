using System.Text.Json;
using System.Text.Json.Serialization;

namespace A2A.Core.Models;

/// <summary>
/// Representa una petición estándar JSON-RPC 2.0 fuertemente tipada e inmutable.
/// </summary>
public sealed record JsonRpcRequest<TParams>(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] TParams Params,
    [property: JsonPropertyName("id"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] object? Id
);

/// <summary>
/// Representa una respuesta estándar JSON-RPC 2.0 de éxito fuertemente tipada e inmutable.
/// </summary>
public sealed record JsonRpcResponse<TResult>(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("result")] TResult Result,
    [property: JsonPropertyName("id"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] object? Id
)
{
    public static JsonRpcResponse<TResult> Success(TResult result, object? id) =>
        new(JsonRpcConstants.JsonRpcVersion, result, id);
}

/// <summary>
/// DTO de error formal según la especificación JSON-RPC 2.0.
/// </summary>
public sealed record JsonRpcError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Data = null
);

/// <summary>
/// Respuesta formal de error JSON-RPC 2.0.
/// </summary>
public sealed record JsonRpcErrorResponse(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("error")] JsonRpcError Error,
    [property: JsonPropertyName("id"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] object? Id
)
{
    public static JsonRpcErrorResponse Create(int code, string message, object? id, object? data = null) =>
        new(JsonRpcConstants.JsonRpcVersion, new JsonRpcError(code, message, data), id);

    public static JsonRpcErrorResponse ParseError(string? detail = null) =>
        Create(JsonRpcConstants.ParseError, JsonRpcConstants.ParseErrorMessage, null, detail);

    public static JsonRpcErrorResponse InvalidRequest(object? id = null, string? detail = null) =>
        Create(JsonRpcConstants.InvalidRequest, detail ?? JsonRpcConstants.InvalidRequestMessage, id);

    public static JsonRpcErrorResponse MethodNotFound(object? id, string? method = null) =>
        Create(JsonRpcConstants.MethodNotFound, method is not null ? $"Method '{method}' not found." : JsonRpcConstants.MethodNotFoundMessage, id);

    public static JsonRpcErrorResponse InvalidParams(object? id, string? detail = null) =>
        Create(JsonRpcConstants.InvalidParams, detail ?? JsonRpcConstants.InvalidParamsMessage, id);

    public static JsonRpcErrorResponse TurnConflict(object? id, string? detail = null) =>
        Create(JsonRpcConstants.TurnConflict, detail ?? JsonRpcConstants.TurnConflictMessage, id);
}
