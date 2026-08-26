using Scada.Api.Runtime;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.Views;

namespace Scada.Api.Persistence;

public sealed record EngineeringWorkspaceCheckoutOutcome(
    EngineeringProjectSnapshot Snapshot,
    ImportPreview Preview,
    ImportResult? ApplyResult,
    EngineeringWorkspaceDescriptor Workspace)
{
    public bool CheckedOut => Preview.CanApply && ApplyResult is not null && !ApplyResult.Issues.Any(x => x.IsError);
}

public interface IEngineeringWorkspaceCheckoutService
{
    Task<EngineeringWorkspaceCheckoutOutcome?> CheckoutAsync(
        string projectKey,
        long revision,
        CancellationToken cancellationToken = default);
}

public sealed class EngineeringWorkspaceCheckoutService(
    IEngineeringProjectStore store,
    IEngineeringExchangeService exchange,
    EngineeringWorkspace workspace) : IEngineeringWorkspaceCheckoutService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<EngineeringWorkspaceCheckoutOutcome?> CheckoutAsync(
        string projectKey,
        long revision,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ArgumentException("Project key is required.", nameof(projectKey));
        if (revision < 1)
            throw new ArgumentOutOfRangeException(nameof(revision));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await store.LoadRevisionAsync(projectKey.Trim(), revision, cancellationToken);
            if (snapshot is null) return null;

            var package = ParseAndValidate(snapshot);
            var preview = PreviewInIsolation(package);
            if (!preview.CanApply)
            {
                return new EngineeringWorkspaceCheckoutOutcome(
                    snapshot,
                    preview,
                    null,
                    workspace.Describe());
            }

            var backupJson = exchange.ExportJson(indented: false);
            var backupDescriptor = workspace.Describe();
            ImportResult apply;

            workspace.Clear();
            try
            {
                apply = exchange.Apply(package, ImportMode.CreateAndUpdate);
            }
            catch
            {
                RestoreBackup(backupJson, backupDescriptor);
                throw;
            }

            if (apply.Issues.Any(x => x.IsError))
            {
                RestoreBackup(backupJson, backupDescriptor);
                return new EngineeringWorkspaceCheckoutOutcome(
                    snapshot,
                    preview,
                    apply,
                    workspace.Describe());
            }

            workspace.SetCheckout(
                snapshot.ProjectKey,
                snapshot.ProjectName,
                snapshot.Revision,
                snapshot.SavedAtUtc);
            return new EngineeringWorkspaceCheckoutOutcome(
                snapshot,
                preview,
                apply,
                workspace.Describe());
        }
        finally
        {
            _gate.Release();
        }
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

    private static ImportPreview PreviewInIsolation(EngineeringPackage package)
    {
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var isolated = new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry());

        return isolated.Preview(package, ImportMode.CreateAndUpdate);
    }

    private void RestoreBackup(
        string backupJson,
        EngineeringWorkspaceDescriptor backupDescriptor)
    {
        ImportResult? restored = null;
        try
        {
            var backup = exchange.ParseJson(backupJson);
            workspace.Clear();
            restored = exchange.Apply(backup, ImportMode.CreateAndUpdate);
        }
        finally
        {
            workspace.RestoreDescriptor(backupDescriptor);
        }

        if (restored is null || restored.Issues.Any(x => x.IsError))
            throw new InvalidOperationException(
                "Engineering workspace checkout failed and the previous workspace could not be restored cleanly.");
    }
}
