using System.Collections.Concurrent;
using A2A.Core.Abstractions;
using A2A.Core.Models;

namespace A2A.UnitTests.Fakes;

/// <summary>
/// Doble de prueba en memoria (Fake) de IHotStateStore con operaciones atómicas y sin I/O.
/// </summary>
public sealed class InMemoryHotStateStore : IHotStateStore
{
    private readonly ConcurrentDictionary<string, HotStateSession> _store = new(StringComparer.OrdinalIgnoreCase);

    public ValueTask<HotStateSession?> GetAsync(string negotiationId, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(negotiationId, out var session);
        return ValueTask.FromResult(session);
    }

    public ValueTask SaveAsync(HotStateSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        _store[session.NegotiationId] = session;
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> DeleteAsync(string negotiationId, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_store.TryRemove(negotiationId, out _));
    }

    public void Seed(HotStateSession session)
    {
        _store[session.NegotiationId] = session;
    }

    public void Clear()
    {
        _store.Clear();
    }
}

/// <summary>
/// Doble de prueba que graba en memoria las entradas de log de auditoría forense (Track 1).
/// </summary>
public sealed class RecordingTranscriptStore : ITranscriptStore
{
    public List<(string NegotiationId, string SenderId, long TurnIndex, string RawPayload)> Logs { get; } = new();

    public ValueTask AppendLogAsync(
        string negotiationId,
        string senderId,
        long turnIndex,
        string rawPayload,
        CancellationToken cancellationToken = default)
    {
        Logs.Add((negotiationId, senderId, turnIndex, rawPayload));
        return ValueTask.CompletedTask;
    }
}
