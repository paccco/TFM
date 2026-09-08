using System.Text;
using System.Text.Json;
using A2A.Core.Models;
using A2A.Core.Serialization;
using A2A.Gateway.Pipeline;
using A2A.Gateway.Security;

namespace A2A.Gateway.Endpoints;

public static class JsonRpcEndpoint
{
    public static IEndpointRouteBuilder MapJsonRpcEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var handler = async (
            HttpContext context,
            A2AValidationPipeline pipeline,
            AgentIdentityContext identityContext,
            CancellationToken ct) =>
        {
            // Lectura eficiente del cuerpo en bytes
            byte[] bodyBytes;
            using (var ms = new MemoryStream())
            {
                await context.Request.Body.CopyToAsync(ms, ct);
                bodyBytes = ms.ToArray();
            }

            var result = await pipeline.ExecuteAsync(bodyBytes, identityContext, ct);

            // Serialización optimizada mediante Source Generators
            string jsonResponse = JsonSerializer.Serialize(result, result.GetType(), A2AJsonSerializerContext.Default);
            return Results.Content(jsonResponse, "application/json", Encoding.UTF8, StatusCodes.Status200OK);
        };

        endpoints.MapPost("/rpc", handler)
            .RequireRateLimiting(RateLimitingExtensions.PolicyName)
            .WithName("ProcessJsonRpc")
            .WithDescription("Procesa peticiones JSON-RPC 2.0 a través del pipeline de validación en 4 fases.");

        endpoints.MapPost("/a2a", handler)
            .RequireRateLimiting(RateLimitingExtensions.PolicyName)
            .WithName("ProcessA2A")
            .WithDescription("Alias estandarizado A2A para peticiones de negociación entre agentes.");

        return endpoints;
    }
}
