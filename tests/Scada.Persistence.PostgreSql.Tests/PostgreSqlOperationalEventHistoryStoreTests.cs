using Npgsql;
using Scada.Core.Events;
using Scada.Core.HistoricalQueries;
using Scada.Persistence.PostgreSql;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlOperationalEventHistoryStoreTests
{
    [Fact]
    public async Task Store_PersistsAcrossRestartAndSupportsEventBrowserFilters()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var eventId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var marker = $"integration.event.{Guid.NewGuid():N}";
        var timestamp = DateTimeOffset.UtcNow.AddSeconds(-1);
        var occurrence = new OperationalEventOccurred(
            eventId,
            definitionId,
            marker,
            "state-change",
            "process",
            "runtime.transition",
            "Area01",
            "Area01/Pump01",
            tagId,
            "Area01/Pump01/Running",
            "operator-1",
            "start",
            commandId,
            "pump01.start",
            "Pump started",
            new Dictionary<string, string> { ["from"] = "stopped", ["to"] = "running" },
            timestamp);

        await using (var writer = new PostgreSqlOperationalEventHistoryStore(connectionString))
            await writer.AppendAsync(occurrence);

        // A new store instance simulates process restart. Querying the same row proves
        // history authority is PostgreSQL, not an in-memory event list.
        await using var reader = new PostgreSqlOperationalEventHistoryStore(connectionString);
        var dataset = HistoricalQueryCatalog.Require(HistoricalDatasets.OperationalEvents);
        var page = await reader.QueryAsync(new HistoricalQueryExecution(
            dataset,
            new HistoricalResolvedRange(timestamp.AddMinutes(-1), DateTimeOffset.UtcNow.AddSeconds(1)),
            [
                new HistoricalFilter(
                    "definition.key",
                    HistoricalFilterOperator.Eq,
                    [HistoricalQueryValue.FromString(marker)]),
                new HistoricalFilter(
                    "type",
                    HistoricalFilterOperator.Eq,
                    [HistoricalQueryValue.FromString("state-change")]),
                new HistoricalFilter(
                    "tag.id",
                    HistoricalFilterOperator.Eq,
                    [HistoricalQueryValue.FromGuid(tagId)]),
                new HistoricalFilter(
                    "operator",
                    HistoricalFilterOperator.Eq,
                    [HistoricalQueryValue.FromString("operator-1")])
            ],
            "Pump",
            new HistoricalSort(),
            10,
            null));

        var row = Assert.Single(page.Rows);
        Assert.Equal(eventId.ToString("D"), row.Cells["event.id"].Value);
        Assert.Equal(definitionId.ToString("D"), row.Cells["definition.id"].Value);
        Assert.Equal("process", row.Cells["category"].Value);
        Assert.Equal("runtime.transition", row.Cells["source"].Value);
        Assert.Equal(commandId.ToString("D"), row.Cells["command.id"].Value);
        Assert.Contains("running", row.Cells["context"].Value);
    }

    [Fact]
    public async Task Store_IsAppendOnlyAtDatabaseBoundary()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var eventId = Guid.NewGuid();
        await using var store = new PostgreSqlOperationalEventHistoryStore(connectionString);
        await store.AppendAsync(new OperationalEventOccurred(
            eventId,
            Guid.NewGuid(),
            $"append.only.{Guid.NewGuid():N}",
            "transition",
            "process",
            "runtime",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "append only",
            new Dictionary<string, string>(),
            DateTimeOffset.UtcNow.AddSeconds(-1)));

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "DELETE FROM elitescada.operational_event_history WHERE event_id = @event_id;",
            connection);
        command.Parameters.AddWithValue("event_id", eventId);
        await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
    }
}