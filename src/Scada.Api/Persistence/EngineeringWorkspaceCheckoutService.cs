using Scada.Api.Runtime;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.Reports;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;

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
    EngineeringWorkspace workspace,
    IGatewayEngineeringRegistry? gateways = null,
    IReportEngineeringRegistry? reports = null) : IEngineeringWorkspaceCheckoutService
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

            var (package, importContext) = await ParseAndValidateAsync(snapshot, cancellationToken);
            var preview = PreviewInIsolation(package, importContext);
            if (!preview.CanApply)
            {
                return new EngineeringWorkspaceCheckoutOutcome(
                    snapshot,
                    preview,
                    null,
                    workspace.Describe());
            }

            await using var mutation = await workspace.AcquireMutationAsync(cancellationToken: cancellationToken);

            var backupJson = exchange.ExportJson(indented: false);
            var backupPackage = exchange.ParseJson(backupJson);
            var backupHashes = (backupPackage.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>())
                .Select(x => x.Sha256)
                .ToArray();
            var backupContext = new EngineeringImportContext(
                workspace.VisualAssets.SnapshotPayloads(backupHashes));
            var backupDescriptor = workspace.Describe();
            ImportResult apply;

            ClearWorkspace();
            try
            {
                apply = exchange.Apply(package, ImportMode.CreateAndUpdate, importContext);
            }
            catch
            {
                RestoreBackup(backupPackage, backupContext, backupDescriptor);
                throw;
            }

            if (apply.Issues.Any(x => x.IsError))
            {
                RestoreBackup(backupPackage, backupContext, backupDescriptor);
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

    private async Task<(EngineeringPackage Package, EngineeringImportContext Context)> ParseAndValidateAsync(
        EngineeringProjectSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var package = exchange.ParseJson(snapshot.EngineeringJson);

        if (!snapshot.EngineeringSchema.Equals(package.Schema, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Stored engineering schema '{snapshot.EngineeringSchema}' does not match payload schema '{package.Schema}'.");

        if (snapshot.EngineeringSchemaVersion != package.SchemaVersion)
            throw new InvalidDataException(
                $"Stored engineering schema version {snapshot.EngineeringSchemaVersion} does not match payload version {package.SchemaVersion}.");

        var metadata = package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>();
        var stored = await store.LoadRevisionAssetsAsync(
            snapshot.ProjectKey,
            snapshot.Revision,
            cancellationToken);

        if (metadata.Count == 0)
        {
            if (stored.Count != 0)
                throw new InvalidDataException("Stored revision contains unexpected visual asset payload links.");
            return (package, EngineeringImportContext.Empty);
        }

        if (stored.Count != metadata.Count)
            throw new InvalidDataException("Stored revision visual asset payload count does not match canonical metadata.");

        var byAssetId = stored.ToDictionary(x => x.AssetId);
        var byHash = new Dictionary<string, VisualAssetPayload>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in metadata)
        {
            if (!asset.Id.HasValue || asset.Id.Value == Guid.Empty)
                throw new InvalidDataException($"Stored visual asset '{asset.Key}' is missing a stable ID.");
            if (!byAssetId.TryGetValue(asset.Id.Value, out var storedPayload))
                throw new InvalidDataException($"Stored visual asset '{asset.Key}' payload link is missing.");
            if (!storedPayload.Sha256.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Stored visual asset '{asset.Key}' payload hash does not match canonical metadata.");

            var payload = new VisualAssetPayload(
                storedPayload.Sha256.ToLowerInvariant(),
                storedPayload.MediaType,
                storedPayload.Content.ToArray());

            if (byHash.TryGetValue(payload.Sha256, out var existing) &&
                (!existing.MediaType.Equals(payload.MediaType, StringComparison.OrdinalIgnoreCase) ||
                 !existing.Content.AsSpan().SequenceEqual(payload.Content)))
                throw new InvalidDataException($"Stored visual asset hash '{payload.Sha256}' maps to conflicting payloads.");

            byHash[payload.Sha256] = payload;
        }

        return (package, new EngineeringImportContext(byHash));
    }

    private static ImportPreview PreviewInIsolation(
        EngineeringPackage package,
        EngineeringImportContext context)
    {
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var isolated = new EngineeringExchangeService(
            new InMemoryTagRegistry(),
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry());

        return isolated.Preview(package, ImportMode.CreateAndUpdate, context);
    }

    private void RestoreBackup(
        EngineeringPackage backupPackage,
        EngineeringImportContext backupContext,
        EngineeringWorkspaceDescriptor backupDescriptor)
    {
        ImportResult? restored = null;
        try
        {
            ClearWorkspace();
            restored = exchange.Apply(backupPackage, ImportMode.CreateAndUpdate, backupContext);
        }
        finally
        {
            workspace.RestoreDescriptor(backupDescriptor);
        }

        if (restored is null || restored.Issues.Any(x => x.IsError))
            throw new InvalidOperationException(
                "Engineering workspace checkout failed and the previous workspace could not be restored cleanly.");
    }

    private void ClearWorkspace()
    {
        workspace.Clear();
        gateways?.Clear();
        reports?.Clear();
    }
}
