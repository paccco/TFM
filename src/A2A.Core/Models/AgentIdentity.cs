namespace A2A.Core.Models;

/// <summary>
/// Representa la identidad autenticada de un agente o cliente que invoca el Gateway.
/// </summary>
public sealed record AgentIdentity(
    string AgentId,
    string? CommonName = null,
    string? TenantId = null,
    string AuthMethod = "header"
)
{
    public static AgentIdentity Anonymous => new("anonymous", "anonymous", null, "none");
}

/// <summary>
/// Contexto con ciclo de vida Scoped que almacena la identidad resuelta en la petición activa.
/// </summary>
public sealed class AgentIdentityContext
{
    public AgentIdentity? Current { get; set; }

    public bool IsAuthenticated => Current is not null && Current.AgentId != "anonymous";

    public string GetEffectiveAgentId() => Current?.AgentId ?? "anonymous";
}
