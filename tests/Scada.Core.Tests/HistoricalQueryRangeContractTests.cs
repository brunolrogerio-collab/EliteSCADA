using Scada.Core.HistoricalQueries;

namespace Scada.Core.Tests;

public sealed class HistoricalQueryRangeContractTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 29, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validator_RequiresNowAnchorForRelativeRangeAndNoAnchorForAbsoluteRange()
    {
        var missingAnchor = new HistoricalQueryRequest(
            HistoricalDatasets.HistorianSamples,
            new HistoricalTimeRange(
                HistoricalTimeRangeKind.Relative,
                DurationSeconds: 3600));
        Assert.Throws<ArgumentException>(() =>
            HistoricalQueryValidator.Validate(missingAnchor));

        var unsupportedAnchor = new HistoricalQueryRequest(
            HistoricalDatasets.HistorianSamples,
            new HistoricalTimeRange(
                HistoricalTimeRangeKind.Relative,
                DurationSeconds: 3600,
                Anchor: (HistoricalTimeAnchor)999));
        Assert.Throws<ArgumentException>(() =>
            HistoricalQueryValidator.Validate(unsupportedAnchor));

        var absoluteWithAnchor = new HistoricalQueryRequest(
            HistoricalDatasets.HistorianSamples,
            new HistoricalTimeRange(
                HistoricalTimeRangeKind.Absolute,
                Now.AddHours(-1),
                Now,
                Anchor: HistoricalTimeAnchor.Now));
        Assert.Throws<ArgumentException>(() =>
            HistoricalQueryValidator.Validate(absoluteWithAnchor));

        var valid = HistoricalQueryValidator.Validate(
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Relative(3600)));
        Assert.Equal(HistoricalTimeAnchor.Now, valid.RequestedRange.Anchor);
    }

    [Fact]
    public void Validator_PreservesUtcDateTimeAsHistorianScalarFilter()
    {
        var timestamp = Now.AddMinutes(-5);
        var validated = HistoricalQueryValidator.Validate(
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Relative(3600),
                Filters:
                [
                    new HistoricalFilter(
                        "value",
                        HistoricalFilterOperator.Eq,
                        [HistoricalQueryValue.FromDateTime(timestamp)])
                ]));

        var value = validated.Filters.Single().Values.Single();
        Assert.Equal(HistoricalValueKind.DateTime, value.Kind);
        Assert.Equal(timestamp.ToString("O"), value.Value);
    }
}
