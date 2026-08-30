using System.Text.Json;
using Scada.Core.HistoricalQueries;

namespace Scada.Core.Tests;

public sealed class HistoricalQueryColumnOperatorMetadataTests
{
    [Fact]
    public void PublicColumns_ExposeDeterministicAllowlistedOperatorTokens()
    {
        var historian = HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamples);

        var identity = historian.Fields["tag.id"].ToColumn();
        Assert.True(identity.Filterable);
        Assert.Equal(
            [HistoricalFilterOperator.Eq, HistoricalFilterOperator.NotEq, HistoricalFilterOperator.In],
            identity.Operators);

        var text = historian.Fields["tag.path"].ToColumn();
        Assert.Equal(
            [
                HistoricalFilterOperator.Eq,
                HistoricalFilterOperator.NotEq,
                HistoricalFilterOperator.In,
                HistoricalFilterOperator.Contains,
                HistoricalFilterOperator.StartsWith
            ],
            text.Operators);
        Assert.DoesNotContain(HistoricalFilterOperator.GreaterThan, text.Operators);

        var ordered = historian.Fields["timestamp"].ToColumn();
        Assert.Equal(
            [
                HistoricalFilterOperator.Eq,
                HistoricalFilterOperator.NotEq,
                HistoricalFilterOperator.In,
                HistoricalFilterOperator.GreaterThan,
                HistoricalFilterOperator.GreaterThanOrEqual,
                HistoricalFilterOperator.LessThan,
                HistoricalFilterOperator.LessThanOrEqual
            ],
            ordered.Operators);
        Assert.DoesNotContain(HistoricalFilterOperator.Contains, ordered.Operators);

        var json = JsonSerializer.Serialize(new[] { identity, text, ordered });
        Assert.Contains("\"operators\":[\"eq\",\"notEq\",\"in\"]", json, StringComparison.Ordinal);
        Assert.Contains("\"contains\"", json, StringComparison.Ordinal);
        Assert.Contains("\"startsWith\"", json, StringComparison.Ordinal);
        Assert.Contains("\"gte\"", json, StringComparison.Ordinal);
        Assert.Contains("\"lte\"", json, StringComparison.Ordinal);

        Assert.All(new[] { identity, text, ordered }, column =>
            Assert.Equal(column.Operators.Count > 0, column.Filterable));
    }
}
