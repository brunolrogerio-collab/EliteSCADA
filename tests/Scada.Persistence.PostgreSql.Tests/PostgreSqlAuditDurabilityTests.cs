using Npgsql;
using Scada.Persistence.PostgreSql;
using Scada.Security.Audit;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlAuditDurabilityTests
{
    [Fact]
    public async Task Store_PersistsAcrossStoreInstancesAndPreservesExtendedContext()
    {
        var connectionString = TestConnectionString();
        if (connectionString is null) return;

        var marker = Guid.NewGuid().ToString("N");
        var auditEvent = AuditEvent.Create(
            $"subject-{marker}",
            "Durable User",
            AuditActions.EngineeringSave,
            AuditOutcome.Succeeded,
            "project",
            marker,
            new Dictionary<string, string> { ["operation"] = "save" },
            $"corr-{marker}",
            "Area-Durable",
            $"project-{marker}",
            42,
            new[] { "developer", "auditor" },
            "api");

        await using (var first = new PostgreSqlAuditStore(connectionString))
        {
            await first.InitializeAsync();
            await first.WriteAsync(auditEvent);
        }

        await using var second = new PostgreSqlAuditStore(connectionString);
        await second.InitializeAsync();
        var page = await second.QueryPageAsync(new AuditQuery(
            PageSize: 10,
            SubjectId: $"subject-{marker}",
            CorrelationId: $"corr-{marker}"));

        var persisted = Assert.Single(page.Events);
        Assert.Equal(auditEvent.Id, persisted.Id);
        Assert.Equal("Area-Durable", persisted.Area);
        Assert.Equal($"project-{marker}", persisted.ProjectKey);
        Assert.Equal(42, persisted.Revision);
        Assert.Equal(new[] { "developer", "auditor" }, persisted.Roles);
        Assert.Equal("api", persisted.Source);
    }

    [Fact]
    public async Task Store_KeysetPaginationIsStableForEqualTimestampsAndCombinedFilters()
    {
        var connectionString = TestConnectionString();
        if (connectionString is null) return;

        await using var store = new PostgreSqlAuditStore(connectionString, new AuditQueryPolicy(10));
        await store.InitializeAsync();

        var marker = Guid.NewGuid().ToString("N");
        var timestamp = new DateTimeOffset(2036, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var ids = new[]
        {
            Guid.Parse($"10000000-0000-0000-0000-{marker[..12]}"),
            Guid.Parse($"20000000-0000-0000-0000-{marker[..12]}"),
            Guid.Parse($"30000000-0000-0000-0000-{marker[..12]}")
        };

        foreach (var id in ids)
        {
            await store.WriteAsync(new AuditEvent(
                id,
                timestamp,
                $"subject-{marker}",
                null,
                AuditActions.CommandExecute,
                AuditOutcome.Denied,
                "command",
                $"command-{marker}",
                CorrelationId: $"corr-{marker}",
                Area: $"area-{marker}"));
        }

        await store.WriteAsync(AuditEvent.Create(
            $"other-{marker}",
            null,
            AuditActions.TagWrite,
            AuditOutcome.Succeeded,
            "tag",
            $"other-{marker}"));

        var first = await store.QueryPageAsync(new AuditQuery(
            PageSize: 2,
            FromUtc: timestamp.AddSeconds(-1),
            ToUtc: timestamp.AddSeconds(1),
            SubjectId: $"subject-{marker}",
            Action: AuditActions.CommandExecute,
            Outcome: AuditOutcome.Denied,
            TargetKind: "command",
            TargetId: $"command-{marker}",
            Area: $"area-{marker}",
            CorrelationId: $"corr-{marker}"));

        Assert.Equal(new[] { ids[2], ids[1] }, first.Events.Select(x => x.Id));
        Assert.NotNull(first.NextCursor);

        var second = await store.QueryPageAsync(new AuditQuery(
            PageSize: 2,
            SubjectId: $"subject-{marker}",
            Action: AuditActions.CommandExecute,
            Outcome: AuditOutcome.Denied,
            TargetKind: "command",
            TargetId: $"command-{marker}",
            Area: $"area-{marker}",
            CorrelationId: $"corr-{marker}",
            After: first.NextCursor));

        Assert.Equal(ids[0], Assert.Single(second.Events).Id);
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task Store_SanitizesSensitiveMetadataBeforePostgreSqlPersistence()
    {
        var connectionString = TestConnectionString();
        if (connectionString is null) return;

        await using var store = new PostgreSqlAuditStore(connectionString);
        await store.InitializeAsync();
        var marker = Guid.NewGuid().ToString("N");

        await store.WriteAsync(AuditEvent.Create(
            $"subject-{marker}",
            null,
            AuditActions.AuthenticationLogin,
            AuditOutcome.Failed,
            "identity",
            marker,
            new Dictionary<string, string>
            {
                ["password"] = "PlainTextMustNeverPersist",
                ["apiKey"] = "secret-key",
                ["safe"] = "allowed",
                ["note"] = "Bearer this-must-be-redacted"
            }));

        var persisted = Assert.Single(await store.QueryAsync(subjectId: $"subject-{marker}"));
        Assert.NotNull(persisted.Details);
        Assert.False(persisted.Details!.ContainsKey("password"));
        Assert.False(persisted.Details.ContainsKey("apiKey"));
        Assert.Equal("allowed", persisted.Details["safe"]);
        Assert.Equal("[REDACTED]", persisted.Details["note"]);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT details::text FROM elitescada.audit_events WHERE id = @id;",
            connection);
        command.Parameters.AddWithValue("id", persisted.Id);
        var raw = Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
        Assert.DoesNotContain("PlainTextMustNeverPersist", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-key", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("this-must-be-redacted", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Store_RetentionDeletesInBatchesButOrdinaryDeleteRemainsRejected()
    {
        var connectionString = TestConnectionString();
        if (connectionString is null) return;

        await using var store = new PostgreSqlAuditStore(connectionString);
        await store.InitializeAsync();
        var marker = Guid.NewGuid().ToString("N");
        var cutoff = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var old = new[]
        {
            HistoricalEvent(marker, 1, cutoff.AddDays(-3)),
            HistoricalEvent(marker, 2, cutoff.AddDays(-2)),
            HistoricalEvent(marker, 3, cutoff.AddDays(-1))
        };
        var exact = HistoricalEvent(marker, 4, cutoff);
        var newer = HistoricalEvent(marker, 5, cutoff.AddTicks(1));

        foreach (var auditEvent in old.Append(exact).Append(newer))
            await store.WriteAsync(auditEvent);

        var firstDeleted = await store.ApplyRetentionBatchAsync(cutoff, 2);
        Assert.Equal(2, firstDeleted);
        var secondDeleted = await store.ApplyRetentionBatchAsync(cutoff, 2);
        Assert.Equal(1, secondDeleted);

        var remaining = await store.QueryPageAsync(new AuditQuery(
            PageSize: 10,
            SubjectId: $"retention-{marker}"));
        Assert.Equal(2, remaining.Events.Count);
        Assert.Contains(remaining.Events, x => x.Id == exact.Id);
        Assert.Contains(remaining.Events, x => x.Id == newer.Id);
        Assert.DoesNotContain(remaining.Events, x => old.Any(oldEvent => oldEvent.Id == x.Id));

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var delete = new NpgsqlCommand(
            "DELETE FROM elitescada.audit_events WHERE id = @id;",
            connection);
        delete.Parameters.AddWithValue("id", exact.Id);
        var error = await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync());
        Assert.Contains("append-only", error.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Store_SupportsConcurrentAppendsAndReportsPersistenceHealth()
    {
        var connectionString = TestConnectionString();
        if (connectionString is null) return;

        await using var store = new PostgreSqlAuditStore(connectionString);
        await store.InitializeAsync();
        var marker = Guid.NewGuid().ToString("N");

        var writes = Enumerable.Range(0, 50)
            .Select(index => store.WriteAsync(AuditEvent.Create(
                $"concurrent-{marker}",
                null,
                AuditActions.AuditRead,
                AuditOutcome.Succeeded,
                "test",
                index.ToString(System.Globalization.CultureInfo.InvariantCulture))).AsTask());
        await Task.WhenAll(writes);

        var page = await store.QueryPageAsync(new AuditQuery(
            PageSize: 100,
            SubjectId: $"concurrent-{marker}"));
        Assert.Equal(50, page.Events.Count);
        Assert.Equal(50, page.Events.Select(x => x.Id).Distinct().Count());

        var health = store.GetHealthSnapshot();
        Assert.True(health.PersistedCount >= 50);
        Assert.Equal(0, health.AppendFailureCount);
        Assert.NotNull(health.LastPersistedAtUtc);
    }

    [Fact]
    public async Task Store_MigrationAndInitializationRemainIdempotent()
    {
        var connectionString = TestConnectionString();
        if (connectionString is null) return;

        await using var first = new PostgreSqlAuditStore(connectionString);
        await first.InitializeAsync();
        await first.InitializeAsync();

        await using var second = new PostgreSqlAuditStore(connectionString);
        await second.InitializeAsync();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM elitescada.schema_migrations WHERE migration_key = '007_audit_retention_query_foundation';",
            connection);
        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    private static string? TestConnectionString() =>
        Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");

    private static AuditEvent HistoricalEvent(string marker, int ordinal, DateTimeOffset timestamp) => new(
        Guid.NewGuid(),
        timestamp,
        $"retention-{marker}",
        null,
        AuditActions.AuditRead,
        AuditOutcome.Succeeded,
        "retention-test",
        ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
