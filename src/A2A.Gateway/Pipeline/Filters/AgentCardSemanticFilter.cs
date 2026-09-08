using A2A.Core.Abstractions;
using A2A.Core.Models;

namespace A2A.Gateway.Pipeline.Filters;

/// <summary>
/// Fase 3: Validación semántica contra la Agent Card.
/// Verifica que el método solicitado exista formalmente en el catálogo de habilidades (Skills)
/// publicadas por la Agent Card activa. Retorna -32601 ante métodos desconocidos.
/// </summary>
public sealed class AgentCardSemanticFilter
{
    public static (bool IsValid, JsonRpcErrorResponse? Error, AgentSkill? Skill) Validate(
        string method,
        object? id,
        IAgentCardProvider cardProvider)
    {
        if (!cardProvider.HasSkill(method))
        {
            return (false, JsonRpcErrorResponse.MethodNotFound(id, method), null);
        }

        var skill = cardProvider.GetSkill(method);
        return (true, null, skill);
    }
}
