using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Historian.Memory;
using Scada.Historian.Policies;

namespace Scada.Historian.TimescaleDb.Tests;

public sealed class HistorianCapturePolicyTests
{
    [Fact]
    public void ShouldCapture_PreservesLegacyTagsButHonorsExplicitEngineeringFlag()
    {
        Assert.True(HistorianCapturePolicy.ShouldCapture(CreateTag(null)));
        Assert.True(HistorianCapturePolicy.ShouldCapture(CreateTag("true")));
        Assert.False(HistorianCapturePolicy.ShouldCapture(CreateTag("false")));
        Assert.False(HistorianCapturePolicy.ShouldCapture(CreateTag("not-a-bool")));
    }

    [Fact]
    public void Periodic_OneHundredMillisecondSourceWithSixtySecondCaptureDoesNotWriteAtSourceRate()
    {
        var admission = new HistorianCaptureAdmission();
        var tag = CreateTag(
            enabled: "true",
            dataType: TagDataType.Double,
            strategy: "periodic",
            periodMilliseconds: 60_000);
        var origin = DateTimeOffset.Parse("2026-09-29T18:00:00Z");
        var accepted = 0;
        var coalesced = 0;

        for (var i = 0; i <= 600; i++)
        {
            var timestamp = origin.AddMilliseconds(i * 100);
            var decision = admission.Evaluate(Event(tag, i, timestamp));
            if (decision.Disposition == HistorianCaptureDisposition.Accepted) accepted++;
            if (decision.Disposition == HistorianCaptureDisposition.Coalesced) coalesced++;
        }

        Assert.Equal(2, accepted);
        Assert.Equal(599, coalesced);
    }

    [Fact]
    public void OnChange_BooleanStoresTransitionsWithoutRepeatedSteadyStateValues()
    {
        var admission = new HistorianCaptureAdmission();
        var tag = CreateTag("true", TagDataType.Boolean, "onChange");
        var origin = DateTimeOffset.UtcNow;

        var decisions = new[]
        {
            admission.Evaluate(Event(tag, false, origin)),
            admission.Evaluate(Event(tag, false, origin.AddSeconds(1))),
            admission.Evaluate(Event(tag, true, origin.AddSeconds(2))),
            admission.Evaluate(Event(tag, true, origin.AddSeconds(3))),
            admission.Evaluate(Event(tag, false, origin.AddSeconds(4)))
        };

        Assert.Equal(
            new[]
            {
                HistorianCaptureDisposition.Accepted,
                HistorianCaptureDisposition.Skipped,
                HistorianCaptureDisposition.Accepted,
                HistorianCaptureDisposition.Skipped,
                HistorianCaptureDisposition.Accepted
            },
            decisions.Select(x => x.Disposition));
    }

    [Fact]
    public void Deadband_SuppressesSubThresholdValueButPreservesQualityTransitionAndThresholdCrossing()
    {
        var admission = new HistorianCaptureAdmission();
        var tag = CreateTag("true", TagDataType.Double, "onChangeDeadband", deadband: 1d);
        var origin = DateTimeOffset.UtcNow;

        var first = admission.Evaluate(Event(tag, 10d, origin));
        var below = admission.Evaluate(Event(tag, 10.5d, origin.AddSeconds(1)));
        var quality = admission.Evaluate(Event(tag, 10.5d, origin.AddSeconds(2), TagQuality.Uncertain));
        var belowAfterQuality = admission.Evaluate(Event(tag, 11d, origin.AddSeconds(3), TagQuality.Uncertain));
        var crossing = admission.Evaluate(Event(tag, 11.5d, origin.AddSeconds(4), TagQuality.Uncertain));

        Assert.Equal(HistorianCaptureDisposition.Accepted, first.Disposition);
        Assert.Equal(HistorianCaptureDisposition.Skipped, below.Disposition);
        Assert.Equal(HistorianCaptureDisposition.Accepted, quality.Disposition);
        Assert.Equal("quality-transition", quality.Reason);
        Assert.Equal(HistorianCaptureDisposition.Skipped, belowAfterQuality.Disposition);
        Assert.Equal(HistorianCaptureDisposition.Accepted, crossing.Disposition);
        Assert.Equal("deadband-crossing", crossing.Reason);
    }

    [Fact]
    public void DeadbandMaximumInterval_IsObservationDrivenAndDeterministic()
    {
        var admission = new HistorianCaptureAdmission();
        var tag = CreateTag(
            "true",
            TagDataType.Double,
            "onChangeDeadbandMaxInterval",
            deadband: 5d,
            maximumPeriodMilliseconds: 10_000);
        var origin = DateTimeOffset.UtcNow;

        Assert.True(admission.Evaluate(Event(tag, 100d, origin)).Accepted);
        Assert.False(admission.Evaluate(Event(tag, 101d, origin.AddSeconds(9))).Accepted);

        // No timer is present and therefore no synthetic observation exists at t+10s.
        // The first newly received source observation after the bound is persisted.
        var bounded = admission.Evaluate(Event(tag, 101d, origin.AddSeconds(11)));
        Assert.True(bounded.Accepted);
        Assert.Equal("maximum-interval", bounded.Reason);

        Assert.False(admission.Evaluate(Event(tag, 102d, origin.AddSeconds(12))).Accepted);
    }

    [Fact]
    public void PolicyChange_ResetsPerTagAdmissionSoFirstObservationUnderNewActivePolicyIsAccepted()
    {
        var admission = new HistorianCaptureAdmission();
        var periodic = CreateTag("true", TagDataType.Double, "periodic", periodMilliseconds: 60_000);
        var changed = WithHistorianMetadata(
            periodic,
            strategy: "onChangeDeadband",
            deadband: 2d);
        var origin = DateTimeOffset.UtcNow;

        Assert.True(admission.Evaluate(Event(periodic, 10d, origin)).Accepted);
        Assert.Equal(
            HistorianCaptureDisposition.Coalesced,
            admission.Evaluate(Event(periodic, 10.5d, origin.AddSeconds(1))).Disposition);

        var firstAfterPolicyChange = admission.Evaluate(Event(changed, 10.5d, origin.AddSeconds(2)));
        Assert.True(firstAfterPolicyChange.Accepted);
        Assert.Equal("first-observation", firstAfterPolicyChange.Reason);
    }

    [Fact]
    public void RestartedAdmission_AcceptsFirstObservationDeterministically()
    {
        var tag = CreateTag("true", TagDataType.Boolean, "onChange");
        var timestamp = DateTimeOffset.UtcNow;
        var firstProcess = new HistorianCaptureAdmission();

        Assert.True(firstProcess.Evaluate(Event(tag, true, timestamp)).Accepted);
        Assert.False(firstProcess.Evaluate(Event(tag, true, timestamp.AddSeconds(1))).Accepted);

        var restartedProcess = new HistorianCaptureAdmission();
        var afterRestart = restartedProcess.Evaluate(Event(tag, true, timestamp.AddSeconds(2)));

        Assert.True(afterRestart.Accepted);
        Assert.Equal("first-observation", afterRestart.Reason);
    }

    [Fact]
    public async Task BufferedHistorian_ExposesAcceptedSkippedAndCoalescedVolumeDiagnostics()
    {
        var bus = new InMemoryScadaEventBus();
        await using var historian = new BufferedInMemoryHistorian(bus);
        var tag = CreateTag("true", TagDataType.Double, "periodic", periodMilliseconds: 1_000);
        var timestamp = DateTimeOffset.UtcNow;

        await bus.PublishAsync(Event(tag, 1d, timestamp));
        await bus.PublishAsync(Event(tag, 2d, timestamp.AddMilliseconds(100)));
        await bus.PublishAsync(Event(tag, 3d, timestamp.AddSeconds(1)));

        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (historian.WrittenSamples < 2 && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);

        Assert.Equal(2, historian.AcceptedSamples);
        Assert.Equal(0, historian.SkippedSamples);
        Assert.Equal(1, historian.CoalescedSamples);
        Assert.Equal(2, historian.WrittenSamples);
        Assert.Equal(2, historian.Query(tag.Id, timestamp.AddSeconds(-1), timestamp.AddSeconds(2)).Count);
    }

    [Fact]
    public async Task BufferedHistorian_StoresEnabledTagAndSkipsExplicitlyDisabledTag()
    {
        var bus = new InMemoryScadaEventBus();
        await using var historian = new BufferedInMemoryHistorian(bus);
        var enabled = CreateTag("true");
        var disabled = CreateTag("false");
        var timestamp = DateTimeOffset.UtcNow;

        await bus.PublishAsync(Event(enabled, 10, timestamp));
        await bus.PublishAsync(Event(disabled, 20, timestamp));

        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (historian.WrittenSamples < 1 && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);

        Assert.Single(historian.Query(enabled.Id, timestamp.AddSeconds(-1), timestamp.AddSeconds(1)));
        Assert.Empty(historian.Query(disabled.Id, timestamp.AddSeconds(-1), timestamp.AddSeconds(1)));
        Assert.Equal(1, historian.AcceptedSamples);
        Assert.Equal(1, historian.SkippedSamples);
    }

    private static TagValueChanged Event(
        TagDefinition tag,
        object? value,
        DateTimeOffset timestamp,
        TagQuality quality = TagQuality.Good) =>
        new(
            tag,
            null,
            new TagValue(tag.Id, value, timestamp, quality, tag.Source),
            timestamp);

    private static TagDefinition CreateTag(
        string? enabled,
        TagDataType dataType = TagDataType.Int32,
        string? strategy = null,
        double? deadband = null,
        int? periodMilliseconds = null,
        int? maximumPeriodMilliseconds = null)
    {
        Dictionary<string, string>? metadata = null;
        if (enabled is not null || strategy is not null || deadband.HasValue ||
            periodMilliseconds.HasValue || maximumPeriodMilliseconds.HasValue)
        {
            metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (enabled is not null) metadata[HistorianCapturePolicy.EnabledMetadataKey] = enabled;
            if (strategy is not null) metadata[HistorianCapturePolicy.StrategyMetadataKey] = strategy;
            if (deadband.HasValue) metadata[HistorianCapturePolicy.DeadbandMetadataKey] = deadband.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (periodMilliseconds.HasValue) metadata[HistorianCapturePolicy.PeriodMetadataKey] = periodMilliseconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (maximumPeriodMilliseconds.HasValue) metadata[HistorianCapturePolicy.MaximumPeriodMetadataKey] = maximumPeriodMilliseconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return new TagDefinition(
            Guid.NewGuid(),
            "Value",
            $"Memory.{Guid.NewGuid():N}",
            dataType,
            Source: "memory.server",
            EngineeringUnit: null,
            Description: null,
            ReadOnly: false,
            Metadata: metadata);
    }

    private static TagDefinition WithHistorianMetadata(
        TagDefinition tag,
        string strategy,
        double? deadband = null,
        int? periodMilliseconds = null,
        int? maximumPeriodMilliseconds = null)
    {
        var metadata = new Dictionary<string, string>(tag.Metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
        {
            [HistorianCapturePolicy.EnabledMetadataKey] = "true",
            [HistorianCapturePolicy.StrategyMetadataKey] = strategy
        };
        metadata.Remove(HistorianCapturePolicy.DeadbandMetadataKey);
        metadata.Remove(HistorianCapturePolicy.PeriodMetadataKey);
        metadata.Remove(HistorianCapturePolicy.MaximumPeriodMetadataKey);
        if (deadband.HasValue) metadata[HistorianCapturePolicy.DeadbandMetadataKey] = deadband.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (periodMilliseconds.HasValue) metadata[HistorianCapturePolicy.PeriodMetadataKey] = periodMilliseconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (maximumPeriodMilliseconds.HasValue) metadata[HistorianCapturePolicy.MaximumPeriodMetadataKey] = maximumPeriodMilliseconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return tag with { Metadata = metadata };
    }
}
