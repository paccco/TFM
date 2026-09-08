using A2A.Core.Abstractions;

namespace A2A.Gateway.Endpoints;

public static class AgentCardEndpoint
{
    public static IEndpointRouteBuilder MapAgentCardEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/.well-known/agent.json", (IAgentCardProvider cardProvider, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "public, max-age=300";
            var precompiled = cardProvider.GetPrecompiledAgentCardUtf8();
            return Results.Bytes(precompiled.ToArray(), "application/json; charset=utf-8");
        })
        .WithName("GetAgentCard")
        .WithDescription("Retorna la Agent Card pública del orquestador en memoria inmutable UTF-8 precompilada.")
        .Produces(StatusCodes.Status200OK, contentType: "application/json");

        return endpoints;
    }
}
