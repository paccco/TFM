namespace A2A.Infrastructure.Configuration;

/// <summary>
/// Opciones de configuración para el almacén de estado caliente (Hot State Store) en Redis.
/// </summary>
public sealed class RedisHotStateOptions
{
    public const string SectionName = "RedisHotState";

    /// <summary>
    /// Cadena de conexión a la instancia de Redis.
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Patrón de formateo para las claves de negociación en Redis.
    /// Debe contener un marcador de posición {0} para el ID de la negociación.
    /// </summary>
    public string KeyPattern { get; set; } = "a2a:negotiation:{0}:hot_state";

    /// <summary>
    /// Tiempo de expiración (TTL) por defecto para sesiones de negociación inactivas.
    /// </summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Genera la clave de Redis normalizada para una sesión dada.
    /// </summary>
    public string FormatKey(string negotiationId) => string.Format(KeyPattern, negotiationId);
}
