using System.Text.Json;
using A2A.Core.Abstractions;
using A2A.Core.Exceptions;
using A2A.Core.Models;
using A2A.Core.Serialization;
using A2A.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace A2A.Infrastructure.Persistence;

/// <summary>
/// Adaptador de infraestructura para el almacenamiento de estado caliente (Hot State Store) en Redis.
/// Proporciona operaciones sub-milisegundo con serialización UTF-8 por Source Generators y mutaciones
/// atómicas gobernadas por scripts Lua para prevenir colisiones de turno y carreras de concurrencia.
/// </summary>
public sealed class RedisHotStateStore : IHotStateStore
{
    private const string SaveHotStateLuaScript = @"
local key = KEYS[1]
local payload = ARGV[1]
local ttlSeconds = tonumber(ARGV[2])
local newTurnIndex = tonumber(ARGV[3])

local existing = redis.call('GET', key)
if existing then
    local currentTurn = string.match(existing, '""turnIndex""%s*:%s*(%d+)')
    if currentTurn and tonumber(currentTurn) > newTurnIndex then
        return 0
    end
end

if ttlSeconds and ttlSeconds > 0 then
    redis.call('SET', key, payload, 'EX', ttlSeconds)
else
    redis.call('SET', key, payload)
end

return 1
";

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IDatabase _database;
    private readonly RedisHotStateOptions _options;
    private readonly ILogger<RedisHotStateStore>? _logger;

    public RedisHotStateStore(
        IConnectionMultiplexer multiplexer,
        IOptions<RedisHotStateOptions> options,
        ILogger<RedisHotStateStore>? logger = null)
    {
        _multiplexer = multiplexer ?? throw new ArgumentNullException(nameof(multiplexer));
        _options = options?.Value ?? new RedisHotStateOptions();
        _database = _multiplexer.GetDatabase();
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<HotStateSession?> GetAsync(string negotiationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(negotiationId);

        var key = _options.FormatKey(negotiationId);
        var redisValue = await _database.StringGetAsync(key).ConfigureAwait(false);

        if (redisValue.IsNullOrEmpty)
        {
            return null;
        }

        try
        {
            var bytes = (byte[])redisValue!;
            return JsonSerializer.Deserialize(bytes, A2AJsonSerializerContext.Default.HotStateSession);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to deserialize HotStateSession for negotiation '{NegotiationId}'", negotiationId);
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(HotStateSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(session.NegotiationId);

        var key = _options.FormatKey(session.NegotiationId);
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(session, A2AJsonSerializerContext.Default.HotStateSession);
        var ttlSeconds = (int)Math.Max(1, _options.DefaultTtl.TotalSeconds);

        var result = (int)await _database.ScriptEvaluateAsync(
            SaveHotStateLuaScript,
            keys: [(RedisKey)key],
            values: [(RedisValue)jsonBytes, (RedisValue)ttlSeconds, (RedisValue)session.TurnIndex]
        ).ConfigureAwait(false);

        if (result == 0)
        {
            _logger?.LogWarning(
                "Rejected stale write on negotiation '{NegotiationId}' with turnIndex {TurnIndex}",
                session.NegotiationId, session.TurnIndex);

            throw new TurnConflictException(
                session.NegotiationId,
                session.TurnIndex,
                $"Concurrent mutation rejected: A newer turn state already exists in Redis for negotiation '{session.NegotiationId}'.");
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> DeleteAsync(string negotiationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(negotiationId);

        var key = _options.FormatKey(negotiationId);
        return await _database.KeyDeleteAsync(key).ConfigureAwait(false);
    }
}
