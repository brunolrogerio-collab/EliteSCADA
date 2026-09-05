namespace Scada.Engineering.Persistence;

public sealed record EngineeringProjectCatalogEntry(
    string ProjectKey,
    string ProjectName,
    long LatestRevision,
    DateTimeOffset LastSavedAtUtc);

public interface IEngineeringProjectCatalog
{
    Task<IReadOnlyCollection<EngineeringProjectCatalogEntry>> ListAsync(
        CancellationToken cancellationToken = default);

    async Task<bool> HasAnyAsync(CancellationToken cancellationToken = default) =>
        (await ListAsync(cancellationToken)).Count > 0;
}
