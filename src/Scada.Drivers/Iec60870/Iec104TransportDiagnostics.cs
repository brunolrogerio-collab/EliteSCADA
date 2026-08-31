namespace Scada.Drivers.Iec60870;

public interface IIec104TransportDiagnosticsSource
{
    Iec104TcpAdapterDiagnosticSnapshot GetTransportDiagnostics();
}

public sealed record Iec104TcpAdapterDiagnosticSnapshot(
    bool IsConnected,
    bool IsDataTransferStarted,
    ushort NextSendSequence,
    ushort OldestUnacknowledgedSendSequence,
    ushort ExpectedReceiveSequence,
    int UnacknowledgedSendCount,
    int PendingReceiveAcknowledgementCount,
    long Connections,
    long Disconnections,
    long IFramesSent,
    long SFramesSent,
    long UFramesSent,
    long IFramesReceived,
    long SFramesReceived,
    long UFramesReceived,
    long AsdusSent,
    long AsdusReceived,
    long StartDtActivationsSent,
    long StartDtConfirmationsReceived,
    long StopDtActivationsSent,
    long StopDtConfirmationsReceived,
    long TestFrameActivationsSent,
    long TestFrameActivationsReceived,
    long TestFrameConfirmationsSent,
    long TestFrameConfirmationsReceived,
    long T0Timeouts,
    long T1Timeouts,
    long T2Expirations,
    long T3Expirations,
    long ProtocolErrors,
    long SessionFailures,
    DateTimeOffset CapturedAt,
    DateTimeOffset? LastActivityAt,
    DateTimeOffset? LastFrameSentAt,
    DateTimeOffset? LastFrameReceivedAt,
    string? LastFailure);
