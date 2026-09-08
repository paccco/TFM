using System.Collections.Frozen;
using System.Text;
using System.Text.Json;
using A2A.Core.Abstractions;
using A2A.Core.Models;
using A2A.Core.Serialization;

namespace A2A.Gateway.Services;

/// <summary>
/// Provee la Agent Card pública y sus habilidades, manteniendo una copia UTF-8 precompilada
/// en memoria inmutable para servir /.well-known/agent.json con cero asignaciones.
/// </summary>
public sealed class AgentCardProvider : IAgentCardProvider
{
    private readonly AgentCard _agentCard;
    private readonly ReadOnlyMemory<byte> _precompiledUtf8;
    private readonly FrozenDictionary<string, AgentSkill> _skillsDictionary;

    public AgentCardProvider(IConfiguration? configuration = null)
    {
        _agentCard = LoadInitialAgentCard(configuration);
        _skillsDictionary = _agentCard.Skills.ToFrozenDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);

        // Serializar de forma determinista usando el contexto Source Generator
        byte[] serializedBytes = JsonSerializer.SerializeToUtf8Bytes(_agentCard, A2AJsonSerializerContext.Default.AgentCard);
        _precompiledUtf8 = new ReadOnlyMemory<byte>(serializedBytes);
    }

    public AgentCard GetAgentCard() => _agentCard;

    public ReadOnlyMemory<byte> GetPrecompiledAgentCardUtf8() => _precompiledUtf8;

    public bool HasSkill(string skillId) => _skillsDictionary.ContainsKey(skillId);

    public AgentSkill? GetSkill(string skillId) =>
        _skillsDictionary.GetValueOrDefault(skillId);

    private static AgentCard LoadInitialAgentCard(IConfiguration? config)
    {
        // Esquemas JSON estándar embebidos para habilidades A2A
        var defaultSkills = new List<AgentSkill>
        {
            new(
                Id: "create_task",
                Name: "Create Negotiation Task",
                Description: "Inicia una nueva sesión o canal de negociación bilateral o multilateral.",
                InputSchema: JsonDocument.Parse("""
                {
                    "type": "object",
                    "required": ["tenant_id", "skill"],
                    "properties": {
                        "tenant_id": { "type": "string" },
                        "skill": { "type": "string" },
                        "payload": { "type": "object" }
                    }
                }
                """).RootElement
            ),
            new(
                Id: "send_message",
                Name: "Send Negotiation Message",
                Description: "Transmite un turno de negociación conteniendo el delta fáctico y propuesta.",
                InputSchema: JsonDocument.Parse("""
                {
                    "type": "object",
                    "required": ["negotiation_id", "turn_index", "sender_id", "message"],
                    "properties": {
                        "negotiation_id": { "type": "string" },
                        "turn_index": { "type": "integer", "minimum": 0 },
                        "sender_id": { "type": "string" },
                        "message": { "type": "object" }
                    }
                }
                """).RootElement
            ),
            new(
                Id: "get_task",
                Name: "Get Negotiation Task State",
                Description: "Consulta el estado consolidado de la negociación, turno actual y resumen fáctico.",
                InputSchema: JsonDocument.Parse("""
                {
                    "type": "object",
                    "required": ["negotiation_id"],
                    "properties": {
                        "negotiation_id": { "type": "string" }
                    }
                }
                """).RootElement
            ),
            new(
                Id: "cancel_task",
                Name: "Cancel Negotiation Task",
                Description: "Cancela anticipadamente una negociación en curso transicionando a estado frozen.",
                InputSchema: JsonDocument.Parse("""
                {
                    "type": "object",
                    "required": ["negotiation_id", "reason"],
                    "properties": {
                        "negotiation_id": { "type": "string" },
                        "reason": { "type": "string" }
                    }
                }
                """).RootElement
            )
        };

        return new AgentCard(
            Id: config?["AgentCard:Id"] ?? "urn:agent:tfm:negotiation-orchestrator",
            Name: config?["AgentCard:Name"] ?? "TFM A2A Negotiation Orchestrator",
            Description: config?["AgentCard:Description"] ?? "Orquestador stateless de negociación comercial entre agentes autónomos bajo estándar A2A y JSON-RPC 2.0.",
            Version: config?["AgentCard:Version"] ?? "1.0.0",
            ProtocolVersion: config?["AgentCard:ProtocolVersion"] ?? "0.3.0",
            Capabilities: new[] { "negotiation.fsm.stateless", "jsonrpc.2.0", "rate_limiting.sliding_window" },
            Skills: defaultSkills,
            Security: new AgentSecurityConfig(
                AuthSchemes: new[] { "header:X-Agent-ID", "mtls:CN" },
                RequiresMtls: false,
                RateLimits: new AgentRateLimitConfig { PermitLimit = 120, WindowSeconds = 60 }
            ),
            Endpoints: new[] { "/rpc", "/a2a", "/.well-known/agent.json" }
        );
    }
}
