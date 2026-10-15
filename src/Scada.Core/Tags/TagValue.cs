namespace Scada.Core.Tags;

/// <summary>
/// Evidence attached to a protocol timestamp. This is provenance only; it does not
/// change the local observation timestamp or imply alignment with another sample.
/// </summary>
public enum TimestampEvidenceState : short
{
    NotRecorded = 0,
    NotProvided = 1,
    PresentUnverified = 2,
    Unsynchronized = 3,
    Synchronized = 4
}

public sealed record TagValue(
    Guid TagId,
    object? Value,
    DateTimeOffset Timestamp,
    TagQuality Quality,
    string? Source = null)
{
    private DateTimeOffset? _sourceTimestamp;
    private DateTimeOffset? _serverTimestamp;
    private TimestampEvidenceState? _sourceTimestampEvidenceState;
    private TimestampEvidenceState? _serverTimestampEvidenceState;

    /// <summary>
    /// Optional timestamp supplied by the originating device/application value.
    /// Timestamp remains the local EliteSCADA observation/publication time.
    /// Protocols that do not provide source time leave this null.
    /// </summary>
    public DateTimeOffset? SourceTimestamp
    {
        get => _sourceTimestamp;
        init => _sourceTimestamp = value?.ToUniversalTime();
    }

    /// <summary>
    /// Evidence for <see cref="SourceTimestamp"/>. When absent (including legacy
    /// payloads which predate this property), a supplied timestamp is conservatively
    /// treated as present but unverified; it is never promoted to synchronized.
    /// </summary>
    public TimestampEvidenceState SourceTimestampEvidenceState
    {
        get => _sourceTimestampEvidenceState ??
               (_sourceTimestamp is null
                   ? TimestampEvidenceState.NotProvided
                   : TimestampEvidenceState.PresentUnverified);
        init => _sourceTimestampEvidenceState = value;
    }

    /// <summary>
    /// Optional intermediary/server timestamp when a protocol exposes one
    /// separately from the source/device timestamp, such as OPC UA DataValue.
    /// Protocols without this distinction leave the value null.
    /// </summary>
    public DateTimeOffset? ServerTimestamp
    {
        get => _serverTimestamp;
        init => _serverTimestamp = value?.ToUniversalTime();
    }

    /// <summary>
    /// Evidence for <see cref="ServerTimestamp"/>. Server timestamps are never
    /// treated as synchronized source/device timestamps.
    /// </summary>
    public TimestampEvidenceState ServerTimestampEvidenceState
    {
        get => _serverTimestampEvidenceState ??
               (_serverTimestamp is null
                   ? TimestampEvidenceState.NotProvided
                   : TimestampEvidenceState.PresentUnverified);
        init
        {
            if (value is TimestampEvidenceState.Synchronized or TimestampEvidenceState.Unsynchronized)
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Server timestamps cannot carry source clock synchronization evidence.");
            _serverTimestampEvidenceState = value;
        }
    }

    /// <summary>
    /// Throws when an explicitly recorded evidence state contradicts its timestamp.
    /// Legacy-unrecorded rows are intentionally allowed to contain either value.
    /// </summary>
    public void ValidateTimestampEvidence()
    {
        ValidateSourceTimestampEvidence(SourceTimestampEvidenceState, SourceTimestamp);
        ValidateServerTimestampEvidence(ServerTimestampEvidenceState, ServerTimestamp);
    }

    private static void ValidateSourceTimestampEvidence(
        TimestampEvidenceState evidence,
        DateTimeOffset? timestamp)
    {
        if (!Enum.IsDefined(evidence))
            throw new InvalidOperationException("Source timestamp evidence state is invalid.");
        if ((evidence == TimestampEvidenceState.NotProvided) != (timestamp is null) &&
            evidence != TimestampEvidenceState.NotRecorded)
            throw new InvalidOperationException("Source timestamp and evidence state are inconsistent.");
    }

    private static void ValidateServerTimestampEvidence(
        TimestampEvidenceState evidence,
        DateTimeOffset? timestamp)
    {
        if (evidence is TimestampEvidenceState.Synchronized or TimestampEvidenceState.Unsynchronized ||
            !Enum.IsDefined(evidence))
            throw new InvalidOperationException("Server timestamp evidence state is invalid.");
        if ((evidence == TimestampEvidenceState.NotProvided) != (timestamp is null) &&
            evidence != TimestampEvidenceState.NotRecorded)
            throw new InvalidOperationException("Server timestamp and evidence state are inconsistent.");
    }

    public static TagValue Good(Guid tagId, object? value, string? source = null)
        => new(tagId, value, DateTimeOffset.UtcNow, TagQuality.Good, source);
}
