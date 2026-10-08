using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Abstractions;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Interactions;
using Scada.Engineering.Persistence;

namespace Scada.Api.Persistence;

public sealed record PublishedRuntimeActivationOutcome(
    EngineeringProjectSnapshot? Snapshot,
    RuntimeActivationResult? Runtime,
    EngineeringProjectActivation? Activation,
    EngineeringProjectLifecycle? Lifecycle)
{
    public bool Found => Snapshot is not null;
    public bool Activated => Runtime?.Activated == true && Activation is not null;
}

public interface IPublishedRuntimeActivationService
{
    Task<PublishedRuntimeActivationOutcome> ActivateAsync(
        string projectKey,
        string? activatedBy = null,
        CancellationToken cancellationToken = default);

    Task<PublishedRuntimeActivationOutcome> ActivateRevisionAsync(
        string projectKey,
        long revision,
        string? activatedBy = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This activation service does not support exact-revision activation.");

    Task<PublishedRuntimeActivationOutcome> ActivateRevisionForHaTakeoverAsync(
        string projectKey,
        long revision,
        string? activatedBy = null,
        CancellationToken cancellationToken = default) =>
        ActivateRevisionAsync(projectKey, revision, activatedBy, cancellationToken);

    Task<PublishedRuntimeActivationOutcome> ActivateCurrentActiveRevisionForHaTakeoverAsync(
        string projectKey,
        string? activatedBy = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This activation service does not support local Active revision recovery.");

    Task<bool> HasPersistedRevisionForHaTakeoverAsync(
        string projectKey,
        long revision,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

public sealed class PublishedRuntimeActivationService(
    IEngineeringProjectPersistenceService persistence,
    IEngineeringExchangeService exchange,
    IEngineeringRuntimeCoordinator runtime,
    IScadaEventBus? eventBus = null,
    IConfiguration? configuration = null,
    GatewayEngineeringRuntimeCoordinator? operationalEvents = null,
    RuntimeHighAvailabilityService? highAvailability = null,
    ActiveDriverInteractionRuntimeCatalog? driverInteractions = null,
    IRichCommandRuntime? richCommands = null,
    ApiAuthorizationService? authorization = null,
    ApiAuditService? audit = null) : IPublishedRuntimeActivationService
{
    public async Task<PublishedRuntimeActivationOutcome> ActivateAsync(
        string projectKey,
        string? activatedBy = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ArgumentException("Project key is required.", nameof(projectKey));

        var snapshot = await persistence.LoadPublishedAsync(projectKey, cancellationToken);
        if (snapshot is null)
            return new PublishedRuntimeActivationOutcome(null, null, null, null);

        return await ActivateSnapshotAsync(snapshot, activatedBy, cancellationToken, haTakeover: false);
    }

    public async Task<PublishedRuntimeActivationOutcome> ActivateRevisionAsync(
        string projectKey,
        long revision,
        string? activatedBy = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ArgumentException("Project key is required.", nameof(projectKey));
        if (revision <= 0)
            throw new ArgumentOutOfRangeException(nameof(revision));

        var snapshot = await persistence.LoadRevisionAsync(projectKey, revision, cancellationToken);
        if (snapshot is null)
            return new PublishedRuntimeActivationOutcome(null, null, null, null);

        return await ActivateSnapshotAsync(snapshot, activatedBy, cancellationToken, haTakeover: false);
    }

    public async Task<PublishedRuntimeActivationOutcome> ActivateRevisionForHaTakeoverAsync(
        string projectKey,
        long revision,
        string? activatedBy = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ArgumentException("Project key is required.", nameof(projectKey));
        if (revision <= 0)
            throw new ArgumentOutOfRangeException(nameof(revision));

        var snapshot = await persistence.LoadRevisionAsync(projectKey, revision, cancellationToken);
        if (snapshot is null)
            return new PublishedRuntimeActivationOutcome(null, null, null, null);

        return await ActivateSnapshotAsync(snapshot, activatedBy, cancellationToken, haTakeover: true);
    }

    public async Task<PublishedRuntimeActivationOutcome> ActivateCurrentActiveRevisionForHaTakeoverAsync(
        string projectKey,
        string? activatedBy = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ArgumentException("Project key is required.", nameof(projectKey));

        var snapshot = await persistence.LoadActiveAsync(projectKey, cancellationToken);
        if (snapshot is null)
            return new PublishedRuntimeActivationOutcome(null, null, null, null);

        return await ActivateSnapshotAsync(snapshot, activatedBy, cancellationToken, haTakeover: true);
    }

    public async Task<bool> HasPersistedRevisionForHaTakeoverAsync(
        string projectKey,
        long revision,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectKey) || revision <= 0)
            return false;

        return await persistence.LoadRevisionAsync(projectKey, revision, cancellationToken) is not null;
    }

    private async Task<PublishedRuntimeActivationOutcome> ActivateSnapshotAsync(
        EngineeringProjectSnapshot snapshot,
        string? activatedBy,
        CancellationToken cancellationToken,
        bool haTakeover)
    {
        var package = ParseAndValidate(snapshot);
        PreparedDriverInteractionRuntimeGraph? preparedInteractions = null;
        try
        {
            if (driverInteractions is not null)
                preparedInteractions = driverInteractions.Prepare(package);
            else
                _ = DriverInteractionEngineeringValidator.NormalizeActiveGraph(package);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
        {
            var invalidGraphLifecycle = await persistence.GetLifecycleAsync(
                snapshot.ProjectKey,
                CancellationToken.None);
            return new PublishedRuntimeActivationOutcome(
                snapshot,
                InvalidInteractionGraph(snapshot, ex.Message),
                null,
                invalidGraphLifecycle);
        }

        var previousInteractions = driverInteractions?.CapturePrepared();
        var interactionsCommitted = false;
        EngineeringProjectActivation? recordedActivation = null;

        async Task CommitAsync(
            RuntimeActivationCommitContext _,
            CancellationToken ct)
        {
            recordedActivation = haTakeover
                ? await persistence.RecordActivationForHaTakeoverAsync(
                    snapshot.ProjectKey,
                    snapshot.Revision,
                    activatedBy,
                    ct)
                : await persistence.RecordActivationAsync(
                    snapshot.ProjectKey,
                    snapshot.Revision,
                    activatedBy,
                    ct);

            if (recordedActivation is null ||
                recordedActivation.ActiveRevision != snapshot.Revision)
            {
                throw new InvalidOperationException(
                    haTakeover
                        ? "Persisted revision changed before HA takeover could be committed."
                        : "Published revision changed before activation could be committed.");
            }

            if (driverInteractions is not null && preparedInteractions is not null)
            {
                driverInteractions.Commit(preparedInteractions);
                interactionsCommitted = true;
            }
        }

        RuntimeActivationResult runtimeResult;
        try
        {
            if (eventBus is not null && configuration is not null)
        {
            var scripts = ServerScriptRuntimeManager.GetShared(
                runtime,
                eventBus,
                configuration,
                driverInteractions);

            if (operationalEvents is not null)
                ServerScriptOperationalEventBridge.Bind(
                    scripts,
                    operationalEvents,
                    highAvailability is null
                        ? null
                        : () => highAvailability.CanOwnIndustrialEffects());

            if (driverInteractions is not null && richCommands is not null && authorization is not null && audit is not null)
                ServerScriptRichCommandBridge.Bind(
                    scripts,
                    driverInteractions,
                    richCommands,
                    authorization,
                    audit);

            runtimeResult = haTakeover
                ? await scripts.ActivateRuntimeForHaTakeoverAsync(
                    snapshot.ProjectKey,
                    snapshot.Revision,
                    package,
                    CommitAsync,
                    cancellationToken)
                : await scripts.ActivateRuntimeAsync(
                    snapshot.ProjectKey,
                    snapshot.Revision,
                    package,
                    CommitAsync,
                    cancellationToken);
        }
        else
        {
            EnsureNoServerScriptsWithoutHost(package);
            runtimeResult = haTakeover
                ? await runtime.ActivateForHaTakeoverAsync(
                    snapshot.ProjectKey,
                    snapshot.Revision,
                    package,
                    CommitAsync,
                    cancellationToken)
                : await runtime.ActivateAsync(
                    snapshot.ProjectKey,
                    snapshot.Revision,
                    package,
                    CommitAsync,
                    cancellationToken);
        }
        }
        catch
        {
            if (interactionsCommitted && driverInteractions is not null && previousInteractions is not null)
                driverInteractions.Commit(previousInteractions);
            throw;
        }

        if (!runtimeResult.Activated &&
            interactionsCommitted &&
            driverInteractions is not null &&
            previousInteractions is not null)
        {
            driverInteractions.Commit(previousInteractions);
            interactionsCommitted = false;
        }

        // The Active revision is the Runtime application authority. Only after a
        // successful committed activation may its Engineering Lock state replace the
        // protection state of the currently running application.
        if (runtimeResult.Activated && recordedActivation is not null)
            EngineeringLockAccess.Replace(exchange, package.EngineeringLock);

        var lifecycle = await persistence.GetLifecycleAsync(
            snapshot.ProjectKey,
            CancellationToken.None);

        return new PublishedRuntimeActivationOutcome(
            snapshot,
            runtimeResult,
            recordedActivation,
            lifecycle);
    }

    private static RuntimeActivationResult InvalidInteractionGraph(
        EngineeringProjectSnapshot snapshot,
        string message) =>
        new(
            snapshot.ProjectKey,
            snapshot.Revision,
            Activated: false,
            Array.Empty<Scada.DriverHost.Engineering.EngineeringDriverIssue>(),
            [
                new RuntimeActivationIssue(
                    "DRIVER_INTERACTION_ACTIVE_GRAPH_INVALID",
                    message,
                    IsError: true)
            ]);

    private EngineeringPackage ParseAndValidate(EngineeringProjectSnapshot snapshot)
    {
        var package = exchange.ParseJson(snapshot.EngineeringJson);

        if (!snapshot.EngineeringSchema.Equals(
                package.Schema,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Stored engineering schema '{snapshot.EngineeringSchema}' does not match payload schema '{package.Schema}'.");
        }

        if (snapshot.EngineeringSchemaVersion != package.SchemaVersion)
        {
            throw new InvalidDataException(
                $"Stored engineering schema version {snapshot.EngineeringSchemaVersion} does not match payload version {package.SchemaVersion}.");
        }

        return package;
    }

    private static void EnsureNoServerScriptsWithoutHost(EngineeringPackage package)
    {
        if (package.Scripts?.Any(script =>
                script.Enabled &&
                script.Scope == Scada.Engineering.Scripts.ScriptEngineeringScope.Server) == true)
        {
            throw new InvalidOperationException(
                "Server Script runtime host dependencies are unavailable for this activation.");
        }
    }
}
