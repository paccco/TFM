using System.Text.Json;
using A2A.Core.Exceptions;
using A2A.Core.Models;
using A2A.Orchestrator;
using A2A.UnitTests.Fakes;

namespace A2A.UnitTests.Orchestrator;

public sealed class NegotiationFsmEngineTests
{
    private readonly InMemoryHotStateStore _hotStateStore = new();
    private readonly RecordingTranscriptStore _transcriptStore = new();
    private readonly NegotiationFsmEngine _engine;

    public NegotiationFsmEngineTests()
    {
        _engine = new NegotiationFsmEngine(_hotStateStore, _transcriptStore);
    }

    [Fact]
    public async Task ProcessTurnAsync_HappyPath_CompletesNegotiationLifecycle()
    {
        // Arrange
        const string negotiationId = "neg-lifecycle-1";
        const string buyer = "agent-buyer-001";
        const string seller = "agent-seller-002";

        var initialSession = HotStateSession.CreateInitial(negotiationId, buyer);
        _hotStateStore.Seed(initialSession);

        var factsDoc = JsonDocument.Parse("{\"sku\":\"ITEM-123\",\"price\":100.0,\"quantity\":50}");

        // Act & Assert - Turn 1: Buyer proposes
        var turn1 = await _engine.ProcessTurnAsync(new NegotiationTurnCommand(
            NegotiationId: negotiationId,
            SenderId: buyer,
            NextParticipantId: seller,
            Action: "propose",
            FactsDelta: factsDoc.RootElement,
            TurnIndex: 1
        ));

        Assert.True(turn1.Success);
        Assert.Equal(NegotiationState.OnBoard, turn1.NewState);
        Assert.Equal(seller, turn1.ActiveTurn);
        Assert.Equal(1, turn1.TurnIndex);

        // Turn 2: Seller counter-proposes
        var counterDoc = JsonDocument.Parse("{\"sku\":\"ITEM-123\",\"price\":95.0,\"quantity\":50}");
        var turn2 = await _engine.ProcessTurnAsync(new NegotiationTurnCommand(
            NegotiationId: negotiationId,
            SenderId: seller,
            NextParticipantId: buyer,
            Action: "counter_propose",
            FactsDelta: counterDoc.RootElement,
            TurnIndex: 2
        ));

        Assert.True(turn2.Success);
        Assert.Equal(NegotiationState.OnBoard, turn2.NewState);
        Assert.Equal(buyer, turn2.ActiveTurn);
        Assert.Equal(2, turn2.TurnIndex);

        // Turn 3: Buyer requests validation against Deterministic Guard
        var turn3 = await _engine.ProcessTurnAsync(new NegotiationTurnCommand(
            NegotiationId: negotiationId,
            SenderId: buyer,
            NextParticipantId: seller,
            Action: "validate",
            FactsDelta: null,
            TurnIndex: 3
        ));

        Assert.True(turn3.Success);
        Assert.Equal(NegotiationState.Validation, turn3.NewState);
        Assert.Equal(seller, turn3.ActiveTurn);
        Assert.Equal(3, turn3.TurnIndex);

        // Turn 4: Seller confirms and concludes
        var turn4 = await _engine.ProcessTurnAsync(new NegotiationTurnCommand(
            NegotiationId: negotiationId,
            SenderId: seller,
            NextParticipantId: buyer,
            Action: "confirm",
            FactsDelta: null,
            TurnIndex: 4
        ));

        Assert.True(turn4.Success);
        Assert.Equal(NegotiationState.Ended, turn4.NewState);
        Assert.Equal(string.Empty, turn4.ActiveTurn);
        Assert.Equal(4, turn4.TurnIndex);

        // Verify Hot State Store
        var finalSession = await _hotStateStore.GetAsync(negotiationId);
        Assert.NotNull(finalSession);
        Assert.Equal(NegotiationState.Ended, finalSession.CurrentState);
        Assert.Equal(4, finalSession.TurnIndex);

        // Verify Transcript Store (Track 1)
        Assert.Equal(4, _transcriptStore.Logs.Count);
        Assert.Equal(negotiationId, _transcriptStore.Logs[0].NegotiationId);
        Assert.Equal(buyer, _transcriptStore.Logs[0].SenderId);
        Assert.Equal(1, _transcriptStore.Logs[0].TurnIndex);
    }

    [Fact]
    public async Task ProcessTurnAsync_ThrowsTurnConflictException_WhenSameAgentTriesConsecutiveTurn()
    {
        // Arrange
        const string negotiationId = "neg-turn-conflict-1";
        const string buyer = "agent-buyer";
        const string seller = "agent-seller";

        _hotStateStore.Seed(HotStateSession.CreateInitial(negotiationId, buyer));

        // Turn 1 by buyer (sets active turn to seller)
        await _engine.ProcessTurnAsync(new NegotiationTurnCommand(
            NegotiationId: negotiationId,
            SenderId: buyer,
            NextParticipantId: seller,
            Action: "propose",
            FactsDelta: null,
            TurnIndex: 1
        ));

        // Act & Assert - Turn 2: Buyer tries to send again instead of seller
        var ex = await Assert.ThrowsAsync<TurnConflictException>(() =>
            _engine.ProcessTurnAsync(new NegotiationTurnCommand(
                NegotiationId: negotiationId,
                SenderId: buyer,
                NextParticipantId: seller,
                Action: "counter_propose",
                FactsDelta: null,
                TurnIndex: 2
            )).AsTask());

        Assert.Equal(negotiationId, ex.NegotiationId);
        Assert.Equal(seller, ex.ExpectedSender);
        Assert.Equal(buyer, ex.ActualSender);
        Assert.Equal(2, ex.TurnIndex);
    }

    [Fact]
    public async Task ProcessTurnAsync_ThrowsTurnOutOfOrderException_WhenTurnIndexIsNotMonotonicallyIncreasing()
    {
        // Arrange
        const string negotiationId = "neg-stale-turn-1";
        const string buyer = "agent-buyer";
        const string seller = "agent-seller";

        _hotStateStore.Seed(HotStateSession.CreateInitial(negotiationId, buyer));

        // Turn 1
        await _engine.ProcessTurnAsync(new NegotiationTurnCommand(
            NegotiationId: negotiationId,
            SenderId: buyer,
            NextParticipantId: seller,
            Action: "propose",
            FactsDelta: null,
            TurnIndex: 1
        ));

        // Act & Assert - Seller sends turn index 1 (same as current session turnIndex)
        var ex = await Assert.ThrowsAsync<TurnOutOfOrderException>(() =>
            _engine.ProcessTurnAsync(new NegotiationTurnCommand(
                NegotiationId: negotiationId,
                SenderId: seller,
                NextParticipantId: buyer,
                Action: "counter_propose",
                FactsDelta: null,
                TurnIndex: 1
            )).AsTask());

        Assert.Equal(negotiationId, ex.NegotiationId);
        Assert.Equal(1, ex.TurnIndex);
        Assert.Equal(2, ex.ExpectedTurnIndex);
    }

    [Fact]
    public async Task ProcessTurnAsync_ThrowsInvalidTransitionException_WhenActionIsIllegalForState()
    {
        // Arrange
        const string negotiationId = "neg-invalid-trans-1";
        const string buyer = "agent-buyer";
        const string seller = "agent-seller";

        _hotStateStore.Seed(HotStateSession.CreateInitial(negotiationId, buyer));

        // Act & Assert - Action 'unknown_illegal_action' from OnBoard
        var ex = await Assert.ThrowsAsync<InvalidTransitionException>(() =>
            _engine.ProcessTurnAsync(new NegotiationTurnCommand(
                NegotiationId: negotiationId,
                SenderId: buyer,
                NextParticipantId: seller,
                Action: "unknown_illegal_action",
                FactsDelta: null,
                TurnIndex: 1
            )).AsTask());

        Assert.Equal(negotiationId, ex.NegotiationId);
        Assert.Equal(NegotiationState.OnBoard, ex.FromState);
        Assert.Equal("unknown_illegal_action", ex.Action);
    }

    [Fact]
    public async Task ProcessTurnAsync_SelfCorrection_RecoversFromBoardFormatErrorToOnBoard()
    {
        // Arrange
        const string negotiationId = "neg-self-correct-1";
        const string buyer = "agent-buyer";
        const string seller = "agent-seller";

        _hotStateStore.Seed(HotStateSession.CreateInitial(negotiationId, buyer));

        // Turn 1: Gateway/Orchestrator reports format error
        var turn1 = await _engine.ProcessTurnAsync(new NegotiationTurnCommand(
            NegotiationId: negotiationId,
            SenderId: buyer,
            NextParticipantId: buyer,
            Action: "report_format_error",
            FactsDelta: null,
            TurnIndex: 1
        ));

        Assert.Equal(NegotiationState.BoardFormatError, turn1.NewState);
        Assert.Equal(buyer, turn1.ActiveTurn);

        // Turn 2: Buyer performs self-correction and fixes format
        var fixedDoc = JsonDocument.Parse("{\"sku\":\"VALID-SKU\",\"price\":50.0}");
        var turn2 = await _engine.ProcessTurnAsync(new NegotiationTurnCommand(
            NegotiationId: negotiationId,
            SenderId: buyer,
            NextParticipantId: seller,
            Action: "fix_format",
            FactsDelta: fixedDoc.RootElement,
            TurnIndex: 2
        ));

        Assert.Equal(NegotiationState.OnBoard, turn2.NewState);
        Assert.Equal(seller, turn2.ActiveTurn);
    }

    [Fact]
    public async Task ProcessTurnAsync_ValidationRejected_ReturnsToOnBoardForRenegotiation()
    {
        // Arrange
        const string negotiationId = "neg-val-reject-1";
        const string buyer = "agent-buyer";
        const string seller = "agent-seller";

        _hotStateStore.Seed(new HotStateSession(
            NegotiationId: negotiationId,
            CurrentState: NegotiationState.Validation,
            ActiveTurn: seller,
            LastSenderId: buyer,
            TurnIndex: 2,
            ConsolidatedFacts: null,
            UpdatedAt: DateTimeOffset.UtcNow
        ));

        // Act - Seller deterministic guard rejects margin, returning to OnBoard
        var result = await _engine.ProcessTurnAsync(new NegotiationTurnCommand(
            NegotiationId: negotiationId,
            SenderId: seller,
            NextParticipantId: buyer,
            Action: "validation_rejected",
            FactsDelta: null,
            TurnIndex: 3
        ));

        Assert.True(result.Success);
        Assert.Equal(NegotiationState.OnBoard, result.NewState);
        Assert.Equal(buyer, result.ActiveTurn);
        Assert.Equal(3, result.TurnIndex);
    }

    [Fact]
    public async Task ProcessTurnAsync_ValidationFormatError_SelfCorrectionRecoversToValidation()
    {
        // Arrange
        const string negotiationId = "neg-val-format-err-1";
        const string buyer = "agent-buyer";
        const string seller = "agent-seller";

        _hotStateStore.Seed(new HotStateSession(
            NegotiationId: negotiationId,
            CurrentState: NegotiationState.Validation,
            ActiveTurn: seller,
            LastSenderId: buyer,
            TurnIndex: 2,
            ConsolidatedFacts: null,
            UpdatedAt: DateTimeOffset.UtcNow
        ));

        // Turn 3: Format error detected in validation payload
        var turn3 = await _engine.ProcessTurnAsync(new NegotiationTurnCommand(
            NegotiationId: negotiationId,
            SenderId: seller,
            NextParticipantId: seller,
            Action: "report_format_error",
            FactsDelta: null,
            TurnIndex: 3
        ));

        Assert.Equal(NegotiationState.ValidationFormatError, turn3.NewState);
        Assert.Equal(seller, turn3.ActiveTurn);

        // Turn 4: Fix format and retry validation
        var turn4 = await _engine.ProcessTurnAsync(new NegotiationTurnCommand(
            NegotiationId: negotiationId,
            SenderId: seller,
            NextParticipantId: buyer,
            Action: "fix_format",
            FactsDelta: null,
            TurnIndex: 4
        ));

        Assert.Equal(NegotiationState.Validation, turn4.NewState);
        Assert.Equal(buyer, turn4.ActiveTurn);
    }

    [Theory]
    [InlineData(NegotiationState.Ended)]
    [InlineData(NegotiationState.Frozen)]
    public async Task ProcessTurnAsync_ThrowsNegotiationTerminalException_WhenSessionIsInTerminalState(NegotiationState terminalState)
    {
        // Arrange
        const string negotiationId = "neg-terminal-1";
        const string agent = "agent-001";

        _hotStateStore.Seed(new HotStateSession(
            NegotiationId: negotiationId,
            CurrentState: terminalState,
            ActiveTurn: string.Empty,
            LastSenderId: agent,
            TurnIndex: 5,
            ConsolidatedFacts: null,
            UpdatedAt: DateTimeOffset.UtcNow
        ));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NegotiationTerminalException>(() =>
            _engine.ProcessTurnAsync(new NegotiationTurnCommand(
                NegotiationId: negotiationId,
                SenderId: agent,
                NextParticipantId: "agent-002",
                Action: "propose",
                FactsDelta: null,
                TurnIndex: 6
            )).AsTask());

        Assert.Equal(negotiationId, ex.NegotiationId);
        Assert.Equal(terminalState, ex.State);
    }

    [Fact]
    public async Task ProcessTurnAsync_ThrowsNegotiationNotFoundException_WhenSessionDoesNotExist()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<NegotiationNotFoundException>(() =>
            _engine.ProcessTurnAsync(new NegotiationTurnCommand(
                NegotiationId: "non-existent-neg",
                SenderId: "agent-001",
                NextParticipantId: "agent-002",
                Action: "propose",
                FactsDelta: null,
                TurnIndex: 1
            )).AsTask());

        Assert.Equal("non-existent-neg", ex.NegotiationId);
    }
}
