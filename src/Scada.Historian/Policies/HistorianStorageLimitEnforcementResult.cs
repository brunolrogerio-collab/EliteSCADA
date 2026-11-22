namespace Scada.Historian.Policies;

public enum HistorianStorageLimitEnforcementStatus
{
    NoPolicy,
    SkippedBusy,
    NoLimitConfigured,
    WarningOnly,
    WithinLimit,
    ChunksRemoved,
    NoEligibleChunks
}

public sealed record HistorianStorageLimitEnforcementResult(
    HistorianStorageLimitEnforcementStatus Status,
    long? LimitBytes,
    long? RawBytesBefore,
    long? RawBytesAfter,
    int DeletedChunkCount,
    DateTimeOffset CheckedAtUtc)
{
    public bool IsOverLimit => LimitBytes.HasValue && RawBytesAfter > LimitBytes;
}
