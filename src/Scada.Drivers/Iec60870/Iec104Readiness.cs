namespace Scada.Drivers.Iec60870;

/// <summary>
/// IEC-104-local readiness evidence aligned with the Coordinator convergence contract.
/// This is deliberately protocol-local until the host-owned readiness seam exists.
/// </summary>
public enum Iec104ReadinessState
{
    NotStarted,
    Starting,
    Ready,
    Faulted,
    Stopped
}

public sealed record Iec104ReadinessSnapshot(
    Iec104ReadinessState State,
    Iec104SessionState SessionState,
    bool IsTransportConnected,
    bool IsDataTransferStarted,
    bool StartupGeneralInterrogationCompleted,
    bool StartupGeneralInterrogationRejected,
    IReadOnlyDictionary<ushort, Iec104GeneralInterrogationState> GeneralInterrogationStates,
    int ReconnectAttempt,
    DateTimeOffset CapturedAt,
    DateTimeOffset? LastFailureAt = null,
    string? LastFailure = null);
