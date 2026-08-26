namespace Scada.Engineering.Persistence;

public sealed record EngineeringProjectSnapshot(
    long Revision,
    string ProjectKey,
    string ProjectName,
    string EngineeringSchema,
    int EngineeringSchemaVersion,
    DateTimeOffset SavedAtUtc,
    string EngineeringJson,
    string? SavedBy = null);

public interface IEngineeringProjectStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<EngineeringProjectSnapshot> SaveAsync(
        string projectKey,
        string projectName,
        string engineeringSchema,
        int engineeringSchemaVersion,
        string engineeringJson,
        string? savedBy = null,
        CancellationToken cancellationToken = default);

    Task<EngineeringProjectSnapshot?> LoadLatestAsync(
        string projectKey,
        CancellationToken cancellationToken = default);

    Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
        string projectKey,
        long revision,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
        string projectKey,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
