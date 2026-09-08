using A2A.Core.Abstractions;
using A2A.Core.Models;
using A2A.Gateway.Endpoints;
using A2A.Gateway.Pipeline;
using A2A.Gateway.Security;
using A2A.Gateway.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Configuración de Kestrel para HTTP/1.1 y HTTP/2 de alto rendimiento
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});

// Registro de servicios del perímetro A2A
builder.Services.AddSingleton<IAgentCardProvider, AgentCardProvider>();
builder.Services.AddSingleton<ITurnGuardService, GatewayTurnGuardService>();

// Stub de despacho desacoplado hacia el futuro orquestador/FSM
builder.Services.AddSingleton<MockNegotiationDispatcher>();
builder.Services.AddSingleton<INegotiationDispatcher>(sp => sp.GetRequiredService<MockNegotiationDispatcher>());

// Contexto de identidad scoped y pipeline de validación
builder.Services.AddScoped<AgentIdentityContext>();
builder.Services.AddScoped<A2AValidationPipeline>();

// Rate limiting particionado por Agent ID / mTLS
builder.Services.AddA2ARateLimiting(builder.Configuration);

var app = builder.Build();

// Middlewares perimetrales
app.UseRateLimiter();
app.UseMiddleware<AgentIdentityMiddleware>();

// Endpoints de la API
app.MapAgentCardEndpoint();
app.MapJsonRpcEndpoints();

app.Run();

// Requerido para pruebas de integración con WebApplicationFactory
public partial class Program { }
