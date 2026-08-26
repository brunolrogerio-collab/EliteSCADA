using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Simulation;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
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
}

public sealed class PublishedRuntimeActivationService(
    IEngineeringProjectPersistenceService persistence,
    IEngineeringExchangeService exchange,
    IEngineeringRuntimeCoordinator runtime,
    SimulationDriver simulationFallback) : IPublishedRuntimeActivationService
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

        var package = ParseAndValidate(snapshot);
        EngineeringProjectActivation? recordedActivation = null;
        var fallbackWasRunning = simulationFallback.Status.State is DriverState.Starting or DriverState.Running;

        var runtimeResult = await runtime.ActivateAsync(
            snapshot.ProjectKey,
            snapshot.Revision,
            package,
            async (_, ct) =>
            {
                try
                {
                    if (fallbackWasRunning)
                        await simulationFallback.StopAsync(ct);

                    recordedActivation = await persistence.RecordActivationAsync(
                        snapshot.ProjectKey,
                        snapshot.Revision,
                        activatedBy,
                        ct);

                    if (recordedActivation is null || recordedActivation.ActiveRevision != snapshot.Revision)
                        throw new InvalidOperationException(
                            "Published revision changed before activation could be committed.");
                }
                catch
                {
                    if (fallbackWasRunning && simulationFallback.Status.State != DriverState.Running)
                        await simulationFallback.StartAsync(CancellationToken.None);
                    throw;
                }
            },
            cancellationToken);

        var lifecycle = await persistence.GetLifecycleAsync(snapshot.ProjectKey, CancellationToken.None);
        return new PublishedRuntimeActivationOutcome(snapshot, runtimeResult, recordedActivation, lifecycle);
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
