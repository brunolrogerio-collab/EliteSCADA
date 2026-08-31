namespace Scada.Drivers.Dnp3;

public enum Dnp3SessionState
{
    Stopped,
    Connecting,
    StartupIntegrity,
    Online,
    Degraded,
    Reconnecting,
    Faulted,
    Stopping
}

public sealed record Dnp3Measurement(
    Dnp3PointKind PointKind,
    ushort Index,
    object? Value,
    Dnp3ObjectVariation Variation,
    bool IsEvent,
    Dnp3PointFlagSet Flags,
    DateTimeOffset? SourceTimestamp = null,
    bool SourceTimestampSynchronized = true);

public sealed record Dnp3CommandResult(
    bool Succeeded,
    string Status,
    string? Message = null)
{
    public static Dnp3CommandResult Success(string status = "SUCCESS") => new(true, status);

    public static Dnp3CommandResult Failure(string status, string? message = null) => new(false, status, message);
}

public sealed record Dnp3SessionDiagnosticSnapshot(
    string? Endpoint,
    Dnp3SessionState State,
    DateTimeOffset StateChangedAt,
    DateTimeOffset? LastSuccessfulCommunicationAt = null,
    DateTimeOffset? LastFailedCommunicationAt = null,
    string? LastError = null,
    long Requests = 0,
    long SuccessfulOperations = 0,
    long FailedOperations = 0,
    long ConsecutiveFailures = 0,
    long Timeouts = 0,
    long Connections = 0,
    long Disconnections = 0,
    long Reconnects = 0,
    long ReadOperations = 0,
    long WriteOperations = 0,
    long StartupIntegrityScans = 0,
    long Class0Scans = 0,
    long Class1Scans = 0,
    long Class2Scans = 0,
    long Class3Scans = 0,
    long UnsolicitedResponses = 0,
    long RestartDetections = 0,
    long EventBufferOverflowDetections = 0,
    double RecentFailureRate = 0d);

/// <summary>
/// EliteSCADA-owned seam around a concrete DNP3 Master stack. Vendor/library
/// types remain behind this interface. Implementations must report material
/// session-state transitions through stateHandler and must never retain or
/// replay a process command across reconnect. Execute methods fail promptly
/// when there is no active association able to execute the command.
/// </summary>
public interface IDnp3MasterSession : IAsyncDisposable
{
    Dnp3SessionState State { get; }

    ValueTask StartAsync(
        Dnp3AssociationOptions options,
        Func<Dnp3Measurement, CancellationToken, ValueTask> measurementHandler,
        Func<Dnp3SessionState, CancellationToken, ValueTask> stateHandler,
        CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);

    ValueTask<Dnp3CommandResult> ExecuteBinaryAsync(
        ushort index,
        Dnp3BinaryOperation operation,
        Dnp3BinaryCommandProfile profile,
        CancellationToken cancellationToken = default);

    ValueTask<Dnp3CommandResult> ExecuteAnalogAsync(
        ushort index,
        object value,
        Dnp3AnalogCommandProfile profile,
        CancellationToken cancellationToken = default);

    Dnp3SessionDiagnosticSnapshot GetDiagnostics();
}

public sealed class Dnp3CommandException(string status, string? message = null)
    : InvalidOperationException(string.IsNullOrWhiteSpace(message) ? $"DNP3 command failed: {status}." : $"DNP3 command failed: {status}. {message}")
{
    public string CommandStatus { get; } = status;
}
