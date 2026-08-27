namespace Scada.Drivers.Modbus;

public sealed record ModbusTcpTransportDiagnosticSnapshot(
    string Host,
    int Port,
    TimeSpan RequestTimeout,
    bool IsConnected,
    long ConnectionCount,
    long DisconnectionCount,
    long ReconnectCount,
    long RequestAttempts,
    long SuccessfulRequestAttempts,
    long FailedRequestAttempts,
    long TimeoutCount,
    TimeSpan? LastRequestDuration,
    TimeSpan? AverageRequestDuration,
    DateTimeOffset? LastConnectedAt,
    DateTimeOffset? LastDisconnectedAt);

public sealed record ModbusTcpDiagnosticSnapshot(
    string Host,
    int Port,
    TimeSpan ScanRate,
    TimeSpan RequestTimeout,
    int PollBlockCount,
    IReadOnlyCollection<byte> UnitIds,
    long SuccessfulPollBlocks,
    long FailedPollBlocks,
    long FailedPollCycles,
    long ConsecutiveFailedCycles,
    DateTimeOffset? LastSuccessfulPollAt,
    DateTimeOffset? LastFailedPollAt,
    TimeSpan? LastPollDuration,
    ModbusTcpTransportDiagnosticSnapshot Transport);
