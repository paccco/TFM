using System.Text.Json;
using System.Text.Json.Serialization;

namespace A2A.Core.Models;

/// <summary>
/// Representa la especificación pública de la Agent Card conforme al estándar A2A.
/// </summary>
public sealed record AgentCard(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("protocolVersion")] string ProtocolVersion,
    [property: JsonPropertyName("capabilities")] IReadOnlyList<string> Capabilities,
    [property: JsonPropertyName("skills")] IReadOnlyList<AgentSkill> Skills,
    [property: JsonPropertyName("security")] AgentSecurityConfig Security,
    [property: JsonPropertyName("endpoints")] IReadOnlyList<string> Endpoints
);

/// <summary>
/// Habilidad o método soportado por el agente en la Agent Card.
/// </summary>
public sealed record AgentSkill(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] JsonElement? InputSchema = null,
    [property: JsonPropertyName("outputSchema")] JsonElement? OutputSchema = null
);

/// <summary>
/// Configuración de seguridad perimetral requerida por el agente.
/// </summary>
public sealed record AgentSecurityConfig(
    [property: JsonPropertyName("authSchemes")] IReadOnlyList<string> AuthSchemes,
    [property: JsonPropertyName("requiresMtls")] bool RequiresMtls = false,
    [property: JsonPropertyName("rateLimits")] AgentRateLimitConfig? RateLimits = null
);

/// <summary>
/// Parámetros de cuota y limitación de tasa definidos en la Agent Card.
/// </summary>
public sealed class AgentRateLimitConfig
{
    [JsonPropertyName("permitLimit")]
    public int PermitLimit { get; set; } = 100;

    [JsonPropertyName("windowSeconds")]
    public int WindowSeconds { get; set; } = 60;

    public AgentRateLimitConfig() { }

    public AgentRateLimitConfig(int permitLimit, int windowSeconds)
    {
        PermitLimit = permitLimit;
        WindowSeconds = windowSeconds;
    }
}
