using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104SequenceStateTests
{
    [Fact]
    public void SendWindowBlocksUntilPeerAcknowledgementAdvances()
    {
        var state = new Iec104SequenceState();

        Assert.Equal((ushort)0, state.ReserveSendSequence(2));
        Assert.Equal((ushort)1, state.ReserveSendSequence(2));
        Assert.Equal(2, state.UnacknowledgedSendCount);
        Assert.Throws<InvalidOperationException>(() => state.ReserveSendSequence(2));

        state.AcceptPeerAcknowledgement(1);

        Assert.Equal(1, state.UnacknowledgedSendCount);
        Assert.Equal((ushort)2, state.ReserveSendSequence(2));
    }

    [Fact]
    public void PeerAcknowledgementCannotAdvanceBeyondOutstandingFrames()
    {
        var state = new Iec104SequenceState();
        _ = state.ReserveSendSequence(12);

        Assert.Throws<Iec104ProtocolException>(() => state.AcceptPeerAcknowledgement(2));
        Assert.Equal((ushort)0, state.OldestUnacknowledgedSendSequence);
        Assert.Equal(1, state.UnacknowledgedSendCount);
    }

    [Fact]
    public void AcceptedIFrameAdvancesExpectedReceiveAndSupervisoryAckState()
    {
        var state = new Iec104SequenceState();

        state.AcceptReceivedIFrame(0, 0);

        Assert.Equal((ushort)1, state.ExpectedReceiveSequence);
        Assert.Equal(1, state.PendingReceiveAcknowledgementCount);
        Assert.True(state.ShouldSendSupervisoryAcknowledgement(1));
        Assert.Equal((ushort)1, state.MarkReceiveAcknowledged());
        Assert.Equal(0, state.PendingReceiveAcknowledgementCount);
    }

    [Fact]
    public void ExactLocalAcknowledgementDoesNotConsumeLaterReceivedFrames()
    {
        var state = new Iec104SequenceState();
        state.AcceptReceivedIFrame(0, 0);
        var capturedAcknowledgement = state.ReceiveAcknowledgementSequence;
        state.AcceptReceivedIFrame(1, 0);

        state.MarkReceiveAcknowledged(capturedAcknowledgement);

        Assert.Equal(1, state.PendingReceiveAcknowledgementCount);
        Assert.Equal((ushort)2, state.MarkReceiveAcknowledged());
        Assert.Equal(0, state.PendingReceiveAcknowledgementCount);
    }

    [Fact]
    public void ExactLocalAcknowledgementCannotAdvanceBeyondReceivedFrames()
    {
        var state = new Iec104SequenceState();
        state.AcceptReceivedIFrame(0, 0);

        Assert.Throws<Iec104ProtocolException>(() => state.MarkReceiveAcknowledged(2));
        Assert.Equal(1, state.PendingReceiveAcknowledgementCount);
    }

    [Fact]
    public void SequenceNumbersWrapAt32768()
    {
        var state = new Iec104SequenceState(32767, 32767);

        Assert.Equal((ushort)32767, state.ReserveSendSequence(12));
        Assert.Equal((ushort)0, state.NextSendSequence);
        Assert.Equal(1, state.UnacknowledgedSendCount);

        state.AcceptPeerAcknowledgement(0);
        Assert.Equal(0, state.UnacknowledgedSendCount);

        state.AcceptReceivedIFrame(32767, 0);
        Assert.Equal((ushort)0, state.ExpectedReceiveSequence);
    }

    [Fact]
    public void UnexpectedReceiveSequenceDoesNotConsumePeerAcknowledgement()
    {
        var state = new Iec104SequenceState();
        _ = state.ReserveSendSequence(12);

        Assert.Throws<Iec104ProtocolException>(() => state.AcceptReceivedIFrame(1, 1));

        Assert.Equal((ushort)0, state.ExpectedReceiveSequence);
        Assert.Equal((ushort)0, state.OldestUnacknowledgedSendSequence);
        Assert.Equal(1, state.UnacknowledgedSendCount);
    }

    [Fact]
    public void ResetReturnsBothDirectionsToFreshSessionState()
    {
        var state = new Iec104SequenceState();
        _ = state.ReserveSendSequence(12);
        state.AcceptReceivedIFrame(0, 0);

        state.Reset();

        Assert.Equal((ushort)0, state.NextSendSequence);
        Assert.Equal((ushort)0, state.ExpectedReceiveSequence);
        Assert.Equal(0, state.UnacknowledgedSendCount);
        Assert.Equal(0, state.PendingReceiveAcknowledgementCount);
    }
}
