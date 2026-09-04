using Scada.Api.Runtime;
using Scada.Core.Abstractions;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;

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
    IScadaEventBus? eventBus = null,
    IConfiguration? configuration = null) : IPersistedRuntimeRecoveryService
{
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

        var package = ParseAndValidate(snapshot);

        RuntimeActivationResult result;
        if (eventBus is not null && configuration is not null)
        {
            var scripts = ServerScriptRuntimeManager.GetShared(
                runtime,
                eventBus,
                configuration);
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

        return new PersistedRuntimeRecoveryResult(
            snapshot.ProjectKey,
            activation.ActiveRevision,
            true,
            result);
    }

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
