namespace Scada.Drivers.SiemensS7Iso;

/// <summary>
/// Protocol-local readiness evidence for the future common DriverHost readiness seam.
/// This does not replace common communication diagnostics and is not a private
/// activation framework.
/// </summary>
public enum S7IsoRuntimeReadinessState
{
    NotStarted,
    Starting,
    Ready,
    Faulted,
    Stopped
}

public sealed record S7IsoRuntimeReadinessSnapshot(
    string DataSourceKey,
    S7IsoRuntimeReadinessState State,
    DateTimeOffset StateChangedAt,
    DateTimeOffset CapturedAt,
    DateTimeOffset? ReadyAt,
    ushort? NegotiatedPduSizeAtReady,
    bool InitialAcquisitionCompleted,
    long InitialAcquisitionAttempts,
    string? LastError);

/// <summary>
/// Narrow Siemens-owned adapter that exposes the evidence the Coordinator-owned
/// runtime readiness contract will later consume.
/// </summary>
public interface IS7IsoRuntimeReadinessSource
{
    S7IsoRuntimeReadinessSnapshot GetS7IsoRuntimeReadiness();
}
