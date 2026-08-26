using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Engineering.Persistence;

public sealed record EngineeringPersistencePreview(
    EngineeringProjectSnapshot Snapshot,
    ImportPreview Preview);

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

    Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
        string projectKey,
        int limit = 50,
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

    public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
        string projectKey,
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        _store.ListRevisionsAsync(projectKey, limit, cancellationToken);

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
