namespace Scada.Drivers.Abstractions;

/// <summary>
/// Protocol-neutral activation/readiness state for one configured external
/// communication Data Source. Readiness is deliberately independent from
/// point-level TAG quality: a source may be Ready while individual points are
/// BadDevice, BadConfiguration, Stale, or have no current sample yet.
/// </summary>
public enum CommunicationDriverReadinessState
{
    NotStarted,
    Starting,
    Ready,
    Faulted,
    Stopped
}

/// <summary>
/// Snapshot of the minimum protocol evidence required for a Data Source to be
/// considered operationally activated. Protocol-specific evidence belongs in
/// sanitized Details and must not contain credentials, private keys, tokens, or
/// other protected material.
/// </summary>
public sealed record CommunicationDriverReadinessSnapshot(
    string DataSourceKey,
    string DriverType,
    CommunicationDriverReadinessState State,
    DateTimeOffset ObservedAt,
    string? Reason = null,
    IReadOnlyDictionary<string, string>? Details = null)
{
    public bool IsReady => State == CommunicationDriverReadinessState.Ready;
}

/// <summary>
/// Optional capability for external communication drivers. It is intentionally
/// separate from ICommunicationDriver so internal/simulated providers and older
/// drivers are not forced to fabricate protocol activation semantics.
/// </summary>
public interface ICommunicationDriverReadinessSource
{
    CommunicationDriverReadinessSnapshot GetCommunicationReadiness();
}
