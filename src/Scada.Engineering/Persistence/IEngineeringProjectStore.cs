namespace Scada.Engineering.Persistence;

public sealed record EngineeringProjectSnapshot(
    long Revision,
    string ProjectKey,
    string ProjectName,
    string EngineeringSchema,
    int EngineeringSchemaVersion,
    DateTimeOffset SavedAtUtc,
    string EngineeringJson,
    string? SavedBy = null,
    long? BasedOnRevision = null);

public sealed record EngineeringRevisionAssetPayload(
    Guid AssetId,
    string Sha256,
    string MediaType,
    byte[] Content)
{
    public long ByteLength => Content.LongLength;

    public EngineeringRevisionAssetPayload Copy() => this with { Content = Content.ToArray() };
}

public enum EngineeringProjectLifecycleStatus
{
    Empty,
    Draft,
    Published,
    ChangesPending
}

public enum EngineeringRuntimeStatus
{
    Inactive,
    ActivationPending,
    Active
}

public sealed record EngineeringProjectPublication(
    string ProjectKey,
    long PublishedRevision,
    DateTimeOffset PublishedAtUtc,
    string? PublishedBy = null);

public sealed record EngineeringProjectActivation(
    string ProjectKey,
    long ActiveRevision,
    DateTimeOffset ActivatedAtUtc,
    string? ActivatedBy = null);

public sealed record EngineeringProjectLifecycle(
    string ProjectKey,
    EngineeringProjectLifecycleStatus Status,
    long? WorkingRevision,
    long? PublishedRevision,
    DateTimeOffset? PublishedAtUtc = null,
    string? PublishedBy = null,
    EngineeringRuntimeStatus RuntimeStatus = EngineeringRuntimeStatus.Inactive,
    long? ActiveRevision = null,
    DateTimeOffset? ActivatedAtUtc = null,
    string? ActivatedBy = null);

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

    Task<EngineeringProjectSnapshot> SaveDerivedAsync(
        string projectKey,
        string projectName,
        string engineeringSchema,
        int engineeringSchemaVersion,
        string engineeringJson,
        long? basedOnRevision,
        string? savedBy = null,
        CancellationToken cancellationToken = default) =>
        SaveAsync(
            projectKey,
            projectName,
            engineeringSchema,
            engineeringSchemaVersion,
            engineeringJson,
            savedBy,
            cancellationToken);

    async Task<EngineeringProjectSnapshot> SaveDerivedWithAssetsAsync(
        string projectKey,
        string projectName,
        string engineeringSchema,
        int engineeringSchemaVersion,
        string engineeringJson,
        long? basedOnRevision,
        IReadOnlyCollection<EngineeringRevisionAssetPayload> assets,
        string? savedBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assets);
        if (assets.Count != 0)
            throw new NotSupportedException("This Engineering project store does not support revision asset payloads.");

        return await SaveDerivedAsync(
            projectKey,
            projectName,
            engineeringSchema,
            engineeringSchemaVersion,
            engineeringJson,
            basedOnRevision,
            savedBy,
            cancellationToken);
    }

    Task<IReadOnlyCollection<EngineeringRevisionAssetPayload>> LoadRevisionAssetsAsync(
        string projectKey,
        long revision,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<EngineeringRevisionAssetPayload>>(
            Array.Empty<EngineeringRevisionAssetPayload>());

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

    Task<EngineeringProjectPublication?> GetPublicationAsync(
        string projectKey,
        CancellationToken cancellationToken = default);

    Task<EngineeringProjectPublication?> PublishRevisionAsync(
        string projectKey,
        long revision,
        string? publishedBy = null,
        CancellationToken cancellationToken = default);

    Task<EngineeringProjectActivation?> GetActivationAsync(
        string projectKey,
        CancellationToken cancellationToken = default);

    Task<EngineeringProjectActivation?> RecordActivationAsync(
        string projectKey,
        long revision,
        string? activatedBy = null,
        CancellationToken cancellationToken = default);
}