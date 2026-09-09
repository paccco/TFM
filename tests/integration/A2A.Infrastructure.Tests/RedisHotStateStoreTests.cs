using System.Text.Json;
using A2A.Core.Abstractions;
using A2A.Core.Exceptions;
using A2A.Core.Models;
using A2A.Infrastructure.Configuration;
using A2A.Infrastructure.DependencyInjection;
using A2A.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace A2A.Infrastructure.Tests;

public sealed class RedisTestFixture : IAsyncLifetime
{
    public RedisContainer Container { get; } = new RedisBuilder()
        .WithImage("redis:7.4-alpine")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(".*Ready to accept connections.*"))
        .Build();

    public IConnectionMultiplexer Multiplexer { get; private set; } = null!;
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
        ConnectionString = Container.GetConnectionString();
        Multiplexer = await ConnectionMultiplexer.ConnectAsync(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (Multiplexer != null)
        {
            await Multiplexer.DisposeAsync();
        }
        await Container.DisposeAsync();
    }
}

public sealed class RedisHotStateStoreTests : IClassFixture<RedisTestFixture>
{
    private readonly RedisTestFixture _fixture;
    private readonly RedisHotStateStore _store;
    private readonly RedisHotStateOptions _options;

    public RedisHotStateStoreTests(RedisTestFixture fixture)
    {
        _fixture = fixture;
        _options = new RedisHotStateOptions
        {
            ConnectionString = _fixture.ConnectionString,
            KeyPattern = "a2a:test:negotiation:{0}:hot_state",
            DefaultTtl = TimeSpan.FromMinutes(10)
        };
        _store = new RedisHotStateStore(_fixture.Multiplexer, Options.Create(_options));
    }

    [Fact]
    public async Task SaveAsync_And_GetAsync_ReturnsExactSession_WithConsolidatedFacts()
    {
        // Arrange
        var negotiationId = "neg-" + Guid.NewGuid().ToString("N");
        var factsJson = """
        {
            "proposedPrice": 1250.75,
            "currency": "EUR",
            "quantity": 10,
            "deliveryDays": 5,
            "slaGuaranteed": true,
            "clauses": ["warranty_2yr", "free_returns"]
        }
        """;
        var factsElement = JsonDocument.Parse(factsJson).RootElement;

        var session = new HotStateSession(
            NegotiationId: negotiationId,
            CurrentState: NegotiationState.Validation,
            ActiveTurn: "agent-buyer-01",
            LastSenderId: "agent-seller-01",
            TurnIndex: 3,
            ConsolidatedFacts: factsElement,
            UpdatedAt: DateTimeOffset.UtcNow
        );

        // Act
        await _store.SaveAsync(session);
        var retrieved = await _store.GetAsync(negotiationId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(session.NegotiationId, retrieved.NegotiationId);
        Assert.Equal(session.CurrentState, retrieved.CurrentState);
        Assert.Equal(session.ActiveTurn, retrieved.ActiveTurn);
        Assert.Equal(session.LastSenderId, retrieved.LastSenderId);
        Assert.Equal(session.TurnIndex, retrieved.TurnIndex);
        Assert.NotNull(retrieved.ConsolidatedFacts);

        var retrievedFacts = retrieved.ConsolidatedFacts.Value;
        Assert.Equal(1250.75, retrievedFacts.GetProperty("proposedPrice").GetDouble());
        Assert.Equal("EUR", retrievedFacts.GetProperty("currency").GetString());
        Assert.Equal(10, retrievedFacts.GetProperty("quantity").GetInt32());
        Assert.Equal(5, retrievedFacts.GetProperty("deliveryDays").GetInt32());
        Assert.True(retrievedFacts.GetProperty("slaGuaranteed").GetBoolean());
        Assert.Equal(2, retrievedFacts.GetProperty("clauses").GetArrayLength());
    }

    [Fact]
    public async Task GetAsync_WhenKeyDoesNotExist_ReturnsNull()
    {
        // Arrange
        var nonExistentId = "neg-missing-" + Guid.NewGuid().ToString("N");

        // Act
        var result = await _store.GetAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_RejectsStaleTurnWrite_WithTurnConflictException()
    {
        // Arrange
        var negotiationId = "neg-turnconflict-" + Guid.NewGuid().ToString("N");

        var sessionTurn1 = HotStateSession.CreateInitial(negotiationId, "agent-1") with
        {
            TurnIndex = 1
        };

        var sessionTurn2 = sessionTurn1 with
        {
            TurnIndex = 2,
            CurrentState = NegotiationState.Validation
        };

        var staleSessionTurn1 = sessionTurn1 with
        {
            CurrentState = NegotiationState.Frozen
        };

        // Act & Assert
        // 1. Initial save at turn 1
        await _store.SaveAsync(sessionTurn1);

        // 2. Advance to turn 2 -> must succeed
        await _store.SaveAsync(sessionTurn2);
        var current = await _store.GetAsync(negotiationId);
        Assert.NotNull(current);
        Assert.Equal(2, current.TurnIndex);
        Assert.Equal(NegotiationState.Validation, current.CurrentState);

        // 3. Attempting to write obsolete turn 1 must be rejected by Lua script with TurnConflictException
        var ex = await Assert.ThrowsAsync<TurnConflictException>(async () =>
        {
            await _store.SaveAsync(staleSessionTurn1);
        });

        Assert.Equal(negotiationId, ex.NegotiationId);
        Assert.Equal(1, ex.TurnIndex);

        // 4. Verify state in Redis remained at turn 2 untouched
        var stateAfterConflict = await _store.GetAsync(negotiationId);
        Assert.NotNull(stateAfterConflict);
        Assert.Equal(2, stateAfterConflict.TurnIndex);
        Assert.Equal(NegotiationState.Validation, stateAfterConflict.CurrentState);
    }

    [Fact]
    public async Task SaveAsync_ConcurrentWrites_MaintainsConsistency()
    {
        // Arrange
        var negotiationId = "neg-concurrent-" + Guid.NewGuid().ToString("N");
        var initial = HotStateSession.CreateInitial(negotiationId, "agent-1");
        await _store.SaveAsync(initial);

        var tasks = new List<Task>();
        var conflictCount = 0;

        // Act: Launch 20 concurrent updates with different turn indexes (1..20)
        for (int i = 1; i <= 20; i++)
        {
            int turn = i;
            tasks.Add(Task.Run(async () =>
            {
                var update = initial with
                {
                    TurnIndex = turn,
                    CurrentState = NegotiationState.Validation,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                try
                {
                    await _store.SaveAsync(update);
                }
                catch (TurnConflictException)
                {
                    Interlocked.Increment(ref conflictCount);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert: Redis state must be intact and have a valid TurnIndex (between 1 and 20)
        var finalState = await _store.GetAsync(negotiationId);
        Assert.NotNull(finalState);
        Assert.True(finalState.TurnIndex >= 1 && finalState.TurnIndex <= 20);
    }

    [Fact]
    public async Task SaveAsync_HonorsExpirationTtl()
    {
        // Arrange
        var shortTtlOptions = new RedisHotStateOptions
        {
            ConnectionString = _fixture.ConnectionString,
            KeyPattern = "a2a:ttl:test:{0}",
            DefaultTtl = TimeSpan.FromSeconds(1) // 1 second TTL
        };
        var shortTtlStore = new RedisHotStateStore(_fixture.Multiplexer, Options.Create(shortTtlOptions));

        var negotiationId = "neg-ttl-" + Guid.NewGuid().ToString("N");
        var session = HotStateSession.CreateInitial(negotiationId, "agent-1");

        // Act
        await shortTtlStore.SaveAsync(session);

        // Verify immediately present
        var immediate = await shortTtlStore.GetAsync(negotiationId);
        Assert.NotNull(immediate);

        // Wait for TTL expiration (1.5s)
        await Task.Delay(TimeSpan.FromMilliseconds(1500));

        var expired = await shortTtlStore.GetAsync(negotiationId);

        // Assert
        Assert.Null(expired);
    }

    [Fact]
    public async Task DeleteAsync_RemovesExistingSession()
    {
        // Arrange
        var negotiationId = "neg-del-" + Guid.NewGuid().ToString("N");
        var session = HotStateSession.CreateInitial(negotiationId, "agent-1");
        await _store.SaveAsync(session);

        // Act
        var deleted = await _store.DeleteAsync(negotiationId);
        var retrievedAfterDelete = await _store.GetAsync(negotiationId);
        var deletedAgain = await _store.DeleteAsync(negotiationId);

        // Assert
        Assert.True(deleted);
        Assert.Null(retrievedAfterDelete);
        Assert.False(deletedAgain);
    }

    [Fact]
    public void AddA2AInfrastructure_RegistersRequiredServices()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Redis"] = _fixture.ConnectionString,
            ["RedisHotState:KeyPattern"] = "a2a:di:test:{0}",
            ["RedisHotState:DefaultTtl"] = "00:05:00"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddA2AInfrastructure(config);
        using var provider = services.BuildServiceProvider();

        var multiplexer = provider.GetService<IConnectionMultiplexer>();
        var hotStateStore = provider.GetService<IHotStateStore>();

        // Assert
        Assert.NotNull(multiplexer);
        Assert.NotNull(hotStateStore);
        Assert.IsType<RedisHotStateStore>(hotStateStore);
    }
}
