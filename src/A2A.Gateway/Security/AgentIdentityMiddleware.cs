using System.Security.Cryptography.X509Certificates;
using A2A.Core.Models;

namespace A2A.Gateway.Security;

/// <summary>
/// Middleware para extraer y poblar AgentIdentityContext a partir de la cabecera X-Agent-ID o mTLS.
/// </summary>
public sealed class AgentIdentityMiddleware
{
    private readonly RequestDelegate _next;
    public const string AgentIdHeaderName = "X-Agent-ID";
    public const string TenantIdHeaderName = "X-Tenant-ID";

    public AgentIdentityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AgentIdentityContext identityContext)
    {
        string? agentId = null;
        string? commonName = null;
        string? tenantId = context.Request.Headers[TenantIdHeaderName].FirstOrDefault();
        string authMethod = "none";

        // 1. Inspeccionar certificado de cliente (mTLS)
        X509Certificate2? clientCert = context.Connection.ClientCertificate;
        if (clientCert is not null)
        {
            commonName = clientCert.GetNameInfo(X509NameType.SimpleName, false);
            if (!string.IsNullOrWhiteSpace(commonName))
            {
                agentId = commonName;
                authMethod = "mtls";
            }
        }

        // 2. Si no hay mTLS, inspeccionar cabecera X-Agent-ID
        if (string.IsNullOrWhiteSpace(agentId))
        {
            var headerValue = context.Request.Headers[AgentIdHeaderName].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                agentId = headerValue.Trim();
                authMethod = "header";
            }
        }

        // 3. Establecer identidad en el contexto Scoped
        if (!string.IsNullOrWhiteSpace(agentId))
        {
            identityContext.Current = new AgentIdentity(
                AgentId: agentId,
                CommonName: commonName,
                TenantId: tenantId,
                AuthMethod: authMethod
            );
        }
        else
        {
            identityContext.Current = AgentIdentity.Anonymous;
        }

        await _next(context);
    }
}
