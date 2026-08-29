namespace Scada.Drivers.Iec60870;

public sealed record Iec104CommandDiagnosticCounters(
    long Requested,
    long Accepted,
    long Completed,
    long Rejected,
    long TimedOut,
    long Ambiguous,
    long Cancelled);

/// <summary>
/// Sanitized protocol-level snapshot for the long-lived IEC-104 managed client.
/// The future public IEC-104 communication driver will compose this into the common
/// CommunicationDriverDiagnosticSnapshot together with canonical TAG quality/count data.
/// </summary>
public sealed record Iec104ManagedDiagnosticSnapshot(
    string RuntimeInstanceId,
    string Host,
    int Port,
    Iec104SessionState SessionState,
    int ReconnectAttempt,
    int InFlightCommands,
    IReadOnlyList<ushort> CommonAddresses,
    TimeSpan T0,
    TimeSpan T1,
    TimeSpan T2,
    TimeSpan T3,
    int K,
    int W,
    long SessionFailures,
    long ObservedPointUpdates,
    DateTimeOffset CapturedAt,
    DateTimeOffset? LastSessionAttemptAt,
    DateTimeOffset? LastObservedPointAt,
    DateTimeOffset? LastFailureAt,
    string? LastError,
    int? LastFailedAttempt,
    TimeSpan? LastReconnectDelay,
    bool? LastBackoffWasReset,
    Iec104CommandDiagnosticCounters Commands,
    Iec104TcpAdapterDiagnosticSnapshot? Transport);
