using System.Security.Cryptography;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.VisualAssets;

namespace Scada.Engineering.Persistence;

public sealed record EngineeringPersistencePreview(
    EngineeringProjectSnapshot Snapshot,
    ImportPreview Preview);

public sealed record EngineeringPublicationResult(
    EngineeringProjectSnapshot Snapshot,
    ImportPreview Preview,
    EngineeringProjectPublication? Publication)
{
    public bool Published => Publication is not null && Preview.CanApply;
}

public interface IEngineeringProjectPersistenceService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<EngineeringProjectSnapshot> SaveCurrentAsync(
        string projectKey,
        string projectName,
        string? savedBy = null,
        CancellationToken cancellationToken = default);

    Task<EngineeringProjectSnapshot> SaveCurrentDerivedAsync(
        string projectKey,
        string projectName,
        long? basedOnRevision,
        string? savedBy = null,
        CancellationToken cancellationToken = default);

    Task<EngineeringProjectSnapshot?> LoadLatestAsync(
        string projectKey,
        CancellationToken cancellationToken = default);

    Task<EngineeringProjectSnapshot?> LoadPublishedAsync(
        string projectKey,
        CancellationToken cancellationToken = default);

    Task<EngineeringProjectSnapshot?> LoadActiveAsync(
        string projectKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
        string projectKey,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<EngineeringProjectLifecycle> GetLifecycleAsync(
        string projectKey,
        CancellationToken cancellationToken = default);

    Task<EngineeringProjectActivation?> GetActivationAsync(
        string projectKey,
        CancellationToken cancellationToken = default);

    Task<EngineeringProjectActivation?> RecordActivationAsync(
        string projectKey,
        long revision,
        string? activatedBy = null,
        CancellationToken cancellationToken = default);

    Task<EngineeringPublicationResult?> PublishRevisionAsync(
        string projectKey,
        long revision,
        string? publishedBy = null,
        CancellationToken cancellationToken = default);

    Task<EngineeringPersistencePreview?> PreviewLatestAsync(
        string projectKey,
        ImportMode mode,
        CancellationToken cancellationToken = default);

    Task<EngineeringPersistencePreview?> PreviewRevisionAsync(
        string projectKey,
        long revision,
        ImportMode mode,
        CancellationToken cancellationToken = default);

    Task<ImportResult?> ApplyLatestAsync(
        string projectKey,
        ImportMode mode,
        CancellationToken cancellationToken = default);

    Task<ImportResult?> ApplyRevisionAsync(
        string projectKey,
        long revision,
        ImportMode mode,
        CancellationToken cancellationToken = default);
}

public sealed class EngineeringProjectPersistenceService : IEngineeringProjectPersistenceService
{
    private readonly IEngineeringExchangeService _exchange;
    private readonly IEngineeringProjectStore _store;
    private readonly IVisualAssetEngineeringRegistry? _visualAssets;

    public EngineeringProjectPersistenceService(
        IEngineeringExchangeService exchange,
        IEngineeringProjectStore store,
        IVisualAssetEngineeringRegistry? visualAssets = null)
    {
        _exchange = exchange;
        _store = store;
        _visualAssets = visualAssets;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        _store.InitializeAsync(cancellationToken);

    public Task<EngineeringProjectSnapshot> SaveCurrentAsync(
        string projectKey,
        string projectName,
        string? savedBy = null,
        CancellationToken cancellationToken = default) =>
        SaveCurrentDerivedAsync(
            projectKey,
            projectName,
            null,
            savedBy,
            cancellationToken);

    public async Task<EngineeringProjectSnapshot> SaveCurrentDerivedAsync(
        string projectKey,
        string projectName,
        long? basedOnRevision,
        string? savedBy = null,
        CancellationToken cancellationToken = default)
    {
        var package = _exchange.ExportPackage();
        var json = _exchange.ExportJson(indented: false);
        var revisionAssets = BuildCurrentRevisionAssets(package);

        return await _store.SaveDerivedWithAssetsAsync(
            projectKey,
            projectName,
            package.Schema,
            package.SchemaVersion,
            json,
            basedOnRevision,
            revisionAssets,
            savedBy,
            cancellationToken);
    }

    public Task<EngineeringProjectSnapshot?> LoadLatestAsync(
        string projectKey,
        CancellationToken cancellationToken = default) =>
        _store.LoadLatestAsync(projectKey, cancellationToken);

    public async Task<EngineeringProjectSnapshot?> LoadPublishedAsync(
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        var publication = await _store.GetPublicationAsync(projectKey, cancellationToken);
        return publication is null
            ? null
            : await _store.LoadRevisionAsync(projectKey, publication.PublishedRevision, cancellationToken);
    }

    public async Task<EngineeringProjectSnapshot?> LoadActiveAsync(
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        var activation = await _store.GetActivationAsync(projectKey, cancellationToken);
        return activation is null
            ? null
            : await _store.LoadRevisionAsync(projectKey, activation.ActiveRevision, cancellationToken);
    }

    public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
        string projectKey,
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        _store.ListRevisionsAsync(projectKey, limit, cancellationToken);

    public async Task<EngineeringProjectLifecycle> GetLifecycleAsync(
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        var latest = await _store.LoadLatestAsync(projectKey, cancellationToken);
        var publication = await _store.GetPublicationAsync(projectKey, cancellationToken);
        var activation = await _store.GetActivationAsync(projectKey, cancellationToken);

        var status = latest is null
            ? EngineeringProjectLifecycleStatus.Empty
            : publication is null
                ? EngineeringProjectLifecycleStatus.Draft
                : publication.PublishedRevision == latest.Revision
                    ? EngineeringProjectLifecycleStatus.Published
                    : EngineeringProjectLifecycleStatus.ChangesPending;

        var runtimeStatus = publication is null
            ? EngineeringRuntimeStatus.Inactive
            : activation is not null && activation.ActiveRevision == publication.PublishedRevision
                ? EngineeringRuntimeStatus.Active
                : EngineeringRuntimeStatus.ActivationPending;

        return new EngineeringProjectLifecycle(
            projectKey,
            status,
            latest?.Revision,
            publication?.PublishedRevision,
            publication?.PublishedAtUtc,
            publication?.PublishedBy,
            runtimeStatus,
            activation?.ActiveRevision,
            activation?.ActivatedAtUtc,
            activation?.ActivatedBy);
    }

    public Task<EngineeringProjectActivation?> GetActivationAsync(
        string projectKey,
        CancellationToken cancellationToken = default) =>
        _store.GetActivationAsync(projectKey, cancellationToken);

    public Task<EngineeringProjectActivation?> RecordActivationAsync(
        string projectKey,
        long revision,
        string? activatedBy = null,
        CancellationToken cancellationToken = default) =>
        _store.RecordActivationAsync(projectKey, revision, activatedBy, cancellationToken);

    public async Task<EngineeringPublicationResult?> PublishRevisionAsync(
        string projectKey,
        long revision,
        string? publishedBy = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadRevisionAsync(projectKey, revision, cancellationToken);
        if (snapshot is null) return null;

        var preview = (await PreviewSnapshotAsync(snapshot, ImportMode.CreateAndUpdate, cancellationToken)).Preview;
        if (!preview.CanApply)
            return new EngineeringPublicationResult(snapshot, preview, null);

        var publication = await _store.PublishRevisionAsync(
            projectKey,
            revision,
            publishedBy,
            cancellationToken);

        return new EngineeringPublicationResult(snapshot, preview, publication);
    }

    public async Task<EngineeringPersistencePreview?> PreviewLatestAsync(
        string projectKey,
        ImportMode mode,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadLatestAsync(projectKey, cancellationToken);
        return snapshot is null
            ? null
            : await PreviewSnapshotAsync(snapshot, mode, cancellationToken);
    }

    public async Task<EngineeringPersistencePreview?> PreviewRevisionAsync(
        string projectKey,
        long revision,
        ImportMode mode,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadRevisionAsync(projectKey, revision, cancellationToken);
        return snapshot is null
            ? null
            : await PreviewSnapshotAsync(snapshot, mode, cancellationToken);
    }

    public async Task<ImportResult?> ApplyLatestAsync(
        string projectKey,
        ImportMode mode,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadLatestAsync(projectKey, cancellationToken);
        return snapshot is null
            ? null
            : await ApplySnapshotAsync(snapshot, mode, cancellationToken);
    }

    public async Task<ImportResult?> ApplyRevisionAsync(
        string projectKey,
        long revision,
        ImportMode mode,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadRevisionAsync(projectKey, revision, cancellationToken);
        return snapshot is null
            ? null
            : await ApplySnapshotAsync(snapshot, mode, cancellationToken);
    }

    private async Task<EngineeringPersistencePreview> PreviewSnapshotAsync(
        EngineeringProjectSnapshot snapshot,
        ImportMode mode,
        CancellationToken cancellationToken)
    {
        var (package, context) = await ParseAndValidateAsync(snapshot, cancellationToken);
        return new EngineeringPersistencePreview(snapshot, _exchange.Preview(package, mode, context));
    }

    private async Task<ImportResult> ApplySnapshotAsync(
        EngineeringProjectSnapshot snapshot,
        ImportMode mode,
        CancellationToken cancellationToken)
    {
        var (package, context) = await ParseAndValidateAsync(snapshot, cancellationToken);
        return _exchange.Apply(package, mode, context);
    }

    private async Task<(EngineeringPackage Package, EngineeringImportContext Context)> ParseAndValidateAsync(
        EngineeringProjectSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var package = _exchange.ParseJson(snapshot.EngineeringJson);

        if (!snapshot.EngineeringSchema.Equals(package.Schema, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Stored engineering schema '{snapshot.EngineeringSchema}' does not match payload schema '{package.Schema}'.");

        if (snapshot.EngineeringSchemaVersion != package.SchemaVersion)
            throw new InvalidDataException(
                $"Stored engineering schema version {snapshot.EngineeringSchemaVersion} does not match payload version {package.SchemaVersion}.");

        var context = await BuildRevisionImportContextAsync(snapshot, package, cancellationToken);
        return (package, context);
    }

    private IReadOnlyCollection<EngineeringRevisionAssetPayload> BuildCurrentRevisionAssets(EngineeringPackage package)
    {
        var assets = package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>();
        if (assets.Count == 0)
            return Array.Empty<EngineeringRevisionAssetPayload>();
        if (_visualAssets is null)
            throw new InvalidOperationException("Visual asset payload registry is required to save a project containing visual assets.");

        var result = new List<EngineeringRevisionAssetPayload>(assets.Count);
        foreach (var asset in assets)
        {
            if (!asset.Id.HasValue || asset.Id.Value == Guid.Empty)
                throw new InvalidDataException($"Visual asset '{asset.Key}' requires a stable ID before revision save.");

            var payload = _visualAssets.FindPayload(asset.Sha256)
                ?? throw new InvalidDataException($"Visual asset '{asset.Key}' payload '{asset.Sha256}' is unavailable.");
            ValidatePayloadAgainstMetadata(asset, payload);
            result.Add(new EngineeringRevisionAssetPayload(
                asset.Id.Value,
                asset.Sha256.ToLowerInvariant(),
                asset.MediaType,
                payload.Content.ToArray()));
        }

        return result;
    }

    private async Task<EngineeringImportContext> BuildRevisionImportContextAsync(
        EngineeringProjectSnapshot snapshot,
        EngineeringPackage package,
        CancellationToken cancellationToken)
    {
        var metadata = package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>();
        var stored = await _store.LoadRevisionAssetsAsync(
            snapshot.ProjectKey,
            snapshot.Revision,
            cancellationToken);

        if (metadata.Count == 0)
        {
            if (stored.Count != 0)
                throw new InvalidDataException("Stored revision contains unexpected visual asset payload links.");
            return EngineeringImportContext.Empty;
        }

        var byAssetId = stored.ToDictionary(x => x.AssetId);
        if (byAssetId.Count != metadata.Count)
            throw new InvalidDataException("Stored revision visual asset payload count does not match canonical metadata.");

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
            ValidatePayloadAgainstMetadata(asset, payload);

            if (byHash.TryGetValue(payload.Sha256, out var existing) &&
                (!existing.MediaType.Equals(payload.MediaType, StringComparison.OrdinalIgnoreCase) ||
                 !existing.Content.AsSpan().SequenceEqual(payload.Content)))
                throw new InvalidDataException($"Stored visual asset hash '{payload.Sha256}' maps to conflicting payloads.");

            byHash[payload.Sha256] = payload;
        }

        return new EngineeringImportContext(byHash);
    }

    private static void ValidatePayloadAgainstMetadata(
        VisualAssetEngineeringDto metadata,
        VisualAssetPayload payload)
    {
        if (!payload.MediaType.Equals(metadata.MediaType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Visual asset '{metadata.Key}' payload media type does not match canonical metadata.");
        if (payload.ByteLength != metadata.ByteLength)
            throw new InvalidDataException($"Visual asset '{metadata.Key}' payload length does not match canonical metadata.");

        var actualHash = Convert.ToHexString(SHA256.HashData(payload.Content)).ToLowerInvariant();
        if (!actualHash.Equals(metadata.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Visual asset '{metadata.Key}' payload hash does not match canonical metadata.");
    }
}