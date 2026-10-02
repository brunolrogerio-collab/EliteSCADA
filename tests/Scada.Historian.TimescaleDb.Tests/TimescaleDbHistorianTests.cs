using Scada.Core.Events;
using Scada.Core.Persistence;
using Scada.Core.Tags;
using Scada.Historian.Memory;
using Scada.Historian.Policies;
using Scada.Historian.TimescaleDb;

namespace Scada.Historian.TimescaleDb.Tests;

public sealed class TimescaleDbHistorianTests
{
    [Fact]
    public async Task Historian_PersistsAndQueriesTypedTagValues()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var eventBus = new InMemoryScadaEventBus();
        await using var historian = new TimescaleDbHistorian(eventBus, connectionString, batchSize: 50);

        var tag = TagDefinition.Create("Pressure", $"Integration.Pressure.{Guid.NewGuid():N}", TagDataType.Double);
        var start = DateTimeOffset.UtcNow.AddSeconds(-1);
        var values = new[]
        {
            new TagValue(tag.Id, 7.25d, DateTimeOffset.UtcNow, TagQuality.Good, "integration-test"),
            new TagValue(tag.Id, 7.50d, DateTimeOffset.UtcNow.AddMilliseconds(10), TagQuality.Uncertain, "integration-test"),
            new TagValue(tag.Id, 8.00d, DateTimeOffset.UtcNow.AddMilliseconds(20), TagQuality.Good, "integration-test")
        };

        foreach (var value in values)
            await eventBus.PublishAsync(new TagValueChanged(tag, null, value, value.Timestamp));

        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (historian.WrittenSamples < values.Length && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(50);

        Assert.Equal(values.Length, historian.WrittenSamples);
        Assert.Equal(0, historian.DroppedSamples);
        Assert.Null(historian.LastWriteError);

        var result = historian.Query(tag.Id, start, DateTimeOffset.UtcNow.AddSeconds(2), 100);

        Assert.Equal(3, result.Count);
        Assert.Equal(7.25d, Convert.ToDouble(result[0].Value));
        Assert.Equal(7.50d, Convert.ToDouble(result[1].Value));
        Assert.Equal(TagQuality.Uncertain, result[1].Quality);
        Assert.Equal("integration-test", result[2].Source);
    }

    [Fact]
    public async Task Historian_DurableWriteIsRejectedWhileMaintenanceAdmissionIsClosed()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var operationId = Guid.NewGuid();
        var admission = new RejectingDurableWriteAdmission(operationId);
        var eventBus = new InMemoryScadaEventBus();
        await using var historian = new TimescaleDbHistorian(
            eventBus,
            connectionString,
            batchSize: 1,
            writeAdmission: admission);

        var tag = TagDefinition.Create(
            "Quiesce",
            $"Integration.Quiesce.{Guid.NewGuid():N}",
            TagDataType.Double);
        var now = DateTimeOffset.UtcNow;
        await eventBus.PublishAsync(new TagValueChanged(
            tag,
            null,
            new TagValue(tag.Id, 42d, now, TagQuality.Good, "quiesce-test"),
            now));

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (historian.LastWriteError is null && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(25);

        var error = Assert.IsType<DurableWriteQuiescedException>(historian.LastWriteError);
        Assert.Equal(operationId, error.OperationId);
        Assert.Equal("historian", error.Writer);
        Assert.Equal(0, historian.WrittenSamples);
        Assert.Equal(1, historian.PendingSamples);
    }

    [Fact]
    public async Task Historian_PreservesBooleanStringAndNullValues()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var eventBus = new InMemoryScadaEventBus();
        await using var historian = new TimescaleDbHistorian(eventBus, connectionString);
        var tag = TagDefinition.Create("State", $"Integration.State.{Guid.NewGuid():N}", TagDataType.String);
        var now = DateTimeOffset.UtcNow;

        var values = new[]
        {
            new TagValue(tag.Id, true, now, TagQuality.Good),
            new TagValue(tag.Id, "AUTO", now.AddMilliseconds(10), TagQuality.Good),
            new TagValue(tag.Id, null, now.AddMilliseconds(20), TagQuality.BadCommunication)
        };

        foreach (var value in values)
            await eventBus.PublishAsync(new TagValueChanged(tag, null, value, value.Timestamp));

        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (historian.WrittenSamples < values.Length && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(50);

        var result = historian.Query(tag.Id, now.AddSeconds(-1), now.AddSeconds(2), 100);

        Assert.Equal(true, result[0].Value);
        Assert.Equal("AUTO", result[1].Value);
        Assert.Null(result[2].Value);
        Assert.Equal(TagQuality.BadCommunication, result[2].Quality);
    }
    [Fact]
    public async Task PeriodicCapture_OneHundredMillisecondObservationsPersistOnlyAcceptedRows()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var eventBus = new InMemoryScadaEventBus();
        await using var historian = new TimescaleDbHistorian(eventBus, connectionString, batchSize: 50);
        var tag = CreatePolicyTag(
            TagDataType.Double,
            strategy: "periodic",
            periodMilliseconds: 60_000);
        var origin = DateTimeOffset.UtcNow;

        for (var i = 0; i <= 600; i++)
        {
            var timestamp = origin.AddMilliseconds(i * 100);
            await eventBus.PublishAsync(new TagValueChanged(
                tag,
                null,
                new TagValue(tag.Id, (double)i, timestamp, TagQuality.Good, "volume-test"),
                timestamp));
        }

        await WaitForWritesAsync(historian, 2);

        Assert.Equal(2, historian.AcceptedSamples);
        Assert.Equal(0, historian.SkippedSamples);
        Assert.Equal(599, historian.CoalescedSamples);
        Assert.Equal(2, historian.WrittenSamples);
        Assert.Equal(0, historian.DroppedSamples);
        Assert.Null(historian.LastWriteError);

        var persisted = historian.Query(tag.Id, origin.AddSeconds(-1), origin.AddSeconds(61), 100);
        Assert.Equal(2, persisted.Count);
        AssertTimestampWithinTimescalePrecision(origin, persisted[0].Timestamp);
        AssertTimestampWithinTimescalePrecision(origin.AddSeconds(60), persisted[1].Timestamp);
    }

    [Fact]
    public async Task MemoryAndTimescaleCapture_DecisionsAndPersistedSamplesAgree()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var eventBus = new InMemoryScadaEventBus();
        await using var memory = new BufferedInMemoryHistorian(eventBus);
        await using var timescale = new TimescaleDbHistorian(eventBus, connectionString, batchSize: 20);
        var tag = CreatePolicyTag(
            TagDataType.Double,
            strategy: "onChangeDeadbandMaxInterval",
            deadband: 1d,
            maximumPeriodMilliseconds: 10_000);
        var origin = DateTimeOffset.UtcNow;

        var observations = new[]
        {
            new TagValue(tag.Id, 10d, origin, TagQuality.Good, "parity-test"),
            new TagValue(tag.Id, 10.5d, origin.AddSeconds(1), TagQuality.Good, "parity-test"),
            new TagValue(tag.Id, 10.5d, origin.AddSeconds(2), TagQuality.Uncertain, "parity-test"),
            new TagValue(tag.Id, 11d, origin.AddSeconds(3), TagQuality.Uncertain, "parity-test"),
            new TagValue(tag.Id, 11.5d, origin.AddSeconds(4), TagQuality.Uncertain, "parity-test"),
            new TagValue(tag.Id, 11.5d, origin.AddSeconds(15), TagQuality.Uncertain, "parity-test")
        };

        foreach (var value in observations)
            await eventBus.PublishAsync(new TagValueChanged(tag, null, value, value.Timestamp));

        await WaitForWritesAsync(timescale, 4);
        var memoryDeadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (memory.WrittenSamples < 4 && DateTimeOffset.UtcNow < memoryDeadline)
            await Task.Delay(10);

        Assert.Equal(memory.AcceptedSamples, timescale.AcceptedSamples);
        Assert.Equal(memory.SkippedSamples, timescale.SkippedSamples);
        Assert.Equal(memory.CoalescedSamples, timescale.CoalescedSamples);
        Assert.Equal(4, timescale.AcceptedSamples);
        Assert.Equal(2, timescale.SkippedSamples);
        Assert.Equal(0, timescale.CoalescedSamples);
        Assert.Equal(0, timescale.DroppedSamples);

        var from = origin.AddSeconds(-1);
        var to = origin.AddSeconds(16);
        var inMemory = memory.Query(tag.Id, from, to, 100);
        var persisted = timescale.Query(tag.Id, from, to, 100);

        Assert.Equal(inMemory.Count, persisted.Count);
        for (var i = 0; i < inMemory.Count; i++)
            AssertTimestampWithinTimescalePrecision(inMemory[i].Timestamp, persisted[i].Timestamp);
        Assert.Equal(
            inMemory.Select(x => x.Quality),
            persisted.Select(x => x.Quality));
        Assert.Equal(
            inMemory.Select(x => Convert.ToDouble(x.Value)),
            persisted.Select(x => Convert.ToDouble(x.Value)));
    }

    [Fact]
    public async Task TimescaleCapture_RestartAcceptsFirstNewObservationWithoutSyntheticHeartbeat()
    {
        var connectionString = Environment.GetEnvironmentVariable("ELITESCADA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var tag = CreatePolicyTag(TagDataType.Boolean, strategy: "onChange");
        var origin = DateTimeOffset.UtcNow;

        var firstBus = new InMemoryScadaEventBus();
        await using (var first = new TimescaleDbHistorian(firstBus, connectionString, batchSize: 10))
        {
            await firstBus.PublishAsync(new TagValueChanged(
                tag,
                null,
                new TagValue(tag.Id, true, origin, TagQuality.Good, "restart-test"),
                origin));
            await WaitForWritesAsync(first, 1);
            Assert.Equal(1, first.AcceptedSamples);
        }

        var secondBus = new InMemoryScadaEventBus();
        await using var second = new TimescaleDbHistorian(secondBus, connectionString, batchSize: 10);
        var restartedObservation = origin.AddSeconds(5);
        await secondBus.PublishAsync(new TagValueChanged(
            tag,
            null,
            new TagValue(tag.Id, true, restartedObservation, TagQuality.Good, "restart-test"),
            restartedObservation));
        await WaitForWritesAsync(second, 1);

        Assert.Equal(1, second.AcceptedSamples);
        Assert.Equal(0, second.SkippedSamples);
        var persisted = second.Query(tag.Id, origin.AddSeconds(-1), origin.AddSeconds(6), 10);
        Assert.Equal(2, persisted.Count);
        AssertTimestampWithinTimescalePrecision(origin, persisted[0].Timestamp);
        AssertTimestampWithinTimescalePrecision(restartedObservation, persisted[1].Timestamp);
    }

    private static void AssertTimestampWithinTimescalePrecision(DateTimeOffset expected, DateTimeOffset actual) =>
        Assert.InRange(
            Math.Abs((actual - expected).Ticks),
            0,
            TimeSpan.TicksPerMicrosecond);

    private static TagDefinition CreatePolicyTag(
        TagDataType dataType,
        string strategy,
        double? deadband = null,
        int? periodMilliseconds = null,
        int? maximumPeriodMilliseconds = null)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [HistorianCapturePolicy.EnabledMetadataKey] = "true",
            [HistorianCapturePolicy.StrategyMetadataKey] = strategy
        };
        if (deadband.HasValue)
            metadata[HistorianCapturePolicy.DeadbandMetadataKey] = deadband.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (periodMilliseconds.HasValue)
            metadata[HistorianCapturePolicy.PeriodMetadataKey] = periodMilliseconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (maximumPeriodMilliseconds.HasValue)
            metadata[HistorianCapturePolicy.MaximumPeriodMetadataKey] = maximumPeriodMilliseconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return new TagDefinition(
            Guid.NewGuid(),
            "CaptureValue",
            $"Integration.Capture.{Guid.NewGuid():N}",
            dataType,
            Source: "integration-test",
            EngineeringUnit: null,
            Description: null,
            ReadOnly: false,
            Metadata: metadata);
    }

    private sealed class RejectingDurableWriteAdmission(Guid operationId) : IDurableWriteAdmission
    {
        public ValueTask<IAsyncDisposable> AcquireAsync(
            string writer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromException<IAsyncDisposable>(
                new DurableWriteQuiescedException(writer, operationId));
        }
    }

    private static async Task WaitForWritesAsync(TimescaleDbHistorian historian, long expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (historian.WrittenSamples < expected && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(50);

        Assert.Equal(expected, historian.WrittenSamples);
        Assert.Null(historian.LastWriteError);
    }

}
