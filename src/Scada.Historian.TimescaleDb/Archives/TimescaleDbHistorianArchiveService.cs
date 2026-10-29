using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Scada.Core.Persistence;
using Scada.Historian.Archives;

namespace Scada.Historian.TimescaleDb.Archives;

/// <summary>
/// Incremental raw-Historian archiving for TimescaleDB. Each UTC-day segment is
/// compressed, read back, hashed, and checkpointed before the next segment begins.
/// The final package is verified again before it is marked complete or source rows
/// are optionally removed.
/// </summary>
public sealed class TimescaleDbHistorianArchiveService : IHistorianArchiveService
{
    private const long ArchiveWorkerLockKey = 6002246818883737416; // "SCADARCH"
    private readonly NpgsqlDataSource _dataSource;
    private readonly IDurableWriteAdmission? _writeAdmission;
    private readonly string _root;

    public TimescaleDbHistorianArchiveService(
        string connectionString,
        string archiveRoot,
        IDurableWriteAdmission? writeAdmission = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveRoot);
        _dataSource = NpgsqlDataSource.Create(connectionString);
        _writeAdmission = writeAdmission;
        _root = Path.GetFullPath(archiveRoot);
    }

    public string DestinationDescription => _root;

    public async Task<IReadOnlyList<HistorianArchiveJob>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInfrastructureAsync(cancellationToken).ConfigureAwait(false);
        const string sql = """
            SELECT archive_id, from_utc, to_utc, next_segment_from_utc, next_sequence, state,
                   delete_source_after_verification, samples_archived, failure_code,
                   package_sha256, archive_length_bytes, created_at_utc,
                   updated_at_utc, completed_at_utc, archive_file_removed_at_utc
            FROM elitescada.historian_archive_jobs
            ORDER BY created_at_utc DESC
            LIMIT 100;
            """;
        await using var command = _dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var jobs = new List<HistorianArchiveJob>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            jobs.Add(ReadJob(reader));
        return jobs;
    }

    public async Task<HistorianArchiveSchedule?> GetScheduleAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInfrastructureAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await ReadScheduleAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<HistorianArchiveSchedule> SaveScheduleAsync(
        HistorianArchiveScheduleConfiguration schedule,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (schedule.StartFromUtc.Offset != TimeSpan.Zero ||
            schedule.ArchiveAfterDays is < 1 or > 36500 ||
            schedule.IntervalDays is < 1 or > 3650 ||
            schedule.WindowDays is < 1 or > 31 ||
            schedule.KeepArchivesDays is < 1 or > 36500 ||
            schedule.StartFromUtc >= DateTimeOffset.UtcNow)
            throw new ArgumentException("Historian archive schedule settings are invalid.");

        await EnsureInfrastructureAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var current = await ReadScheduleAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var cursor = current is null || current.StartFromUtc != schedule.StartFromUtc
            ? schedule.StartFromUtc
            : current.NextFromUtc;
        const string sql = """
            INSERT INTO elitescada.historian_archive_schedule(
                singleton_id, enabled, start_from_utc, next_from_utc, archive_after_days,
                interval_days, window_days, keep_archives_days,
                delete_source_after_verification, next_run_at_utc, last_scheduled_at_utc, updated_at_utc)
            VALUES (1, @enabled, @start, @cursor, @age, @interval, @window, @keep,
                    @cleanup, @nextRun, @lastRun, @now)
            ON CONFLICT (singleton_id) DO UPDATE SET
                enabled = EXCLUDED.enabled,
                start_from_utc = EXCLUDED.start_from_utc,
                next_from_utc = CASE
                    WHEN elitescada.historian_archive_schedule.start_from_utc <> EXCLUDED.start_from_utc
                    THEN EXCLUDED.next_from_utc
                    ELSE elitescada.historian_archive_schedule.next_from_utc END,
                archive_after_days = EXCLUDED.archive_after_days,
                interval_days = EXCLUDED.interval_days,
                window_days = EXCLUDED.window_days,
                keep_archives_days = EXCLUDED.keep_archives_days,
                delete_source_after_verification = EXCLUDED.delete_source_after_verification,
                next_run_at_utc = EXCLUDED.next_run_at_utc,
                updated_at_utc = EXCLUDED.updated_at_utc;
            """;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("enabled", schedule.Enabled);
            command.Parameters.AddWithValue("start", schedule.StartFromUtc);
            command.Parameters.AddWithValue("cursor", cursor);
            command.Parameters.AddWithValue("age", schedule.ArchiveAfterDays);
            command.Parameters.AddWithValue("interval", schedule.IntervalDays);
            command.Parameters.AddWithValue("window", schedule.WindowDays);
            command.Parameters.AddWithValue("keep", (object?)schedule.KeepArchivesDays ?? DBNull.Value);
            command.Parameters.AddWithValue("cleanup", schedule.DeleteSourceAfterVerification);
            command.Parameters.AddWithValue("nextRun", schedule.Enabled ? now : now.AddDays(schedule.IntervalDays));
            command.Parameters.AddWithValue("lastRun", (object?)current?.LastScheduledAtUtc ?? DBNull.Value);
            command.Parameters.AddWithValue("now", now);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return await ReadScheduleAsync(connection, null, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Historian archive schedule was not persisted.");
    }

    public async Task<HistorianArchiveJob> CreateAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        bool deleteSourceAfterVerification,
        CancellationToken cancellationToken = default)
    {
        fromUtc = fromUtc.ToUniversalTime();
        toUtc = toUtc.ToUniversalTime();
        if (fromUtc >= toUtc || toUtc > DateTimeOffset.UtcNow)
            throw new ArgumentException("Archive range must be a non-empty UTC interval that ends in the past.");

        await EnsureInfrastructureAsync(cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(_root);
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        const string sql = """
            INSERT INTO elitescada.historian_archive_jobs(
                archive_id, from_utc, to_utc, next_segment_from_utc, next_sequence,
                state, delete_source_after_verification, samples_archived,
                created_at_utc, updated_at_utc)
            VALUES (@id, @from, @to, @from, 0, 'Queued', @cleanup, 0, @now, @now)
            RETURNING archive_id, from_utc, to_utc, next_segment_from_utc, next_sequence, state,
                      delete_source_after_verification, samples_archived, failure_code,
                      package_sha256, archive_length_bytes, created_at_utc,
                      updated_at_utc, completed_at_utc, archive_file_removed_at_utc;
            """;
        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("from", fromUtc);
        command.Parameters.AddWithValue("to", toUtc);
        command.Parameters.AddWithValue("cleanup", deleteSourceAfterVerification);
        command.Parameters.AddWithValue("now", now);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Historian archive job was not persisted.");
        return ReadJob(reader);
    }

    public async Task<HistorianArchiveJob?> ResumeAsync(Guid archiveId, CancellationToken cancellationToken = default)
    {
        if (archiveId == Guid.Empty) throw new ArgumentException("Archive ID is required.", nameof(archiveId));
        await EnsureInfrastructureAsync(cancellationToken).ConfigureAwait(false);
        const string sql = """
            UPDATE elitescada.historian_archive_jobs
            SET state = 'Queued', failure_code = NULL, updated_at_utc = @now
            WHERE archive_id = @id AND state = 'Blocked'
            RETURNING archive_id, from_utc, to_utc, next_segment_from_utc, next_sequence, state,
                      delete_source_after_verification, samples_archived, failure_code,
                      package_sha256, archive_length_bytes, created_at_utc,
                      updated_at_utc, completed_at_utc, archive_file_removed_at_utc;
            """;
        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", archiveId);
        command.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadJob(reader) : null;
    }

    public async Task ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInfrastructureAsync(cancellationToken).ConfigureAwait(false);
        await using var lockConnection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (var lockCommand = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key);", lockConnection))
        {
            lockCommand.Parameters.AddWithValue("key", NpgsqlDbType.Bigint, ArchiveWorkerLockKey);
            if (!Equals(await lockCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), true)) return;
        }

        Guid? archiveId = null;
        try
        {
            await EnqueueScheduledArchiveAsync(lockConnection, cancellationToken).ConfigureAwait(false);
            await RotateExpiredArchivesAsync(lockConnection, cancellationToken).ConfigureAwait(false);
            var job = await ClaimNextAsync(lockConnection, cancellationToken).ConfigureAwait(false);
            if (job is null) return;
            archiveId = job.ArchiveId;
            if (job.NextSegmentFromUtc < job.ToUtc)
                await ProcessSegmentAsync(lockConnection, job, cancellationToken).ConfigureAwait(false);
            else
                await CompleteArchiveAsync(lockConnection, job, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (archiveId.HasValue)
                await BlockJobAsync(lockConnection, archiveId.Value, MapFailureCode(ex), cancellationToken)
                    .ConfigureAwait(false);
            throw;
        }
        finally
        {
            if (lockConnection.FullState == System.Data.ConnectionState.Open)
            {
                await using var unlock = new NpgsqlCommand("SELECT pg_advisory_unlock(@key);", lockConnection);
                unlock.Parameters.AddWithValue("key", NpgsqlDbType.Bigint, ArchiveWorkerLockKey);
                await unlock.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    public async Task<Stream?> OpenCompletedArchiveAsync(Guid archiveId, CancellationToken cancellationToken = default)
    {
        await EnsureInfrastructureAsync(cancellationToken).ConfigureAwait(false);
        const string sql = "SELECT state FROM elitescada.historian_archive_jobs WHERE archive_id = @id;";
        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", archiveId);
        var state = (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(state, "Completed", StringComparison.Ordinal)) return null;
        var path = PackagePath(archiveId);
        if (!File.Exists(path)) return null;
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    public async Task<HistorianArchiveRestoreResult> RestoreAsync(Stream package, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        await EnsureInfrastructureAsync(cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(_root);
        var uploadPath = Path.Combine(_root, $".restore-{Guid.NewGuid():N}.partial");
        try
        {
            await CopyBoundedAsync(package, uploadPath, HistorianArchiveLimits.MaximumUploadBytes, cancellationToken).ConfigureAwait(false);
            var verification = await HistorianArchiveFormat.VerifyPackageAsync(uploadPath, cancellationToken).ConfigureAwait(false);
            await using var file = new FileStream(uploadPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var zip = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
            long restoredSamples = 0;
            long restoredSegments = 0;
            long alreadyRestored = 0;

            foreach (var segment in verification.Manifest.Segments.OrderBy(x => x.Sequence))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = zip.GetEntry($"segments/{segment.FileName}")
                    ?? throw new InvalidDataException("Verified Historian archive segment disappeared before restore.");
                var restore = await RestoreSegmentAsync(
                    verification.Manifest.ArchiveId,
                    segment,
                    entry,
                    cancellationToken).ConfigureAwait(false);
                if (restore.AlreadyRestored)
                {
                    alreadyRestored++;
                }
                else
                {
                    restoredSegments++;
                    restoredSamples = checked(restoredSamples + restore.SamplesInserted);
                }
            }

            return new HistorianArchiveRestoreResult(
                verification.Manifest.ArchiveId,
                restoredSamples,
                restoredSegments,
                alreadyRestored,
                verification.ArchiveSha256);
        }
        finally
        {
            TryDelete(uploadPath);
        }
    }

    private async Task<HistorianArchiveJob?> ClaimNextAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        const string selectSql = """
            SELECT archive_id
            FROM elitescada.historian_archive_jobs
            WHERE state IN ('Queued', 'Running')
            ORDER BY created_at_utc, archive_id
            FOR UPDATE SKIP LOCKED
            LIMIT 1;
            """;
        Guid? id;
        await using (var select = new NpgsqlCommand(selectSql, connection, transaction))
            id = (Guid?)await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (!id.HasValue)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        const string updateSql = """
            UPDATE elitescada.historian_archive_jobs
            SET state = 'Running', updated_at_utc = @now
            WHERE archive_id = @id;
            """;
        await using (var update = new NpgsqlCommand(updateSql, connection, transaction))
        {
            update.Parameters.AddWithValue("id", id.Value);
            update.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return await ReadJobAsync(connection, id.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessSegmentAsync(NpgsqlConnection lockConnection, HistorianArchiveJob job, CancellationToken cancellationToken)
    {
        var segmentFrom = job.NextSegmentFromUtc.ToUniversalTime();
        var nextMidnightUtc = segmentFrom.UtcDateTime.Date.AddDays(1);
        var segmentTo = new DateTimeOffset(nextMidnightUtc, TimeSpan.Zero);
        if (segmentTo > job.ToUtc) segmentTo = job.ToUtc;
        if (segmentTo <= segmentFrom) throw new InvalidDataException("Historian archive checkpoint is outside its requested range.");

        var stageDirectory = StageDirectory(job.ArchiveId);
        Directory.CreateDirectory(stageDirectory);
        var fileName = $"segment-{job.NextSequence:D6}.jsonl.gz";
        var finalSegmentPath = HistorianArchiveFormat.ResolveSegmentPath(stageDirectory, fileName);
        var temporarySegmentPath = finalSegmentPath + ".partial";
        TryDelete(temporarySegmentPath);

        HistorianArchiveSegment segment;
        if (File.Exists(finalSegmentPath))
        {
            // The file is durably flushed and verified before it is moved into place.
            // Reuse it if the process stopped before its database checkpoint committed.
            await using var staged = new FileStream(finalSegmentPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            segment = await HistorianArchiveFormat.InspectSegmentContentAsync(
                staged,
                job.NextSequence,
                segmentFrom,
                segmentTo,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            segment = await HistorianArchiveFormat.WriteSegmentAsync(
                temporarySegmentPath,
                job.NextSequence,
                segmentFrom,
                segmentTo,
                ReadSegmentSamplesAsync(segmentFrom, segmentTo, cancellationToken),
                cancellationToken).ConfigureAwait(false);
            await using (var file = new FileStream(temporarySegmentPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await HistorianArchiveFormat.VerifySegmentContentAsync(file, segment, cancellationToken).ConfigureAwait(false);
            File.Move(temporarySegmentPath, finalSegmentPath, overwrite: true);
        }

        await using var transaction = await lockConnection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        const string insertSql = """
            INSERT INTO elitescada.historian_archive_job_segments(
                archive_id, sequence, from_utc, to_utc, sample_count, sha256, file_name)
            VALUES (@id, @sequence, @from, @to, @count, @sha, @file)
            ON CONFLICT (archive_id, sequence) DO UPDATE SET
                from_utc = EXCLUDED.from_utc,
                to_utc = EXCLUDED.to_utc,
                sample_count = EXCLUDED.sample_count,
                sha256 = EXCLUDED.sha256,
                file_name = EXCLUDED.file_name;
            """;
        await using (var insert = new NpgsqlCommand(insertSql, lockConnection, transaction))
        {
            insert.Parameters.AddWithValue("id", job.ArchiveId);
            insert.Parameters.AddWithValue("sequence", segment.Sequence);
            insert.Parameters.AddWithValue("from", segment.FromUtc);
            insert.Parameters.AddWithValue("to", segment.ToUtc);
            insert.Parameters.AddWithValue("count", segment.SampleCount);
            insert.Parameters.AddWithValue("sha", segment.Sha256);
            insert.Parameters.AddWithValue("file", segment.FileName);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        const string updateSql = """
            UPDATE elitescada.historian_archive_jobs
            SET next_segment_from_utc = @next, next_sequence = @sequence,
                samples_archived = samples_archived + @count,
                updated_at_utc = @now, failure_code = NULL
            WHERE archive_id = @id AND state = 'Running';
            """;
        await using (var update = new NpgsqlCommand(updateSql, lockConnection, transaction))
        {
            update.Parameters.AddWithValue("next", segmentTo);
            update.Parameters.AddWithValue("sequence", checked(segment.Sequence + 1));
            update.Parameters.AddWithValue("count", segment.SampleCount);
            update.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
            update.Parameters.AddWithValue("id", job.ArchiveId);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task CompleteArchiveAsync(NpgsqlConnection connection, HistorianArchiveJob job, CancellationToken cancellationToken)
    {
        var segments = await ReadSegmentsAsync(connection, job.ArchiveId, cancellationToken).ConfigureAwait(false);
        if (segments.Count == 0) throw new InvalidDataException("Historian archive has no completed segments.");
        var manifest = new HistorianArchiveManifest(
            HistorianArchiveFormat.FormatName,
            HistorianArchiveFormat.CurrentVersion,
            job.ArchiveId,
            job.CreatedAtUtc.ToUniversalTime(),
            job.FromUtc.ToUniversalTime(),
            job.ToUtc.ToUniversalTime(),
            segments);
        var packagePath = PackagePath(job.ArchiveId);
        await HistorianArchiveFormat.CreatePackageAsync(
            packagePath,
            manifest,
            StageDirectory(job.ArchiveId),
            cancellationToken).ConfigureAwait(false);

        var verification = await HistorianArchiveFormat.VerifyPackageAsync(packagePath, cancellationToken).ConfigureAwait(false);
        await using var durableWrite = job.DeleteSourceAfterVerification && _writeAdmission is not null
            ? await _writeAdmission.AcquireOptionalAsync("historian-archive-source-cleanup", cancellationToken).ConfigureAwait(false)
            : null;
        if (job.DeleteSourceAfterVerification)
            await DeleteArchivedSourceRowsAsync(connection, job.ArchiveId, packagePath, verification, cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        const string completeSql = """
            UPDATE elitescada.historian_archive_jobs
            SET state = 'Completed', package_sha256 = @sha, archive_length_bytes = @length,
                failure_code = NULL, completed_at_utc = @now, updated_at_utc = @now
            WHERE archive_id = @id;
            """;
        await using var complete = new NpgsqlCommand(completeSql, connection, transaction);
        complete.Parameters.AddWithValue("sha", verification.ArchiveSha256);
        complete.Parameters.AddWithValue("length", verification.ArchiveLengthBytes);
        complete.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
        complete.Parameters.AddWithValue("id", job.ArchiveId);
        await complete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        const string advanceScheduleSql = """
            UPDATE elitescada.historian_archive_schedule schedule
            SET next_from_utc = GREATEST(schedule.next_from_utc, job.to_utc), updated_at_utc = @now
            FROM elitescada.historian_archive_jobs job
            WHERE job.archive_id = @id AND job.schedule_id = schedule.singleton_id
              AND schedule.next_from_utc = job.from_utc;
            """;
        await using (var advance = new NpgsqlCommand(advanceScheduleSql, connection, transaction))
        {
            advance.Parameters.AddWithValue("id", job.ArchiveId);
            advance.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
            await advance.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        TryDeleteDirectory(StageDirectory(job.ArchiveId));
    }

    private static async Task DeleteArchivedSourceRowsAsync(
        NpgsqlConnection connection,
        Guid archiveId,
        string packagePath,
        HistorianArchiveVerification verification,
        CancellationToken cancellationToken)
    {
        const string cleanupStateSql = """
            SELECT source_cleanup_completed_at_utc
            FROM elitescada.historian_archive_job_segments
            WHERE archive_id = @id AND sequence = @sequence
            FOR UPDATE;
            """;
        const string createSql = """
            CREATE TEMP TABLE historian_archive_cleanup_rows
            (LIKE elitescada.tag_history INCLUDING DEFAULTS) ON COMMIT DROP;
            """;

        const string deleteSql = """
            WITH expected AS (
                SELECT tag_id, ts, quality, source, value, data_type, source_ts,
                       source_ts_evidence, server_ts, server_ts_evidence, capture_profile_id,
                       capture_policy_state, capture_policy_fingerprint, COUNT(*) AS archived_count
                FROM historian_archive_cleanup_rows
                GROUP BY tag_id, ts, quality, source, value, data_type, source_ts,
                         source_ts_evidence, server_ts, server_ts_evidence, capture_profile_id,
                         capture_policy_state, capture_policy_fingerprint
            ), matched AS (
                SELECT h.ctid, e.archived_count,
                       ROW_NUMBER() OVER (
                           PARTITION BY h.tag_id, h.ts, h.quality, h.source, h.value, h.data_type,
                                        h.source_ts, h.source_ts_evidence, h.server_ts,
                                        h.server_ts_evidence, h.capture_profile_id,
                                        h.capture_policy_state, h.capture_policy_fingerprint
                           ORDER BY h.ctid) AS occurrence
                FROM elitescada.tag_history h
                JOIN expected e
                  ON h.tag_id = e.tag_id AND h.ts = e.ts AND h.quality = e.quality
                 AND h.source IS NOT DISTINCT FROM e.source AND h.value = e.value
                 AND h.data_type IS NOT DISTINCT FROM e.data_type
                 AND h.source_ts IS NOT DISTINCT FROM e.source_ts
                 AND h.source_ts_evidence IS NOT DISTINCT FROM e.source_ts_evidence
                 AND h.server_ts IS NOT DISTINCT FROM e.server_ts
                 AND h.server_ts_evidence IS NOT DISTINCT FROM e.server_ts_evidence
                 AND h.capture_profile_id IS NOT DISTINCT FROM e.capture_profile_id
                 AND h.capture_policy_state IS NOT DISTINCT FROM e.capture_policy_state
                 AND h.capture_policy_fingerprint IS NOT DISTINCT FROM e.capture_policy_fingerprint
                WHERE h.ts >= @from AND h.ts < @to
            )
            DELETE FROM elitescada.tag_history h USING matched m
            WHERE h.ctid = m.ctid AND m.occurrence <= m.archived_count;
            """;

        await using var packageFile = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var package = new ZipArchive(packageFile, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var segment in verification.Manifest.Segments.OrderBy(x => x.Sequence))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            bool alreadyCleaned;
            await using (var state = new NpgsqlCommand(cleanupStateSql, connection, transaction))
            {
                state.Parameters.AddWithValue("id", archiveId);
                state.Parameters.AddWithValue("sequence", segment.Sequence);
                await using var reader = await state.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    throw new InvalidDataException("Verified archive segment has no persisted job checkpoint.");
                alreadyCleaned = !reader.IsDBNull(0);
            }
            if (alreadyCleaned)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            await using (var create = new NpgsqlCommand(createSql, connection, transaction))
                await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            var entry = package.GetEntry($"segments/{segment.FileName}")
                ?? throw new InvalidDataException("Verified archive segment disappeared before source cleanup.");
            {
                await using var importer = await connection.BeginBinaryImportAsync("""
                    COPY historian_archive_cleanup_rows (
                        tag_id, ts, quality, source, value, data_type, source_ts, source_ts_evidence,
                        server_ts, server_ts_evidence, capture_profile_id, capture_policy_state,
                        capture_policy_fingerprint) FROM STDIN (FORMAT BINARY)
                    """, cancellationToken).ConfigureAwait(false);
                await using var compressed = entry.Open();
                await using var gzip = new GZipStream(compressed, CompressionMode.Decompress, leaveOpen: true);
                using var reader = new StreamReader(gzip);
                while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                {
                    var sample = JsonSerializer.Deserialize<HistorianArchiveSample>(line, ArchiveJsonOptions)
                        ?? throw new InvalidDataException("Verified archive sample is empty.");
                    await importer.StartRowAsync(cancellationToken).ConfigureAwait(false);
                    await importer.WriteAsync(sample.TagId, NpgsqlDbType.Uuid, cancellationToken).ConfigureAwait(false);
                    await importer.WriteAsync(sample.Timestamp.UtcDateTime, NpgsqlDbType.TimestampTz, cancellationToken).ConfigureAwait(false);
                    await importer.WriteAsync(sample.Quality, NpgsqlDbType.Integer, cancellationToken).ConfigureAwait(false);
                    await WriteNullableAsync(importer, sample.Source, NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
                    await importer.WriteAsync(sample.ValueJson, NpgsqlDbType.Jsonb, cancellationToken).ConfigureAwait(false);
                    await WriteNullableAsync(importer, sample.DataType, NpgsqlDbType.Smallint, cancellationToken).ConfigureAwait(false);
                    await WriteNullableAsync(importer, sample.SourceTimestamp?.UtcDateTime, NpgsqlDbType.TimestampTz, cancellationToken).ConfigureAwait(false);
                    await WriteNullableAsync(importer, sample.SourceTimestampEvidence, NpgsqlDbType.Smallint, cancellationToken).ConfigureAwait(false);
                    await WriteNullableAsync(importer, sample.ServerTimestamp?.UtcDateTime, NpgsqlDbType.TimestampTz, cancellationToken).ConfigureAwait(false);
                    await WriteNullableAsync(importer, sample.ServerTimestampEvidence, NpgsqlDbType.Smallint, cancellationToken).ConfigureAwait(false);
                    await WriteNullableAsync(importer, sample.CaptureProfileId, NpgsqlDbType.Uuid, cancellationToken).ConfigureAwait(false);
                    await WriteNullableAsync(importer, sample.CapturePolicyState, NpgsqlDbType.Smallint, cancellationToken).ConfigureAwait(false);
                    var fingerprint = sample.CapturePolicyFingerprintBase64 is null
                        ? null
                        : Convert.FromBase64String(sample.CapturePolicyFingerprintBase64);
                    await WriteNullableAsync(importer, fingerprint, NpgsqlDbType.Bytea, cancellationToken).ConfigureAwait(false);
                }
                await importer.CompleteAsync(cancellationToken).ConfigureAwait(false);
            }
            await using (var delete = new NpgsqlCommand(deleteSql, connection, transaction))
            {
                delete.Parameters.AddWithValue("from", segment.FromUtc);
                delete.Parameters.AddWithValue("to", segment.ToUtc);
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            const string markSql = """
                UPDATE elitescada.historian_archive_job_segments
                SET source_cleanup_completed_at_utc = @now
                WHERE archive_id = @id AND sequence = @sequence
                  AND source_cleanup_completed_at_utc IS NULL;
                """;
            await using (var mark = new NpgsqlCommand(markSql, connection, transaction))
            {
                mark.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
                mark.Parameters.AddWithValue("id", archiveId);
                mark.Parameters.AddWithValue("sequence", segment.Sequence);
                if (await mark.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                    throw new InvalidOperationException("Historian archive source-cleanup checkpoint could not be saved.");
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<HistorianArchiveSchedule?> ReadScheduleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT enabled, start_from_utc, next_from_utc, archive_after_days, interval_days,
                   window_days, keep_archives_days, delete_source_after_verification,
                   next_run_at_utc, last_scheduled_at_utc
            FROM elitescada.historian_archive_schedule WHERE singleton_id = 1
            """ + (transaction is null ? string.Empty : " FOR UPDATE");
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return new HistorianArchiveSchedule(
            reader.GetBoolean(0),
            reader.GetFieldValue<DateTimeOffset>(1).ToUniversalTime(),
            reader.GetFieldValue<DateTimeOffset>(2).ToUniversalTime(),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.IsDBNull(6) ? null : reader.GetInt32(6),
            reader.GetBoolean(7),
            reader.GetFieldValue<DateTimeOffset>(8).ToUniversalTime(),
            reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9).ToUniversalTime());
    }

    private async Task EnqueueScheduledArchiveAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        HistorianArchiveSchedule? schedule;
        const string lockSql = """
            SELECT enabled, start_from_utc, next_from_utc, archive_after_days, interval_days,
                   window_days, keep_archives_days, delete_source_after_verification,
                   next_run_at_utc, last_scheduled_at_utc
            FROM elitescada.historian_archive_schedule WHERE singleton_id = 1 FOR UPDATE;
            """;
        await using (var command = new NpgsqlCommand(lockSql, connection, transaction))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            schedule = await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? new HistorianArchiveSchedule(
                    reader.GetBoolean(0), reader.GetFieldValue<DateTimeOffset>(1).ToUniversalTime(),
                    reader.GetFieldValue<DateTimeOffset>(2).ToUniversalTime(), reader.GetInt32(3),
                    reader.GetInt32(4), reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    reader.GetBoolean(7), reader.GetFieldValue<DateTimeOffset>(8).ToUniversalTime(),
                    reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9).ToUniversalTime())
                : null;
        }

        var now = DateTimeOffset.UtcNow;
        if (schedule is null || !schedule.Enabled || schedule.NextRunAtUtc > now)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await using (var active = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM elitescada.historian_archive_jobs WHERE schedule_id = 1 AND state <> 'Completed');",
            connection, transaction))
        {
            if (Equals(await active.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), true))
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        var safeThroughUtc = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-schedule.ArchiveAfterDays), TimeSpan.Zero);
        var nextRun = now.AddDays(schedule.IntervalDays);
        if (schedule.NextFromUtc >= safeThroughUtc)
        {
            await UpdateScheduleRunAsync(connection, transaction, nextRun, now, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var windowEnd = schedule.NextFromUtc.AddDays(schedule.WindowDays);
        var toUtc = windowEnd < safeThroughUtc ? windowEnd : safeThroughUtc;
        const string insertSql = """
            INSERT INTO elitescada.historian_archive_jobs(
                archive_id, from_utc, to_utc, next_segment_from_utc, next_sequence,
                state, delete_source_after_verification, samples_archived, schedule_id,
                created_at_utc, updated_at_utc)
            VALUES (@id, @from, @to, @from, 0, 'Queued', @cleanup, 0, 1, @now, @now);
            """;
        await using (var insert = new NpgsqlCommand(insertSql, connection, transaction))
        {
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("from", schedule.NextFromUtc);
            insert.Parameters.AddWithValue("to", toUtc);
            insert.Parameters.AddWithValue("cleanup", schedule.DeleteSourceAfterVerification);
            insert.Parameters.AddWithValue("now", now);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await UpdateScheduleRunAsync(connection, transaction, nextRun, now, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpdateScheduleRunAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTimeOffset nextRun,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var update = new NpgsqlCommand("""
            UPDATE elitescada.historian_archive_schedule
            SET next_run_at_utc = @next, last_scheduled_at_utc = @now, updated_at_utc = @now
            WHERE singleton_id = 1;
            """, connection, transaction);
        update.Parameters.AddWithValue("next", nextRun);
        update.Parameters.AddWithValue("now", now);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RotateExpiredArchivesAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        int? keepDays;
        const string scheduleSql = "SELECT keep_archives_days FROM elitescada.historian_archive_schedule WHERE singleton_id = 1;";
        await using (var schedule = new NpgsqlCommand(scheduleSql, connection))
        {
            var value = await schedule.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            keepDays = value is null or DBNull ? null : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        if (keepDays is null) return;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-keepDays.Value);
        var expired = new List<Guid>();
        const string listSql = """
            SELECT archive_id FROM elitescada.historian_archive_jobs
            WHERE state = 'Completed' AND completed_at_utc < @cutoff
              AND archive_file_removed_at_utc IS NULL
            ORDER BY completed_at_utc LIMIT 25;
            """;
        await using (var list = new NpgsqlCommand(listSql, connection))
        {
            list.Parameters.AddWithValue("cutoff", cutoff);
            await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) expired.Add(reader.GetGuid(0));
        }

        foreach (var id in expired)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { if (File.Exists(PackagePath(id))) File.Delete(PackagePath(id)); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            const string markSql = """
                UPDATE elitescada.historian_archive_jobs
                SET archive_file_removed_at_utc = @now, updated_at_utc = @now
                WHERE archive_id = @id AND archive_file_removed_at_utc IS NULL;
                """;
            await using var mark = new NpgsqlCommand(markSql, connection);
            mark.Parameters.AddWithValue("id", id);
            mark.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
            await mark.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<(bool AlreadyRestored, long SamplesInserted)> RestoreSegmentAsync(
        Guid archiveId,
        HistorianArchiveSegment segment,
        ZipArchiveEntry entry,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (var rangeLock = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0));", connection, transaction))
        {
            rangeLock.Parameters.AddWithValue("key", $"historian-archive-restore:{segment.FromUtc.UtcTicks}:{segment.ToUtc.UtcTicks}");
            await rangeLock.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        const string existingSql = """
            SELECT sha256
            FROM elitescada.historian_archive_restored_segments
            WHERE archive_id = @id AND sequence = @sequence
            FOR UPDATE;
            """;
        string? existingSha;
        await using (var existing = new NpgsqlCommand(existingSql, connection, transaction))
        {
            existing.Parameters.AddWithValue("id", archiveId);
            existing.Parameters.AddWithValue("sequence", segment.Sequence);
            existingSha = (string?)await existing.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
        if (existingSha is not null)
        {
            if (!string.Equals(existingSha.Trim(), segment.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("This archive ID was previously restored with a different segment checksum.");
        }

        await using (var create = new NpgsqlCommand("CREATE TEMP TABLE historian_archive_restore_rows (LIKE elitescada.tag_history INCLUDING DEFAULTS) ON COMMIT DROP;", connection, transaction))
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using (var copy = await connection.BeginBinaryImportAsync("""
            COPY historian_archive_restore_rows (
                tag_id, ts, quality, source, value, data_type, source_ts, source_ts_evidence,
                server_ts, server_ts_evidence, capture_profile_id, capture_policy_state,
                capture_policy_fingerprint) FROM STDIN (FORMAT BINARY)
            """, cancellationToken).ConfigureAwait(false))
        await using (var compressed = entry.Open())
        await using (var gzip = new GZipStream(compressed, CompressionMode.Decompress, leaveOpen: true))
        using (var reader = new StreamReader(gzip))
        {
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sample = JsonSerializer.Deserialize<HistorianArchiveSample>(line, ArchiveJsonOptions)
                    ?? throw new InvalidDataException("Historian archive sample is empty.");
                await copy.StartRowAsync(cancellationToken).ConfigureAwait(false);
                await copy.WriteAsync(sample.TagId, NpgsqlDbType.Uuid, cancellationToken).ConfigureAwait(false);
                await copy.WriteAsync(sample.Timestamp.UtcDateTime, NpgsqlDbType.TimestampTz, cancellationToken).ConfigureAwait(false);
                await copy.WriteAsync(sample.Quality, NpgsqlDbType.Integer, cancellationToken).ConfigureAwait(false);
                await WriteNullableAsync(copy, sample.Source, NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
                await copy.WriteAsync(sample.ValueJson, NpgsqlDbType.Jsonb, cancellationToken).ConfigureAwait(false);
                await WriteNullableAsync(copy, sample.DataType, NpgsqlDbType.Smallint, cancellationToken).ConfigureAwait(false);
                await WriteNullableAsync(copy, sample.SourceTimestamp?.UtcDateTime, NpgsqlDbType.TimestampTz, cancellationToken).ConfigureAwait(false);
                await WriteNullableAsync(copy, sample.SourceTimestampEvidence, NpgsqlDbType.Smallint, cancellationToken).ConfigureAwait(false);
                await WriteNullableAsync(copy, sample.ServerTimestamp?.UtcDateTime, NpgsqlDbType.TimestampTz, cancellationToken).ConfigureAwait(false);
                await WriteNullableAsync(copy, sample.ServerTimestampEvidence, NpgsqlDbType.Smallint, cancellationToken).ConfigureAwait(false);
                await WriteNullableAsync(copy, sample.CaptureProfileId, NpgsqlDbType.Uuid, cancellationToken).ConfigureAwait(false);
                await WriteNullableAsync(copy, sample.CapturePolicyState, NpgsqlDbType.Smallint, cancellationToken).ConfigureAwait(false);
                byte[]? fingerprint = sample.CapturePolicyFingerprintBase64 is null
                    ? null
                    : Convert.FromBase64String(sample.CapturePolicyFingerprintBase64);
                await WriteNullableAsync(copy, fingerprint, NpgsqlDbType.Bytea, cancellationToken).ConfigureAwait(false);
            }
            await copy.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }

        const string mergeSql = """
            WITH incoming_keys AS (
                SELECT DISTINCT tag_id, ts, quality, source, value, data_type, source_ts,
                       source_ts_evidence, server_ts, server_ts_evidence, capture_profile_id,
                       capture_policy_state, capture_policy_fingerprint
                FROM historian_archive_restore_rows
            ), existing AS (
                SELECT h.tag_id, h.ts, h.quality, h.source, h.value, h.data_type, h.source_ts,
                       h.source_ts_evidence, h.server_ts, h.server_ts_evidence, h.capture_profile_id,
                       h.capture_policy_state, h.capture_policy_fingerprint, COUNT(*) AS existing_count
                FROM elitescada.tag_history h
                JOIN incoming_keys i
                  ON h.tag_id = i.tag_id AND h.ts = i.ts AND h.quality = i.quality
                 AND h.source IS NOT DISTINCT FROM i.source AND h.value = i.value
                 AND h.data_type IS NOT DISTINCT FROM i.data_type
                 AND h.source_ts IS NOT DISTINCT FROM i.source_ts
                 AND h.source_ts_evidence IS NOT DISTINCT FROM i.source_ts_evidence
                 AND h.server_ts IS NOT DISTINCT FROM i.server_ts
                 AND h.server_ts_evidence IS NOT DISTINCT FROM i.server_ts_evidence
                 AND h.capture_profile_id IS NOT DISTINCT FROM i.capture_profile_id
                 AND h.capture_policy_state IS NOT DISTINCT FROM i.capture_policy_state
                 AND h.capture_policy_fingerprint IS NOT DISTINCT FROM i.capture_policy_fingerprint
                WHERE h.ts >= @from AND h.ts < @to
                GROUP BY h.tag_id, h.ts, h.quality, h.source, h.value, h.data_type, h.source_ts,
                         h.source_ts_evidence, h.server_ts, h.server_ts_evidence, h.capture_profile_id,
                         h.capture_policy_state, h.capture_policy_fingerprint
            ), incoming AS (
                SELECT r.*,
                       ROW_NUMBER() OVER (
                           PARTITION BY tag_id, ts, quality, source, value, data_type, source_ts,
                                        source_ts_evidence, server_ts, server_ts_evidence,
                                        capture_profile_id, capture_policy_state, capture_policy_fingerprint
                           ORDER BY ctid) AS occurrence
                FROM historian_archive_restore_rows r
            )
            INSERT INTO elitescada.tag_history(
                tag_id, ts, quality, source, value, data_type, source_ts, source_ts_evidence,
                server_ts, server_ts_evidence, capture_profile_id, capture_policy_state,
                capture_policy_fingerprint)
            SELECT i.tag_id, i.ts, i.quality, i.source, i.value, i.data_type, i.source_ts,
                   i.source_ts_evidence, i.server_ts, i.server_ts_evidence, i.capture_profile_id,
                   i.capture_policy_state, i.capture_policy_fingerprint
            FROM incoming i
            LEFT JOIN existing e
              ON i.tag_id = e.tag_id AND i.ts = e.ts AND i.quality = e.quality
             AND i.source IS NOT DISTINCT FROM e.source AND i.value = e.value
             AND i.data_type IS NOT DISTINCT FROM e.data_type
             AND i.source_ts IS NOT DISTINCT FROM e.source_ts
             AND i.source_ts_evidence IS NOT DISTINCT FROM e.source_ts_evidence
             AND i.server_ts IS NOT DISTINCT FROM e.server_ts
             AND i.server_ts_evidence IS NOT DISTINCT FROM e.server_ts_evidence
             AND i.capture_profile_id IS NOT DISTINCT FROM e.capture_profile_id
             AND i.capture_policy_state IS NOT DISTINCT FROM e.capture_policy_state
             AND i.capture_policy_fingerprint IS NOT DISTINCT FROM e.capture_policy_fingerprint
            WHERE i.occurrence > COALESCE(e.existing_count, 0);
            """;
        await using var merge = new NpgsqlCommand(mergeSql, connection, transaction);
        merge.Parameters.AddWithValue("from", segment.FromUtc);
        merge.Parameters.AddWithValue("to", segment.ToUtc);
        var samplesInserted = await merge.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        const string markerSql = """
            INSERT INTO elitescada.historian_archive_restored_segments(
                archive_id, sequence, sha256, sample_count, restored_at_utc)
            VALUES (@id, @sequence, @sha, @count, @now)
            ON CONFLICT (archive_id, sequence) DO UPDATE SET
                sha256 = EXCLUDED.sha256,
                sample_count = EXCLUDED.sample_count,
                restored_at_utc = EXCLUDED.restored_at_utc;
            """;
        await using (var marker = new NpgsqlCommand(markerSql, connection, transaction))
        {
            marker.Parameters.AddWithValue("id", archiveId);
            marker.Parameters.AddWithValue("sequence", segment.Sequence);
            marker.Parameters.AddWithValue("sha", segment.Sha256);
            marker.Parameters.AddWithValue("count", segment.SampleCount);
            marker.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
            await marker.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return (existingSha is not null && samplesInserted == 0, samplesInserted);
    }

    private static async Task WriteNullableAsync<T>(
        NpgsqlBinaryImporter importer,
        T? value,
        NpgsqlDbType type,
        CancellationToken cancellationToken) where T : struct
    {
        if (value.HasValue) await importer.WriteAsync(value.Value, type, cancellationToken).ConfigureAwait(false);
        else await importer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteNullableAsync(
        NpgsqlBinaryImporter importer,
        string? value,
        NpgsqlDbType type,
        CancellationToken cancellationToken)
    {
        if (value is null) await importer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
        else await importer.WriteAsync(value, type, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteNullableAsync(
        NpgsqlBinaryImporter importer,
        byte[]? value,
        NpgsqlDbType type,
        CancellationToken cancellationToken)
    {
        if (value is null) await importer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
        else await importer.WriteAsync(value, type, cancellationToken).ConfigureAwait(false);
    }

    private async IAsyncEnumerable<HistorianArchiveSample> ReadSegmentSamplesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT tag_id, ts, quality, source, value::text, data_type,
                   source_ts, source_ts_evidence, server_ts, server_ts_evidence,
                   capture_profile_id, capture_policy_state, capture_policy_fingerprint
            FROM elitescada.tag_history
            WHERE ts >= @from AND ts < @to
            ORDER BY ts, tag_id;
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("from", fromUtc);
        command.Parameters.AddWithValue("to", toUtc);
        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new HistorianArchiveSample(
                reader.GetGuid(0),
                reader.GetFieldValue<DateTimeOffset>(1).ToUniversalTime(),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt16(5),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6).ToUniversalTime(),
                reader.IsDBNull(7) ? null : reader.GetInt16(7),
                reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8).ToUniversalTime(),
                reader.IsDBNull(9) ? null : reader.GetInt16(9),
                reader.IsDBNull(10) ? null : reader.GetGuid(10),
                reader.IsDBNull(11) ? null : reader.GetInt16(11),
                reader.IsDBNull(12) ? null : Convert.ToBase64String(reader.GetFieldValue<byte[]>(12)));
        }
    }

    private async Task<IReadOnlyList<HistorianArchiveSegment>> ReadSegmentsAsync(
        NpgsqlConnection connection,
        Guid archiveId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT sequence, file_name, from_utc, to_utc, sample_count, sha256
            FROM elitescada.historian_archive_job_segments
            WHERE archive_id = @id
            ORDER BY sequence;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", archiveId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var segments = new List<HistorianArchiveSegment>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            segments.Add(new HistorianArchiveSegment(
                reader.GetInt32(0), reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2).ToUniversalTime(),
                reader.GetFieldValue<DateTimeOffset>(3).ToUniversalTime(),
                reader.GetInt64(4), reader.GetString(5).Trim()));
        return segments;
    }

    private async Task<HistorianArchiveJob?> ReadJobAsync(NpgsqlConnection connection, Guid archiveId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT archive_id, from_utc, to_utc, next_segment_from_utc, next_sequence, state,
                   delete_source_after_verification, samples_archived, failure_code,
                   package_sha256, archive_length_bytes, created_at_utc,
                   updated_at_utc, completed_at_utc, archive_file_removed_at_utc
            FROM elitescada.historian_archive_jobs WHERE archive_id = @id;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", archiveId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadJob(reader) : null;
    }

    private async Task BlockJobAsync(NpgsqlConnection connection, Guid archiveId, string failureCode, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = """
                UPDATE elitescada.historian_archive_jobs
                SET state = 'Blocked', failure_code = @failure, updated_at_utc = @now
                WHERE archive_id = @id AND state <> 'Completed';
                """;
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", archiveId);
            command.Parameters.AddWithValue("failure", failureCode);
            command.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (NpgsqlException) { }
    }

    private async Task EnsureInfrastructureAsync(CancellationToken cancellationToken) =>
        await TimescaleHistorianInfrastructure.EnsureRawAsync(_dataSource, cancellationToken).ConfigureAwait(false);

    private static HistorianArchiveJob ReadJob(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetFieldValue<DateTimeOffset>(1).ToUniversalTime(),
        reader.GetFieldValue<DateTimeOffset>(2).ToUniversalTime(),
        reader.GetFieldValue<DateTimeOffset>(3).ToUniversalTime(),
        reader.GetInt32(4),
        Enum.TryParse<HistorianArchiveJobState>(reader.GetString(5), out var state) ? state : HistorianArchiveJobState.Blocked,
        reader.GetBoolean(6),
        reader.GetInt64(7),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.IsDBNull(9) ? null : reader.GetString(9).Trim(),
        reader.IsDBNull(10) ? null : reader.GetInt64(10),
        reader.GetFieldValue<DateTimeOffset>(11).ToUniversalTime(),
        reader.GetFieldValue<DateTimeOffset>(12).ToUniversalTime(),
        reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13).ToUniversalTime(),
        reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14).ToUniversalTime());

    private string StageDirectory(Guid id) => Path.Combine(_root, $".{id:N}.staging");
    private string PackagePath(Guid id) => Path.Combine(_root, $"{id:N}.scadhist");

    private static string MapFailureCode(Exception exception) => exception switch
    {
        IOException or UnauthorizedAccessException => "archive-destination-unavailable",
        InvalidDataException => "archive-verification-failed",
        NpgsqlException => "historian-database-unavailable",
        _ => "archive-processing-failed"
    };

    private static async Task CopyBoundedAsync(Stream source, string destinationPath, long maximumBytes, CancellationToken cancellationToken)
    {
        await using var destination = new FileStream(
            destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[64 * 1024];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total = checked(total + read);
            if (total > maximumBytes) throw new InvalidDataException("Historian archive upload exceeds the configured maximum size.");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        destination.Flush(flushToDisk: true);
    }

    private static readonly JsonSerializerOptions ArchiveJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public async ValueTask DisposeAsync() => await _dataSource.DisposeAsync().ConfigureAwait(false);
}
