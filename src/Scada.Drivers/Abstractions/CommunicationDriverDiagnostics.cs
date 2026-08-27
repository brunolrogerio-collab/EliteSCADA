using Scada.Core.Tags;

namespace Scada.Drivers.Abstractions;

/// <summary>
/// Protocol-neutral operational state for external communication diagnostics.
/// This is intentionally separate from the legacy coarse DriverState so
/// communication drivers can evolve richer health semantics without forcing
/// non-network/internal providers to fabricate transport state.
/// </summary>
public enum CommunicationDriverOperationalState
{
    Stopped,
    Starting,
    Healthy,
    Degraded,
    Reconnecting,
    Faulted,
    Stopping
}

public sealed record CommunicationDriverCounters(
    long Cycles,
    long Requests,
    long SuccessfulOperations,
    long FailedOperations,
    long ConsecutiveFailures,
    long Timeouts,
    long Connections,
    long Disconnections,
    long Reconnects,
    long ReadOperations,
    long WriteOperations,
    long UpdatesPublished);

public sealed record CommunicationTagQualitySummary(
    int Good,
    int BadCommunication,
    int Uncertain,
    int Bad,
    int BadConfiguration,
    int BadDevice,
    int Stale,
    int Disabled,
    int NoCurrentSample)
{
    public int Total => Good + BadCommunication + Uncertain + Bad + BadConfiguration + BadDevice + Stale + Disabled + NoCurrentSample;
}

/// <summary>
/// Common runtime diagnostic snapshot for one configured external communication
/// Data Source / driver instance. Protocol-specific diagnostics may be appended
/// as sanitized string details, but common fields never depend on one protocol.
/// </summary>
public sealed record CommunicationDriverDiagnosticSnapshot(
    string DataSourceKey,
    string DataSourceName,
    string DriverType,
    string RuntimeInstanceId,
    string? Endpoint,
    CommunicationDriverOperationalState State,
    DateTimeOffset StateChangedAt,
    DateTimeOffset CapturedAt,
    DateTimeOffset? LastSuccessfulCommunicationAt,
    DateTimeOffset? LastFailedCommunicationAt,
    string? LastError,
    TimeSpan? DataAge,
    TimeSpan? ConfiguredScanInterval,
    TimeSpan? LastOperationDuration,
    TimeSpan? AverageOperationDuration,
    TimeSpan? LastScanDuration,
    double RecentFailureRate,
    int AssociatedTagCount,
    CommunicationTagQualitySummary TagQuality,
    CommunicationDriverCounters Counters,
    IReadOnlyDictionary<string, string>? ProtocolDetails = null);

/// <summary>
/// Optional capability for external communication drivers. It is deliberately
/// not part of ICommunicationDriver so Internal Memory and Simulation do not
/// have to pretend they own network transports or reconnect/timeout metrics.
/// </summary>
public interface ICommunicationDiagnosticsSource
{
    CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics();
}
