namespace A2A.Core.Abstractions;

/// <summary>
/// Implementación no-op / stub de ITranscriptStore para entornos desacoplados o testing.
/// </summary>
public sealed class NullTranscriptStore : ITranscriptStore
{
    public static readonly NullTranscriptStore Instance = new();

    public ValueTask AppendLogAsync(
        string negotiationId,
        string senderId,
        long turnIndex,
        string rawPayload,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
