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
                "/api/engineering/asset-process-intelligence/analytics/net-observed-rate/v1/catalog",
                "/api/engineering/asset-process-intelligence/analytics/net-observed-rate/v1/query",
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
        public TestRuntimeCoordinator Runtime { get; }
        public InMemoryAuthorityPolicyStore Authority { get; }
        public RecordingHistoricalQueryService Historian { get; }
        public AssetProcessIntelligenceAnalyticsService Analytics { get; }
        public AssetProcessIntelligenceAnalyticsQueryRequest Request { get; }

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
                gate, highAvailability, runtime, authority, historian, analytics, services);
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
        public string MachineFingerprint => "analytics-test-machine";
        public string MachineRequestCode => "analytics-test-request";
        public LicenseVerificationResult CurrentVerification => new(
            LicenseState.Valid,
            V3Entitlements: new MachineLicenseV3Entitlements(1, 1, false, licensed));
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
