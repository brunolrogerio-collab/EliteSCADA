namespace Scada.Drivers.Iec60870;

public sealed class Iec104SequenceState
{
    private int _nextSendSequence;
    private int _oldestUnacknowledgedSendSequence;
    private int _expectedReceiveSequence;
    private int _lastAcknowledgedReceiveSequence;

    public Iec104SequenceState(ushort initialSendSequence = 0, ushort initialReceiveSequence = 0)
    {
        ValidateSequence(initialSendSequence, nameof(initialSendSequence));
        ValidateSequence(initialReceiveSequence, nameof(initialReceiveSequence));

        _nextSendSequence = initialSendSequence;
        _oldestUnacknowledgedSendSequence = initialSendSequence;
        _expectedReceiveSequence = initialReceiveSequence;
        _lastAcknowledgedReceiveSequence = initialReceiveSequence;
    }

    public ushort NextSendSequence => checked((ushort)_nextSendSequence);
    public ushort OldestUnacknowledgedSendSequence => checked((ushort)_oldestUnacknowledgedSendSequence);
    public ushort ExpectedReceiveSequence => checked((ushort)_expectedReceiveSequence);
    public ushort ReceiveAcknowledgementSequence => checked((ushort)_expectedReceiveSequence);
    public int UnacknowledgedSendCount => Distance(_oldestUnacknowledgedSendSequence, _nextSendSequence);
    public int PendingReceiveAcknowledgementCount => Distance(_lastAcknowledgedReceiveSequence, _expectedReceiveSequence);

    public ushort ReserveSendSequence(int sendWindow)
    {
        ValidateWindow(sendWindow, nameof(sendWindow));
        if (UnacknowledgedSendCount >= sendWindow)
            throw new InvalidOperationException("IEC-104 send window is full; another I-format frame cannot be reserved until peer acknowledgement advances N(R).");

        var reserved = checked((ushort)_nextSendSequence);
        _nextSendSequence = Increment(_nextSendSequence);
        return reserved;
    }

    public void AcceptPeerAcknowledgement(ushort receiveSequence)
    {
        ValidateSequence(receiveSequence, nameof(receiveSequence));
        _ = ValidatePeerAcknowledgement(receiveSequence);
        _oldestUnacknowledgedSendSequence = receiveSequence;
    }

    public void AcceptReceivedIFrame(ushort sendSequence, ushort receiveSequence)
    {
        ValidateSequence(sendSequence, nameof(sendSequence));
        ValidateSequence(receiveSequence, nameof(receiveSequence));

        if (sendSequence != _expectedReceiveSequence)
        {
            throw new Iec104ProtocolException(
                $"Unexpected IEC-104 I-format N(S) {sendSequence}; expected {_expectedReceiveSequence}.");
        }

        _ = ValidatePeerAcknowledgement(receiveSequence);

        _oldestUnacknowledgedSendSequence = receiveSequence;
        _expectedReceiveSequence = Increment(_expectedReceiveSequence);
    }

    public bool ShouldSendSupervisoryAcknowledgement(int receiveWindow)
    {
        ValidateWindow(receiveWindow, nameof(receiveWindow));
        return PendingReceiveAcknowledgementCount >= receiveWindow;
    }

    public ushort MarkReceiveAcknowledged()
    {
        var receiveSequence = checked((ushort)_expectedReceiveSequence);
        MarkReceiveAcknowledged(receiveSequence);
        return receiveSequence;
    }

    public void MarkReceiveAcknowledged(ushort receiveSequence)
    {
        ValidateSequence(receiveSequence, nameof(receiveSequence));

        var pending = PendingReceiveAcknowledgementCount;
        var acknowledged = Distance(_lastAcknowledgedReceiveSequence, receiveSequence);
        if (acknowledged > pending)
        {
            throw new Iec104ProtocolException(
                $"IEC-104 local acknowledgement N(R) {receiveSequence} advances beyond the {pending} received I-format frame(s) awaiting acknowledgement.");
        }

        _lastAcknowledgedReceiveSequence = receiveSequence;
    }

    public void Reset()
    {
        _nextSendSequence = 0;
        _oldestUnacknowledgedSendSequence = 0;
        _expectedReceiveSequence = 0;
        _lastAcknowledgedReceiveSequence = 0;
    }

    private int ValidatePeerAcknowledgement(ushort receiveSequence)
    {
        var outstanding = UnacknowledgedSendCount;
        var acknowledged = Distance(_oldestUnacknowledgedSendSequence, receiveSequence);
        if (acknowledged > outstanding)
        {
            throw new Iec104ProtocolException(
                $"IEC-104 peer acknowledgement N(R) {receiveSequence} advances beyond the {outstanding} outstanding I-format frame(s).");
        }

        return acknowledged;
    }

    private static int Increment(int sequence) => (sequence + 1) % Iec104ApciCodec.SequenceModulo;

    private static int Distance(int fromInclusive, int toExclusive) =>
        (toExclusive - fromInclusive + Iec104ApciCodec.SequenceModulo) % Iec104ApciCodec.SequenceModulo;

    private static void ValidateSequence(ushort sequence, string parameterName)
    {
        if (sequence >= Iec104ApciCodec.SequenceModulo)
            throw new ArgumentOutOfRangeException(parameterName, sequence, "IEC-104 sequence number must be in the range 0..32767.");
    }

    private static void ValidateWindow(int window, string parameterName)
    {
        if (window is < 1 or >= Iec104ApciCodec.SequenceModulo)
            throw new ArgumentOutOfRangeException(parameterName, window, "IEC-104 window must be in the range 1..32767.");
    }
}
