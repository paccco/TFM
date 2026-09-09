using System.Text.Json;
using System.Text.Json.Serialization;

namespace A2A.Core.Models;

/// <summary>
/// Snapshot consolidado del estado vivo de la sesión en el Hot State Store (Track 2).
/// </summary>
public sealed record HotStateSession(
    [property: JsonPropertyName("negotiationId")] string NegotiationId,
    [property: JsonPropertyName("currentState")] NegotiationState CurrentState,
    [property: JsonPropertyName("activeTurn")] string ActiveTurn,
    [property: JsonPropertyName("lastSenderId")] string LastSenderId,
    [property: JsonPropertyName("turnIndex")] long TurnIndex,
    [property: JsonPropertyName("consolidatedFacts")] JsonElement? ConsolidatedFacts,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt
)
{
    public static HotStateSession CreateInitial(
        string negotiationId,
        string initiatorAgentId,
        JsonElement? initialFacts = null) =>
        new(
            NegotiationId: negotiationId,
            CurrentState: NegotiationState.OnBoard,
            ActiveTurn: initiatorAgentId,
            LastSenderId: string.Empty,
            TurnIndex: 0,
            ConsolidatedFacts: initialFacts,
            UpdatedAt: DateTimeOffset.UtcNow
        );
}
