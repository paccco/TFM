using System.Text.Json;
using A2A.Core.Abstractions;
using A2A.Core.Models;

namespace A2A.Gateway.Pipeline.Filters;

/// <summary>
/// Fase 2: Validación de esquema de parámetros ('params').
/// Comprueba que los parámetros existan, sean un objeto JSON válido y satisfagan
/// los requisitos tipados de la habilidad requerida.
/// Retorna -32602 ante parámetros inválidos.
/// Si el método no existe en el catálogo, delega la semántica a la Fase 3 (-32601).
/// </summary>
public sealed class ParameterSchemaFilter
{
    public static (bool IsValid, JsonRpcErrorResponse? Error, JsonElement ParamsElement) Validate(
        JsonElement root,
        string method,
        object? id,
        IAgentCardProvider cardProvider)
    {
        // 1. Validar que exista la propiedad "params"
        if (!root.TryGetProperty("params", out var paramsProp))
        {
            return (false, JsonRpcErrorResponse.InvalidParams(id, "Missing 'params' property in JSON-RPC request."), default);
        }

        // 2. En el estándar de negociación A2A, los parámetros estructurados deben ser un Objeto
        if (paramsProp.ValueKind != JsonValueKind.Object)
        {
            return (false, JsonRpcErrorResponse.InvalidParams(id, "'params' property must be a JSON object."), default);
        }

        // 3. Si el método es reconocido en la Agent Card, verificar la conformidad de esquema de parámetros
        if (cardProvider.HasSkill(method))
        {
            var validationError = ValidateKnownSkillParameters(method, paramsProp);
            if (validationError != null)
            {
                return (false, JsonRpcErrorResponse.InvalidParams(id, validationError), paramsProp);
            }
        }

        return (true, null, paramsProp);
    }

    private static string? ValidateKnownSkillParameters(string method, JsonElement paramsObj)
    {
        switch (method.ToLowerInvariant())
        {
            case "create_task":
                if (!paramsObj.TryGetProperty("tenant_id", out var tenantProp) ||
                    tenantProp.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(tenantProp.GetString()))
                {
                    return "Parameter 'tenant_id' is required and must be a non-empty string.";
                }

                if (!paramsObj.TryGetProperty("skill", out var skillProp) ||
                    skillProp.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(skillProp.GetString()))
                {
                    return "Parameter 'skill' is required and must be a non-empty string.";
                }
                break;

            case "send_message":
                if (!paramsObj.TryGetProperty("negotiation_id", out var negIdProp) ||
                    negIdProp.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(negIdProp.GetString()))
                {
                    return "Parameter 'negotiation_id' is required and must be a non-empty string.";
                }

                if (!paramsObj.TryGetProperty("turn_index", out var turnProp) ||
                    turnProp.ValueKind != JsonValueKind.Number ||
                    !turnProp.TryGetInt64(out var turnVal) || turnVal < 0)
                {
                    return "Parameter 'turn_index' is required and must be an integer >= 0.";
                }

                if (!paramsObj.TryGetProperty("sender_id", out var senderProp) ||
                    senderProp.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(senderProp.GetString()))
                {
                    return "Parameter 'sender_id' is required and must be a non-empty string.";
                }

                if (!paramsObj.TryGetProperty("message", out var msgProp) ||
                    (msgProp.ValueKind != JsonValueKind.Object && msgProp.ValueKind != JsonValueKind.String))
                {
                    return "Parameter 'message' is required and must be a valid object or text payload.";
                }
                break;

            case "get_task":
                if (!paramsObj.TryGetProperty("negotiation_id", out var getNegProp) ||
                    getNegProp.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(getNegProp.GetString()))
                {
                    return "Parameter 'negotiation_id' is required and must be a non-empty string.";
                }
                break;

            case "cancel_task":
                if (!paramsObj.TryGetProperty("negotiation_id", out var cancelNegProp) ||
                    cancelNegProp.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(cancelNegProp.GetString()))
                {
                    return "Parameter 'negotiation_id' is required and must be a non-empty string.";
                }

                if (!paramsObj.TryGetProperty("reason", out var reasonProp) ||
                    reasonProp.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(reasonProp.GetString()))
                {
                    return "Parameter 'reason' is required and must be a non-empty string.";
                }
                break;
        }

        return null;
    }
}
