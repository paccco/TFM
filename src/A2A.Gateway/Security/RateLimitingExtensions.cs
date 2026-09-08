using System.Threading.RateLimiting;
using A2A.Core.Models;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace A2A.Gateway.Security;

public static class RateLimitingExtensions
{
    public const string PolicyName = "A2APartitionedPolicy";

    public static IServiceCollection AddA2ARateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AgentRateLimitConfig>()
            .Bind(configuration.GetSection("RateLimiting"))
            .PostConfigure(options =>
            {
                if (options.PermitLimit <= 0) options = new AgentRateLimitConfig(100, 60);
            });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(PolicyName, httpContext =>
            {
                var rateLimitOptions = httpContext.RequestServices.GetRequiredService<IOptions<AgentRateLimitConfig>>().Value;
                var identityContext = httpContext.RequestServices.GetService<AgentIdentityContext>();
                string partitionKey = identityContext?.Current?.AgentId
                    ?? httpContext.Request.Headers[AgentIdentityMiddleware.AgentIdHeaderName].FirstOrDefault()
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                        SegmentsPerWindow = 4,
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }
}
