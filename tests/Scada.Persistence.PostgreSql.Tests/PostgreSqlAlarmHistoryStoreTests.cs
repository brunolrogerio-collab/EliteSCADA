using Npgsql;
using Scada.Core.Alarms;
using Scada.Core.HistoricalQueries;
using Scada.Persistence.PostgreSql;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlAlarmHistoryStoreTests
{
    [Fact]
    public async Task Store_AppendsFiltersAndPagesAlarmTransitions()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlAlarmHistoryStore(connectionString);
        var alarmId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var tagPath = $"Integration.Alarm.{Guid.NewGuid():N}";
        var timestamp = DateTimeOffset.UtcNow.AddSeconds(-1);
        var previous = Instance(
            alarmId,
            tagId,
            AlarmState.Normal,
            AlarmPriority.High,
            timestamp);
        var active = previous with
        {
            State = AlarmState.Active,
            LastTransition = timestamp,
            Message = "High pressure"
        };
        var acknowledged = active with
        {
            State = AlarmState.Acknowledged,
            LastTransition = timestamp
        };

        await store.AppendAsync(
            new AlarmStateChanged(previous, active, timestamp),
            tagPath);
        await store.AppendAsync(
            new AlarmStateChanged(active, acknowledged, timestamp),
            tagPath);

        var dataset = HistoricalQueryCatalog.Require(HistoricalDatasets.AlarmEvents);
        var range = new HistoricalResolvedRange(
            timestamp.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddSeconds(1));
        var filters = new[]
        {
            new HistoricalFilter(
                "priority",
                HistoricalFilterOperator.Eq,
                [HistoricalQueryValue.FromNumber((int)AlarmPriority.High)]),
            new HistoricalFilter(
                "tag.path",
                HistoricalFilterOperator.Eq,
                [HistoricalQueryValue.FromString(tagPath)])
        };
        var first = await store.QueryAsync(new HistoricalQueryExecution(
            dataset,
            range,
            filters,
            "pressure",
            new HistoricalSort(),
            1,
            null));

        Assert.Single(first.Rows);
        Assert.NotNull(first.NextPosition);
        Assert.Equal(tagPath, first.Rows[0].Cells["tag.path"].Value);
        Assert.Equal("3", first.Rows[0].Cells["priority"].Value);

        var second = await store.QueryAsync(new HistoricalQueryExecution(
            dataset,
            range,
            filters,
            "pressure",
            new HistoricalSort(),
            1,
            first.NextPosition));

        Assert.Single(second.Rows);
        Assert.Null(second.NextPosition);
        Assert.Equal(alarmId.ToString("D"), second.Rows[0].Cells["alarm.id"].Value);
        Assert.NotEqual(
            first.Rows[0].Cells["state"].Value,
            second.Rows[0].Cells["state"].Value);
    }

    [Fact]
    public async Task Store_CombinesRepeatedFieldFiltersWithAndSemantics()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlAlarmHistoryStore(connectionString);
        var tagId = Guid.NewGuid();
        var tagPath = $"Integration.Alarm.Range.{Guid.NewGuid():N}";
        var timestamp = DateTimeOffset.UtcNow.AddSeconds(-1);

        foreach (var priority in new[]
                 {
                     AlarmPriority.Medium,
                     AlarmPriority.High,
                     AlarmPriority.Critical
                 })
        {
            var previous = Instance(
                Guid.NewGuid(),
                tagId,
                AlarmState.Normal,
                priority,
                timestamp);
            var current = previous with
            {
                State = AlarmState.Active,
                LastTransition = timestamp
            };
            await store.AppendAsync(
                new AlarmStateChanged(previous, current, timestamp),
                tagPath);
        }

        var dataset = HistoricalQueryCatalog.Require(HistoricalDatasets.AlarmEvents);
        var page = await store.QueryAsync(new HistoricalQueryExecution(
            dataset,
            new HistoricalResolvedRange(
                timestamp.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddSeconds(1)),
            [
                new HistoricalFilter(
                    "tag.path",
                    HistoricalFilterOperator.Eq,
                    [HistoricalQueryValue.FromString(tagPath)]),
                new HistoricalFilter(
                    "priority",
                    HistoricalFilterOperator.GreaterThanOrEqual,
                    [HistoricalQueryValue.FromNumber((int)AlarmPriority.High)]),
                new HistoricalFilter(
                    "priority",
                    HistoricalFilterOperator.LessThanOrEqual,
                    [HistoricalQueryValue.FromNumber((int)AlarmPriority.High)])
            ],
            null,
            new HistoricalSort(),
            10,
            null));

        var row = Assert.Single(page.Rows);
        Assert.Equal("3", row.Cells["priority"].Value);
        Assert.Equal(tagPath, row.Cells["tag.path"].Value);
    }

    [Fact]
    public async Task Store_IsAppendOnlyAtDatabaseBoundary()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var store = new PostgreSqlAlarmHistoryStore(connectionString);
        var alarmId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var marker = $"Integration.AppendOnly.{Guid.NewGuid():N}";
        var timestamp = DateTimeOffset.UtcNow.AddSeconds(-1);
        var previous = Instance(
            alarmId,
            tagId,
            AlarmState.Normal,
            AlarmPriority.Medium,
            timestamp);
        var current = previous with
        {
            State = AlarmState.Active,
            LastTransition = timestamp
        };
        await store.AppendAsync(
            new AlarmStateChanged(previous, current, timestamp),
            marker);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "DELETE FROM elitescada.alarm_history WHERE tag_path = @tag_path;",
            connection);
        command.Parameters.AddWithValue("tag_path", marker);
        await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
    }

    private static AlarmInstance Instance(
        Guid alarmId,
        Guid tagId,
        AlarmState state,
        AlarmPriority priority,
        DateTimeOffset timestamp) =>
        new(
            alarmId,
            "Alarm",
            tagId,
            AlarmType.Digital,
            priority,
            state,
            timestamp,
            true,
            "Area",
            "High pressure");
}
