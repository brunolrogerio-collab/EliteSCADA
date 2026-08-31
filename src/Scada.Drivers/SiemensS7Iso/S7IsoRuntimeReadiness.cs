using System.Globalization;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.SiemensS7Iso;

/// <summary>
/// Protocol-local readiness evidence bridged into the shared DriverHost readiness seam.
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
/// Siemens-owned adapter that preserves S7-specific activation evidence while
/// exposing the protocol-neutral readiness contract consumed by DriverHost.
/// </summary>
public interface IS7IsoRuntimeReadinessSource : ICommunicationDriverReadinessSource
{
    S7IsoRuntimeReadinessSnapshot GetS7IsoRuntimeReadiness();

    CommunicationDriverReadinessSnapshot ICommunicationDriverReadinessSource.GetCommunicationReadiness()
    {
        var snapshot = GetS7IsoRuntimeReadiness();
        var state = snapshot.State switch
        {
            S7IsoRuntimeReadinessState.NotStarted => CommunicationDriverReadinessState.NotStarted,
            S7IsoRuntimeReadinessState.Starting => CommunicationDriverReadinessState.Starting,
            S7IsoRuntimeReadinessState.Ready => CommunicationDriverReadinessState.Ready,
            S7IsoRuntimeReadinessState.Faulted => CommunicationDriverReadinessState.Faulted,
            S7IsoRuntimeReadinessState.Stopped => CommunicationDriverReadinessState.Stopped,
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot.State), snapshot.State, "Unsupported S7 readiness state.")
        };

        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["initialAcquisitionCompleted"] = snapshot.InitialAcquisitionCompleted ? "true" : "false",
            ["initialAcquisitionAttempts"] = snapshot.InitialAcquisitionAttempts.ToString(CultureInfo.InvariantCulture),
            ["negotiatedPduSize"] = snapshot.NegotiatedPduSizeAtReady?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ["readyAtUtc"] = snapshot.ReadyAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty
        };

        return new CommunicationDriverReadinessSnapshot(
            snapshot.DataSourceKey,
            "siemens.s7.iso",
            state,
            snapshot.CapturedAt,
            snapshot.LastError,
            details);
    }
}
