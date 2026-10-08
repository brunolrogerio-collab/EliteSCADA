using Scada.Api.Licensing;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Abstractions;
using Scada.Core.Product.Licensing;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Interactions;
using Scada.Engineering.Persistence;
using Scada.Security.Authorization;

namespace Scada.Api.Persistence;

public sealed record PersistedRuntimeRecoveryResult(
    string ProjectKey,
    long? PersistedActiveRevision,
    bool Found,
    RuntimeActivationResult? Runtime)
{
    public bool Recovered => Found && Runtime?.Activated == true;

    public bool IsExpectedAuthorityDenial =>
        Found &&
        Runtime?.RuntimeIssues.Any(issue =>
            issue.IsError &&
            (issue.Code == PersistedRuntimeRecoveryService.RecoveryDeniedIssueCode ||
             issue.Code == ProductLicensedRuntimeCoordinator.EntitlementDeniedIssueCode ||
             issue.Code == HighAvailabilityRuntimeCoordinator.AuthorityDeniedIssueCode)) == true;
}

public interface IPersistedRuntimeRecoveryService
{
    Task<PersistedRuntimeRecoveryResult> RecoverAsync(
        string projectKey,
        CancellationToken cancellationToken = default);
}

public sealed class PersistedRuntimeRecoveryService(
    IEngineeringProjectPersistenceService persistence,
    IEngineeringExchangeService exchange,
    IEngineeringRuntimeCoordinator runtime,
    IProductLicenseService licensing,
    IRuntimeSessionLeaseStore authorityStore,
    IScadaEventBus? eventBus = null,
    IConfiguration? configuration = null,
    GatewayEngineeringRuntimeCoordinator? operationalEvents = null,
    RuntimeHighAvailabilityService? highAvailability = null,
    RuntimeHaProtectionCoordinator? highAvailabilityProtection = null,
    ActiveDriverInteractionRuntimeCatalog? driverInteractions = null,
    IRichCommandRuntime? richCommands = null,
    ApiAuthorizationService? authorization = null,
    ApiAuditService? audit = null) : IPersistedRuntimeRecoveryService
{
    public const string RecoveryDeniedIssueCode = "PERSISTED_RUNTIME_RECOVERY_DENIED";
    public const string TransitionPendingDiagnostic =
        "Persisted Runtime recovery is denied while a product authority transition is pending.";
    public const string InvalidLicenseDiagnostic =
        "Persisted Runtime recovery is denied because the installed product license is invalid.";
    public const string DemoAnchorMissingDiagnostic =
        "Persisted Runtime recovery is denied because Demo authority has no durable start anchor.";
    public const string DemoAnchorMismatchDiagnostic =
        "Persisted Runtime recovery is denied because the Active revision is not bound to the current durable Demo session.";
    public async Task<PersistedRuntimeRecoveryResult> RecoverAsync(
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ArgumentException("Project key is required.", nameof(projectKey));

        var activation = await persistence.GetActivationAsync(
            projectKey,
            cancellationToken);
        if (activation is null)
            return new PersistedRuntimeRecoveryResult(
                projectKey.Trim(),
                null,
                false,
                null);

        var snapshot = await persistence.LoadActiveAsync(
            projectKey,
            cancellationToken);
        if (snapshot is null)
        {
            return new PersistedRuntimeRecoveryResult(
                projectKey.Trim(),
                activation.ActiveRevision,
                false,
                null);
        }

        var authority = await authorityStore.GetAuthorityStateAsync(cancellationToken);
        var verification = licensing.CurrentVerification;

        if (authority.TransitionPending)
        {
            return RecoveryDenied(
                snapshot,
                activation.ActiveRevision,
                TransitionPendingDiagnostic);
        }

        if (verification.State == LicenseState.Invalid)
        {
            return RecoveryDenied(
                snapshot,
                activation.ActiveRevision,
                InvalidLicenseDiagnostic);
        }

        if (verification.State == LicenseState.Demo &&
            !authority.DemoStartedAtUtc.HasValue)
        {
            return RecoveryDenied(
                snapshot,
                activation.ActiveRevision,
                DemoAnchorMissingDiagnostic);
        }

        if (verification.State == LicenseState.Demo &&
            activation.DemoStartedAtUtc != authority.DemoStartedAtUtc)
        {
            return RecoveryDenied(
                snapshot,
                activation.ActiveRevision,
                DemoAnchorMismatchDiagnostic);
        }

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
            return InvalidInteractionGraph(
                snapshot,
                activation.ActiveRevision,
                ex.Message);
        }

        var recoverAsHaActive = highAvailability?.Enabled == true &&
            (highAvailability.CanOwnIndustrialEffects() ||
             highAvailabilityProtection?.CanActivateLocalHaTakeover() == true);

        var previousInteractions = driverInteractions?.CapturePrepared();
        var interactionsCommitted = false;
        Task CommitInteractionsAsync(RuntimeActivationCommitContext _, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (driverInteractions is not null && preparedInteractions is not null)
            {
                driverInteractions.Commit(preparedInteractions);
                interactionsCommitted = true;
            }

            return Task.CompletedTask;
        }

        RuntimeActivationResult result;
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

                result = recoverAsHaActive
                    ? await scripts.ActivateRuntimeForHaTakeoverAsync(
                        snapshot.ProjectKey,
                        snapshot.Revision,
                        package,
                        CommitInteractionsAsync,
                        cancellationToken)
                    : await scripts.ActivateRuntimeAsync(
                        snapshot.ProjectKey,
                        snapshot.Revision,
                        package,
                        cancellationToken);
            }
            else
            {
                EnsureNoServerScriptsWithoutHost(package);
                result = recoverAsHaActive
                    ? await runtime.ActivateForHaTakeoverAsync(
                        snapshot.ProjectKey,
                        snapshot.Revision,
                        package,
                        CommitInteractionsAsync,
                        cancellationToken)
                    : await runtime.ActivateAsync(
                        snapshot.ProjectKey,
                        snapshot.Revision,
                        package,
                        cancellationToken);
            }
        }
        catch
        {
            if (interactionsCommitted && driverInteractions is not null && previousInteractions is not null)
                driverInteractions.Commit(previousInteractions);
            throw;
        }

        // Ordinary recovery must use the no-callback Runtime path so a persisted
        // recovery cannot renew an expired Demo session. Publish its Active graph
        // only after Runtime activation succeeds. HA takeover keeps its existing
        // transactional callback path above.
        if (result.Activated &&
            !recoverAsHaActive &&
            driverInteractions is not null &&
            preparedInteractions is not null)
        {
            driverInteractions.Commit(preparedInteractions);
            interactionsCommitted = true;
        }

        if (!result.Activated &&
            interactionsCommitted &&
            driverInteractions is not null &&
            previousInteractions is not null)
        {
            driverInteractions.Commit(previousInteractions);
        }

        // Restart/recovery must restore the protection state carried by the durable
        // Active revision. A failed runtime recovery must not overwrite the current
        // in-memory protection state.
        if (result.Activated)
        {
            EngineeringLockAccess.Replace(exchange, package.EngineeringLock);
        }

        return new PersistedRuntimeRecoveryResult(
            snapshot.ProjectKey,
            activation.ActiveRevision,
            true,
            result);
    }

    private static PersistedRuntimeRecoveryResult InvalidInteractionGraph(
        EngineeringProjectSnapshot snapshot,
        long persistedActiveRevision,
        string message) =>
        new(
            snapshot.ProjectKey,
            persistedActiveRevision,
            Found: true,
            Runtime: new RuntimeActivationResult(
                snapshot.ProjectKey,
                snapshot.Revision,
                Activated: false,
                Array.Empty<EngineeringDriverIssue>(),
                [
                    new RuntimeActivationIssue(
                        "DRIVER_INTERACTION_ACTIVE_GRAPH_INVALID",
                        message,
                        IsError: true)
                ]));

    private static PersistedRuntimeRecoveryResult RecoveryDenied(
        EngineeringProjectSnapshot snapshot,
        long persistedActiveRevision,
        string diagnostic) =>
        new(
            snapshot.ProjectKey,
            persistedActiveRevision,
            Found: true,
            Runtime: new RuntimeActivationResult(
                snapshot.ProjectKey,
                snapshot.Revision,
                Activated: false,
                Array.Empty<EngineeringDriverIssue>(),
                new[]
                {
                    new RuntimeActivationIssue(
                        RecoveryDeniedIssueCode,
                        diagnostic,
                        IsError: true)
                }));

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
                "Server Script runtime host dependencies are unavailable for this recovery.");
        }
    }
}
