using Scada.Api.Licensing;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Abstractions;
using Scada.Core.Product.Licensing;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
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
    GatewayEngineeringRuntimeCoordinator? operationalEvents = null) : IPersistedRuntimeRecoveryService
{
    public const string RecoveryDeniedIssueCode = "PERSISTED_RUNTIME_RECOVERY_DENIED";
    public const string TransitionPendingDiagnostic =
        "Persisted Runtime recovery is denied while a product authority transition is pending.";
    public const string InvalidLicenseDiagnostic =
        "Persisted Runtime recovery is denied because the installed product license is invalid.";
    public const string DemoAnchorMissingDiagnostic =
        "Persisted Runtime recovery is denied because Demo authority has no durable start anchor.";
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

        var package = ParseAndValidate(snapshot);

        RuntimeActivationResult result;
        if (eventBus is not null && configuration is not null)
        {
            var scripts = ServerScriptRuntimeManager.GetShared(
                runtime,
                eventBus,
                configuration);

            if (operationalEvents is not null)
                ServerScriptOperationalEventBridge.Bind(scripts, operationalEvents);

            result = await scripts.ActivateRuntimeAsync(
                snapshot.ProjectKey,
                snapshot.Revision,
                package,
                cancellationToken);
        }
        else
        {
            EnsureNoServerScriptsWithoutHost(package);
            result = await runtime.ActivateAsync(
                snapshot.ProjectKey,
                snapshot.Revision,
                package,
                cancellationToken);
        }

        // Restart/recovery must restore the protection state carried by the durable
        // Active revision. A failed runtime recovery must not overwrite the current
        // in-memory protection state.
        if (result.Activated)
            EngineeringLockAccess.Replace(exchange, package.EngineeringLock);

        return new PersistedRuntimeRecoveryResult(
            snapshot.ProjectKey,
            activation.ActiveRevision,
            true,
            result);
    }

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
