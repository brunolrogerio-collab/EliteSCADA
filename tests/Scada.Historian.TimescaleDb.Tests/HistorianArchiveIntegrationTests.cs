using Npgsql;
using NpgsqlTypes;
using Scada.Historian.Archives;
using Scada.Historian.TimescaleDb.Archives;

namespace Scada.Historian.TimescaleDb.Tests;

public sealed class HistorianArchiveIntegrationTests
{
    [Fact]
    public async Task Archive_is_read_back_before_cleanup_and_restore_is_resumable_and_idempotent()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var root = Path.Combine(Path.GetTempPath(), $"scadivo-historian-archive-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var archiveId = Guid.Empty;
        var tagId = Guid.NewGuid();
        var from = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-3).AddHours(12), TimeSpan.Zero);
        var to = from.AddDays(1);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var service = new TimescaleDbHistorianArchiveService(connectionString, root);
        try
        {
            await service.CreateAsync(from, to, deleteSourceAfterVerification: true);
            var jobs = await service.ListAsync();
            var job = Assert.Single(jobs, x => x.FromUtc == from && x.ToUtc == to);
            archiveId = job.ArchiveId;
            await InsertSamplesAsync(dataSource, tagId, from.AddHours(1), from.AddHours(2));

            await service.ProcessNextAsync(); // Persist the first UTC-day segment.
            await InsertSampleAsync(dataSource, tagId, from.AddHours(2), "late-arrival");
            for (var attempt = 0; attempt < 8; attempt++)
            {
                await service.ProcessNextAsync();
                job = Assert.Single(await service.ListAsync(), x => x.ArchiveId == archiveId);
                if (job.State is HistorianArchiveJobState.Completed or HistorianArchiveJobState.Blocked) break;
            }
            Assert.Equal(HistorianArchiveJobState.Completed, job.State);
            Assert.Equal(1, await CountSamplesAsync(dataSource, tagId)); // A late row not present in the verified package is preserved.

            var archivePath = Path.Combine(root, $"{archiveId:N}.scadhist");
            var verification = await HistorianArchiveFormat.VerifyPackageAsync(archivePath);
            Assert.Equal(2, verification.Manifest.Segments.Sum(x => x.SampleCount));
            Assert.NotNull(job.Sha256);
            Assert.Equal(job.Sha256, verification.ArchiveSha256);

            await DeleteSourceForTestAsync(dataSource, tagId);
            Assert.Equal(0, await CountSamplesAsync(dataSource, tagId));

            await using var restoreFile = File.OpenRead(archivePath);
            var restored = await service.RestoreAsync(restoreFile);
            Assert.Equal(2, restored.SamplesRestored);
            Assert.Equal(2, restored.SegmentsRestored);
            Assert.Equal(2, await CountSamplesAsync(dataSource, tagId));

            await using var replayFile = File.OpenRead(archivePath);
            var replay = await service.RestoreAsync(replayFile);
            Assert.Equal(0, replay.SamplesRestored);
            Assert.Equal(2, replay.SegmentsAlreadyRestored);
            Assert.Equal(2, await CountSamplesAsync(dataSource, tagId));
        }
        finally
        {
            await DeleteSourceForTestAsync(dataSource, tagId);
            if (archiveId != Guid.Empty)
            {
                await using (var restoredSegments = dataSource.CreateCommand("DELETE FROM elitescada.historian_archive_restored_segments WHERE archive_id = @id;"))
                {
                    restoredSegments.Parameters.AddWithValue("id", archiveId);
                    await restoredSegments.ExecuteNonQueryAsync();
                }
                await using (var archiveJob = dataSource.CreateCommand("DELETE FROM elitescada.historian_archive_jobs WHERE archive_id = @id;"))
                {
                    archiveJob.Parameters.AddWithValue("id", archiveId);
                    await archiveJob.ExecuteNonQueryAsync();
                }
            }
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Archive_recovers_verified_segment_after_process_stops_before_database_checkpoint()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var root = Path.Combine(Path.GetTempPath(), $"scadivo-historian-archive-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var archiveId = Guid.Empty;
        var tagId = Guid.NewGuid();
        var timestamp = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-5).AddHours(1), TimeSpan.Zero);
        var from = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-5), TimeSpan.Zero);
        var to = from.AddDays(1);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var service = new TimescaleDbHistorianArchiveService(connectionString, root);
        try
        {
            var job = await service.CreateAsync(from, to, deleteSourceAfterVerification: false);
            archiveId = job.ArchiveId;
            await InsertSampleAsync(dataSource, tagId, timestamp, "before-interruption");

            var stagedDirectory = Path.Combine(root, $".{archiveId:N}.staging");
            var stagedPath = Path.Combine(stagedDirectory, "segment-000000.jsonl.gz");
            var sample = new HistorianArchiveSample(
                tagId, timestamp, 0, "archive-integration-test", System.Text.Json.JsonSerializer.Serialize("before-interruption"),
                4, null, null, null, null, null, null, null);
            await HistorianArchiveFormat.WriteSegmentAsync(stagedPath, 0, from, to, OneSampleAsync(sample));

            // Model retention removing source data after the verified stage was flushed,
            // but before the worker could persist the checkpoint in PostgreSQL.
            await DeleteSourceForTestAsync(dataSource, tagId);
            await service.ProcessNextAsync();
            job = Assert.Single(await service.ListAsync(), x => x.ArchiveId == archiveId);
            Assert.Equal(to, job.NextSegmentFromUtc);
            Assert.Equal(1, job.SamplesArchived);

            await service.ProcessNextAsync();
            job = Assert.Single(await service.ListAsync(), x => x.ArchiveId == archiveId);
            Assert.Equal(HistorianArchiveJobState.Completed, job.State);
            var verification = await HistorianArchiveFormat.VerifyPackageAsync(Path.Combine(root, $"{archiveId:N}.scadhist"));
            Assert.Equal(1, verification.Manifest.Segments.Sum(x => x.SampleCount));
            Assert.Equal(0, await CountSamplesAsync(dataSource, tagId));
        }
        finally
        {
            await DeleteSourceForTestAsync(dataSource, tagId);
            if (archiveId != Guid.Empty)
            {
                await using var deleteJob = dataSource.CreateCommand("DELETE FROM elitescada.historian_archive_jobs WHERE archive_id = @id;");
                deleteJob.Parameters.AddWithValue("id", archiveId);
                await deleteJob.ExecuteNonQueryAsync();
            }
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Archive_destination_failure_preserves_source_and_can_resume_after_repair()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var root = Path.Combine(Path.GetTempPath(), $"scadivo-historian-archive-destination-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var archiveId = Guid.Empty;
        var tagId = Guid.NewGuid();
        var from = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-6), TimeSpan.Zero);
        var timestamp = from.AddHours(2);
        var to = from.AddDays(1);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var service = new TimescaleDbHistorianArchiveService(connectionString, root);
        try
        {
            await InsertSampleAsync(dataSource, tagId, timestamp, "must-remain-until-verified");
            var job = await service.CreateAsync(from, to, deleteSourceAfterVerification: true);
            archiveId = job.ArchiveId;
            var stagePath = Path.Combine(root, $".{archiveId:N}.staging");
            await File.WriteAllTextAsync(stagePath, "destination unavailable: file blocks stage directory");

            await Assert.ThrowsAsync<IOException>(() => service.ProcessNextAsync());
            job = Assert.Single(await service.ListAsync(), x => x.ArchiveId == archiveId);
            Assert.Equal(HistorianArchiveJobState.Blocked, job.State);
            Assert.Equal("archive-destination-unavailable", job.FailureCode);
            Assert.Equal(1, await CountSamplesAsync(dataSource, tagId));

            File.Delete(stagePath);
            job = Assert.IsType<HistorianArchiveJob>(await service.ResumeAsync(archiveId));
            Assert.Equal(HistorianArchiveJobState.Queued, job.State);
            await service.ProcessNextAsync();
            await service.ProcessNextAsync();

            job = Assert.Single(await service.ListAsync(), x => x.ArchiveId == archiveId);
            Assert.Equal(HistorianArchiveJobState.Completed, job.State);
            Assert.Equal(0, await CountSamplesAsync(dataSource, tagId));
            var verification = await HistorianArchiveFormat.VerifyPackageAsync(Path.Combine(root, $"{archiveId:N}.scadhist"));
            Assert.Equal(1, verification.Manifest.Segments.Sum(x => x.SampleCount));
        }
        finally
        {
            await DeleteSourceForTestAsync(dataSource, tagId);
            if (archiveId != Guid.Empty)
            {
                await using var deleteJob = dataSource.CreateCommand("DELETE FROM elitescada.historian_archive_jobs WHERE archive_id = @id;");
                deleteJob.Parameters.AddWithValue("id", archiveId);
                await deleteJob.ExecuteNonQueryAsync();
            }
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Archive_cleanup_checkpoint_does_not_delete_late_duplicate_after_restart()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var root = Path.Combine(Path.GetTempPath(), $"scadivo-historian-cleanup-checkpoint-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var archiveId = Guid.Empty;
        var tagId = Guid.NewGuid();
        var from = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-8), TimeSpan.Zero);
        var archivedFirst = from.AddHours(2);
        var archivedSecond = from.AddDays(1).AddHours(2);
        var to = from.AddDays(2);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var service = new TimescaleDbHistorianArchiveService(connectionString, root);
        try
        {
            await InsertSampleAsync(dataSource, tagId, archivedFirst, "same-payload");
            await InsertSampleAsync(dataSource, tagId, archivedSecond, "later-day");
            var job = await service.CreateAsync(from, to, deleteSourceAfterVerification: true);
            archiveId = job.ArchiveId;
            await service.ProcessNextAsync();
            await service.ProcessNextAsync();

            // Model a committed first-segment cleanup, followed by a late-arriving
            // duplicate with the same payload before the process restarts.
            await using (var removeFirst = dataSource.CreateCommand("DELETE FROM elitescada.tag_history WHERE tag_id = @tag AND ts < @next;"))
            {
                removeFirst.Parameters.AddWithValue("tag", tagId);
                removeFirst.Parameters.AddWithValue("next", from.AddDays(1));
                await removeFirst.ExecuteNonQueryAsync();
            }
            await using (var markFirst = dataSource.CreateCommand("""
                UPDATE elitescada.historian_archive_job_segments
                SET source_cleanup_completed_at_utc = @now
                WHERE archive_id = @id AND sequence = 0;
                """))
            {
                markFirst.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
                markFirst.Parameters.AddWithValue("id", archiveId);
                await markFirst.ExecuteNonQueryAsync();
            }
            await InsertSampleAsync(dataSource, tagId, archivedFirst, "same-payload");

            await service.ProcessNextAsync();
            job = Assert.Single(await service.ListAsync(), x => x.ArchiveId == archiveId);
            Assert.Equal(HistorianArchiveJobState.Completed, job.State);
            Assert.Equal(1, await CountSamplesAsync(dataSource, tagId));
            Assert.Equal("same-payload", await ReadOnlyValueAsync(dataSource, tagId));
        }
        finally
        {
            await DeleteSourceForTestAsync(dataSource, tagId);
            if (archiveId != Guid.Empty)
            {
                await using var deleteJob = dataSource.CreateCommand("DELETE FROM elitescada.historian_archive_jobs WHERE archive_id = @id;");
                deleteJob.Parameters.AddWithValue("id", archiveId);
                await deleteJob.ExecuteNonQueryAsync();
            }
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Restore_does_not_duplicate_live_samples_and_can_recover_them_after_later_retention()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var root = Path.Combine(Path.GetTempPath(), $"scadivo-historian-restore-idempotent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var archiveId = Guid.Empty;
        var tagId = Guid.NewGuid();
        var from = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-9), TimeSpan.Zero);
        var to = from.AddDays(1);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var service = new TimescaleDbHistorianArchiveService(connectionString, root);
        try
        {
            await InsertSampleAsync(dataSource, tagId, from.AddHours(1), "still-live");
            var job = await service.CreateAsync(from, to, deleteSourceAfterVerification: false);
            archiveId = job.ArchiveId;
            await service.ProcessNextAsync();
            await service.ProcessNextAsync();
            var archivePath = Path.Combine(root, $"{archiveId:N}.scadhist");
            Assert.Equal(1, await CountSamplesAsync(dataSource, tagId));

            await using (var liveSourceRestore = File.OpenRead(archivePath))
            {
                var first = await service.RestoreAsync(liveSourceRestore);
                Assert.Equal(0, first.SamplesRestored);
                Assert.Equal(1, first.SegmentsRestored);
                Assert.Equal(0, first.SegmentsAlreadyRestored);
            }
            Assert.Equal(1, await CountSamplesAsync(dataSource, tagId));

            await using (var repeated = File.OpenRead(archivePath))
            {
                var second = await service.RestoreAsync(repeated);
                Assert.Equal(0, second.SamplesRestored);
                Assert.Equal(0, second.SegmentsRestored);
                Assert.Equal(1, second.SegmentsAlreadyRestored);
            }
            Assert.Equal(1, await CountSamplesAsync(dataSource, tagId));

            await DeleteSourceForTestAsync(dataSource, tagId); // A later retention pass removes the restored row.
            await using (var restoreAfterRetention = File.OpenRead(archivePath))
            {
                var recovered = await service.RestoreAsync(restoreAfterRetention);
                Assert.Equal(1, recovered.SamplesRestored);
                Assert.Equal(1, recovered.SegmentsRestored);
                Assert.Equal(0, recovered.SegmentsAlreadyRestored);
            }
            Assert.Equal(1, await CountSamplesAsync(dataSource, tagId));
        }
        finally
        {
            await DeleteSourceForTestAsync(dataSource, tagId);
            if (archiveId != Guid.Empty)
            {
                await using var deleteJob = dataSource.CreateCommand("DELETE FROM elitescada.historian_archive_jobs WHERE archive_id = @id;");
                deleteJob.Parameters.AddWithValue("id", archiveId);
                await deleteJob.ExecuteNonQueryAsync();
            }
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Scheduled_archive_advances_after_verification_and_rotates_only_completed_files()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var root = Path.Combine(Path.GetTempPath(), $"scadivo-historian-schedule-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var tagId = Guid.NewGuid();
        var archiveId = Guid.Empty;
        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-4), TimeSpan.Zero);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var service = new TimescaleDbHistorianArchiveService(connectionString, root);
        try
        {
            await DeleteSourceForTestAsync(dataSource, tagId);
            await InsertSampleAsync(dataSource, tagId, start.AddHours(1), "scheduled");
            var saved = await service.SaveScheduleAsync(new HistorianArchiveScheduleConfiguration(
                Enabled: true,
                StartFromUtc: start,
                ArchiveAfterDays: 1,
                IntervalDays: 1,
                WindowDays: 1,
                KeepArchivesDays: 1,
                DeleteSourceAfterVerification: false));
            Assert.Equal(start, saved.NextFromUtc);

            HistorianArchiveJob? job = null;
            for (var attempt = 0; attempt < 8; attempt++)
            {
                await service.ProcessNextAsync();
                var scheduledJobs = await service.ListAsync();
                job = scheduledJobs.FirstOrDefault(x => x.FromUtc == start && x.State is not HistorianArchiveJobState.Blocked);
                if (job?.State == HistorianArchiveJobState.Completed) break;
            }

            Assert.NotNull(job);
            Assert.Equal(HistorianArchiveJobState.Completed, job.State);
            archiveId = job.ArchiveId;
            var progressed = await service.GetScheduleAsync();
            Assert.Equal(job.ToUtc, progressed?.NextFromUtc);
            Assert.Equal(1, await CountSamplesAsync(dataSource, tagId));

            await using (var age = dataSource.CreateCommand("UPDATE elitescada.historian_archive_jobs SET completed_at_utc = @old WHERE archive_id = @id;"))
            {
                age.Parameters.AddWithValue("old", DateTimeOffset.UtcNow.AddDays(-2));
                age.Parameters.AddWithValue("id", archiveId);
                await age.ExecuteNonQueryAsync();
            }
            await service.ProcessNextAsync();
            var rotated = Assert.Single(await service.ListAsync(), x => x.ArchiveId == archiveId);
            Assert.NotNull(rotated.ArchiveFileRemovedAtUtc);
            Assert.False(File.Exists(Path.Combine(root, $"{archiveId:N}.scadhist")));
            Assert.Equal(1, await CountSamplesAsync(dataSource, tagId)); // File rotation never removes live Historian rows.
        }
        finally
        {
            await DeleteSourceForTestAsync(dataSource, tagId);
            await using (var resetSchedule = dataSource.CreateCommand("DELETE FROM elitescada.historian_archive_schedule WHERE singleton_id = 1;"))
                await resetSchedule.ExecuteNonQueryAsync();
            if (archiveId != Guid.Empty)
            {
                await using var deleteJob = dataSource.CreateCommand("DELETE FROM elitescada.historian_archive_jobs WHERE archive_id = @id;");
                deleteJob.Parameters.AddWithValue("id", archiveId);
                await deleteJob.ExecuteNonQueryAsync();
            }
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task InsertSamplesAsync(NpgsqlDataSource dataSource, Guid tagId, DateTimeOffset first, DateTimeOffset second)
    {
        const string sql = """
            INSERT INTO elitescada.tag_history(tag_id, ts, quality, source, value, data_type)
            VALUES (@tag, @ts, 0, 'archive-integration-test', @value, 4);
            """;
        await using var connection = await dataSource.OpenConnectionAsync();
        foreach (var timestamp in new[] { first, second })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("tag", tagId);
            command.Parameters.AddWithValue("ts", timestamp);
            command.Parameters.Add("value", NpgsqlDbType.Jsonb).Value = timestamp.Hour.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async IAsyncEnumerable<HistorianArchiveSample> OneSampleAsync(HistorianArchiveSample sample)
    {
        yield return sample;
        await Task.CompletedTask;
    }

    private static async Task InsertSampleAsync(NpgsqlDataSource dataSource, Guid tagId, DateTimeOffset timestamp, string value)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO elitescada.tag_history(tag_id, ts, quality, source, value, data_type)
            VALUES (@tag, @ts, 0, 'archive-integration-test', @value, 4);
            """);
        command.Parameters.AddWithValue("tag", tagId);
        command.Parameters.AddWithValue("ts", timestamp);
        command.Parameters.Add("value", NpgsqlDbType.Jsonb).Value = System.Text.Json.JsonSerializer.Serialize(value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountSamplesAsync(NpgsqlDataSource dataSource, Guid tagId)
    {
        await using var command = dataSource.CreateCommand("SELECT count(*) FROM elitescada.tag_history WHERE tag_id = @tag;");
        command.Parameters.AddWithValue("tag", tagId);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<string?> ReadOnlyValueAsync(NpgsqlDataSource dataSource, Guid tagId)
    {
        await using var command = dataSource.CreateCommand("SELECT value #>> '{}' FROM elitescada.tag_history WHERE tag_id = @tag;");
        command.Parameters.AddWithValue("tag", tagId);
        return (string?)await command.ExecuteScalarAsync();
    }

    private static async Task DeleteSourceForTestAsync(NpgsqlDataSource dataSource, Guid tagId)
    {
        await using var command = dataSource.CreateCommand("DELETE FROM elitescada.tag_history WHERE tag_id = @tag;");
        command.Parameters.AddWithValue("tag", tagId);
        await command.ExecuteNonQueryAsync();
    }
}
