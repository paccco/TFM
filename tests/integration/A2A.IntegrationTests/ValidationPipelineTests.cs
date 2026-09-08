using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace A2A.IntegrationTests;

public class ValidationPipelineTests : IClassFixture<GatewayFixture>
{
    private readonly HttpClient _client;
    private readonly GatewayFixture _fixture;

    public ValidationPipelineTests(GatewayFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    private static StringContent CreateJsonContent(string json) =>
        new(json, Encoding.UTF8, "application/json");

    // =========================================================================
    // ESCENARIO 1: Petición malformada -> Error JSON-RPC -32700 / -32600
    // =========================================================================

    [Fact]
    public async Task Scenario1_MalformedJson_ReturnsParseError_32700()
    {
        // Arrange: JSON sintácticamente corrupto
        var malformedContent = CreateJsonContent("{ \"jsonrpc\": \"2.0\", \"method\": \"send_message\", bad_json }");

        // Act
        var response = await _client.PostAsync("/rpc", malformedContent);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("id").ValueKind);
        var error = root.GetProperty("error");
        Assert.Equal(-32700, error.GetProperty("code").GetInt32());
        Assert.Contains("Parse error", error.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("{ \"method\": \"send_message\", \"params\": {}, \"id\": 1 }")] // Falta "jsonrpc": "2.0"
    [InlineData("{ \"jsonrpc\": \"1.0\", \"method\": \"send_message\", \"params\": {}, \"id\": 1 }")] // jsonrpc inválido
    [InlineData("{ \"jsonrpc\": \"2.0\", \"params\": {}, \"id\": 1 }")] // Falta "method"
    [InlineData("{ \"jsonrpc\": \"2.0\", \"method\": \"\", \"params\": {}, \"id\": 1 }")] // "method" vacío
    [InlineData("{ \"jsonrpc\": \"2.0\", \"method\": \"send_message\", \"params\": {} }")] // Falta "id"
    public async Task Scenario1_InvalidRequestStructure_ReturnsInvalidRequest_32600(string requestJson)
    {
        // Act
        var response = await _client.PostAsync("/rpc", CreateJsonContent(requestJson));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        var error = root.GetProperty("error");
        Assert.Equal(-32600, error.GetProperty("code").GetInt32());
    }

    // =========================================================================
    // ESCENARIO 2: Método inexistente en Agent Card -> Error JSON-RPC -32601
    // =========================================================================

    [Fact]
    public async Task Scenario2_NonExistentMethod_ReturnsMethodNotFound_32601()
    {
        // Arrange: método no publicado en la Agent Card
        string requestJson = """
        {
            "jsonrpc": "2.0",
            "method": "unsupported_foreign_skill",
            "params": {
                "negotiation_id": "neg-100",
                "turn_index": 1
            },
            "id": 102
        }
        """;

        // Act
        var response = await _client.PostAsync("/rpc", CreateJsonContent(requestJson));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal(102, root.GetProperty("id").GetInt32());
        var error = root.GetProperty("error");
        Assert.Equal(-32601, error.GetProperty("code").GetInt32());
        Assert.Contains("unsupported_foreign_skill", error.GetProperty("message").GetString());
    }

    // =========================================================================
    // ESCENARIO 3: Parámetros inválidos -> Error JSON-RPC -32602
    // =========================================================================

    [Fact]
    public async Task Scenario3_PrimitiveParams_ReturnsInvalidParams_32602()
    {
        // Arrange: params no es un objeto
        string requestJson = """
        {
            "jsonrpc": "2.0",
            "method": "send_message",
            "params": 12345,
            "id": "req-primitive"
        }
        """;

        // Act
        var response = await _client.PostAsync("/rpc", CreateJsonContent(requestJson));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("req-primitive", root.GetProperty("id").GetString());
        var error = root.GetProperty("error");
        Assert.Equal(-32602, error.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Scenario3_SendMessage_MissingRequiredTurnIndex_ReturnsInvalidParams_32602()
    {
        // Arrange: send_message requiere negotiation_id, turn_index, sender_id y message
        string requestJson = """
        {
            "jsonrpc": "2.0",
            "method": "send_message",
            "params": {
                "negotiation_id": "neg-200",
                "sender_id": "agent-01",
                "message": { "text": "oferta" }
            },
            "id": 103
        }
        """;

        // Act
        var response = await _client.PostAsync("/rpc", CreateJsonContent(requestJson));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(103, root.GetProperty("id").GetInt32());
        var error = root.GetProperty("error");
        Assert.Equal(-32602, error.GetProperty("code").GetInt32());
        Assert.Contains("turn_index", error.GetProperty("message").GetString());
    }

    // =========================================================================
    // ESCENARIO 4: Turno fuera de secuencia o duplicado -> Error JSON-RPC -32001
    // =========================================================================

    [Fact]
    public async Task Scenario4_DuplicateOrOutOfOrderTurn_ReturnsTurnConflict_32001()
    {
        // Arrange
        string negId = "neg-turn-conflict-" + Guid.NewGuid().ToString("N")[..8];
        string requestTurn0 = $$"""
        {
            "jsonrpc": "2.0",
            "method": "send_message",
            "params": {
                "negotiation_id": "{{negId}}",
                "turn_index": 0,
                "sender_id": "agent-01",
                "message": { "offer": 100 }
            },
            "id": "turn-0"
        }
        """;

        // 1. Emitir turno 0 con éxito
        var response1 = await _client.PostAsync("/rpc", CreateJsonContent(requestTurn0));
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        var json1 = await response1.Content.ReadAsStringAsync();
        using (var doc1 = JsonDocument.Parse(json1))
        {
            Assert.True(doc1.RootElement.TryGetProperty("result", out _));
        }

        // 2. Emitir de nuevo el turno 0 (duplicado)
        var response2 = await _client.PostAsync("/rpc", CreateJsonContent(requestTurn0));

        // Assert: El orquestador/FSM detecta conflicto y el Gateway mapea a -32001
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        var json2 = await response2.Content.ReadAsStringAsync();
        using var doc2 = JsonDocument.Parse(json2);
        var root2 = doc2.RootElement;

        Assert.Equal("turn-0", root2.GetProperty("id").GetString());
        var error = root2.GetProperty("error");
        Assert.Equal(-32001, error.GetProperty("code").GetInt32());
        Assert.Contains("Turn conflict", error.GetProperty("message").GetString());
    }

    // =========================================================================
    // ESCENARIO 5: Petición válida -> Pasa pipeline, llega a dispatcher y responde OK
    // =========================================================================

    [Fact]
    public async Task Scenario5_ValidRequest_ReachesDispatcher_ReturnsSuccessWithMatchingId()
    {
        // Arrange
        string negId = "neg-valid-" + Guid.NewGuid().ToString("N")[..8];
        string requestJson = $$"""
        {
            "jsonrpc": "2.0",
            "method": "send_message",
            "params": {
                "negotiation_id": "{{negId}}",
                "turn_index": 0,
                "sender_id": "agent-buyer",
                "message": { "proposal": "SKU-99", "quantity": 10, "unit_price": 45.5 }
            },
            "id": 999
        }
        """;

        var request = new HttpRequestMessage(HttpMethod.Post, "/rpc")
        {
            Content = CreateJsonContent(requestJson)
        };
        request.Headers.Add("X-Agent-ID", "agent-buyer");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal(999, root.GetProperty("id").GetInt32());
        Assert.True(root.TryGetProperty("result", out var resultObj));
        Assert.True(resultObj.GetProperty("success").GetBoolean());
        Assert.Equal(negId, resultObj.GetProperty("negotiationId").GetString());
        Assert.Equal(0, resultObj.GetProperty("turnIndex").GetInt64());
        Assert.Equal("acknowledged", resultObj.GetProperty("status").GetString());

        // Verificar que el stub desacoplado del despachador recibió el comando
        var lastEnvelope = _fixture.MockDispatcher.DispatchedEnvelopes
            .FirstOrDefault(e => e.NegotiationId == negId);

        Assert.NotNull(lastEnvelope);
        Assert.Equal("send_message", lastEnvelope.Method);
        Assert.Equal("agent-buyer", lastEnvelope.Emisor.AgentId);
    }

    [Fact]
    public async Task Scenario5_AliasEndpointA2A_ExecutesSuccessfully()
    {
        // Arrange: Validar que el endpoint /a2a funciona de manera idéntica a /rpc
        string requestJson = """
        {
            "jsonrpc": "2.0",
            "method": "create_task",
            "params": {
                "tenant_id": "tenant-corp-a",
                "skill": "contract_negotiation",
                "payload": { "terms": "standard" }
            },
            "id": "alias-test-42"
        }
        """;

        // Act
        var response = await _client.PostAsync("/a2a", CreateJsonContent(requestJson));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("alias-test-42", root.GetProperty("id").GetString());
        Assert.True(root.TryGetProperty("result", out var resultObj));
        Assert.True(resultObj.GetProperty("success").GetBoolean());
    }
}
