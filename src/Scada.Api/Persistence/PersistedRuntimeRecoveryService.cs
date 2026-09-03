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
    IScadaEventBus eventBus,
    IConfiguration configuration) : IPersistedRuntimeRecoveryService
{
    public async Task<PersistedRuntimeRecoveryResult> RecoverAsync(
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ArgumentException("Project key is required.", nameof(projectKey));

        var activation = await persistence.GetActivationAsync(projectKey, cancellationToken);
        if (activation is null)
            return new PersistedRuntimeRecoveryResult(projectKey.Trim(), null, false, null);

        var snapshot = await persistence.LoadActiveAsync(projectKey, cancellationToken);
        if (snapshot is null)
        {
            return new PersistedRuntimeRecoveryResult(
                projectKey.Trim(),
                activation.ActiveRevision,
                false,
                null);
        }

        var package = ParseAndValidate(snapshot);
        var result = await runtime.ActivateAsync(
            snapshot.ProjectKey,
            snapshot.Revision,
            package,
            cancellationToken);

        if (result.Activated)
        {
            await ServerScriptRuntimeManager
                .GetShared(runtime, eventBus, configuration)
                .ActivateAsync(snapshot.ProjectKey, snapshot.Revision, package.Scripts, cancellationToken);
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

        if (!snapshot.EngineeringSchema.Equals(package.Schema, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Stored engineering schema '{snapshot.EngineeringSchema}' does not match payload schema '{package.Schema}'.");

        if (snapshot.EngineeringSchemaVersion != package.SchemaVersion)
            throw new InvalidDataException(
                $"Stored engineering schema version {snapshot.EngineeringSchemaVersion} does not match payload version {package.SchemaVersion}.");

        return package;
    }
}
