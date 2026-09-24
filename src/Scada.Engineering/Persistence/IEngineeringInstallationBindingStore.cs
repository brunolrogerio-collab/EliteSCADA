namespace Scada.Engineering.Persistence;

public enum EngineeringInstallationBindingState
{
    Legacy,
    Neutral,
    AttachInProgress,
    Attached,
    DetachInProgress
}

public sealed record EngineeringInstallationBindingSnapshot(
    EngineeringInstallationBindingState State,
    string? ProjectKey,
    long Generation,
    DateTimeOffset UpdatedAtUtc)
{
    public bool IsAttached => State == EngineeringInstallationBindingState.Attached;
    public bool IsNeutral => State == EngineeringInstallationBindingState.Neutral;
}

/// <summary>
/// Durable installation-level Application binding. Revisions remain canonical Application
/// lifecycle data; this journal only identifies whether one project is currently attached
/// to this EliteSCADA installation and makes attach/detach restart-safe.
/// </summary>
public interface IEngineeringInstallationBindingStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<EngineeringInstallationBindingSnapshot> GetAsync(CancellationToken cancellationToken = default);

    Task<EngineeringInstallationBindingSnapshot> AdoptLegacyAsync(
        string? projectKey,
        CancellationToken cancellationToken = default);

    Task<EngineeringInstallationBindingSnapshot> BeginAttachAsync(
        string projectKey,
        CancellationToken cancellationToken = default);

    Task<EngineeringInstallationBindingSnapshot> CompleteAttachAsync(
        string projectKey,
        CancellationToken cancellationToken = default);

    Task<EngineeringInstallationBindingSnapshot> AbortAttachAsync(
        string projectKey,
        CancellationToken cancellationToken = default);

    Task<EngineeringInstallationBindingSnapshot> BeginDetachAsync(
        string projectKey,
        CancellationToken cancellationToken = default);

    Task<EngineeringInstallationBindingSnapshot> CompleteDetachAsync(
        string projectKey,
        CancellationToken cancellationToken = default);
}
