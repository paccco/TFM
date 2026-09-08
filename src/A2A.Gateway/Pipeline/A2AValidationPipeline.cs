using System.Text.Json;
using A2A.Core.Abstractions;
using A2A.Core.Exceptions;
using A2A.Core.Models;
using A2A.Gateway.Pipeline.Filters;

namespace A2A.Gateway.Pipeline;

/// <summary>
/// Orquestador del pipeline de validación en cascada en 4 fases para peticiones JSON-RPC 2.0.
/// Aplica el principio de fallo rápido (fail-fast) y mapea las excepciones de dominio del orquestador.
/// </summary>
public sealed class A2AValidationPipeline
{
    private readonly IAgentCardProvider _cardProvider;
    private readonly ITurnGuardService _turnGuardService;
    private readonly INegotiationDispatcher _dispatcher;

    public A2AValidationPipeline(
        IAgentCardProvider cardProvider,
        ITurnGuardService turnGuardService,
        INegotiationDispatcher dispatcher)
    {
        _cardProvider = cardProvider;
        _turnGuardService = turnGuardService;
        _dispatcher = dispatcher;
    }

    public async ValueTask<object> ExecuteAsync(
        byte[] rawBodyBytes,
        AgentIdentityContext identityContext,
        CancellationToken cancellationToken = default)
    {
        // -------------------------------------------------------------
        // FASE 1: Validación de Transporte y Protocolo JSON-RPC 2.0
        // (jsonrpc: "2.0", method: string, id: present)
        // Errores: -32700 (Parse error) o -32600 (Invalid Request)
        // -------------------------------------------------------------
        var phase1 = JsonRpcTransportFilter.Validate(rawBodyBytes);
        if (!phase1.IsValid)
        {
            return phase1.Error!;
        }

        var root = phase1.ParsedRoot;
        var id = phase1.Id;
        var method = phase1.Method!;

        // -------------------------------------------------------------
        // FASE 2: Validación de Conformidad de Esquema de Parámetros
        // Errores: -32602 (Invalid params)
        // Si el método no existe, delega la semántica a la Fase 3
        // -------------------------------------------------------------
        var phase2 = ParameterSchemaFilter.Validate(root, method, id, _cardProvider);
        if (!phase2.IsValid)
        {
            return phase2.Error!;
        }

        var paramsObj = phase2.ParamsElement;

        // -------------------------------------------------------------
        // FASE 3: Validación Semántica contra la Agent Card
        // Verifica que la habilidad solicitada exista en el catálogo
        // Errores: -32601 (Method not found)
        // -------------------------------------------------------------
        var phase3 = AgentCardSemanticFilter.Validate(method, id, _cardProvider);
        if (!phase3.IsValid)
        {
            return phase3.Error!;
        }

        // -------------------------------------------------------------
        // FASE 4: Coherencia de Ciclo de Vida y Turno (Gateway Passthrough)
        // Validación perimetral de negotiation_id, turn_index y correlación
        // de identidad con AgentIdentityContext
        // -------------------------------------------------------------
        var phase4 = await TurnGuardFilter.ValidateAsync(
            method,
            paramsObj,
            id,
            identityContext,
            _turnGuardService,
            cancellationToken);

        if (!phase4.IsValid)
        {
            return phase4.Error!;
        }

        // -------------------------------------------------------------
        // DESPACHO DESACOPLADO Y MAPEO DE EXCEPCIONES DE DOMINIO
        // Invoca INegotiationDispatcher y captura TurnConflictException (-32001)
        // -------------------------------------------------------------
        var emisor = identityContext.Current ?? AgentIdentity.Anonymous;
        var envelope = new A2ACommandEnvelope(
            NegotiationId: phase4.NegotiationId,
            Method: method,
            Payload: paramsObj,
            Emisor: emisor,
            TurnIndex: phase4.TurnIndex
        );

        try
        {
            var dispatchResult = await _dispatcher.DispatchAsync(envelope, cancellationToken);
            return JsonRpcResponse<NegotiationDispatchResult>.Success(dispatchResult, id);
        }
        catch (TurnConflictException ex)
        {
            // Mapeo formal de excepción de dominio del orquestador a JSON-RPC -32001
            return JsonRpcErrorResponse.TurnConflict(id, ex.Message);
        }
        catch (A2AGatewayException ex)
        {
            return JsonRpcErrorResponse.Create(ex.ErrorCode, ex.Message, id, ex.ErrorData);
        }
        catch (Exception ex)
        {
            return JsonRpcErrorResponse.Create(
                JsonRpcConstants.InternalError,
                "Internal error processing negotiation dispatch.",
                id,
                ex.Message);
        }
    }
}
