using A2A.Core.Models;

namespace A2A.Core.Abstractions;

/// <summary>
/// Contrato para la provisión en memoria de la Agent Card pública y sus habilidades.
/// </summary>
public interface IAgentCardProvider
{
    AgentCard GetAgentCard();

    ReadOnlyMemory<byte> GetPrecompiledAgentCardUtf8();

    bool HasSkill(string skillId);

    AgentSkill? GetSkill(string skillId);
}
