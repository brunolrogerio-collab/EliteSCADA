using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scada.Api.Historian;
using Scada.Api.Licensing;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.HistoricalQueries;
using Scada.Core.Product.Licensing;
using Scada.Core.Tags;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.Intelligence;
using Scada.Engineering.Persistence;
using Scada.Engineering.Security;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class AssetProcessIntelligenceAnalyticsTests
{
    private static readonly DateTimeOffset FromUtc = DateTimeOffset.Parse("2026-10-14T00:00:00Z");

    [Fact]
    public void Catalog_IsVersionedAndBackedByImmutableCollections()
    {
        Assert.Equal(1, AssetProcessIntelligenceKpiCatalogV1.CatalogVersion);
        Assert.Equal("1.0.0", AssetProcessIntelligenceKpiCatalogV1.RuleVersion);
        Assert.IsNotType<HashSet<int>>(AssetProcessIntelligenceKpiCatalogV1.AllowedBucketSizesSeconds);
        var buckets = Assert.IsAssignableFrom<ISet<int>>(AssetProcessIntelligenceKpiCatalogV1.AllowedBucketSizesSeconds);
        Assert.Throws<NotSupportedException>(() => buckets.Add(123));
        Assert.DoesNotContain(123, buckets);
        Assert.Contains(900, AssetProcessIntelligenceKpiCatalogV1.AllowedBucketSizesSeconds);
        Assert.Contains(AssetProcessIntelligenceKpiCatalogV1.Rules, rule => rule.Id == "sample.mean.good.v1");
        Assert.Contains(AssetProcessIntelligenceKpiCatalogV1.Rules, rule => rule.Kind == "raw");
        Assert.All(
            AssetProcessIntelligenceKpiCatalogV1.Rules.Where(rule => rule.Kind == "derived"),
            rule => Assert.Equal("1.0.0", rule.Version));
    }

    [Fact]
    public void NetObservedRateV1Catalog_IsSeparateVersionedAndImmutable()
    {
        Assert.Equal(1, AssetProcessIntelligenceNetObservedRateV1Catalog.ContractVersion);
        Assert.Equal(1, AssetProcessIntelligenceNetObservedRateV1Catalog.CatalogVersion);
        Assert.Equal("net-observed-rate.v1", AssetProcessIntelligenceNetObservedRateV1Catalog.RuleId);
        Assert.Equal("1.0.0", AssetProcessIntelligenceNetObservedRateV1Catalog.RuleVersion);
        var rule = Assert.Single(AssetProcessIntelligenceNetObservedRateV1Catalog.Rules);
        Assert.Equal("net-observed-rate.v1", rule.Id);
        Assert.Contains("Historian observation timestamps", rule.Formula, StringComparison.Ordinal);
        Assert.Contains("without inference, conversion, or normalization", rule.UnitSemantics, StringComparison.Ordinal);
        var rules = Assert.IsAssignableFrom<IList<AssetProcessIntelligenceKpiRule>>(
            AssetProcessIntelligenceNetObservedRateV1Catalog.Rules);
        Assert.Throws<NotSupportedException>(() => rules.Add(rule));

        var described = JsonSerializer.SerializeToElement(AssetProcessIntelligenceNetObservedRateV1Catalog.Describe());
        Assert.Equal(1, described.GetProperty("catalogVersion").GetInt32());
        Assert.Equal("Good", described.GetProperty("acceptedQuality").GetString());
        Assert.Equal(31, described.GetProperty("maximumWindowDays").GetInt32());
        Assert.Equal(50000, described.GetProperty("maximumRowsPerWindow").GetInt32());
        Assert.Equal(200, described.GetProperty("pageSize").GetInt32());
        Assert.Equal(
            new[] { "available", "insufficient_data", "unsupported" },
            described.GetProperty("statuses").EnumerateArray().Select(item => item.GetString()));
    }

    [Theory]
    [InlineData(2d, 8d, 3d)]
    [InlineData(8d, 2d, -3d)]
    [InlineData(4d, 4d, 0d)]
    public void NetObservedRateV1_CalculatesOnlyTheNetEndpointRate(double firstValue, double lastValue, double expectedRate)
    {
        var metric = AssetProcessIntelligenceNetObservedRateV1Calculator.Calculate(
        [
            Observation(FromUtc.AddSeconds(2), "Good", HistoricalQueryValue.FromDouble(lastValue)),
            Observation(FromUtc, "Good", HistoricalQueryValue.FromDouble(firstValue))
        ],
        "bar");

        Assert.Equal("available", metric.Status);
        Assert.Empty(metric.ReasonCodes);
        Assert.Equal(expectedRate, metric.Value);
        Assert.Equal("bar/s", metric.Unit);
        Assert.Equal(2d, metric.ElapsedSeconds);
        Assert.Equal(2, metric.RawObservationCount);
        Assert.Equal(2, metric.AcceptedSampleCount);
        Assert.Equal(0, metric.RejectedSampleCount);
        Assert.Equal(FromUtc, metric.FirstAccepted!.TimestampUtc);
        Assert.Equal(firstValue, metric.FirstAccepted.Value);
        Assert.Equal(FromUtc.AddSeconds(2), metric.LastAccepted!.TimestampUtc);
        Assert.Equal(lastValue, metric.LastAccepted.Value);
    }

    [Fact]
    public void NetObservedRateV1_RejectsPoorUnknownAndNonnumericObservationsWithoutLosingCounts()
    {
        var metric = AssetProcessIntelligenceNetObservedRateV1Calculator.Calculate(
        [
            Observation(FromUtc, "Good", HistoricalQueryValue.FromDouble(10)),
            Observation(FromUtc.AddSeconds(1), "Uncertain", HistoricalQueryValue.FromDouble(500)),
            Observation(FromUtc.AddSeconds(2), "Goodish", HistoricalQueryValue.FromDouble(500)),
            Observation(FromUtc.AddSeconds(3), "Good", HistoricalQueryValue.FromString("not numeric")),
            Observation(FromUtc.AddSeconds(4), "Good", new HistoricalQueryValue(HistoricalValueKind.Number, "NaN")),
            Observation(FromUtc.AddSeconds(5), "Good", new HistoricalQueryValue(HistoricalValueKind.Number, "Infinity")),
            Observation(FromUtc.AddSeconds(6), null, HistoricalQueryValue.FromDouble(900))
        ],
        "bar");

        Assert.Equal("insufficient_data", metric.Status);
        Assert.Null(metric.Value);
        Assert.Contains("fewer_than_two_accepted_good_finite_samples", metric.ReasonCodes);
        Assert.Equal(7, metric.RawObservationCount);
        Assert.Equal(1, metric.AcceptedSampleCount);
        Assert.Equal(6, metric.RejectedSampleCount);
        Assert.Equal(4, metric.QualityCounts["Good"]);
        Assert.Equal(1, metric.QualityCounts["Uncertain"]);
        Assert.Equal(1, metric.QualityCounts["Missing"]);
        Assert.Equal(1, metric.QualityCounts["Unrecognized"]);
        Assert.Equal(10d, metric.FirstAccepted!.Value);
        Assert.Equal(10d, metric.LastAccepted!.Value);
    }

    [Fact]
    public void NetObservedRateV1_MissingUnitIsUnsupportedAndNeverFabricatesOne()
    {
        var metric = AssetProcessIntelligenceNetObservedRateV1Calculator.Calculate(
        [
            Observation(FromUtc, "Good", HistoricalQueryValue.FromDouble(1)),
            Observation(FromUtc.AddSeconds(2), "Good", HistoricalQueryValue.FromDouble(5))
        ],
        " ");

        Assert.Equal("unsupported", metric.Status);
        Assert.Contains("engineering_unit_missing", metric.ReasonCodes);
        Assert.Null(metric.Value);
        Assert.Null(metric.Unit);
        Assert.Equal(2, metric.AcceptedSampleCount);
        Assert.Equal(FromUtc, metric.FirstAccepted!.TimestampUtc);
        Assert.Equal(FromUtc.AddSeconds(2), metric.LastAccepted!.TimestampUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NetObservedRateV1_NonPositiveEndpointElapsedTimeIsInsufficient(int elapsedSeconds)
    {
        var metric = AssetProcessIntelligenceNetObservedRateV1Calculator.CalculateFromEndpoints(
            2,
            2,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["Good"] = 2 },
            new AssetProcessIntelligenceNetObservedRateV1Endpoint(FromUtc.AddSeconds(10), 1),
            new AssetProcessIntelligenceNetObservedRateV1Endpoint(FromUtc.AddSeconds(10 + elapsedSeconds), 3),
            "bar");

        Assert.Equal("insufficient_data", metric.Status);
        Assert.Contains("non_positive_endpoint_elapsed_seconds", metric.ReasonCodes);
        Assert.Null(metric.Value);
        Assert.Equal((double)elapsedSeconds, metric.ElapsedSeconds);
    }

    [Fact]
    public void NetObservedRateV1_NonFiniteArithmeticIsInsufficientRatherThanInfinity()
    {
        var metric = AssetProcessIntelligenceNetObservedRateV1Calculator.Calculate(
        [
            Observation(FromUtc, "Good", HistoricalQueryValue.FromDouble(-double.MaxValue)),
            Observation(FromUtc.AddSeconds(1), "Good", HistoricalQueryValue.FromDouble(double.MaxValue))
        ],
        "bar");

        Assert.Equal("insufficient_data", metric.Status);
        Assert.Contains("non_finite_calculated_rate", metric.ReasonCodes);
        Assert.Null(metric.Value);
    }

    [Fact]
    public async Task NetObservedRateV1_UsesHistorianTimestampAndPreservesStablePagingTieOrder()
    {
        using var fixture = Fixture.Create();
        var firstTimestamp = FromUtc.AddSeconds(1);
        var lastTimestamp = FromUtc.AddSeconds(601);
        fixture.Historian.Respond = request => request.Page!.Cursor is null
            ? Response(request, Enumerable.Range(0, 200)
                .Select(index => Row(fixture.Tag.Id, firstTimestamp, "Good",
                    HistoricalQueryValue.FromInt32(index == 0 ? 2 : index == 199 ? 4 : 3)))
                .ToArray(), "stable-next")
            : Response(request,
            [
                Row(fixture.Tag.Id, lastTimestamp, "Good", HistoricalQueryValue.FromInt32(20)),
                Row(fixture.Tag.Id, lastTimestamp, "Good", HistoricalQueryValue.FromInt32(22))
            ]);

        var result = await fixture.ExecuteNetObservedRateV1Async();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceNetObservedRateV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(2, fixture.Historian.Calls);
        Assert.Equal(HistoricalDatasets.HistorianSamples, fixture.Historian.Requests[0].Dataset);
        Assert.Equal("timestamp", Assert.Single(fixture.Historian.Requests[0].OrderBy!).Field);
        Assert.Equal(HistoricalSortDirection.Ascending, fixture.Historian.Requests[0].OrderBy![0].Direction);
        Assert.Equal(200, fixture.Historian.Requests[0].Page!.Size);
        Assert.Equal("stable-next", fixture.Historian.Requests[1].Page!.Cursor);
        Assert.Equal("available", payload.Metric.Status);
        Assert.Equal(fixture.Request.Window, payload.Window);
        Assert.Equal(Fixture.ProjectKey, payload.ProjectKey);
        Assert.Equal(fixture.Request.TagId, payload.TagId);
        Assert.Equal(22d, payload.Metric.LastAccepted!.Value);
        Assert.Equal(2d, payload.Metric.FirstAccepted!.Value);
        Assert.Equal(20d / 600d, payload.Metric.Value);
        Assert.Equal("bar/s", payload.Metric.Unit);
        Assert.Equal(202, payload.Metric.RawObservationCount);
        Assert.Equal("timestamp", payload.Provenance.TimestampBasis.Split('`')[1]);
        Assert.Contains("source/server timestamps are not used", payload.Provenance.TimestampBasis, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, true, true, true, 403)]
    [InlineData(true, false, true, true, 403)]
    [InlineData(true, true, false, true, 409)]
    [InlineData(true, true, true, false, 409)]
    public async Task NetObservedRateV1_RequiresEntitlementTrendUseTagReadPermitAndActiveScope(
        bool licensed,
        bool grantTagRead,
        bool grantPermit,
        bool activeScope,
        int expectedStatus)
    {
        using var fixture = Fixture.Create(licensed: licensed, grantTagRead: grantTagRead,
            grantPermit: grantPermit, activeScope: activeScope);

        var result = await fixture.ExecuteNetObservedRateV1Async();

        AssertStatus(expectedStatus, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task NetObservedRateV1_RequiresTrendUseAndCoherentSavedWorkspaceRevision()
    {
        using (var noTrend = Fixture.Create(grantTrendUse: false))
        {
            var denied = await noTrend.ExecuteNetObservedRateV1Async();
            AssertStatus(StatusCodes.Status403Forbidden, denied);
            Assert.Equal(0, noTrend.Historian.Calls);
        }

        using (var mismatch = Fixture.Create(workspaceRevision: 41, runtimeRevision: 42))
        {
            var rejected = await mismatch.ExecuteNetObservedRateV1Async();
            AssertStatus(StatusCodes.Status409Conflict, rejected);
            Assert.Equal(0, mismatch.Historian.Calls);
        }

        using (var dirty = Fixture.Create())
        {
            dirty.Workspace.Tags.Upsert(dirty.Tag with { Description = "unsaved" });
            var rejected = await dirty.ExecuteNetObservedRateV1Async();
            AssertStatus(StatusCodes.Status409Conflict, rejected);
            Assert.Equal(0, dirty.Historian.Calls);
        }
    }

    [Fact]
    public async Task NetObservedRateV1_PermitCancellationDuringReadReturnsNoPartialMetric()
    {
        using var fixture = Fixture.Create();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Historian.RespondAsync = async (request, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Response(request,
                [Row(fixture.Tag.Id, FromUtc.AddSeconds(1), "Good", HistoricalQueryValue.FromDouble(5))]);
        };

        var task = fixture.ExecuteNetObservedRateV1Async();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        fixture.Gate.Pause(Fixture.ProjectKey, fixture.ScopeKind, fixture.ScopeId);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceNetObservedRateV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task NetObservedRateV1_TagReadRevocationDuringReadDiscardsRows()
    {
        using var fixture = Fixture.Create();
        fixture.Historian.Respond = request =>
        {
            var prior = fixture.Authority.Snapshot();
            var role = Assert.Single(prior.Roles);
            fixture.Authority.TryReplaceAsync(
                prior.Version,
                [role with { Grants = [new CapabilityGrantEngineeringDto(SecurityCapability.TrendUse)] }],
                prior.Scopes).GetAwaiter().GetResult();
            return Response(request,
            [
                Row(fixture.Tag.Id, FromUtc.AddSeconds(1), "Good", HistoricalQueryValue.FromDouble(5)),
                Row(fixture.Tag.Id, FromUtc.AddSeconds(2), "Good", HistoricalQueryValue.FromDouble(6))
            ]);
        };

        var result = await fixture.ExecuteNetObservedRateV1Async();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceNetObservedRateV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task NetObservedRateV1_AtRowLimitReturnsCompleteResultButNoPartialResultWhenMoreRemain()
    {
        using var complete = Fixture.Create();
        complete.Historian.Respond = request =>
        {
            var page = complete.Historian.Calls - 1;
            var cursor = page == 249 ? null : $"page-{page + 1}";
            return Response(request, Enumerable.Range(0, 200)
                .Select(_ => Row(complete.Tag.Id, FromUtc.AddSeconds(1), "Good", HistoricalQueryValue.FromInt32(1)))
                .ToArray(), cursor);
        };

        var completed = await complete.ExecuteNetObservedRateV1Async();

        AssertStatus(StatusCodes.Status200OK, completed);
        var completePayload = Assert.IsType<AssetProcessIntelligenceNetObservedRateV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(completed).Value);
        Assert.Equal(50000, completePayload.Metric.RawObservationCount);
        Assert.Equal("insufficient_data", completePayload.Metric.Status);
        Assert.Equal(250, complete.Historian.Calls);

        using var truncated = Fixture.Create();
        truncated.Historian.Respond = request =>
        {
            var page = truncated.Historian.Calls - 1;
            return Response(request, Enumerable.Range(0, 200)
                .Select(_ => Row(truncated.Tag.Id, FromUtc.AddSeconds(1), "Good", HistoricalQueryValue.FromInt32(1)))
                .ToArray(), $"page-{page + 1}");
        };

        var rejected = await truncated.ExecuteNetObservedRateV1Async();

        AssertStatus(StatusCodes.Status413PayloadTooLarge, rejected);
        Assert.Equal(250, truncated.Historian.Calls);
        var error = Assert.IsType<AssetProcessIntelligenceAnalyticsApiError>(
            Assert.IsAssignableFrom<IValueHttpResult>(rejected).Value);
        Assert.Equal("INTELLIGENCE_NET_OBSERVED_RATE_WINDOW_TOO_DENSE", error.Code);
    }

    [Fact]
    public void KpiV1_FiltersQualityAndNonnumericValuesAndOrdersFirstLastByHistorianTimestamp()
    {
        var window = Window(FromUtc, FromUtc.AddHours(1));
        var rows = new[]
        {
            Observation(FromUtc.AddMinutes(20), "Good", HistoricalQueryValue.FromDouble(2)),
            Observation(FromUtc.AddMinutes(10), "Good", HistoricalQueryValue.FromDouble(4)),
            Observation(FromUtc.AddMinutes(15), "Uncertain", HistoricalQueryValue.FromDouble(99)),
            Observation(FromUtc.AddMinutes(30), "Good", HistoricalQueryValue.FromString("not numeric")),
            Observation(FromUtc.AddMinutes(40), "BadCommunication", HistoricalQueryValue.FromInt32(8)),
            Observation(FromUtc.AddMinutes(50), null, new HistoricalQueryValue(HistoricalValueKind.Number, "NaN"))
        };

        var summary = AssetProcessIntelligenceKpiV1.Calculate(rows, window, 900);

        Assert.Equal(6, summary.RawObservationCount);
        Assert.Equal(2, summary.AcceptedSampleCount);
        Assert.Equal(4, summary.RejectedSampleCount);
        Assert.Equal(3, summary.QualityCounts["Good"]);
        Assert.Equal(1, summary.QualityCounts["Uncertain"]);
        Assert.Equal(1, summary.QualityCounts["BadCommunication"]);
        Assert.Equal(1, summary.QualityCounts["Missing"]);
        Assert.Equal(2, summary.ValidSampleCount);
        Assert.Equal(2d, summary.Minimum);
        Assert.Equal(4d, summary.Maximum);
        Assert.Equal(3d, summary.SampleMean);
        Assert.Equal(FromUtc.AddMinutes(10), summary.FirstObserved!.TimestampUtc);
        Assert.Equal(4d, summary.FirstObserved.Value);
        Assert.Equal("sample.first.good.v1", summary.FirstObserved.RuleId);
        Assert.Equal(FromUtc.AddMinutes(20), summary.LastObserved!.TimestampUtc);
        Assert.Equal(2d, summary.LastObserved.Value);
        Assert.Equal("sample.last.good.v1", summary.LastObserved.RuleId);
        Assert.Equal(-2d, summary.Delta);
        Assert.Equal("unknown", summary.Completeness);
        Assert.Null(summary.ExpectedSampleCount);
        Assert.Equal(4, summary.Trend.Sum(bucket => bucket.RejectedSampleCount));
        Assert.Equal(2, summary.Trend.Sum(bucket => bucket.AcceptedSampleCount));
    }

    [Fact]
    public void KpiV1_PreservesStage3AQualityNormalizationWhenAcceptingGoodNumericSamples()
    {
        var window = Window(FromUtc, FromUtc.AddMinutes(15));
        var rows = new[]
        {
            Observation(FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromInt32(1)),
            Observation(FromUtc.AddMinutes(2), " good ", HistoricalQueryValue.FromInt32(2)),
            Observation(FromUtc.AddMinutes(3), "gOoD", HistoricalQueryValue.FromInt32(3)),
            Observation(FromUtc.AddMinutes(4), " Good ", HistoricalQueryValue.FromInt32(4)),
            Observation(FromUtc.AddMinutes(5), " Goodish ", HistoricalQueryValue.FromInt32(5))
        };

        var summary = AssetProcessIntelligenceKpiV1.Calculate(rows, window, 900);

        Assert.Equal(4, summary.AcceptedSampleCount);
        Assert.Equal(1, summary.RejectedSampleCount);
        Assert.Equal(4, summary.QualityCounts["Good"]);
        Assert.Equal(1, summary.QualityCounts["Unrecognized"]);
        Assert.True(AssetProcessIntelligenceKpiV1.TryGetAcceptedGoodNumericValue(rows[1], out var normalizedValue));
        Assert.Equal(2d, normalizedValue);
        Assert.False(AssetProcessIntelligenceKpiV1.TryGetAcceptedGoodNumericValue(rows[4], out _));
    }

    [Fact]
    public void KpiV1_EmptyAndSparseWindowsKeepEmptyBucketsAndUnknownCompleteness()
    {
        var empty = AssetProcessIntelligenceKpiV1.Calculate(
            Array.Empty<AssetProcessIntelligenceRawObservation>(),
            Window(FromUtc, FromUtc.AddMinutes(30)),
            900);

        Assert.Equal(0, empty.RawObservationCount);
        Assert.Equal(0, empty.ValidSampleCount);
        Assert.Null(empty.SampleMean);
        Assert.Null(empty.FirstObserved);
        Assert.Null(empty.LastObserved);
        Assert.Null(empty.Delta);
        Assert.Equal(2, empty.Trend.Count);
        Assert.All(empty.Trend, bucket => Assert.Equal(0, bucket.RawObservationCount));
        Assert.Equal("unknown", empty.Completeness);

        var sparse = AssetProcessIntelligenceKpiV1.Calculate(
            [Observation(FromUtc.AddMinutes(20), "Good", HistoricalQueryValue.FromInt32(7))],
            Window(FromUtc, FromUtc.AddMinutes(30)),
            900);
        Assert.Equal(2, sparse.Trend.Count);
        Assert.Equal(1, sparse.Trend[1].AcceptedSampleCount);
        Assert.Equal(0, sparse.Trend[0].AcceptedSampleCount);
    }

    [Fact]
    public void KpiV1_PreservesProviderOrderForEqualTimestampFirstAndLast()
    {
        var timestamp = FromUtc.AddMinutes(5);
        var summary = AssetProcessIntelligenceKpiV1.Calculate(
        [
            Observation(timestamp, "Good", HistoricalQueryValue.FromInt32(1)),
            Observation(timestamp, "Good", HistoricalQueryValue.FromInt32(2))
        ],
        Window(FromUtc, FromUtc.AddMinutes(15)),
        900);

        Assert.Equal(1d, summary.FirstObserved!.Value);
        Assert.Equal(2d, summary.LastObserved!.Value);
    }

    [Fact]
    public async Task DirtyWorkspaceReturnsConflictBeforeAnyHistorianQuery()
    {
        using var fixture = Fixture.Create();
        fixture.Workspace.Tags.Upsert(fixture.Tag with { Description = "unsaved" });

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task WorkspaceRuntimeRevisionMismatchReturnsConflictBeforeAnyHistorianQuery()
    {
        using var fixture = Fixture.Create(workspaceRevision: 41, runtimeRevision: 42);

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task NonnumericActiveTagIsNotQueried()
    {
        using var fixture = Fixture.Create(tagDataType: TagDataType.String);

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status404NotFound, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Theory]
    [InlineData(false, true, true, true, 403)]
    [InlineData(true, false, true, true, 403)]
    [InlineData(true, true, false, true, 409)]
    [InlineData(true, true, true, false, 409)]
    public async Task EntitlementTrendUseTagReadAndPermitAreIndependentlyRequired(
        bool licensed,
        bool tagRead,
        bool permit,
        bool activeScope,
        int expectedStatus)
    {
        using var fixture = Fixture.Create(licensed: licensed, grantTagRead: tagRead,
            grantPermit: permit, activeScope: activeScope);

        var result = await fixture.ExecuteAsync();

        AssertStatus(expectedStatus, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task MissingTrendUseStopsBeforeHistorianQuery()
    {
        using var fixture = Fixture.Create(grantTagRead: true, grantTrendUse: false);

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task SuccessfulReadUsesCanonicalDatasetAndReturnsProvenanceAndDerivedMetrics()
    {
        using var fixture = Fixture.Create();
        fixture.Historian.Respond = request => Response(
            request,
            [
                Row(fixture.Tag.Id, FromUtc.AddMinutes(20), "Good", HistoricalQueryValue.FromInt32(8)),
                Row(fixture.Tag.Id, FromUtc.AddMinutes(10), "Good", HistoricalQueryValue.FromInt32(2))
            ]);

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(HistoricalDatasets.HistorianSamples, fixture.Historian.Requests[0].Dataset);
        Assert.Equal(HistoricalFilterOperator.Eq, Assert.Single(fixture.Historian.Requests[0].Filters!).Operator);
        Assert.Equal("Plant.Pump.Pressure", payload.TagPath);
        Assert.Equal("bar", payload.Unit);
        Assert.Single(payload.Provenance.Assignments);
        Assert.Equal(2, payload.Period.Summary.RawObservationCount);
        Assert.Equal(2, payload.Period.Summary.AcceptedSampleCount);
        Assert.Equal(5d, payload.Period.Summary.SampleMean);
        Assert.Equal(6d, payload.Period.Summary.Delta);
        Assert.Equal("not_assessed", payload.Provenance.RegimeEquivalence);
        Assert.Equal("unknown", payload.Period.Summary.Completeness);
    }

    [Fact]
    public async Task ProfileCoverageReturnsPerSignalDescriptiveTrendsAndExactHistorianTimestampOverlap()
    {
        using var fixture = Fixture.Create();
        var secondTag = fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");
        fixture.Historian.Respond = request =>
        {
            var tagId = Assert.Single(Assert.Single(request.Filters!).Values).AsGuid();
            if (tagId == fixture.Tag.Id)
                return Response(request,
                [
                    Row(tagId, FromUtc.AddMinutes(10), "Good", HistoricalQueryValue.FromDouble(1)),
                    Row(tagId, FromUtc.AddMinutes(20), "BadCommunication", HistoricalQueryValue.FromDouble(99))
                ]);
            Assert.Equal(secondTag.Id, tagId);
            return Response(request,
            [
                Row(tagId, FromUtc.AddMinutes(10), "Good", HistoricalQueryValue.FromDouble(1200)),
                Row(tagId, FromUtc.AddMinutes(30), "Good", HistoricalQueryValue.FromDouble(1300))
            ]);
        };

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceProfileCoverageResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(2, fixture.Historian.Calls);
        Assert.Equal(2, payload.Signals.Count);
        Assert.Equal("bar", payload.Signals[0].Unit);
        Assert.Equal("rpm", payload.Signals[1].Unit);
        Assert.Equal(1, payload.Signals[0].Summary!.AcceptedSampleCount);
        Assert.Equal(2, payload.Signals[1].Summary!.AcceptedSampleCount);
        Assert.Equal("metadata only; SignalRole is not interpreted", payload.Provenance.SignalRoleInterpretation);
        Assert.Equal("exact_historian_timestamp_overlap_observed", payload.TemporalAlignment.Status);
        Assert.Equal(1, payload.TemporalAlignment.ExactHistorianTimestampIntersectionCount);
        Assert.Equal("unverified", payload.TemporalAlignment.SourceAcquisitionAlignment);
        Assert.Equal("unknown", payload.TemporalAlignment.ExpectedCadence);
        Assert.Equal("unknown", payload.TemporalAlignment.Completeness);
        Assert.Contains("unsupported", payload.Provenance.ProcessKpiDisposition);
    }

    [Fact]
    public async Task ProfileCoverageMarksUnalignedAndPoorQualityRequiredSignalsInsufficient()
    {
        using var fixture = Fixture.Create();
        var secondTag = fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");
        fixture.Historian.Respond = request =>
        {
            var tagId = Assert.Single(Assert.Single(request.Filters!).Values).AsGuid();
            var timestamp = tagId == fixture.Tag.Id ? FromUtc.AddMinutes(10) : FromUtc.AddMinutes(11);
            var quality = tagId == secondTag.Id ? "Uncertain" : "Good";
            return Response(request,
                [Row(tagId, timestamp, quality, HistoricalQueryValue.FromDouble(5))]);
        };

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceProfileCoverageResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("insufficient_required_signal_data", payload.TemporalAlignment.Status);
        Assert.Equal(1, payload.TemporalAlignment.RequiredSignalsWithGoodSamples);
        Assert.Equal(0, payload.TemporalAlignment.ExactHistorianTimestampIntersectionCount);
        Assert.Equal("insufficient_no_good_numeric_samples", payload.Signals.Single(signal => signal.TagId == secondTag.Id).Status);
        Assert.All(payload.Signals, signal => Assert.Equal("unknown", signal.Summary!.Completeness));
    }

    [Fact]
    public async Task ProfileCoverageDoesNotCallDifferentHistorianTimestampsAligned()
    {
        using var fixture = Fixture.Create();
        var secondTag = fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");
        fixture.Historian.Respond = request =>
        {
            var tagId = Assert.Single(Assert.Single(request.Filters!).Values).AsGuid();
            var timestamp = tagId == fixture.Tag.Id ? FromUtc.AddMinutes(10) : FromUtc.AddMinutes(11);
            return Response(request,
                [Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(5))]);
        };

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceProfileCoverageResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(2, payload.TemporalAlignment.RequiredSignalsWithGoodSamples);
        Assert.Equal(0, payload.TemporalAlignment.ExactHistorianTimestampIntersectionCount);
        Assert.Equal("insufficient_exact_historian_timestamp_overlap", payload.TemporalAlignment.Status);
        Assert.All(payload.Signals, signal => Assert.Equal("descriptive_trend_available", signal.Status));
        Assert.Equal(2, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ProfileCoverageReportsNonnumericRequiredSignalAsUnsupportedWithoutQueryingIt()
    {
        using var fixture = Fixture.Create();
        var secondTag = fixture.AddProfileSignal("running", "motor-state", TagDataType.Boolean, null);
        fixture.Historian.Respond = request =>
        {
            var tagId = Assert.Single(Assert.Single(request.Filters!).Values).AsGuid();
            return Response(request,
                [Row(tagId, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5))]);
        };

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceProfileCoverageResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        var unsupported = Assert.Single(payload.Signals, signal => signal.TagId == secondTag.Id);
        Assert.Equal("unsupported_non_numeric_tag_type", unsupported.Status);
        Assert.Null(unsupported.Summary);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.Equal("insufficient_required_signal_data", payload.TemporalAlignment.Status);
    }

    [Fact]
    public async Task ProfileCoverageEnforcesOneTotalRowCapAcrossSignalsWithoutReturningPartialResults()
    {
        using var fixture = Fixture.Create();
        var secondTag = fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");
        var firstTagPage = 0;
        fixture.Historian.Respond = request =>
        {
            var tagId = Assert.Single(Assert.Single(request.Filters!).Values).AsGuid();
            if (tagId == secondTag.Id)
                return Response(request,
                    [Row(tagId, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(6))]);

            var cursor = firstTagPage == 249 ? null : $"first-{firstTagPage + 1}";
            firstTagPage++;
            return Response(request, Enumerable.Range(0, 200)
                .Select(_ => Row(tagId, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5)))
                .ToArray(), cursor);
        };

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status413PayloadTooLarge, result);
        var error = Assert.IsType<AssetProcessIntelligenceAnalyticsApiError>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("INTELLIGENCE_PROFILE_WINDOW_TOO_DENSE", error.Code);
        Assert.Equal(251, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceProfileCoverageResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task ProfileCoverageStopsBeforeHistorianWhenTrendUseIsMissing()
    {
        using var fixture = Fixture.Create(grantTrendUse: false);
        fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public void ProfileCoverageCatalogStatesItsBoundsAndNonPhysicalTimestampRule()
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            AssetProcessIntelligenceProfileCoverageCatalogV1.Describe()));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("catalogVersion").GetInt32());
        Assert.Equal(16, root.GetProperty("maximumSignals").GetInt32());
        Assert.Equal(50000, root.GetProperty("maximumRowsPerWindow").GetInt32());
        Assert.Equal("metadata_only", root.GetProperty("signalRoleInterpretation").GetString());
        Assert.Equal("none", root.GetProperty("unitConversion").GetString());
        Assert.Contains("unsupported", root.GetProperty("processKpiDisposition").GetString());
        Assert.Contains("exact Historian event-time equality only",
            root.GetProperty("rules")[0].GetProperty("formula").GetString());
    }

    [Fact]
    public async Task ProfileCoverageRejectsDuplicateTagReferencesBeforeHistorianAccess()
    {
        using var fixture = Fixture.Create();
        var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
        var profile = Assert.Single(configuration.Profiles!);
        var original = Assert.Single(profile.Signals!);
        var duplicate = original with { Id = Guid.NewGuid(), Key = "pressure-alias" };
        fixture.Workspace.AssetProcessIntelligence.Replace(configuration with
        {
            Profiles = [profile with { Signals = [original, duplicate] }]
        });
        var descriptor = fixture.Workspace.Describe();
        fixture.Workspace.SetCheckout(Fixture.ProjectKey, "Stage 3B duplicate reference test",
            descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status400BadRequest, result);
        var error = Assert.IsType<AssetProcessIntelligenceAnalyticsApiError>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("INTELLIGENCE_PROFILE_DUPLICATE_TAG_REFERENCE_UNSUPPORTED", error.Code);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ProfileCoveragePreauthorizesEveryTagBeforeTheFirstHistorianQuery()
    {
        using var fixture = Fixture.Create(grantTagRead: false);
        fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task PausingProfileScopeAfterFirstSignalDiscardsTheWholeProfileResponse()
    {
        using var fixture = Fixture.Create();
        fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");
        fixture.Historian.Respond = request =>
        {
            var tagId = Assert.Single(Assert.Single(request.Filters!).Values).AsGuid();
            fixture.Gate.Pause(Fixture.ProjectKey, fixture.ScopeKind, fixture.ScopeId);
            return Response(request,
                [Row(tagId, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5))]);
        };

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceProfileCoverageResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task ProfileCoverageRejectsAProfileOutsideTheRequestedActiveScope()
    {
        using var fixture = Fixture.Create();

        var result = await fixture.ExecuteProfileCoverageAsync(scopeId: Guid.NewGuid());

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task ProfileCoverageRejectsMissingIntelligenceEntitlementBeforeHistorianAccess()
    {
        using var fixture = Fixture.Create(licensed: false);
        fixture.AddProfileSignal("speed", "motor-speed", TagDataType.Double, "rpm");

        var result = await fixture.ExecuteProfileCoverageAsync();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task LocationScopeIncludesEquipmentByStableLocationAncestry()
    {
        using var fixture = Fixture.Create(scopeKind: AssetProcessIntelligenceScopeKind.Location, nestedLocation: true);
        fixture.Historian.Respond = request => Response(
            request,
            [Row(fixture.Tag.Id, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5))]);

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Single(payload.Provenance.Assignments);
        Assert.Equal(fixture.EquipmentId, payload.Provenance.Assignments[0].EquipmentId);
        Assert.Equal(1, fixture.Historian.Calls);
    }

    [Fact]
    public async Task UnequalComparisonWindowsAreRejectedBeforeAnyHistorianQuery()
    {
        using var fixture = Fixture.Create();
        var request = fixture.Request with
        {
            ComparisonWindow = Window(FromUtc.AddDays(-1), FromUtc.AddHours(1))
        };

        var result = await fixture.ExecuteAsync(request);

        AssertStatus(StatusCodes.Status400BadRequest, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task EqualDurationComparisonUsesTwoExactAbsoluteWindowsAndMakesNoRegimeClaim()
    {
        using var fixture = Fixture.Create();
        fixture.Historian.Respond = request =>
        {
            var page = fixture.Historian.Calls - 1;
            var value = page == 0 ? 8 : 3;
            return Response(request,
                [Row(fixture.Tag.Id, request.Range.FromUtc!.Value.AddMinutes(5), "Good",
                    HistoricalQueryValue.FromInt32(value))]);
        };
        var request = fixture.Request with
        {
            ComparisonWindow = Window(FromUtc.AddDays(-1), FromUtc.AddDays(-1).AddHours(1))
        };

        var result = await fixture.ExecuteAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(2, fixture.Historian.Calls);
        Assert.Equal(request.Window!.FromUtc, fixture.Historian.Requests[0].Range.FromUtc);
        Assert.Equal(request.Window.ToUtc, fixture.Historian.Requests[0].Range.ToUtc);
        Assert.Equal(request.ComparisonWindow!.FromUtc, fixture.Historian.Requests[1].Range.FromUtc);
        Assert.Equal(request.ComparisonWindow.ToUtc, fixture.Historian.Requests[1].Range.ToUtc);
        Assert.Equal(5d, payload.Comparison!.SampleMeanDelta);
        Assert.Equal("not_assessed", payload.Comparison.RegimeEquivalence);
    }

    [Fact]
    public async Task ActiveRuntimeRevisionChangeDuringReadDiscardsTheWholeResult()
    {
        using var fixture = Fixture.Create();
        fixture.Historian.Respond = request =>
        {
            fixture.Runtime.SetRevision(42);
            return Response(request,
                [Row(fixture.Tag.Id, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5))]);
        };

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task TagReadRevocationBetweenPagesDiscardsAllRowsAndDoesNotQueryNextPage()
    {
        using var fixture = Fixture.Create();
        fixture.Historian.Respond = request =>
        {
            var prior = fixture.Authority.Snapshot();
            var role = Assert.Single(prior.Roles);
            fixture.Authority.TryReplaceAsync(
                prior.Version,
                [role with { Grants = [new CapabilityGrantEngineeringDto(SecurityCapability.TrendUse)] }],
                prior.Scopes).GetAwaiter().GetResult();
            return Response(
                request,
                [Row(fixture.Tag.Id, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5))],
                "opaque-next");
        };

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task TrendUseRevocationBetweenPagesDiscardsAllRowsAndDoesNotQueryNextPage()
    {
        using var fixture = Fixture.Create();
        fixture.Historian.Respond = request =>
        {
            var prior = fixture.Authority.Snapshot();
            var role = Assert.Single(prior.Roles);
            fixture.Authority.TryReplaceAsync(
                prior.Version,
                [role with { Grants = [new CapabilityGrantEngineeringDto(SecurityCapability.TagRead)] }],
                prior.Scopes).GetAwaiter().GetResult();
            return Response(
                request,
                [Row(fixture.Tag.Id, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5))],
                "opaque-next");
        };

        var result = await fixture.ExecuteAsync();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public void EngineeringValidatorRejectsAmbiguousScopeIdentityAtRegistryBoundary()
    {
        var scopeId = Guid.NewGuid();
        var configuration = new AssetProcessIntelligenceEngineeringDto(
            Version: 1,
            Scopes:
            [
                new AssetProcessIntelligenceScopeEngineeringDto(AssetProcessIntelligenceScopeKind.Equipment, scopeId, true),
                new AssetProcessIntelligenceScopeEngineeringDto(AssetProcessIntelligenceScopeKind.Equipment, scopeId, true)
            ]);

        var issues = AssetProcessIntelligenceEngineeringValidator.Validate(configuration, null, null);

        Assert.Contains(issues, issue => issue.Code == "INTELLIGENCE_SCOPE_DUPLICATE");
        Assert.Throws<InvalidDataException>(() =>
            new InMemoryAssetProcessIntelligenceEngineeringRegistry().Replace(configuration));
    }

    [Fact]
    public async Task PausingScopeCancelsInFlightReadAndReturnsNoKpi()
    {
        using var fixture = Fixture.Create();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Historian.RespondAsync = async (request, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Response(request, []);
        };

        var task = fixture.ExecuteAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        fixture.Gate.Pause(Fixture.ProjectKey, fixture.ScopeKind, fixture.ScopeId);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
    }

    [Fact]
    public async Task AuthorityPermitRevocationCancelsInFlightReadAndDiscardsItsRows()
    {
        using var fixture = Fixture.Create();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Historian.RespondAsync = async (request, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Response(request,
                [Row(fixture.Tag.Id, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(5))]);
        };

        var task = fixture.ExecuteAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        // AuthorityPolicyHaCoordinator invalidates all active permits before it persists
        // and replicates a policy mutation; the in-flight analytics request must observe it.
        fixture.Gate.InvalidateAll();
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task ExactObservationCapIsAcceptedOnlyWhenPaginationHasEnded()
    {
        using var complete = Fixture.Create();
        complete.Historian.Respond = request =>
        {
            var page = complete.Historian.Calls - 1;
            var cursor = page == 249 ? null : $"page-{page + 1}";
            return Response(request, Enumerable.Range(0, 200)
                .Select(_ => Row(complete.Tag.Id, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromInt32(1)))
                .ToArray(), cursor);
        };

        var completed = await complete.ExecuteAsync();

        AssertStatus(StatusCodes.Status200OK, completed);
        var completePayload = Assert.IsType<AssetProcessIntelligenceAnalyticsResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(completed).Value);
        Assert.Equal(AssetProcessIntelligenceKpiCatalogV1.MaximumRowsPerWindow,
            completePayload.Period.Summary.RawObservationCount);
        Assert.Equal(250, complete.Historian.Calls);

        using var truncated = Fixture.Create();
        truncated.Historian.Respond = request =>
        {
            var page = truncated.Historian.Calls - 1;
            var cursor = $"page-{page + 1}";
            return Response(request, Enumerable.Range(0, 200)
                .Select(_ => Row(truncated.Tag.Id, FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromInt32(1)))
                .ToArray(), cursor);
        };

        var rejected = await truncated.ExecuteAsync();

        AssertStatus(StatusCodes.Status413PayloadTooLarge, rejected);
        Assert.Equal(250, truncated.Historian.Calls);
        var error = Assert.IsType<AssetProcessIntelligenceAnalyticsApiError>(
            Assert.IsAssignableFrom<IValueHttpResult>(rejected).Value);
        Assert.Equal("INTELLIGENCE_ANALYTICS_WINDOW_TOO_DENSE", error.Code);
    }

    [Fact]
    public async Task EndpointSurfaceContainsOnlyCatalogAndReadOnlyQueries()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<AssetProcessIntelligenceAnalyticsService>(_ => null!);
        await using var app = builder.Build();
        app.MapAssetProcessIntelligenceAnalyticsEndpoints();

        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "/api/engineering/asset-process-intelligence/analytics/catalog",
                "/api/engineering/asset-process-intelligence/analytics/current-frequency-comparison/v1/catalog",
                "/api/engineering/asset-process-intelligence/analytics/current-frequency-comparison/v1/query",
                "/api/engineering/asset-process-intelligence/analytics/net-observed-rate/v1/catalog",
                "/api/engineering/asset-process-intelligence/analytics/net-observed-rate/v1/query",
                "/api/engineering/asset-process-intelligence/analytics/performance/v1/catalog",
                "/api/engineering/asset-process-intelligence/analytics/performance/v1/query",
                "/api/engineering/asset-process-intelligence/analytics/profile-coverage",
                "/api/engineering/asset-process-intelligence/analytics/profile-coverage/catalog",
                "/api/engineering/asset-process-intelligence/analytics/query"
            },
            routes);
        var methods = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => endpoint.Metadata.OfType<HttpMethodMetadata>().Single().HttpMethods)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(new[] { "GET", "POST" }, methods);
    }

    [Fact]
    public void PerformanceV1BCatalog_DeclaresThreeBoundedRecordedIndicators()
    {
        var catalog = JsonSerializer.SerializeToElement(AssetProcessIntelligencePerformanceV1BCatalog.Describe());

        Assert.Equal(1, catalog.GetProperty("contractVersion").GetInt32());
        Assert.Equal(1, catalog.GetProperty("catalogVersion").GetInt32());
        Assert.Equal("historian.samples.v2", catalog.GetProperty("dataset").GetString());
        Assert.Equal(50000, catalog.GetProperty("maximumRawRowsAcrossSelectedTags").GetInt32());
        Assert.Equal(200, catalog.GetProperty("pageSize").GetInt32());
        Assert.StartsWith("unverified", catalog.GetProperty("physicalSimultaneity").GetString());
        Assert.Equal(3, catalog.GetProperty("rules").GetArrayLength());
        var ratioRule = AssetProcessIntelligencePerformanceV1BCatalog.Rules.Single(
            rule => rule.Id == "recorded-pressure-power-input-ratio.v1");
        Assert.Equal("derived", ratioRule.Kind);
        Assert.Equal(2, ratioRule.MinimumQualifiedTuples);
        Assert.Contains("unit.volumetric_flow.cubic_metre_per_hour",
            ratioRule.InputUnits.Single(input => input.Role == "pump.discharge_volumetric_flow").AcceptedUnitCodes);
        Assert.Contains("unit.pressure.kilopascal",
            ratioRule.InputUnits.Single(input => input.Role == "pump.direct_differential_pressure").AcceptedUnitCodes);
    }

    [Fact]
    public async Task PerformanceV1B_SpecificEnergyUsesCanonicalPowerAndProductionRateAndExactSourceTimes()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals(
            "specific-active-energy-at-recorded-production-rate.v1", "process.net_good_production_mass_rate");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var signal = tags[tagId];
            var t1 = FromUtc.AddMinutes(5);
            var t2 = FromUtc.AddMinutes(10);
            var values = signal.Path.EndsWith("ActivePower", StringComparison.Ordinal)
                ? new[] { 3.6d, 7.2d } // kW -> 3600/7200 W
                : new[] { 3600d, 7200d }; // kg/h -> 1/2 kg/s
            return ResponseV2(query,
            [
                V2Row(tagId, t1, "Good", HistoricalQueryValue.FromDouble(values[0]), t1, "Synchronized"),
                V2Row(tagId, t2, "Good", HistoricalQueryValue.FromDouble(values[1]), t2, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("available", payload.Summary.Status);
        Assert.Equal(2, payload.Summary.QualifiedTupleCount);
        Assert.Equal(0.001d, payload.Summary.Minimum!.Value, 12);
        Assert.Equal(0.001d, payload.Summary.Maximum!.Value, 12);
        Assert.Equal(0.001d, payload.Summary.ArithmeticMean!.Value, 12);
        Assert.Equal(4, payload.TotalRawObservationCount);
        Assert.Contains(payload.Signals, signal => signal.RoleCode == "equipment.active_input_power" &&
            signal.SignalKind == "measured_total_input");
        Assert.Contains(payload.Signals, signal => signal.RoleCode == "process.net_good_production_mass_rate" &&
            signal.SignalKind == "measured_output" && signal.ControlledUnitCode == "unit.mass_flow.kilogram_per_hour");
        Assert.Equal(HistoricalDatasets.HistorianSamplesV2, fixture.Historian.Requests[0].Dataset);
        Assert.All(fixture.Historian.Requests, query =>
        {
            Assert.Equal(2, query.Version);
            Assert.Equal(200, query.Page!.Size);
            Assert.Equal("timestamp", Assert.Single(query.OrderBy!).Field);
        });
    }

    [Fact]
    public async Task PerformanceV1B_SpecificEnergyMatchesDocumentedVolumeRateExample()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("specific-active-energy-at-recorded-production-rate.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var value = tags[tagId].Path.EndsWith("ActivePower", StringComparison.Ordinal)
                ? 2d
                : 20d / 3600d; // 2 kW at 20 m³/h = 0.1 kWh/m³.
            var first = FromUtc.AddMinutes(1);
            var second = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(value), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(value), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(0.1d, payload.Summary.ArithmeticMean!.Value, 12);
        Assert.Contains(payload.Signals, signal => signal.RoleCode == "process.net_good_production_volume_rate" &&
            signal.SignalKind == "measured_output" &&
            signal.CanonicalUnit == "m³/s" &&
            signal.ControlledUnitCode == "unit.volumetric_flow.cubic_metre_per_second");
    }

    [Theory]
    [InlineData("recorded-differential-pressure-power.v1", 1666.6666666666667d)]
    [InlineData("recorded-pressure-power-input-ratio.v1", 83.33333333333333d)]
    public async Task PerformanceV1B_MatchesDocumentedPressurePowerExamples(string ruleId, double expectedMean)
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals(ruleId);
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var value = tags[tagId].Path switch
            {
                var path when path.EndsWith("Flow", StringComparison.Ordinal) => 20d,
                var path when path.EndsWith("DifferentialPressure", StringComparison.Ordinal) => 3d,
                _ => 2000d
            };
            var first = FromUtc.AddMinutes(1);
            var second = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(value), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(value), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(expectedMean, payload.Summary.ArithmeticMean!.Value, 8);
        Assert.Equal(ruleId == "recorded-pressure-power-input-ratio.v1" ? "% pressure-only power / measured electrical input" : "W",
            payload.Summary.Unit);
    }

    [Fact]
    public async Task PerformanceV1B_RatioRetainsAndFlagsFiniteResultsAboveOneHundredPercent()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-pressure-power-input-ratio.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var signal = tags[tagId];
            var (value1, value2) = signal.Path switch
            {
                var path when path.EndsWith("Flow", StringComparison.Ordinal) => (3600d, 7200d),
                var path when path.EndsWith("DifferentialPressure", StringComparison.Ordinal) => (1d, 4d),
                _ => (100d, 400d)
            };
            var t1 = FromUtc.AddMinutes(1);
            var t2 = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, t1, "Good", HistoricalQueryValue.FromDouble(value1), t1, "Synchronized"),
                V2Row(tagId, t2, "Good", HistoricalQueryValue.FromDouble(value2), t2, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("available", payload.Summary.Status);
        Assert.Equal(100000d, payload.Summary.Minimum);
        Assert.Equal(200000d, payload.Summary.Maximum);
        Assert.Equal(150000d, payload.Summary.ArithmeticMean);
        Assert.All(payload.Tuples, tuple => Assert.True(tuple.AboveOneHundredPercent));
        Assert.Contains("result_above_100_percent_observed_not_clipped", payload.Summary.ReasonCodes);
        Assert.Contains(payload.Signals, signal => signal.RoleCode == "pump.discharge_volumetric_flow" &&
            signal.FlowDirection == "pump_to_process");
        Assert.Contains(payload.Signals, signal => signal.RoleCode == "pump.direct_differential_pressure" &&
            signal.PressureReference == "differential_outlet_minus_inlet");
        Assert.Contains("not hydraulic, motor, or overall system efficiency", payload.Provenance.MeasurementInterpretation,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PerformanceV1B_ZeroPressureNumeratorIsAValidQualifiedResult()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp1 = FromUtc.AddMinutes(1);
            var timestamp2 = FromUtc.AddMinutes(2);
            var isPressure = tags[tagId].Path.EndsWith("DifferentialPressure", StringComparison.Ordinal);
            var value = isPressure ? 0d : 3600d;
            return ResponseV2(query,
            [
                V2Row(tagId, timestamp1, "Good", HistoricalQueryValue.FromDouble(value), timestamp1, "Synchronized"),
                V2Row(tagId, timestamp2, "Good", HistoricalQueryValue.FromDouble(value), timestamp2, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("available", payload.Summary.Status);
        Assert.Equal(2, payload.Summary.QualifiedTupleCount);
        Assert.Equal(0d, payload.Summary.ArithmeticMean);
    }

    [Fact]
    public async Task PerformanceV1B_DifferentialPressurePowerConvertsMixedV1InputsToWatts()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var value = tags[tagId].Path.EndsWith("Flow", StringComparison.Ordinal) ? 3600d : 1d;
            var first = FromUtc.AddMinutes(1);
            var second = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(value), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(value), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("W", payload.Summary.Unit);
        Assert.Equal(100000d, payload.Summary.Minimum);
        Assert.Equal(100000d, payload.Summary.ArithmeticMean);
    }

    [Fact]
    public async Task PerformanceV1B_UsesV2FlowAndPressureUnitsThroughControlledResolver()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-pressure-power-input-ratio.v1");
        var flow = tags.Values.Single(tag => tag.Path.EndsWith("Flow", StringComparison.Ordinal)) with
        {
            ControlledEngineeringUnit = new ControlledEngineeringUnitReference(2, "unit.volumetric_flow.cubic_metre_per_second")
        };
        var pressure = tags.Values.Single(tag => tag.Path.EndsWith("DifferentialPressure", StringComparison.Ordinal)) with
        {
            ControlledEngineeringUnit = new ControlledEngineeringUnitReference(2, "unit.pressure.kilopascal")
        };
        fixture.Workspace.Tags.Upsert(flow);
        fixture.Workspace.Tags.Upsert(pressure);
        fixture.Runtime.SetTag(flow);
        fixture.Runtime.SetTag(pressure);
        var descriptor = fixture.Workspace.Describe();
        fixture.Workspace.SetCheckout(Fixture.ProjectKey, "V2 unit resolver test", descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var path = tags[tagId].Path;
            var value = path.EndsWith("Flow", StringComparison.Ordinal) ? 1d
                : path.EndsWith("DifferentialPressure", StringComparison.Ordinal) ? 100d : 100d;
            var first = FromUtc.AddMinutes(1);
            var second = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(value), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(value), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(100000d, payload.Summary.ArithmeticMean);
        Assert.Contains(payload.Signals, signal => signal.RoleCode == "pump.discharge_volumetric_flow" && signal.UnitCatalogVersion == 2);
        Assert.Contains(payload.Signals, signal => signal.RoleCode == "pump.direct_differential_pressure" && signal.UnitCatalogVersion == 2);
    }

    [Theory]
    [InlineData("reverse-flow", "flow_not_forward_positive")]
    [InlineData("negative-pressure", "negative_differential_pressure")]
    public async Task PerformanceV1B_InvalidPhysicalDomainProducesUnavailableStatsNotFabricatedZero(
        string invalidInput,
        string expectedReason)
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var isFlow = tags[tagId].Path.EndsWith("Flow", StringComparison.Ordinal);
            var value = invalidInput == "reverse-flow"
                ? isFlow ? -3600d : 1d
                : isFlow ? 3600d : -1d;
            var timestamp1 = FromUtc.AddMinutes(1);
            var timestamp2 = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, timestamp1, "Good", HistoricalQueryValue.FromDouble(value), timestamp1, "Synchronized"),
                V2Row(tagId, timestamp2, "Good", HistoricalQueryValue.FromDouble(value), timestamp2, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("insufficient_data", payload.Summary.Status);
        Assert.Equal(0, payload.Summary.QualifiedTupleCount);
        Assert.Null(payload.Summary.Minimum);
        Assert.Null(payload.Summary.Maximum);
        Assert.Null(payload.Summary.ArithmeticMean);
        Assert.All(payload.Tuples, tuple => Assert.Equal(expectedReason, tuple.ReasonCode));
    }

    [Theory]
    [InlineData("zero-production-rate", "nonpositive_production_rate")]
    [InlineData("negative-power", "negative_power")]
    public async Task PerformanceV1B_SpecificEnergyRejectsInvalidProductionDenominatorAndNegativePower(
        string invalidInput,
        string expectedReason)
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals(
            "specific-active-energy-at-recorded-production-rate.v1", "process.net_good_production_item_rate");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var isPower = tags[tagId].Path.EndsWith("ActivePower", StringComparison.Ordinal);
            var value = invalidInput == "zero-production-rate"
                ? isPower ? 3.6d : 0d
                : isPower ? -1d : 3600d;
            var first = FromUtc.AddMinutes(1);
            var second = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(value), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(value), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("insufficient_data", payload.Summary.Status);
        Assert.Contains(expectedReason, payload.Summary.ReasonCodes);
        Assert.All(payload.Tuples, tuple => Assert.Equal(expectedReason, tuple.ReasonCode));
    }

    [Fact]
    public async Task PerformanceV1B_RatioRejectsNonpositiveMeasuredInputPower()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-pressure-power-input-ratio.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var path = tags[tagId].Path;
            var value = path.EndsWith("Flow", StringComparison.Ordinal) ? 3600d
                : path.EndsWith("DifferentialPressure", StringComparison.Ordinal) ? 1d : 0d;
            var first = FromUtc.AddMinutes(1);
            var second = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(value), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(value), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("insufficient_data", payload.Summary.Status);
        Assert.Contains("nonpositive_input_power", payload.Summary.ReasonCodes);
    }

    [Fact]
    public async Task PerformanceV1B_NonFiniteFormulaArithmeticIsRejectedNotClipped()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var path = tags[tagId].Path;
            var value = path.EndsWith("Flow", StringComparison.Ordinal) ? double.MaxValue : 1e303;
            var first = FromUtc.AddMinutes(1);
            var second = FromUtc.AddMinutes(2);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(value), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(value), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("insufficient_data", payload.Summary.Status);
        Assert.Contains("non_finite_arithmetic", payload.Summary.ReasonCodes);
        Assert.All(payload.Tuples, tuple => Assert.Null(tuple.Value));
    }

    [Fact]
    public async Task PerformanceV1B_DoesNotInterpolateOrJoinNearestSourceTimestamps()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var offset = tags[tagId].Path.EndsWith("DifferentialPressure", StringComparison.Ordinal)
                ? TimeSpan.FromSeconds(1) : TimeSpan.Zero;
            var timestamps = new[] { FromUtc.AddMinutes(1), FromUtc.AddMinutes(2) };
            return ResponseV2(query, timestamps.Select(timestamp =>
                V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(1), timestamp + offset, "Synchronized")).ToArray());
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("insufficient_data", payload.Summary.Status);
        Assert.Empty(payload.Tuples);
        Assert.All(payload.Inputs, input => Assert.Equal(0, input.PairedCount));
        Assert.All(payload.Inputs, input => Assert.Equal(2, input.RejectionCounts["no_exact_source_timestamp_pair"]));
    }

    [Fact]
    public async Task PerformanceV1B_CountsNonnumericBadUnsynchronizedOutOfWindowAndMissingEvidence()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        var times = Enumerable.Range(1, 6).Select(index => FromUtc.AddMinutes(index)).ToArray();
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            if (tags[tagId].Path.EndsWith("DifferentialPressure", StringComparison.Ordinal))
                return ResponseV2(query,
                    [V2Row(tagId, times[0], "Good", HistoricalQueryValue.FromDouble(1), times[0], "Synchronized")]);
            return ResponseV2(query,
            [
                V2Row(tagId, times[0], "Good", HistoricalQueryValue.FromDouble(3600), times[0], "Synchronized"),
                V2Row(tagId, times[1], "Good", HistoricalQueryValue.FromString("not numeric"), times[1], "Synchronized"),
                V2Row(tagId, times[2], "Good", HistoricalQueryValue.FromDouble(3600), times[2], "PresentUnverified"),
                V2Row(tagId, times[3], "Good", HistoricalQueryValue.FromDouble(3600), FromUtc.AddDays(32), "Synchronized"),
                V2Row(tagId, times[4], "BadCommunication", HistoricalQueryValue.FromDouble(3600), times[4], "Synchronized"),
                V2Row(tagId, times[5], "Good", HistoricalQueryValue.FromDouble(3600), null, "NotRecorded")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        var flowSignal = fixture.Workspace.AssetProcessIntelligence.Snapshot().Profiles!.Single().Signals!
            .Single(signal => signal.TagId == tags.Values.Single(tag => tag.Path.EndsWith("Flow", StringComparison.Ordinal)).Id);
        var evidence = payload.Inputs.Single(input => input.SignalId == flowSignal.Id);
        Assert.Equal(6, evidence.RawObservationCount);
        Assert.Equal(1, evidence.EligibleSynchronizedCount);
        Assert.Equal(1, evidence.PairedCount);
        Assert.Equal(1, evidence.QualifiedCount);
        Assert.Equal("insufficient_data", payload.Summary.Status);
        Assert.Null(payload.Summary.ArithmeticMean);
        Assert.Contains("value_not_finite_numeric", evidence.RejectionCounts.Keys);
        Assert.Contains("source_timestamp_not_synchronized", evidence.RejectionCounts.Keys);
        Assert.Contains("source_timestamp_outside_requested_window", evidence.RejectionCounts.Keys);
        Assert.Contains("quality_not_good", evidence.RejectionCounts.Keys);
        Assert.Contains("source_timestamp_missing", evidence.RejectionCounts.Keys);
    }

    [Fact]
    public async Task PerformanceV1B_ExcludesDuplicateSourceGroupSplitAcrossHistorianPages()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        var flowTag = tags.Values.Single(tag => tag.Path.EndsWith("Flow", StringComparison.Ordinal));
        var pressureTag = tags.Values.Single(tag => tag.Path.EndsWith("DifferentialPressure", StringComparison.Ordinal));
        var duplicateTime = FromUtc.AddSeconds(200);
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            if (tagId == pressureTag.Id)
                return ResponseV2(query,
                    [V2Row(tagId, duplicateTime, "Good", HistoricalQueryValue.FromDouble(1), duplicateTime, "Synchronized")]);
            if (query.Page!.Cursor is null)
            {
                var rows = Enumerable.Range(0, 200).Select(index =>
                {
                    var observed = FromUtc.AddSeconds(index + 1);
                    var source = index == 199 ? duplicateTime : observed;
                    return V2Row(tagId, observed, "Good", HistoricalQueryValue.FromDouble(3600), source, "Synchronized");
                }).ToArray();
                return ResponseV2(query, rows, "second-flow-page");
            }
            return ResponseV2(query,
                [V2Row(tagId, FromUtc.AddSeconds(201), "BadCommunication", HistoricalQueryValue.FromDouble(3600), duplicateTime, "PresentUnverified")]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        var flowSignalId = fixture.Workspace.AssetProcessIntelligence.Snapshot().Profiles!.Single().Signals!
            .Single(signal => signal.TagId == flowTag.Id).Id;
        var flowEvidence = payload.Inputs.Single(input => input.SignalId == flowSignalId);
        Assert.Equal(201, flowEvidence.RawObservationCount);
        Assert.Equal(199, flowEvidence.EligibleSynchronizedCount);
        Assert.Equal(2, flowEvidence.RejectionCounts["ambiguous_source_timestamp"]);
        Assert.Equal(0, flowEvidence.PairedCount);
        Assert.Empty(payload.Tuples);
        Assert.Equal(202, payload.TotalRawObservationCount);
    }

    [Fact]
    public async Task PerformanceV1B_AuthorizesEverySelectedTagBeforeFirstHistorianRead()
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
        var selectedFlow = configuration.Profiles!.Single().Signals!.Single(signal => signal.Id == request.SignalIds![0]);
        var tag = tags[selectedFlow.TagId] with { AccessPolicy = new TagAccessPolicy(ReadRoles: ["other-role"]) };
        fixture.Workspace.Tags.Upsert(tag);
        fixture.Runtime.SetTag(tag);
        var descriptor = fixture.Workspace.Describe();
        fixture.Workspace.SetCheckout(Fixture.ProjectKey, "tag authorization test", descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Theory]
    [InlineData(false, 200)]
    [InlineData(true, 413)]
    public async Task PerformanceV1B_AggregateRowLimitCountsRejectedRowsAndProbesRemainingInputs(
        bool extraRejectedRow,
        int expectedStatus)
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        var firstTagId = tags.Values.OrderBy(tag => tag.Path, StringComparer.Ordinal).First().Id;
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            if (tagId != firstTagId)
                return ResponseV2(query, extraRejectedRow
                    ? [V2Row(tagId, FromUtc.AddSeconds(1), "BadCommunication", HistoricalQueryValue.FromString("rejected"), null, "NotRecorded")]
                    : []);
            var page = fixture.Historian.Calls - 1;
            var cursor = page == 249 ? null : $"performance-page-{page + 1}";
            return ResponseV2(query, Enumerable.Range(0, 200)
                .Select(_ => V2Row(tagId, FromUtc.AddSeconds(1), "BadCommunication",
                    HistoricalQueryValue.FromString("rejected raw row"), null, "NotRecorded"))
                .ToArray(), cursor);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(expectedStatus == 200 ? StatusCodes.Status200OK : StatusCodes.Status413PayloadTooLarge, result);
        Assert.Equal(251, fixture.Historian.Calls);
        if (expectedStatus == 200)
        {
            var payload = Assert.IsType<AssetProcessIntelligencePerformanceV1BResponse>(
                Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
            Assert.Equal(50000, payload.TotalRawObservationCount);
            Assert.Equal("insufficient_data", payload.Summary.Status);
        }
        else
        {
            Assert.IsNotType<AssetProcessIntelligencePerformanceV1BResponse>(
                Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        }
    }

    [Fact]
    public async Task PerformanceV1B_PermitRevocationDuringHistorianReadDiscardsAllEvidence()
    {
        using var fixture = Fixture.Create();
        var (request, _) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Historian.RespondAsync = async (query, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ResponseV2(query, []);
        };

        var task = fixture.ExecutePerformanceV1BAsync(request);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        fixture.Gate.Pause(Fixture.ProjectKey, fixture.ScopeKind, fixture.ScopeId);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Theory]
    [InlineData("tag-read", StatusCodes.Status403Forbidden)]
    [InlineData("workspace", StatusCodes.Status409Conflict)]
    [InlineData("scope", StatusCodes.Status409Conflict)]
    [InlineData("revision", StatusCodes.Status409Conflict)]
    [InlineData("entitlement", StatusCodes.Status403Forbidden)]
    public async Task PerformanceV1B_RevocationOrContextChangeBetweenPagesDiscardsTheWholeQuery(
        string mutationKind,
        int expectedStatus)
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        var firstSelectedSignal = fixture.Workspace.AssetProcessIntelligence.Snapshot().Profiles!.Single().Signals!
            .Single(signal => signal.Id == request.SignalIds![0]);
        fixture.Historian.Respond = query =>
        {
            switch (mutationKind)
            {
                case "tag-read":
                    fixture.Runtime.SetTag(tags[firstSelectedSignal.TagId] with
                    {
                        AccessPolicy = new TagAccessPolicy(ReadRoles: ["other-role"])
                    });
                    break;
                case "workspace":
                case "scope":
                    var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
                    var scopes = configuration.Scopes!.Select(scope => scope with { Active = mutationKind != "scope" }).ToArray();
                    fixture.Workspace.AssetProcessIntelligence.Replace(configuration with { Scopes = scopes });
                    break;
                case "revision":
                    fixture.Runtime.SetRevision(42);
                    break;
                case "entitlement":
                    fixture.Licensing.Licensed = false;
                    break;
            }
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = FromUtc.AddMinutes(1);
            return ResponseV2(query,
                [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(1), timestamp, "Synchronized")],
                "next-page");
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(expectedStatus, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Theory]
    [InlineData("repeated-cursor")]
    [InlineData("malformed-row")]
    [InlineData("out-of-order-rows")]
    public async Task PerformanceV1B_MalformedOrUnorderedHistorianEvidenceFailsClosed(string failureKind)
    {
        using var fixture = Fixture.Create();
        var (request, _) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            if (failureKind == "repeated-cursor")
                return ResponseV2(query, [], "same-cursor");
            if (failureKind == "malformed-row")
                return ResponseV2(query, [null!]);
            var first = FromUtc.AddMinutes(2);
            var second = FromUtc.AddMinutes(1);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(1), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(1), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status503ServiceUnavailable, result);
        Assert.IsNotType<AssetProcessIntelligencePerformanceV1BResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.True(fixture.Historian.Calls >= 1);
    }

    [Theory]
    [InlineData("duplicate-selection")]
    [InlineData("wrong-role")]
    [InlineData("unsupported-unit")]
    [InlineData("unsupported-unit-version")]
    [InlineData("missing-selected-signal")]
    public async Task PerformanceV1B_RejectsInvalidInputSelectionAndSemanticsBeforeHistorianRead(string invalidCase)
    {
        using var fixture = Fixture.Create();
        var (request, tags) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        if (invalidCase == "duplicate-selection")
            request = request with { SignalIds = [request.SignalIds![0], request.SignalIds[0]] };
        else if (invalidCase == "missing-selected-signal")
            request = request with { SignalIds = [request.SignalIds![0]] };
        else
        {
            var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
            var profile = configuration.Profiles!.Single();
            if (invalidCase == "wrong-role")
            {
                var invalidSignals = profile.Signals!.Select(signal => signal.Id == request.SignalIds![0]
                    ? signal with { SemanticBinding = signal.SemanticBinding! with { FlowDirection = "process_to_pump" } }
                    : signal).ToArray();
                fixture.Workspace.AssetProcessIntelligence.Replace(configuration with { Profiles = [profile with { Signals = invalidSignals }] });
            }
            else
            {
                var selectedSignal = profile.Signals!.Single(signal => signal.Id == request.SignalIds![0]);
                var tagId = selectedSignal.TagId;
                var tag = fixture.Runtime.Tags().Single(item => item.Id == tagId) with
                {
                    ControlledEngineeringUnit = invalidCase == "unsupported-unit-version"
                        ? new ControlledEngineeringUnitReference(99, "unit.volumetric_flow.cubic_metre_per_second")
                        : new ControlledEngineeringUnitReference(1, "unit.pressure.bar")
                };
                fixture.Workspace.Tags.Upsert(tag);
                fixture.Runtime.SetTag(tag);
            }
            var descriptor = fixture.Workspace.Describe();
            fixture.Workspace.SetCheckout(Fixture.ProjectKey, "invalid performance input", descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);
        }

        var result = await fixture.ExecutePerformanceV1BAsync(request);

        AssertStatus(StatusCodes.Status400BadRequest, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public void PerformanceV1B_UnsupportedSemanticCatalogVersionIsRejectedByEngineeringBeforeAnyHistorianRead()
    {
        using var fixture = Fixture.Create();
        var (request, _) = fixture.AddPerformanceSignals("recorded-differential-pressure-power.v1");
        var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
        var profile = configuration.Profiles!.Single();
        var invalidSignals = profile.Signals!.Select(signal => signal.Id == request.SignalIds![0]
            ? signal with { SemanticBinding = signal.SemanticBinding! with { CatalogVersion = 99 } }
            : signal).ToArray();

        Assert.Throws<InvalidDataException>(() => fixture.Workspace.AssetProcessIntelligence.Replace(
            configuration with { Profiles = [profile with { Signals = invalidSignals }] }));
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public void CurrentFrequencyComparisonV1Catalog_DeclaresBoundedAAndHzRule()
    {
        var catalog = JsonSerializer.SerializeToElement(
            AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog.Describe());

        Assert.Equal(1, catalog.GetProperty("contractVersion").GetInt32());
        Assert.Equal(1, catalog.GetProperty("catalogVersion").GetInt32());
        Assert.Equal("current-at-recorded-frequency.compare.v1", catalog.GetProperty("ruleId").GetString());
        Assert.Equal("historian.samples.v2", catalog.GetProperty("dataset").GetString());
        Assert.Equal(2, catalog.GetProperty("datasetContractVersion").GetInt32());
        Assert.Equal("unit.electric_current.ampere", catalog.GetProperty("currentUnit").GetProperty("code").GetString());
        Assert.Equal("unit.frequency.hertz", catalog.GetProperty("frequencyUnit").GetProperty("code").GetString());
        Assert.Equal(50000, catalog.GetProperty("maximumRawRowsAcrossBothTagsAndBothWindows").GetInt32());
        Assert.Equal(200, catalog.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public void CurrentFrequencyComparisonV1_RawDuplicateTimestampAcrossEvidenceStatesIsAmbiguous()
    {
        var window = Window(FromUtc, FromUtc.AddMinutes(10));
        var duplicateTime = FromUtc.AddMinutes(2);
        var ampere = new ControlledEngineeringUnitReference(1, "unit.electric_current.ampere");
        var hertz = new ControlledEngineeringUnitReference(1, "unit.frequency.hertz");
        var currentRows = new[]
        {
            CurrentFrequencyRow(FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(10), FromUtc.AddMinutes(1), "Synchronized"),
            CurrentFrequencyRow(FromUtc.AddMinutes(2), "Good", HistoricalQueryValue.FromDouble(99), duplicateTime, "Synchronized"),
            CurrentFrequencyRow(FromUtc.AddMinutes(2).AddSeconds(1), "Good", HistoricalQueryValue.FromDouble(101), duplicateTime, "PresentUnverified")
        };
        var frequencyRows = new[]
        {
            CurrentFrequencyRow(FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(50), FromUtc.AddMinutes(1), "Synchronized"),
            CurrentFrequencyRow(FromUtc.AddMinutes(2), "Good", HistoricalQueryValue.FromDouble(50), duplicateTime, "Synchronized")
        };

        var forward = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateWindow(
            window, currentRows, frequencyRows, ampere, hertz, 50, 50);
        var reverse = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateWindow(
            window, currentRows.Reverse().ToArray(), frequencyRows.Reverse().ToArray(), ampere, hertz, 50, 50);

        Assert.Equal(1, forward.Evidence.Current.AmbiguousSourceTimestampGroupCount);
        Assert.Equal(2, forward.Evidence.Current.AmbiguousSourceTimestampRowCount);
        Assert.Equal(1, forward.Evidence.ExactSourceTimestampPairCount);
        Assert.Equal(1, forward.Evidence.SelectedPairCount);
        var metric = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateMetric(
            forward,
            new AssetProcessIntelligenceCurrentFrequencyWindowCalculation(
                forward.Evidence with { SelectedPairCount = 2 }, 1d),
            false);
        Assert.Equal("insufficient_data", metric.Status);
        Assert.Contains("fewer_than_two_primary_frequency_conditioned_pairs", metric.ReasonCodes);
        Assert.Equal(
            forward.Evidence.Pairs.Select(pair => pair.SourceTimestampUtc),
            reverse.Evidence.Pairs.Select(pair => pair.SourceTimestampUtc));
        Assert.Equal(10d, forward.MeanCurrentA);
    }

    [Fact]
    public void CurrentFrequencyComparisonV1_FrequencyBoundsAreInclusiveAndOutOfRangePairsAreCounted()
    {
        var window = Window(FromUtc, FromUtc.AddMinutes(10));
        var currentRows = new List<AssetProcessIntelligenceCurrentFrequencyRawObservation>();
        var frequencyRows = new List<AssetProcessIntelligenceCurrentFrequencyRawObservation>();
        var currentValues = new[] { 10d, 20d, 30d, 40d };
        var frequencies = new[] { 49d, 51d, 48.999d, 51.001d };
        for (var index = 0; index < currentValues.Length; index++)
        {
            var timestamp = FromUtc.AddMinutes(index + 1);
            currentRows.Add(CurrentFrequencyRow(timestamp, "Good", HistoricalQueryValue.FromDouble(currentValues[index]), timestamp, "Synchronized"));
            frequencyRows.Add(CurrentFrequencyRow(timestamp, "Good", HistoricalQueryValue.FromDouble(frequencies[index]), timestamp, "Synchronized"));
        }

        var calculation = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateWindow(
            window,
            currentRows,
            frequencyRows,
            new ControlledEngineeringUnitReference(1, "unit.electric_current.ampere"),
            new ControlledEngineeringUnitReference(1, "unit.frequency.hertz"),
            49,
            51);

        Assert.Equal(2, calculation.Evidence.FrequencyInRangePairCount);
        Assert.Equal(2, calculation.Evidence.FrequencyOutOfRangePairCount);
        Assert.Equal(2, calculation.Evidence.SelectedPairCount);
        Assert.Equal(15d, calculation.MeanCurrentA);
        Assert.Equal(2, calculation.Evidence.Frequency.RejectionCounts["frequency_outside_requested_range"]);
    }

    [Fact]
    public void CurrentFrequencyComparisonV1_ZeroComparisonMeanRetainsAbsoluteDifference()
    {
        var window = Window(FromUtc, FromUtc.AddMinutes(10));
        var primary = new AssetProcessIntelligenceCurrentFrequencyWindowCalculation(
            EmptyCurrentFrequencyEvidence(window) with { SelectedPairCount = 2 }, 4d);
        var comparison = new AssetProcessIntelligenceCurrentFrequencyWindowCalculation(
            EmptyCurrentFrequencyEvidence(Window(FromUtc.AddHours(2), FromUtc.AddHours(2).AddMinutes(10)))
                with { SelectedPairCount = 2 }, 0d);

        var metric = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateMetric(
            primary, comparison, false);

        Assert.Equal("available", metric.Status);
        Assert.Equal(4d, metric.DifferenceA);
        Assert.Null(metric.PercentChange);
        Assert.Equal("unsupported", metric.PercentChangeStatus);
        Assert.Equal("comparison_mean_zero_percent_change_unavailable", metric.PercentChangeReasonCode);
    }

    [Fact]
    public void CurrentFrequencyComparisonV1_RejectsMissingUnverifiedAndOutOfWindowSourceTimeEvidence()
    {
        var window = Window(FromUtc, FromUtc.AddMinutes(10));
        var currentRows = new[]
        {
            CurrentFrequencyRow(FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(10), FromUtc.AddMinutes(1), "Synchronized"),
            CurrentFrequencyRow(FromUtc.AddMinutes(2), "Good", HistoricalQueryValue.FromDouble(10), FromUtc.AddMinutes(2), "Synchronized"),
            CurrentFrequencyRow(FromUtc.AddMinutes(3), "Good", HistoricalQueryValue.FromDouble(10), FromUtc.AddMinutes(3), "Synchronized"),
            CurrentFrequencyRow(FromUtc.AddMinutes(4), "Good", HistoricalQueryValue.FromDouble(10), FromUtc.AddMinutes(4), "Synchronized")
        };
        var frequencyRows = new[]
        {
            CurrentFrequencyRow(FromUtc.AddMinutes(1), "Good", HistoricalQueryValue.FromDouble(50), FromUtc.AddMinutes(1), "PresentUnverified"),
            CurrentFrequencyRow(FromUtc.AddMinutes(2), "Good", HistoricalQueryValue.FromDouble(50), FromUtc.AddSeconds(-1), "Synchronized"),
            CurrentFrequencyRow(FromUtc.AddMinutes(3), "Good", HistoricalQueryValue.FromDouble(50), null, "NotRecorded"),
            CurrentFrequencyRow(FromUtc.AddMinutes(4), "BadCommunication", HistoricalQueryValue.FromDouble(50), FromUtc.AddMinutes(4), "Synchronized")
        };

        var calculation = AssetProcessIntelligenceCurrentFrequencyComparisonV1Calculator.CalculateWindow(
            window,
            currentRows,
            frequencyRows,
            new ControlledEngineeringUnitReference(1, "unit.electric_current.ampere"),
            new ControlledEngineeringUnitReference(1, "unit.frequency.hertz"),
            49,
            51);

        Assert.Equal(0, calculation.Evidence.ExactSourceTimestampPairCount);
        Assert.Equal(0, calculation.Evidence.SelectedPairCount);
        Assert.Equal(1, calculation.Evidence.Frequency.RejectionCounts["source_timestamp_not_synchronized"]);
        Assert.Equal(1, calculation.Evidence.Frequency.RejectionCounts["source_timestamp_outside_requested_window"]);
        Assert.Equal(1, calculation.Evidence.Frequency.RejectionCounts["source_timestamp_missing"]);
        Assert.Equal(1, calculation.Evidence.Frequency.RejectionCounts["quality_not_good"]);
        Assert.Equal(1, calculation.Evidence.Frequency.QualityCounts["BadCommunication"]);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_UsesExactSynchronizedPairsAndReturnsReproducibleMeans()
    {
        using var fixture = Fixture.Create();
        var (currentTag, frequencyTag, request) = fixture.AddCurrentFrequencySignals();
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var from = query.Range.FromUtc!.Value;
            var isPrimary = from == request.PrimaryWindow!.FromUtc;
            var first = from.AddMinutes(5);
            var second = from.AddMinutes(10);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromDouble(tagId == currentTag.Id
                    ? isPrimary ? 4 : 2 : 50), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromDouble(tagId == currentTag.Id
                    ? isPrimary ? 6 : 4 : 50), second, "Synchronized"),
                V2Row(tagId, from.AddMinutes(15), "Good", HistoricalQueryValue.FromString("not numeric"),
                    from.AddMinutes(15), "Synchronized"),
                V2Row(tagId, from.AddMinutes(20), "Good", HistoricalQueryValue.FromBoolean(true),
                    from.AddMinutes(20), "Synchronized")
            ]);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(4, fixture.Historian.Calls);
        Assert.All(fixture.Historian.Requests, query =>
        {
            Assert.Equal(HistoricalDatasets.HistorianSamplesV2, query.Dataset);
            Assert.Equal(2, query.Version);
            Assert.Equal("timestamp", Assert.Single(query.OrderBy!).Field);
            Assert.Equal(200, query.Page!.Size);
        });
        Assert.Equal(currentTag.Id, payload.CurrentSignal.TagId);
        Assert.Equal(frequencyTag.Id, payload.FrequencySignal.TagId);
        Assert.Equal(5d, payload.Metric.PrimaryMeanCurrentA);
        Assert.Equal(3d, payload.Metric.ComparisonMeanCurrentA);
        Assert.Equal(2d, payload.Metric.DifferenceA);
        Assert.Equal(200d / 3d, payload.Metric.PercentChange!.Value, 10);
        Assert.Equal(16, payload.TotalRawObservationCount);
        Assert.Equal(2, payload.Primary.SelectedPairCount);
        Assert.Equal(2, payload.Comparison.SelectedPairCount);
        Assert.Equal(4, payload.Primary.Current.RawObservationCount);
        Assert.Equal(2, payload.Primary.Current.RejectedObservationCount);
        Assert.Equal(4, payload.Primary.Frequency.RawObservationCount);
        Assert.Equal(2, payload.Primary.Frequency.RejectedObservationCount);
        Assert.Equal("A", payload.CurrentSignal.CanonicalUnit);
        Assert.Equal("Hz", payload.FrequencySignal.CanonicalUnit);
    }

    [Theory]
    [InlineData("unit.electric_current.milliampere", 1)]
    [InlineData("unit.electric_current.milliampere", 2)]
    [InlineData("unit.electric_current.ampere", 2)]
    public async Task CurrentFrequencyComparisonV1_RejectsUnsupportedControlledUnitBeforeHistorianRead(
        string currentUnitCode,
        int currentUnitVersion)
    {
        using var fixture = Fixture.Create();
        var (_, _, request) = fixture.AddCurrentFrequencySignals(
            currentUnit: new ControlledEngineeringUnitReference(currentUnitVersion, currentUnitCode));

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async(request);

        AssertStatus(StatusCodes.Status400BadRequest, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_RejectsOverlappingWindowsBeforeHistorianRead()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        var request = fixture.CurrentFrequencyRequest! with
        {
            ComparisonWindow = Window(FromUtc.AddMinutes(30), FromUtc.AddMinutes(90))
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async(request);

        AssertStatus(StatusCodes.Status400BadRequest, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_OnlyStringAndBooleanRowsReturnInsufficientDataNotProviderFailure()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var first = query.Range.FromUtc!.Value.AddMinutes(5);
            var second = query.Range.FromUtc.Value.AddMinutes(10);
            return ResponseV2(query,
            [
                V2Row(tagId, first, "Good", HistoricalQueryValue.FromString("legacy string"), first, "Synchronized"),
                V2Row(tagId, second, "Good", HistoricalQueryValue.FromBoolean(false), second, "Synchronized")
            ]);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("insufficient_data", payload.Metric.Status);
        Assert.Null(payload.Metric.PrimaryMeanCurrentA);
        Assert.Null(payload.Metric.ComparisonMeanCurrentA);
        Assert.Equal(2, payload.Primary.Current.RejectedObservationCount);
        Assert.Equal(2, payload.Primary.Frequency.RejectedObservationCount);
        Assert.Equal(8, payload.TotalRawObservationCount);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_RejectsSetpointSemanticBeforeHistorianRead()
    {
        using var fixture = Fixture.Create();
        var (_, _, request) = fixture.AddCurrentFrequencySignals();
        var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
        var profile = configuration.Profiles!.Single();
        var invalidSignals = profile.Signals!.Select(signal => signal.Id == request.FrequencySignalId
            ? signal with
            {
                SemanticBinding = signal.SemanticBinding! with { SignalKind = "command_setpoint" }
            }
            : signal).ToArray();
        fixture.Workspace.AssetProcessIntelligence.Replace(configuration with
        {
            Profiles = [profile with { Signals = invalidSignals }]
        });
        var descriptor = fixture.Workspace.Describe();
        fixture.Workspace.SetCheckout(Fixture.ProjectKey, "invalid measured output test",
            descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async(request);

        AssertStatus(StatusCodes.Status400BadRequest, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_AuthorizesBothSelectedTagsBeforeFirstHistorianRead()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals(frequencyAccessPolicy: new TagAccessPolicy(ReadRoles: ["different-role"]));

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(0, fixture.Historian.Calls);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_DuplicateRawTimestampSplitAcrossPagesIsExcludedDeterministically()
    {
        using var fixture = Fixture.Create();
        var (currentTag, _, request) = fixture.AddCurrentFrequencySignals();
        var primaryFirst = request.PrimaryWindow!.FromUtc.AddMinutes(5);
        var primaryDuplicate = request.PrimaryWindow.FromUtc.AddMinutes(6);
        var comparisonFirst = request.ComparisonWindow!.FromUtc.AddMinutes(5);
        var comparisonSecond = request.ComparisonWindow.FromUtc.AddMinutes(6);
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var from = query.Range.FromUtc!.Value;
            if (tagId == currentTag.Id && from == request.PrimaryWindow.FromUtc)
            {
                if (query.Page!.Cursor is null)
                    return ResponseV2(query,
                    [
                        V2Row(tagId, primaryFirst, "Good", HistoricalQueryValue.FromDouble(10), primaryFirst, "Synchronized"),
                        V2Row(tagId, primaryDuplicate, "Good", HistoricalQueryValue.FromDouble(99), primaryDuplicate, "Synchronized")
                    ], "current-primary-second-page");
                return ResponseV2(query,
                    [V2Row(tagId, primaryDuplicate.AddSeconds(1), "Good", HistoricalQueryValue.FromDouble(101), primaryDuplicate, "PresentUnverified")]);
            }

            var isPrimary = from == request.PrimaryWindow.FromUtc;
            var times = isPrimary ? new[] { primaryFirst, primaryDuplicate } : new[] { comparisonFirst, comparisonSecond };
            return ResponseV2(query, times.Select((timestamp, index) =>
            {
                var sourceTimestamp = timestamp;
                var value = tagId == currentTag.Id
                    ? isPrimary ? index == 0 ? 10d : 99d : index == 0 ? 3d : 5d
                    : 50d;
                return V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(value), sourceTimestamp, "Synchronized");
            }).ToArray());
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async(request);

        AssertStatus(StatusCodes.Status200OK, result);
        var payload = Assert.IsType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(5, fixture.Historian.Calls);
        Assert.Equal("current-primary-second-page", fixture.Historian.Requests[1].Page!.Cursor);
        Assert.Equal(1, payload.Primary.Current.AmbiguousSourceTimestampGroupCount);
        Assert.Equal(2, payload.Primary.Current.AmbiguousSourceTimestampRowCount);
        Assert.Equal(1, payload.Primary.ExactSourceTimestampPairCount);
        Assert.Equal(1, payload.Primary.SelectedPairCount);
        Assert.Equal(primaryFirst, Assert.Single(payload.Primary.Pairs).SourceTimestampUtc);
        Assert.Equal("insufficient_data", payload.Metric.Status);
        Assert.Contains("fewer_than_two_primary_frequency_conditioned_pairs", payload.Metric.ReasonCodes);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_GlobalRowCapCountsRejectedRowsAndReturnsNoPartialResult()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        fixture.Historian.Respond = query =>
        {
            var pageNumber = fixture.Historian.Calls - 1;
            var cursor = pageNumber == 249 ? null : $"global-page-{pageNumber + 1}";
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            return ResponseV2(query, Enumerable.Range(0, 200)
                .Select(_ => V2Row(tagId, query.Range.FromUtc!.Value.AddSeconds(1), "Good",
                    HistoricalQueryValue.FromString("not numeric"), null, "NotRecorded"))
                .ToArray(), cursor);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status413PayloadTooLarge, result);
        Assert.Equal(250, fixture.Historian.Calls);
        var error = Assert.IsType<AssetProcessIntelligenceAnalyticsApiError>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("INTELLIGENCE_CURRENT_FREQUENCY_WINDOW_TOO_DENSE", error.Code);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_PermitRevocationAfterAnyPageDiscardsAllEvidence()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        fixture.Historian.Respond = query =>
        {
            fixture.Gate.Pause(Fixture.ProjectKey, fixture.ScopeKind, fixture.ScopeId);
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
            return ResponseV2(query,
                [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(5), timestamp, "Synchronized")]);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_FrequencyTagReadRevocationDuringCurrentPageStopsBeforeNextQuery()
    {
        using var fixture = Fixture.Create();
        var (currentTag, frequencyTag, _) = fixture.AddCurrentFrequencySignals(
            frequencyAccessPolicy: new TagAccessPolicy(ReadRoles: ["operator"]));

        fixture.Historian.Respond = query =>
        {
            fixture.Runtime.SetTag(frequencyTag with
            {
                AccessPolicy = new TagAccessPolicy(ReadRoles: ["different-role"])
            });
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
            return ResponseV2(query,
                [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(10), timestamp, "Synchronized")]);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.Equal(currentTag.Id, fixture.Historian.Requests[0].Filters!.Single().Values.Single().AsGuid());
        Assert.IsNotType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_PermitCancellationWhileHistorianQueryIsInFlightIsDeterministic()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Historian.RespondAsync = async (query, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ResponseV2(query, []);
        };

        var task = fixture.ExecuteCurrentFrequencyComparisonV1Async();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        fixture.Gate.Pause(Fixture.ProjectKey, fixture.ScopeKind, fixture.ScopeId);
        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_WorkspaceMutationDuringPageDiscardsTheWholeCalculation()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        fixture.Historian.Respond = query =>
        {
            fixture.Workspace.Tags.Upsert(fixture.Tag with { Description = "unsaved during query" });
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
            return ResponseV2(query,
                [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(10), timestamp, "Synchronized")]);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_RuntimeRevisionChangeDuringPageDiscardsTheWholeCalculation()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        fixture.Historian.Respond = query =>
        {
            fixture.Runtime.SetRevision(42);
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
            return ResponseV2(query,
                [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(10), timestamp, "Synchronized")]);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status409Conflict, result);
        Assert.Equal(1, fixture.Historian.Calls);
        Assert.IsNotType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_RejectsUnknownAssignmentAndInactiveScopeBeforeHistorianRead()
    {
        using (var invalidAssignment = Fixture.Create())
        {
            var (_, _, request) = invalidAssignment.AddCurrentFrequencySignals();
            var result = await invalidAssignment.ExecuteCurrentFrequencyComparisonV1Async(
                request with { AssignmentId = Guid.NewGuid() });

            AssertStatus(StatusCodes.Status404NotFound, result);
            Assert.Equal(0, invalidAssignment.Historian.Calls);
        }

        using (var invalidScope = Fixture.Create())
        {
            var (_, _, request) = invalidScope.AddCurrentFrequencySignals();
            var result = await invalidScope.ExecuteCurrentFrequencyComparisonV1Async(
                request with { ScopeId = Guid.NewGuid() });

            AssertStatus(StatusCodes.Status409Conflict, result);
            Assert.Equal(0, invalidScope.Historian.Calls);
        }
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_AggregateCapIsSharedAcrossTagsAndWindowsAndCountsRejectedRows()
    {
        using var fixture = Fixture.Create();
        fixture.AddCurrentFrequencySignals();
        var pageCounts = new Dictionary<(Guid Tag, DateTimeOffset From), int>();
        var partitions = new List<(Guid Tag, DateTimeOffset From)>();
        fixture.Historian.Respond = query =>
        {
            var tagId = query.Filters!.Single().Values.Single().AsGuid();
            var key = (Tag: tagId, From: query.Range.FromUtc!.Value);
            pageCounts.TryGetValue(key, out var localPage);
            if (!pageCounts.ContainsKey(key)) partitions.Add(key);
            pageCounts[key] = localPage + 1;
            var partitionIndex = partitions.IndexOf(key);
            var cursor = $"{partitionIndex}-{localPage + 1}";
            // The first three TAG/window partitions end at 12,400 rows each;
            // the fourth reaches the shared 50,000 cap while advertising another page.
            if (partitionIndex == 3 && localPage == 63)
                cursor = "still-more-after-global-cap";
            else if (partitionIndex < 3 && localPage == 61)
                cursor = null;
            var timestamp = query.Range.FromUtc.Value.AddSeconds(1);
            return ResponseV2(query, Enumerable.Range(0, 200)
                .Select(_ => V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromString("rejected raw row"), null, "NotRecorded"))
                .ToArray(), cursor);
        };

        var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async();

        AssertStatus(StatusCodes.Status413PayloadTooLarge, result);
        Assert.Equal(250, fixture.Historian.Calls);
        Assert.Equal(4, pageCounts.Count);
        Assert.All(pageCounts.Values.Take(3), count => Assert.Equal(62, count));
        Assert.Equal(64, pageCounts.Values.Last());
        var error = Assert.IsType<AssetProcessIntelligenceAnalyticsApiError>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("INTELLIGENCE_CURRENT_FREQUENCY_WINDOW_TOO_DENSE", error.Code);
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_RepeatedCursorAndInvalidV2MetadataReturnNoPartialResult()
    {
        using (var repeated = Fixture.Create())
        {
            repeated.AddCurrentFrequencySignals();
            repeated.Historian.Respond = query =>
            {
                var tagId = query.Filters!.Single().Values.Single().AsGuid();
                var timestamp = query.Range.FromUtc!.Value.AddMinutes(1);
                return ResponseV2(query,
                    [V2Row(tagId, timestamp, "Good", HistoricalQueryValue.FromDouble(1), timestamp, "Synchronized")],
                    "same-cursor");
            };

            var invalidCursor = await repeated.ExecuteCurrentFrequencyComparisonV1Async();

            AssertStatus(StatusCodes.Status503ServiceUnavailable, invalidCursor);
            Assert.Equal(2, repeated.Historian.Calls);
            Assert.IsNotType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
                Assert.IsAssignableFrom<IValueHttpResult>(invalidCursor).Value);
        }

        using (var malformed = Fixture.Create())
        {
            malformed.AddCurrentFrequencySignals();
            malformed.Historian.Respond = query => Response(
                query,
                [Row(query.Filters!.Single().Values.Single().AsGuid(), FromUtc.AddMinutes(1),
                    "Good", HistoricalQueryValue.FromDouble(1))]);

            var invalidMetadata = await malformed.ExecuteCurrentFrequencyComparisonV1Async();

            AssertStatus(StatusCodes.Status503ServiceUnavailable, invalidMetadata);
            Assert.Equal(1, malformed.Historian.Calls);
            Assert.IsNotType<AssetProcessIntelligenceCurrentFrequencyComparisonV1Response>(
                Assert.IsAssignableFrom<IValueHttpResult>(invalidMetadata).Value);
        }
    }

    [Fact]
    public async Task CurrentFrequencyComparisonV1_RejectsMissingOrWrongSemanticBindingsBeforeHistorianRead()
    {
        foreach (var wrongRole in new[] { false, true })
        {
            using var fixture = Fixture.Create();
            var (_, _, request) = fixture.AddCurrentFrequencySignals();
            var configuration = fixture.Workspace.AssetProcessIntelligence.Snapshot();
            var profile = configuration.Profiles!.Single();
            var signals = profile.Signals!.Select(signal => signal.Id == request.FrequencySignalId
                ? signal with
                {
                    SemanticBinding = wrongRole
                        ? signal.SemanticBinding! with
                        {
                            RoleCode = "pump.discharge_pressure",
                            SignalKind = null,
                            FrequencyReference = null,
                            PressureReference = "gauge"
                        }
                        : null
                }
                : signal).ToArray();
            fixture.Workspace.AssetProcessIntelligence.Replace(configuration with
            {
                Profiles = [profile with { Signals = signals }]
            });
            var descriptor = fixture.Workspace.Describe();
            fixture.Workspace.SetCheckout(Fixture.ProjectKey, "invalid semantic binding test",
                descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);

            var result = await fixture.ExecuteCurrentFrequencyComparisonV1Async(request);

            AssertStatus(StatusCodes.Status400BadRequest, result);
            Assert.Equal(0, fixture.Historian.Calls);
        }
    }

    private static AssetProcessIntelligenceCurrentFrequencyRawObservation CurrentFrequencyRow(
        DateTimeOffset observed,
        string? quality,
        HistoricalQueryValue? value,
        DateTimeOffset? source,
        string? evidence) => new(observed, quality, value, source, evidence);

    private static HistoricalQueryRow V2Row(
        Guid tagId,
        DateTimeOffset timestamp,
        string? quality,
        HistoricalQueryValue value,
        DateTimeOffset? sourceTimestamp,
        string? sourceEvidence) =>
        new(new Dictionary<string, HistoricalQueryValue>(StringComparer.Ordinal)
        {
            ["tag.id"] = HistoricalQueryValue.FromGuid(tagId),
            ["timestamp"] = HistoricalQueryValue.FromDateTime(timestamp),
            ["quality"] = quality is null ? HistoricalQueryValue.Null() : HistoricalQueryValue.FromEnum(quality),
            ["value"] = value,
            ["source.timestamp"] = sourceTimestamp.HasValue
                ? HistoricalQueryValue.FromDateTime(sourceTimestamp.Value)
                : HistoricalQueryValue.Null(),
            ["source.timestamp.status"] = sourceEvidence is null
                ? HistoricalQueryValue.Null()
                : HistoricalQueryValue.FromEnum(sourceEvidence)
        });

    private static HistoricalQueryResponse ResponseV2(
        HistoricalQueryRequest request,
        IReadOnlyList<HistoricalQueryRow> rows,
        string? cursor = null) => new(
        2,
        HistoricalDatasets.HistorianSamplesV2,
        HistoricalQueryCatalog.Require(HistoricalDatasets.HistorianSamplesV2).Columns,
        rows,
        request.Range.FromUtc!.Value,
        request.Range.ToUtc!.Value,
        cursor,
        request.Page!.Size);

    private static AssetProcessIntelligenceCurrentFrequencyComparisonV1WindowEvidence EmptyCurrentFrequencyEvidence(
        AssetProcessIntelligenceAnalyticsWindow window)
    {
        var emptySignal = new AssetProcessIntelligenceCurrentFrequencyComparisonV1SignalWindowEvidence(
            0, 0, 0, 0, 0, 0,
            new Dictionary<string, int>(),
            new Dictionary<string, int>());
        return new(window, emptySignal, emptySignal, 0, 0, 0, 0, 0, 0, 0, Array.Empty<AssetProcessIntelligenceCurrentFrequencyComparisonV1PairEvidence>());
    }

    private static AssetProcessIntelligenceRawObservation Observation(
        DateTimeOffset timestamp,
        string? quality,
        HistoricalQueryValue? value) => new(timestamp, quality, value);

    private static AssetProcessIntelligenceAnalyticsWindow Window(DateTimeOffset from, DateTimeOffset to) => new(from, to);

    private static HistoricalQueryRow Row(Guid tagId, DateTimeOffset timestamp, string? quality, HistoricalQueryValue value) =>
        new(new Dictionary<string, HistoricalQueryValue>(StringComparer.Ordinal)
        {
            ["tag.id"] = HistoricalQueryValue.FromGuid(tagId),
            ["timestamp"] = HistoricalQueryValue.FromDateTime(timestamp),
            ["quality"] = quality is null ? HistoricalQueryValue.Null() : HistoricalQueryValue.FromEnum(quality),
            ["value"] = value
        });

    private static HistoricalQueryResponse Response(
        HistoricalQueryRequest request,
        IReadOnlyList<HistoricalQueryRow> rows,
        string? cursor = null) => new(
        HistoricalQueryContract.Version,
        HistoricalDatasets.HistorianSamples,
        Array.Empty<HistoricalColumn>(),
        rows,
        request.Range.FromUtc!.Value,
        request.Range.ToUtc!.Value,
        cursor,
        request.Page!.Size);

    private static void AssertStatus(int expected, IResult result) =>
        Assert.Equal(expected, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);

    private sealed class Fixture : IDisposable
    {
        public const string ProjectKey = "analytics-test";
        private readonly ServiceProvider _services;

        private Fixture(
            EngineeringWorkspace workspace,
            TagDefinition tag,
            Guid equipmentId,
            Guid scopeId,
            AssetProcessIntelligenceScopeKind scopeKind,
            AssetProcessIntelligenceExecutionGate gate,
            RuntimeHighAvailabilityService highAvailability,
            TestLicenseService licensing,
            TestRuntimeCoordinator runtime,
            InMemoryAuthorityPolicyStore authority,
            RecordingHistoricalQueryService historian,
            AssetProcessIntelligenceAnalyticsService analytics,
            ServiceProvider services)
        {
            Workspace = workspace;
            Tag = tag;
            EquipmentId = equipmentId;
            ScopeId = scopeId;
            ScopeKind = scopeKind;
            Gate = gate;
            HighAvailability = highAvailability;
            Licensing = licensing;
            Runtime = runtime;
            Authority = authority;
            Historian = historian;
            Analytics = analytics;
            _services = services;
            Request = new AssetProcessIntelligenceAnalyticsQueryRequest(
                scopeKind,
                scopeId,
                tag.Id,
                Window(FromUtc, FromUtc.AddHours(1)));
        }

        public EngineeringWorkspace Workspace { get; }
        public TagDefinition Tag { get; }
        public Guid EquipmentId { get; }
        public Guid ScopeId { get; }
        public AssetProcessIntelligenceScopeKind ScopeKind { get; }
        public AssetProcessIntelligenceExecutionGate Gate { get; }
        public RuntimeHighAvailabilityService HighAvailability { get; }
        public TestLicenseService Licensing { get; }
        public TestRuntimeCoordinator Runtime { get; }
        public InMemoryAuthorityPolicyStore Authority { get; }
        public RecordingHistoricalQueryService Historian { get; }
        public AssetProcessIntelligenceAnalyticsService Analytics { get; }
        public AssetProcessIntelligenceAnalyticsQueryRequest Request { get; }
        public AssetProcessIntelligenceCurrentFrequencyComparisonV1QueryRequest? CurrentFrequencyRequest { get; private set; }
        public AssetProcessIntelligencePerformanceV1BQueryRequest? PerformanceV1BRequest { get; private set; }

        public Task<IResult> ExecuteAsync(
            AssetProcessIntelligenceAnalyticsQueryRequest? request = null)
        {
            var context = CreateContext();
            return Analytics.ExecuteAsync(context, request ?? Request, context.RequestAborted);
        }

        public Task<IResult> ExecuteNetObservedRateV1Async(
            AssetProcessIntelligenceNetObservedRateV1QueryRequest? request = null)
        {
            var context = CreateContext();
            return Analytics.ExecuteNetObservedRateV1Async(
                context,
                request ?? new AssetProcessIntelligenceNetObservedRateV1QueryRequest(
                    ScopeKind,
                    ScopeId,
                    Tag.Id,
                    Request.Window),
                context.RequestAborted);
        }

        public Task<IResult> ExecuteProfileCoverageAsync(Guid? scopeId = null)
        {
            var profile = Workspace.AssetProcessIntelligence.Snapshot().Profiles!.Single();
            var context = CreateContext();
            return Analytics.ExecuteProfileCoverageAsync(
                context,
                new AssetProcessIntelligenceProfileCoverageQueryRequest(
                    ScopeKind,
                    scopeId ?? ScopeId,
                    profile.Id,
                    Window(FromUtc, FromUtc.AddHours(1))),
                context.RequestAborted);
        }

        public Task<IResult> ExecuteCurrentFrequencyComparisonV1Async(
            AssetProcessIntelligenceCurrentFrequencyComparisonV1QueryRequest? request = null)
        {
            var context = CreateContext();
            return Analytics.ExecuteCurrentFrequencyComparisonV1Async(
                context,
                request ?? CurrentFrequencyRequest ?? throw new InvalidOperationException("Current/frequency signals were not configured."),
                context.RequestAborted);
        }

        public Task<IResult> ExecutePerformanceV1BAsync(AssetProcessIntelligencePerformanceV1BQueryRequest? request = null)
        {
            var context = CreateContext();
            return Analytics.ExecutePerformanceV1BAsync(
                context,
                request ?? PerformanceV1BRequest ?? throw new InvalidOperationException("Performance signals were not configured."),
                context.RequestAborted);
        }

        public (AssetProcessIntelligencePerformanceV1BQueryRequest Request, Dictionary<Guid, TagDefinition> Tags)
            AddPerformanceSignals(string ruleId, string productionRole = "process.net_good_production_volume_rate")
        {
            var specifications = ruleId switch
            {
                "specific-active-energy-at-recorded-production-rate.v1" => new[]
                {
                    ("equipment.active_input_power", "ActivePower", 2, "unit.power.kilowatt"),
                    (productionRole, "ProductionRate", 2, productionRole switch
                    {
                        "process.net_good_production_mass_rate" => "unit.mass_flow.kilogram_per_hour",
                        "process.net_good_production_item_rate" => "unit.item_rate.item_per_hour",
                        _ => "unit.volumetric_flow.cubic_metre_per_second"
                    })
                },
                "recorded-differential-pressure-power.v1" => new[]
                {
                    ("pump.discharge_volumetric_flow", "Flow", 1, "unit.volumetric_flow.cubic_metre_per_hour"),
                    ("pump.direct_differential_pressure", "DifferentialPressure", 2, "unit.pressure.bar")
                },
                "recorded-pressure-power-input-ratio.v1" => new[]
                {
                    ("pump.discharge_volumetric_flow", "Flow", 1, "unit.volumetric_flow.cubic_metre_per_hour"),
                    ("pump.direct_differential_pressure", "DifferentialPressure", 2, "unit.pressure.bar"),
                    ("equipment.active_input_power", "ActivePower", 2, "unit.power.watt")
                },
                _ => throw new ArgumentOutOfRangeException(nameof(ruleId))
            };
            var tags = new Dictionary<Guid, TagDefinition>();
            var signals = new List<AssetProcessIntelligenceSignalEngineeringDto>();
            foreach (var (role, suffix, semanticCatalog, unitCode) in specifications)
            {
                var tag = new TagDefinition(
                    Guid.NewGuid(), suffix, $"Plant.Pump.{suffix}", TagDataType.Double, null, null, null, true,
                    ControlledEngineeringUnit: new ControlledEngineeringUnitReference(
                        suffix == "DifferentialPressure" || suffix == "Flow" ? 1 : 2, unitCode));
                Workspace.Tags.Register(tag);
                Runtime.AddTag(tag);
                tags.Add(tag.Id, tag);
                var binding = role switch
                {
                    "equipment.active_input_power" => new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(
                        2, role, SignalKind: "measured_total_input"),
                    "process.net_good_production_volume_rate" or "process.net_good_production_mass_rate" or "process.net_good_production_item_rate" =>
                        new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(2, role, SignalKind: "measured_output"),
                    "pump.discharge_volumetric_flow" => new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(
                        1, role, FlowDirection: "pump_to_process"),
                    "pump.direct_differential_pressure" => new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(
                        2, role, SignalKind: "measured_output", PressureReference: "differential_outlet_minus_inlet"),
                    _ => throw new InvalidOperationException("Unexpected performance role.")
                };
                signals.Add(new AssetProcessIntelligenceSignalEngineeringDto(
                    Guid.NewGuid(), suffix, "legacy role is intentionally opaque", tag.Id, SemanticBinding: binding));
            }
            var configuration = Workspace.AssetProcessIntelligence.Snapshot();
            var profile = configuration.Profiles!.Single();
            Workspace.AssetProcessIntelligence.Replace(configuration with
            {
                Profiles = [profile with { Signals = profile.Signals!.Concat(signals).ToArray() }]
            });
            var descriptor = Workspace.Describe();
            Workspace.SetCheckout(ProjectKey, "Performance V1B test", descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);
            var assignment = configuration.Assignments!.Single();
            var request = new AssetProcessIntelligencePerformanceV1BQueryRequest(
                ScopeKind, ScopeId, assignment.Id, profile.Id, ruleId,
                Array.AsReadOnly(signals.Select(signal => signal.Id).ToArray()),
                Window(FromUtc, FromUtc.AddHours(1)));
            PerformanceV1BRequest = request;
            return (request, tags);
        }

        public (TagDefinition CurrentTag, TagDefinition FrequencyTag,
            AssetProcessIntelligenceCurrentFrequencyComparisonV1QueryRequest Request) AddCurrentFrequencySignals(
            ControlledEngineeringUnitReference? currentUnit = null,
            TagAccessPolicy? frequencyAccessPolicy = null)
        {
            currentUnit ??= new ControlledEngineeringUnitReference(1, "unit.electric_current.ampere");
            var currentTag = new TagDefinition(
                Guid.NewGuid(), "Current", "Plant.Pump.Current", TagDataType.Double,
                null, "A", null, true,
                AccessPolicy: null,
                ControlledEngineeringUnit: currentUnit);
            var frequencyTag = new TagDefinition(
                Guid.NewGuid(), "Drive frequency", "Plant.Pump.DriveFrequency", TagDataType.Double,
                null, "Hz", null, true,
                AccessPolicy: frequencyAccessPolicy,
                ControlledEngineeringUnit: new ControlledEngineeringUnitReference(1, "unit.frequency.hertz"));
            Workspace.Tags.Register(currentTag);
            Workspace.Tags.Register(frequencyTag);
            Runtime.AddTag(currentTag);
            Runtime.AddTag(frequencyTag);

            var configuration = Workspace.AssetProcessIntelligence.Snapshot();
            var profile = configuration.Profiles!.Single();
            var currentSignalId = Guid.NewGuid();
            var frequencySignalId = Guid.NewGuid();
            var currentSignal = new AssetProcessIntelligenceSignalEngineeringDto(
                currentSignalId, "electrical-current", "opaque legacy text", currentTag.Id,
                SemanticBinding: new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(
                    1, "pump.electrical_current", CurrentBasis: "line_rms"));
            var frequencySignal = new AssetProcessIntelligenceSignalEngineeringDto(
                frequencySignalId, "measured-drive-frequency", "also opaque", frequencyTag.Id,
                SemanticBinding: new AssetProcessIntelligenceSignalSemanticBindingEngineeringDto(
                    1, "pump.drive_frequency", SignalKind: "measured_output", FrequencyReference: "drive_electrical"));
            Workspace.AssetProcessIntelligence.Replace(configuration with
            {
                Profiles = [profile with
                {
                    Signals = (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
                        .Concat([currentSignal, frequencySignal]).ToArray()
                }]
            });
            var descriptor = Workspace.Describe();
            Workspace.SetCheckout(ProjectKey, "Stage 3B test", descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);
            var assignmentId = configuration.Assignments!.Single().Id;
            var request = new AssetProcessIntelligenceCurrentFrequencyComparisonV1QueryRequest(
                ScopeKind,
                ScopeId,
                assignmentId,
                currentSignalId,
                frequencySignalId,
                Window(FromUtc, FromUtc.AddHours(1)),
                Window(FromUtc.AddHours(2), FromUtc.AddHours(3)),
                49,
                51);
            CurrentFrequencyRequest = request;
            return (currentTag, frequencyTag, request);
        }

        public TagDefinition AddProfileSignal(string key, string role, TagDataType dataType, string? unit)
        {
            var tag = new TagDefinition(
                Guid.NewGuid(),
                key,
                $"Plant.Pump.{key}",
                dataType,
                null,
                unit,
                null,
                true);
            Workspace.Tags.Register(tag);
            Runtime.AddTag(tag);
            var configuration = Workspace.AssetProcessIntelligence.Snapshot();
            var profile = configuration.Profiles!.Single();
            var signal = new AssetProcessIntelligenceSignalEngineeringDto(
                Guid.NewGuid(),
                key,
                role,
                tag.Id);
            Workspace.AssetProcessIntelligence.Replace(configuration with
            {
                Profiles = [profile with { Signals = (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>()).Append(signal).ToArray() }]
            });
            var descriptor = Workspace.Describe();
            Workspace.SetCheckout(ProjectKey, "Stage 3B test", descriptor.BaseRevision!.Value, DateTimeOffset.UtcNow);
            return tag;
        }

        private static DefaultHttpContext CreateContext()
        {
            var context = new DefaultHttpContext();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "analytics-user"),
                new Claim("role", "operator")
            ],
            "analytics-test"));
            return context;
        }

        public static Fixture Create(
            bool licensed = true,
            bool grantTagRead = true,
            bool grantTrendUse = true,
            bool grantPermit = true,
            bool activeScope = true,
            long workspaceRevision = 41,
            long? runtimeRevision = null,
            AssetProcessIntelligenceScopeKind scopeKind = AssetProcessIntelligenceScopeKind.Equipment,
            bool nestedLocation = false,
            TagDataType tagDataType = TagDataType.Double,
            string? tagUnit = "bar")
        {
            var runtimeVersion = runtimeRevision ?? workspaceRevision;
            var workspace = new EngineeringWorkspace(seedDemo: false);
            var tagId = Guid.NewGuid();
            var tag = new TagDefinition(
                tagId,
                "Pressure",
                "Plant.Pump.Pressure",
                tagDataType,
                null,
                tagUnit,
                null,
                true);
            workspace.Tags.Register(tag);
            var equipmentId = Guid.NewGuid();
            var scopeId = scopeKind == AssetProcessIntelligenceScopeKind.Equipment
                ? equipmentId
                : Guid.NewGuid();
            Guid? locationId = null;
            if (scopeKind == AssetProcessIntelligenceScopeKind.Location)
            {
                var targetLocation = new LocationEngineeringDto(scopeId, "Plant");
                workspace.Assets.UpsertLocation(targetLocation);
                locationId = scopeId;
                if (nestedLocation)
                {
                    var child = Guid.NewGuid();
                    workspace.Assets.UpsertLocation(new LocationEngineeringDto(child, "Area", scopeId));
                    locationId = child;
                }
            }
            workspace.Assets.UpsertEquipment(new EquipmentEngineeringDto(
                equipmentId,
                "Plant.Pump",
                "Pump",
                LocationId: locationId));
            var profileId = Guid.NewGuid();
            var assignmentId = Guid.NewGuid();
            workspace.AssetProcessIntelligence.Replace(new AssetProcessIntelligenceEngineeringDto(
                Version: 2,
                Scopes: [new AssetProcessIntelligenceScopeEngineeringDto(scopeKind, scopeId, activeScope)],
                Profiles: [new AssetProcessIntelligenceProfileEngineeringDto(
                    profileId,
                    "pump.profile",
                    "Pump profile",
                    [new AssetProcessIntelligenceSignalEngineeringDto(Guid.NewGuid(), "pressure", "free-text-only", tagId)])],
                Assignments: [new AssetProcessIntelligenceAssignmentEngineeringDto(assignmentId, equipmentId, profileId)],
                ProcessStages: []));
            workspace.SetCheckout(ProjectKey, "Stage 3A test", workspaceRevision, DateTimeOffset.UtcNow);

            var roleId = Guid.NewGuid();
            var grants = new List<CapabilityGrantEngineeringDto>();
            if (grantTrendUse) grants.Add(new CapabilityGrantEngineeringDto(SecurityCapability.TrendUse));
            if (grantTagRead) grants.Add(new CapabilityGrantEngineeringDto(SecurityCapability.TagRead));
            var authority = new InMemoryAuthorityPolicyStore(
                [new SecurityRoleEngineeringDto(roleId, "operator", "Operator", Grants: grants)],
                []);

            var runtime = new TestRuntimeCoordinator(ProjectKey, runtimeVersion, tag);
            var runtimeFacade = new ScadaRuntimeFacade(runtime);
            var highAvailability = new RuntimeHighAvailabilityService(new ConfigurationBuilder().Build());
            var gate = new AssetProcessIntelligenceExecutionGate();
            if (grantPermit)
                gate.Activate(ProjectKey, runtimeVersion, scopeKind, scopeId);
            var licensing = new TestLicenseService(licensed);
            var historian = new RecordingHistoricalQueryService();
            var persistence = PersistenceProxy.Create(new EngineeringProjectSnapshot(
                runtimeVersion,
                ProjectKey,
                "Stage 3A test",
                "engineering",
                25,
                DateTimeOffset.UtcNow,
                "{}"));
            var services = new ServiceCollection()
                .AddSingleton<IEngineeringProjectPersistenceService>(persistence)
                .AddSingleton(highAvailability)
                .BuildServiceProvider();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:Enabled"] = "true" })
                .Build();
            var security = new ApiAuthorizationService(services, authority, configuration);
            var analytics = new AssetProcessIntelligenceAnalyticsService(
                workspace,
                runtimeFacade,
                highAvailability,
                licensing,
                gate,
                security,
                historian);
            return new Fixture(workspace, tag, equipmentId, scopeId, scopeKind,
                gate, highAvailability, licensing, runtime, authority, historian, analytics, services);
        }

        public void Dispose()
        {
            Workspace.Dispose();
            _services.Dispose();
        }
    }

    private sealed class RecordingHistoricalQueryService : IHistoricalQueryService
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public List<HistoricalQueryRequest> Requests { get; } = [];
        public Func<HistoricalQueryRequest, HistoricalQueryResponse>? Respond { get; set; }
        public Func<HistoricalQueryRequest, CancellationToken, Task<HistoricalQueryResponse>>? RespondAsync { get; set; }

        public async Task<HistoricalQueryResponse> QueryAsync(
            HistoricalQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            Requests.Add(request);
            if (RespondAsync is not null)
                return await RespondAsync(request, cancellationToken);
            return Respond?.Invoke(request) ?? Response(request, []);
        }
    }

    private sealed class TestLicenseService(bool licensed) : IProductLicenseService
    {
        public bool Licensed { get; set; } = licensed;
        public string MachineFingerprint => "analytics-test-machine";
        public string MachineRequestCode => "analytics-test-request";
        public LicenseVerificationResult CurrentVerification => new(
            LicenseState.Valid,
            V3Entitlements: new MachineLicenseV3Entitlements(1, 1, false, Licensed));
        public LicenseVerificationResult VerifyCandidate(string licenseCode) => CurrentVerification;
        public void InstallLicense(string licenseCode) => throw new NotSupportedException();
        public void RemoveLicense() => throw new NotSupportedException();
        public RunEntitlementDecision EvaluateRun(int projectTagCount) =>
            ProductEntitlementEvaluator.Evaluate(CurrentVerification, projectTagCount);
    }

    private sealed class TestRuntimeCoordinator(string projectKey, long revision, TagDefinition tag)
        : IEngineeringRuntimeCoordinator
    {
        private readonly List<TagDefinition> _tags = [tag];
        private RuntimeDescriptor _descriptor = new(
            projectKey, revision, DateTimeOffset.Parse("2026-10-14T00:00:00Z"), [], [], 1, 0);

        public RuntimeDescriptor Describe() => _descriptor;
        public void SetRevision(long revision) => _descriptor = _descriptor with { Revision = revision };
        public void AddTag(TagDefinition tag) => _tags.Add(tag);
        public void SetTag(TagDefinition tag)
        {
            _tags.RemoveAll(existing => existing.Id == tag.Id);
            _tags.Add(tag);
        }
        public IReadOnlyCollection<TagDefinition> Tags() => _tags.ToArray();
        public IReadOnlyCollection<TagValue> CurrentValues() => [];
        public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() => [];
        public IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false) => [];
        public IReadOnlyCollection<CommandDefinition> Commands() => [];
        public IReadOnlyCollection<ClientMemoryRuntimeSource> ClientMemorySources() => [];
        public bool TryGetTag(Guid tagId, out TagDefinition? result)
        {
            result = _tags.SingleOrDefault(item => item.Id == tagId);
            return result is not null;
        }
        public bool TryGetTagByPath(string path, out TagDefinition? result)
        {
            result = _tags.SingleOrDefault(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
            return result is not null;
        }
        public bool TryGetCurrent(Guid tagId, out TagValue? value) { value = null; return false; }
        public bool TryGetCommand(Guid commandId, out CommandDefinition? command) { command = null; return false; }
        public bool IsServerMemoryTag(Guid tagId) => false;
        public ValueTask<bool> AcknowledgeAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> ShelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> UnshelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ResetServerMemoryRetainedValueAsync(Guid tagId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ExecuteCommandAsync(Guid commandId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public Task<RuntimeActivationResult> ActivateAsync(string projectKey, long revision, EngineeringPackage package, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RuntimeActivationResult> ActivateAsync(string projectKey, long revision, EngineeringPackage package, Func<RuntimeActivationCommitContext, CancellationToken, Task> commitAsync, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public class PersistenceProxy : DispatchProxy
    {
        public EngineeringProjectSnapshot? ActiveSnapshot { get; set; }

        public static IEngineeringProjectPersistenceService Create(EngineeringProjectSnapshot active)
        {
            var persistence = DispatchProxy.Create<IEngineeringProjectPersistenceService, PersistenceProxy>();
            ((PersistenceProxy)(object)persistence).ActiveSnapshot = active;
            return persistence;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IEngineeringProjectPersistenceService.LoadActiveAsync))
                return Task.FromResult(ActiveSnapshot);
            throw new NotSupportedException($"Persistence method '{targetMethod?.Name}' is not part of this test.");
        }
    }
}
