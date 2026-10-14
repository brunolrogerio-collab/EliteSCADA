using System.Reflection;
using System.Security.Claims;
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
    public async Task EndpointSurfaceContainsOnlyCatalogAndReadOnlyQuery()
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
                "/api/engineering/asset-process-intelligence/analytics/query"
            },
            routes);
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
            var context = new DefaultHttpContext();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "analytics-user"),
                new Claim("role", "operator")
            ],
            "analytics-test"));
            return Analytics.ExecuteAsync(context, request ?? Request, context.RequestAborted);
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
            TagDataType tagDataType = TagDataType.Double)
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
                "bar",
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
        private RuntimeDescriptor _descriptor = new(
            projectKey, revision, DateTimeOffset.Parse("2026-10-14T00:00:00Z"), [], [], 1, 0);

        public RuntimeDescriptor Describe() => _descriptor;
        public void SetRevision(long revision) => _descriptor = _descriptor with { Revision = revision };
        public IReadOnlyCollection<TagDefinition> Tags() => [tag];
        public IReadOnlyCollection<TagValue> CurrentValues() => [];
        public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() => [];
        public IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false) => [];
        public IReadOnlyCollection<CommandDefinition> Commands() => [];
        public IReadOnlyCollection<ClientMemoryRuntimeSource> ClientMemorySources() => [];
        public bool TryGetTag(Guid tagId, out TagDefinition? result)
        {
            result = tagId == tag.Id ? tag : null;
            return result is not null;
        }
        public bool TryGetTagByPath(string path, out TagDefinition? result)
        {
            result = string.Equals(path, tag.Path, StringComparison.OrdinalIgnoreCase) ? tag : null;
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
