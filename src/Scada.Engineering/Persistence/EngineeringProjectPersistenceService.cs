using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

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

    public EngineeringProjectPersistenceService(
        IEngineeringExchangeService exchange,
        IEngineeringProjectStore store)
    {
        _exchange = exchange;
        _store = store;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        _store.InitializeAsync(cancellationToken);

    public async Task<EngineeringProjectSnapshot> SaveCurrentAsync(
        string projectKey,
        string projectName,
        string? savedBy = null,
        CancellationToken cancellationToken = default)
    {
        var package = _exchange.ExportPackage();
        var json = _exchange.ExportJson(indented: false);

        return await _store.SaveAsync(
            projectKey,
            projectName,
            package.Schema,
            package.SchemaVersion,
            json,
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

        var preview = PreviewSnapshot(snapshot, ImportMode.CreateAndUpdate).Preview;
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
        return snapshot is null ? null : PreviewSnapshot(snapshot, mode);
    }

    public async Task<EngineeringPersistencePreview?> PreviewRevisionAsync(
        string projectKey,
        long revision,
        ImportMode mode,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadRevisionAsync(projectKey, revision, cancellationToken);
        return snapshot is null ? null : PreviewSnapshot(snapshot, mode);
    }

    public async Task<ImportResult?> ApplyLatestAsync(
        string projectKey,
        ImportMode mode,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadLatestAsync(projectKey, cancellationToken);
        return snapshot is null ? null : ApplySnapshot(snapshot, mode);
    }

    public async Task<ImportResult?> ApplyRevisionAsync(
        string projectKey,
        long revision,
        ImportMode mode,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadRevisionAsync(projectKey, revision, cancellationToken);
        return snapshot is null ? null : ApplySnapshot(snapshot, mode);
    }

    private EngineeringPersistencePreview PreviewSnapshot(
        EngineeringProjectSnapshot snapshot,
        ImportMode mode)
    {
        var package = ParseAndValidate(snapshot);
        return new EngineeringPersistencePreview(snapshot, _exchange.Preview(package, mode));
    }

    private ImportResult ApplySnapshot(
        EngineeringProjectSnapshot snapshot,
        ImportMode mode)
    {
        var package = ParseAndValidate(snapshot);
        return _exchange.Apply(package, mode);
    }

    private EngineeringPackage ParseAndValidate(EngineeringProjectSnapshot snapshot)
    {
        var package = _exchange.ParseJson(snapshot.EngineeringJson);

        if (!snapshot.EngineeringSchema.Equals(package.Schema, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Stored engineering schema '{snapshot.EngineeringSchema}' does not match payload schema '{package.Schema}'.");

        if (snapshot.EngineeringSchemaVersion != package.SchemaVersion)
            throw new InvalidDataException(
                $"Stored engineering schema version {snapshot.EngineeringSchemaVersion} does not match payload version {package.SchemaVersion}.");

        return package;
    }
}
