namespace Scada.Historian.Archives;

public static class HistorianArchiveLimits
{
    public const long MaximumUploadBytes = 100L * 1024 * 1024 * 1024;
}

public enum HistorianArchiveJobState
{
    Queued,
    Running,
    Blocked,
    Completed
}

public sealed record HistorianArchiveJob(
    Guid ArchiveId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    DateTimeOffset NextSegmentFromUtc,
    int NextSequence,
    HistorianArchiveJobState State,
    bool DeleteSourceAfterVerification,
    long SamplesArchived,
    string? FailureCode,
    string? Sha256,
    long? ArchiveLengthBytes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? ArchiveFileRemovedAtUtc);

public sealed record HistorianArchiveRestoreResult(
    Guid ArchiveId,
    long SamplesRestored,
    long SegmentsRestored,
    long SegmentsAlreadyRestored,
    string Sha256);

public sealed record HistorianArchiveSchedule(
    bool Enabled,
    DateTimeOffset StartFromUtc,
    DateTimeOffset NextFromUtc,
    int ArchiveAfterDays,
    int IntervalDays,
    int WindowDays,
    int? KeepArchivesDays,
    bool DeleteSourceAfterVerification,
    DateTimeOffset NextRunAtUtc,
    DateTimeOffset? LastScheduledAtUtc);

public sealed record HistorianArchiveScheduleConfiguration(
    bool Enabled,
    DateTimeOffset StartFromUtc,
    int ArchiveAfterDays,
    int IntervalDays,
    int WindowDays,
    int? KeepArchivesDays,
    bool DeleteSourceAfterVerification);

public interface IHistorianArchiveService : IAsyncDisposable
{
    string DestinationDescription { get; }

    Task<IReadOnlyList<HistorianArchiveJob>> ListAsync(CancellationToken cancellationToken = default);

    Task<HistorianArchiveSchedule?> GetScheduleAsync(CancellationToken cancellationToken = default);

    Task<HistorianArchiveSchedule> SaveScheduleAsync(
        HistorianArchiveScheduleConfiguration schedule,
        CancellationToken cancellationToken = default);

    Task<HistorianArchiveJob> CreateAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        bool deleteSourceAfterVerification,
        CancellationToken cancellationToken = default);

    Task<HistorianArchiveJob?> ResumeAsync(Guid archiveId, CancellationToken cancellationToken = default);

    Task ProcessNextAsync(CancellationToken cancellationToken = default);

    Task<Stream?> OpenCompletedArchiveAsync(Guid archiveId, CancellationToken cancellationToken = default);

    Task<HistorianArchiveRestoreResult> RestoreAsync(Stream package, CancellationToken cancellationToken = default);
}
