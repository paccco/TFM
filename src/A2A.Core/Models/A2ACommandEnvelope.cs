using System.Text.Json;

namespace A2A.Core.Models;

/// <summary>
/// Sobre de transporte interno que desacopla la recepción perimetral JSON-RPC del dominio del orquestador.
/// </summary>
public sealed record A2ACommandEnvelope(
    string NegotiationId,
    string Method,
    JsonElement Payload,
    AgentIdentity Emisor,
    long TurnIndex
);
