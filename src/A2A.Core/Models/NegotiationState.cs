using System.Text.Json.Serialization;

namespace A2A.Core.Models;

/// <summary>
/// Estados formales admisibles en la FSM determinista del Orquestador A2A.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<NegotiationState>))]
public enum NegotiationState
{
    /// <summary>Fase activa de diálogo comercial y contraofertas (A2A: working).</summary>
    OnBoard,

    /// <summary>Error sintáctico o de formato en el payload; activa bucle de autocorrección (A2A: input_required).</summary>
    BoardFormatError,

    /// <summary>Auditoría determinista de reglas de negocio contra Deterministic Guard (A2A: evaluating).</summary>
    Validation,

    /// <summary>Discrepancia de esquema entre Fact Sheet y backend de negocio (A2A: input_required).</summary>
    ValidationFormatError,

    /// <summary>Acuerdo formalmente cerrado y firmado (A2A: completed) [Estado Terminal].</summary>
    Ended,

    /// <summary>Sesión congelada o fallida por reintentos o rechazo definitivo (A2A: failed) [Estado Terminal/Pausa].</summary>
    Frozen
}

/// <summary>
/// Métodos de extensión y mapeos canónicos para NegotiationState.
/// </summary>
public static class NegotiationStateExtensions
{
    public static string ToWireState(this NegotiationState state) => state switch
    {
        NegotiationState.OnBoard => "on_board",
        NegotiationState.BoardFormatError => "board_format_err",
        NegotiationState.Validation => "validation",
        NegotiationState.ValidationFormatError => "val_format_err",
        NegotiationState.Ended => "ended",
        NegotiationState.Frozen => "frozen",
        _ => state.ToString().ToLowerInvariant()
    };

    public static string ToA2AState(this NegotiationState state) => state switch
    {
        NegotiationState.OnBoard => "working",
        NegotiationState.BoardFormatError => "input_required",
        NegotiationState.Validation => "evaluating",
        NegotiationState.ValidationFormatError => "input_required",
        NegotiationState.Ended => "completed",
        NegotiationState.Frozen => "failed",
        _ => "working"
    };

    public static bool IsTerminal(this NegotiationState state) =>
        state is NegotiationState.Ended or NegotiationState.Frozen;
}
