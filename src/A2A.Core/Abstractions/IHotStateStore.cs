using A2A.Core.Models;

namespace A2A.Core.Abstractions;

/// <summary>
/// Puerto de persistencia de ultra-baja latencia para el estado vivo de la negociación (Track 2).
/// </summary>
public interface IHotStateStore
{
    ValueTask<HotStateSession?> GetAsync(string negotiationId, CancellationToken cancellationToken = default);
    ValueTask SaveAsync(HotStateSession session, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(string negotiationId, CancellationToken cancellationToken = default);
}
