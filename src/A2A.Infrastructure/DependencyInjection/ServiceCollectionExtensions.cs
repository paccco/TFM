using A2A.Core.Abstractions;
using A2A.Infrastructure.Configuration;
using A2A.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace A2A.Infrastructure.DependencyInjection;

/// <summary>
/// Métodos de extensión para registrar la infraestructura de persistencia Redis en el contenedor de DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra los servicios de infraestructura de A2A, incluyendo la conexión a Redis y el IHotStateStore,
    /// a partir de la configuración provista.
    /// </summary>
    public static IServiceCollection AddA2AInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(RedisHotStateOptions.SectionName);
        services.Configure<RedisHotStateOptions>(section);

        // Permitir ConnectionStrings:Redis como fallback
        var redisConnStr = configuration.GetConnectionString("Redis")
            ?? section[nameof(RedisHotStateOptions.ConnectionString)]
            ?? "localhost:6379";

        services.TryAddSingleton<IConnectionMultiplexer>(_ =>
        {
            var configOptions = ConfigurationOptions.Parse(redisConnStr);
            configOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(configOptions);
        });

        services.TryAddSingleton<IHotStateStore, RedisHotStateStore>();

        return services;
    }

    /// <summary>
    /// Registra los servicios de infraestructura de A2A con configuración fluida programática.
    /// </summary>
    public static IServiceCollection AddA2AInfrastructure(
        this IServiceCollection services,
        Action<RedisHotStateOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        services.TryAddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RedisHotStateOptions>>().Value;
            var configOptions = ConfigurationOptions.Parse(options.ConnectionString);
            configOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(configOptions);
        });

        services.TryAddSingleton<IHotStateStore, RedisHotStateStore>();

        return services;
    }
}
