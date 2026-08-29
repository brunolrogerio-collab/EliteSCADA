namespace Scada.Drivers.Iec60870;

public sealed record Iec104SessionOptions
{
    public TimeSpan T0 { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan T1 { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan T2 { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan T3 { get; init; } = TimeSpan.FromSeconds(20);
    public int K { get; init; } = 12;
    public int W { get; init; } = 8;

    public void Validate()
    {
        ValidatePositive(T0, nameof(T0));
        ValidatePositive(T1, nameof(T1));
        ValidatePositive(T2, nameof(T2));
        ValidatePositive(T3, nameof(T3));

        if (T2 >= T1)
            throw new ArgumentException("IEC-104 requires T2 to be smaller than T1 so delayed receive acknowledgement cannot outlive transmit supervision.", nameof(T2));
        if (K is < 1 or >= Iec104ApciCodec.SequenceModulo)
            throw new ArgumentOutOfRangeException(nameof(K), K, "IEC-104 K must be in the range 1..32767.");
        if (W is < 1 or >= Iec104ApciCodec.SequenceModulo)
            throw new ArgumentOutOfRangeException(nameof(W), W, "IEC-104 W must be in the range 1..32767.");
        if (W > K)
            throw new ArgumentException("IEC-104 receive acknowledgement window W cannot exceed send window K.", nameof(W));
    }

    private static void ValidatePositive(TimeSpan value, string parameterName)
    {
        if (value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(parameterName, value, "IEC-104 timer must be greater than zero.");
    }
}

public enum Iec104SessionState
{
    Stopped,
    Connecting,
    TcpConnected,
    StartingDataTransfer,
    Running,
    Stopping,
    Reconnecting,
    Faulted
}

public sealed class Iec104SessionStateMachine
{
    public Iec104SessionState State { get; private set; } = Iec104SessionState.Stopped;

    public void TransitionTo(Iec104SessionState next)
    {
        if (next == State)
            return;

        if (!IsAllowed(State, next))
            throw new InvalidOperationException($"Invalid IEC-104 session state transition: {State} -> {next}.");

        State = next;
    }

    private static bool IsAllowed(Iec104SessionState current, Iec104SessionState next) => current switch
    {
        Iec104SessionState.Stopped => next == Iec104SessionState.Connecting,
        Iec104SessionState.Connecting => next is Iec104SessionState.TcpConnected or Iec104SessionState.Reconnecting or Iec104SessionState.Faulted or Iec104SessionState.Stopped,
        Iec104SessionState.TcpConnected => next is Iec104SessionState.StartingDataTransfer or Iec104SessionState.Stopping or Iec104SessionState.Reconnecting or Iec104SessionState.Faulted,
        Iec104SessionState.StartingDataTransfer => next is Iec104SessionState.Running or Iec104SessionState.Stopping or Iec104SessionState.Reconnecting or Iec104SessionState.Faulted,
        Iec104SessionState.Running => next is Iec104SessionState.Stopping or Iec104SessionState.Reconnecting or Iec104SessionState.Faulted,
        Iec104SessionState.Stopping => next is Iec104SessionState.Stopped or Iec104SessionState.Reconnecting or Iec104SessionState.Faulted,
        Iec104SessionState.Reconnecting => next is Iec104SessionState.Connecting or Iec104SessionState.Stopped or Iec104SessionState.Faulted,
        Iec104SessionState.Faulted => next is Iec104SessionState.Reconnecting or Iec104SessionState.Stopped,
        _ => false
    };
}
