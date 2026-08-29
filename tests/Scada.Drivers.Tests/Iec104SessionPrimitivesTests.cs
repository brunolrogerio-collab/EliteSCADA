using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104SessionPrimitivesTests
{
    [Fact]
    public void DefaultSessionOptionsAreValid()
    {
        var options = new Iec104SessionOptions();

        options.Validate();

        Assert.Equal(TimeSpan.FromSeconds(30), options.T0);
        Assert.Equal(TimeSpan.FromSeconds(15), options.T1);
        Assert.Equal(TimeSpan.FromSeconds(10), options.T2);
        Assert.Equal(TimeSpan.FromSeconds(20), options.T3);
        Assert.Equal(12, options.K);
        Assert.Equal(8, options.W);
    }

    [Fact]
    public void SessionOptionsRejectT2GreaterThanOrEqualToT1()
    {
        var options = new Iec104SessionOptions
        {
            T1 = TimeSpan.FromSeconds(10),
            T2 = TimeSpan.FromSeconds(10)
        };

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void SessionOptionsRejectReceiveWindowLargerThanSendWindow()
    {
        var options = new Iec104SessionOptions
        {
            K = 4,
            W = 5
        };

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void SessionStateMachineAllowsNormalStartupAndShutdownPath()
    {
        var state = new Iec104SessionStateMachine();

        state.TransitionTo(Iec104SessionState.Connecting);
        state.TransitionTo(Iec104SessionState.TcpConnected);
        state.TransitionTo(Iec104SessionState.StartingDataTransfer);
        state.TransitionTo(Iec104SessionState.Running);
        state.TransitionTo(Iec104SessionState.Stopping);
        state.TransitionTo(Iec104SessionState.Stopped);

        Assert.Equal(Iec104SessionState.Stopped, state.State);
    }

    [Fact]
    public void SessionStateMachineRejectsRunningBeforeTcpAndStartDt()
    {
        var state = new Iec104SessionStateMachine();

        Assert.Throws<InvalidOperationException>(() => state.TransitionTo(Iec104SessionState.Running));
        Assert.Equal(Iec104SessionState.Stopped, state.State);
    }

    [Fact]
    public void SessionStateMachineSupportsReconnectPathWithoutPretendingTransportIsRunning()
    {
        var state = new Iec104SessionStateMachine();
        state.TransitionTo(Iec104SessionState.Connecting);
        state.TransitionTo(Iec104SessionState.TcpConnected);
        state.TransitionTo(Iec104SessionState.StartingDataTransfer);
        state.TransitionTo(Iec104SessionState.Running);

        state.TransitionTo(Iec104SessionState.Reconnecting);
        Assert.Equal(Iec104SessionState.Reconnecting, state.State);

        state.TransitionTo(Iec104SessionState.Connecting);
        Assert.Equal(Iec104SessionState.Connecting, state.State);
    }
}
