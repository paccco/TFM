namespace A2A.Core.Abstractions;

/// <summary>
/// Puerto de registro inmutable y append-only para auditoría forense y legal (Track 1).
/// </summary>
public interface ITranscriptStore
{
    ValueTask AppendLogAsync(
        string negotiationId,
        string senderId,
        long turnIndex,
        string rawPayload,
        CancellationToken cancellationToken = default);
}
