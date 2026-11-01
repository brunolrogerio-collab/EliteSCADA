namespace Scada.Core.Alarms;

public enum AlarmSuppressionTargetKind
{
    Alarm = 1,
    Location = 2
}

public static class AlarmSuppressionReasonCodes
{
    public const string Maintenance = "maintenance";
    public const string Startup = "startup";
    public const string Testing = "testing";
    public const string Other = "other";

    public static bool IsValid(string? value) => value is
        Maintenance or Startup or Testing or Other;
}

/// <summary>
/// Durable operator-requested suppression of annunciation. This is deliberately
/// separate from AlarmInstance.State and from the in-memory shelving operation.
/// </summary>
public sealed record AlarmSuppressionRecord(
    string ProjectKey,
    AlarmSuppressionTargetKind TargetKind,
    Guid TargetId,
    string ReasonCode,
    string Actor,
    DateTimeOffset AppliedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    long Version = 0);

public sealed record AlarmSuppressionMutationResult(
    bool Changed,
    AlarmSuppressionRecord? Previous,
    AlarmSuppressionRecord? Current);

public sealed record AlarmSuppressionStoreSnapshot(
    string ProjectKey,
    long Revision,
    IReadOnlyCollection<AlarmSuppressionRecord> Records);

public interface IAlarmSuppressionStore
{
    Task<AlarmSuppressionStoreSnapshot> ReadSnapshotAsync(
        string projectKey,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AlarmSuppressionRecord>> ReadActiveAsync(
        string projectKey,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<AlarmSuppressionMutationResult> SetAsync(
        AlarmSuppressionRecord record,
        CancellationToken cancellationToken = default);

    Task<AlarmSuppressionMutationResult> ReleaseAsync(
        string projectKey,
        AlarmSuppressionTargetKind targetKind,
        Guid targetId,
        CancellationToken cancellationToken = default);

    Task ReplaceProjectSnapshotAsync(
        string projectKey,
        long revision,
        IReadOnlyCollection<AlarmSuppressionRecord> records,
        CancellationToken cancellationToken = default);
}

public sealed class AlarmSuppressionUnavailableException : InvalidOperationException
{
    public AlarmSuppressionUnavailableException()
        : base("Durable alarm suppression storage is not configured.")
    {
    }
}

public sealed class UnavailableAlarmSuppressionStore : IAlarmSuppressionStore
{
    public Task<AlarmSuppressionStoreSnapshot> ReadSnapshotAsync(
        string projectKey,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromException<AlarmSuppressionStoreSnapshot>(new AlarmSuppressionUnavailableException());

    public Task<IReadOnlyCollection<AlarmSuppressionRecord>> ReadActiveAsync(
        string projectKey,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromException<IReadOnlyCollection<AlarmSuppressionRecord>>(new AlarmSuppressionUnavailableException());

    public Task<AlarmSuppressionMutationResult> SetAsync(
        AlarmSuppressionRecord record,
        CancellationToken cancellationToken = default) =>
        Task.FromException<AlarmSuppressionMutationResult>(new AlarmSuppressionUnavailableException());

    public Task<AlarmSuppressionMutationResult> ReleaseAsync(
        string projectKey,
        AlarmSuppressionTargetKind targetKind,
        Guid targetId,
        CancellationToken cancellationToken = default) =>
        Task.FromException<AlarmSuppressionMutationResult>(new AlarmSuppressionUnavailableException());

    public Task ReplaceProjectSnapshotAsync(
        string projectKey,
        long revision,
        IReadOnlyCollection<AlarmSuppressionRecord> records,
        CancellationToken cancellationToken = default) =>
        Task.FromException(new AlarmSuppressionUnavailableException());
}
