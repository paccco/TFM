using System.Text.Json;
using A2A.Core.Models;

namespace A2A.Gateway.Pipeline.Filters;

/// <summary>
/// Fase 1: Validación de transporte y conformidad estructural JSON-RPC 2.0.
/// Valida presencia de "jsonrpc": "2.0", "method" (string no vacío) y "id".
/// Retorna -32700 ante JSON inválido o -32600 ante estructura de petición no conforme.
/// </summary>
public sealed class JsonRpcTransportFilter
{
    public static (bool IsValid, JsonRpcErrorResponse? Error, JsonElement ParsedRoot, object? Id, string? Method) Validate(
        byte[] rawBodyBytes)
    {
        if (rawBodyBytes == null || rawBodyBytes.Length == 0)
        {
            return (false, JsonRpcErrorResponse.InvalidRequest(null, "Empty request body."), default, null, null);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(rawBodyBytes);
        }
        catch (JsonException ex)
        {
            return (false, JsonRpcErrorResponse.ParseError(ex.Message), default, null, null);
        }

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return (false, JsonRpcErrorResponse.InvalidRequest(null, "JSON-RPC request must be a JSON object."), default, null, null);
        }

        // 1. Extraer ID primero (si es parseable) para correlar respuestas de error
        object? id = null;
        if (root.TryGetProperty("id", out var idProp))
        {
            id = idProp.ValueKind switch
            {
                JsonValueKind.Number when idProp.TryGetInt64(out var longVal) => longVal,
                JsonValueKind.Number => idProp.GetDouble(),
                JsonValueKind.String => idProp.GetString(),
                JsonValueKind.Null => null,
                _ => null
            };
        }
        else
        {
            // El campo "id" es mandatorio en peticiones de negociación A2A
            return (false, JsonRpcErrorResponse.InvalidRequest(null, "Missing required 'id' field."), root, null, null);
        }

        // 2. Validar "jsonrpc" == "2.0"
        if (!root.TryGetProperty("jsonrpc", out var versionProp) ||
            versionProp.ValueKind != JsonValueKind.String ||
            versionProp.GetString() != JsonRpcConstants.JsonRpcVersion)
        {
            return (false, JsonRpcErrorResponse.InvalidRequest(id, "Invalid or missing 'jsonrpc' version. Must be '2.0'."), root, id, null);
        }

        // 3. Validar "method" no nulo y no vacío
        if (!root.TryGetProperty("method", out var methodProp) ||
            methodProp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(methodProp.GetString()))
        {
            return (false, JsonRpcErrorResponse.InvalidRequest(id, "Missing or invalid 'method' field. Must be a non-empty string."), root, id, null);
        }

        string method = methodProp.GetString()!;
        return (true, null, root, id, method);
    }
}
