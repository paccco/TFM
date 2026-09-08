using A2A.Core.Models;

namespace A2A.Core.Abstractions;

/// <summary>
/// Resultado de la validación estructural de turno en el perímetro del Gateway.
/// </summary>
public readonly record struct TurnValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public long TurnIndex { get; init; }

    public static TurnValidationResult Success(long turnIndex) =>
        new() { IsValid = true, TurnIndex = turnIndex };

    public static TurnValidationResult Failure(string errorMessage, long turnIndex = 0) =>
        new() { IsValid = false, ErrorMessage = errorMessage, TurnIndex = turnIndex };
}

/// <summary>
/// Contrato del servicio de guardia de turno en el Gateway para comprobación estructural y correlación de emisor.
/// </summary>
public interface ITurnGuardService
{
    ValueTask<TurnValidationResult> ValidateStructuralTurnAsync(
        string? negotiationId,
        string? senderId,
        long? turnIndex,
        AgentIdentityContext identityContext,
        CancellationToken cancellationToken = default);
}
