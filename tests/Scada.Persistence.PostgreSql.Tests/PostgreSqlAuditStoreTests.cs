using Npgsql;
using Scada.Persistence.PostgreSql;
using Scada.Security.Audit;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlAuditStoreTests
{
    [Fact]
    public async Task Store_WritesQueriesAndFiltersAuditEvents()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlAuditStore(connectionString);
        await store.InitializeAsync();
        await store.InitializeAsync();

        var marker = Guid.NewGuid().ToString("N");
        var first = AuditEvent.Create(
            $"operator-{marker}",
            "Operator Test",
            AuditActions.TagWrite,
            AuditOutcome.Denied,
            "tag",
            $"Plant.{marker}.Setpoint",
            new Dictionary<string, string> { ["source"] = "integration-test" },
            $"corr-{marker}-1");
        var second = AuditEvent.Create(
            $"developer-{marker}",
            "Developer Test",
            AuditActions.TagWrite,
            AuditOutcome.Succeeded,
            "tag",
            $"Plant.{marker}.Setpoint",
            new Dictionary<string, string> { ["source"] = "integration-test" },
            $"corr-{marker}-2");
        var third = AuditEvent.Create(
            $"developer-{marker}",
            "Developer Test",
            AuditActions.EngineeringImportApply,
            AuditOutcome.Failed,
            "engineering-workspace",
            marker,
            correlationId: $"corr-{marker}-3");

        await store.WriteAsync(first);
        await store.WriteAsync(second);
        await store.WriteAsync(third);

        var bySubject = await store.QueryAsync(subjectId: $"developer-{marker}");
        Assert.Equal(2, bySubject.Count);
        Assert.All(bySubject, item => Assert.Equal($"developer-{marker}", item.SubjectId));

        var succeededWrites = await store.QueryAsync(
            action: AuditActions.TagWrite,
            outcome: AuditOutcome.Succeeded);
        Assert.Contains(succeededWrites, item => item.Id == second.Id);
        Assert.DoesNotContain(succeededWrites, item => item.Id == first.Id);

        var byTime = await store.QueryAsync(
            fromUtc: first.TimestampUtc.AddSeconds(-1),
            toUtc: third.TimestampUtc.AddSeconds(1));
        Assert.Contains(byTime, item => item.Id == first.Id);
        Assert.Contains(byTime, item => item.Id == second.Id);
        Assert.Contains(byTime, item => item.Id == third.Id);

        var persisted = bySubject.Single(item => item.Id == second.Id);
        Assert.Equal("Developer Test", persisted.DisplayName);
        Assert.Equal($"corr-{marker}-2", persisted.CorrelationId);
        Assert.Equal("integration-test", persisted.Details!["source"]);
    }

    [Fact]
    public async Task Store_DatabaseRejectsUpdateDeleteAndTruncate()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlAuditStore(connectionString);
        await store.InitializeAsync();

        var marker = Guid.NewGuid().ToString("N");
        var auditEvent = AuditEvent.Create(
            $"append-only-{marker}",
            null,
            AuditActions.AlarmAcknowledge,
            AuditOutcome.Succeeded,
            "alarm",
            marker);
        await store.WriteAsync(auditEvent);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var update = new NpgsqlCommand(
            "UPDATE elitescada.audit_events SET target_id = 'tampered' WHERE id = @id;",
            connection);
        update.Parameters.AddWithValue("id", auditEvent.Id);
        var updateError = await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());
        Assert.Contains("append-only", updateError.MessageText, StringComparison.OrdinalIgnoreCase);

        await using var delete = new NpgsqlCommand(
            "DELETE FROM elitescada.audit_events WHERE id = @id;",
            connection);
        delete.Parameters.AddWithValue("id", auditEvent.Id);
        var deleteError = await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync());
        Assert.Contains("append-only", deleteError.MessageText, StringComparison.OrdinalIgnoreCase);

        await using var truncate = new NpgsqlCommand(
            "TRUNCATE TABLE elitescada.audit_events;",
            connection);
        var truncateError = await Assert.ThrowsAsync<PostgresException>(() => truncate.ExecuteNonQueryAsync());
        Assert.Contains("append-only", truncateError.MessageText, StringComparison.OrdinalIgnoreCase);

        var persisted = await store.QueryAsync(subjectId: $"append-only-{marker}");
        var stored = Assert.Single(persisted);
        Assert.Equal(marker, stored.TargetId);
    }

    [Fact]
    public async Task Store_RejectsInvalidQueryBounds()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlAuditStore(connectionString);
        await store.InitializeAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.QueryAsync(limit: 0));
        await Assert.ThrowsAsync<ArgumentException>(() => store.QueryAsync(
            fromUtc: DateTimeOffset.UtcNow,
            toUtc: DateTimeOffset.UtcNow.AddMinutes(-1)));
    }
}
