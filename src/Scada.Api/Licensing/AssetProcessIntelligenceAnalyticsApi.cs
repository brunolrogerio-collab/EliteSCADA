namespace Scada.Api.Licensing;

public static class AssetProcessIntelligenceAnalyticsApi
{
    public const string Route = "/api/engineering/asset-process-intelligence/analytics";

    public static IEndpointRouteBuilder MapAssetProcessIntelligenceAnalyticsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup(Route);
        group.MapGet("/catalog", () => Results.Ok(AssetProcessIntelligenceKpiCatalogV1.Describe()));
        group.MapGet("/profile-coverage/catalog", () => Results.Ok(AssetProcessIntelligenceProfileCoverageCatalogV1.Describe()));
        group.MapGet("/net-observed-rate/v1/catalog", () =>
            Results.Ok(AssetProcessIntelligenceNetObservedRateV1Catalog.Describe()));
        group.MapGet("/current-frequency-comparison/v1/catalog", () =>
            Results.Ok(AssetProcessIntelligenceCurrentFrequencyComparisonV1Catalog.Describe()));
        group.MapGet("/contextual-reference-deviation/v1/catalog", () =>
            Results.Ok(AssetProcessIntelligenceContextualReferenceDeviationV1Catalog.Describe()));
        group.MapGet("/contextual-deviation-vector/v1/catalog", () =>
            Results.Ok(AssetProcessIntelligenceContextualDeviationVectorV1Catalog.Describe()));
        group.MapGet("/performance/v1/catalog", () =>
            Results.Ok(AssetProcessIntelligencePerformanceV1BCatalog.Describe()));
        group.MapPost("/query", (
            HttpContext context,
            AssetProcessIntelligenceAnalyticsQueryRequest request,
            AssetProcessIntelligenceAnalyticsService service,
            CancellationToken cancellationToken) =>
            service.ExecuteAsync(context, request, cancellationToken));
        group.MapPost("/net-observed-rate/v1/query", (
            HttpContext context,
            AssetProcessIntelligenceNetObservedRateV1QueryRequest request,
            AssetProcessIntelligenceAnalyticsService service,
            CancellationToken cancellationToken) =>
            service.ExecuteNetObservedRateV1Async(context, request, cancellationToken));
        group.MapPost("/current-frequency-comparison/v1/query", (
            HttpContext context,
            AssetProcessIntelligenceCurrentFrequencyComparisonV1QueryRequest request,
            AssetProcessIntelligenceAnalyticsService service,
            CancellationToken cancellationToken) =>
            service.ExecuteCurrentFrequencyComparisonV1Async(context, request, cancellationToken));
        group.MapPost("/contextual-reference-deviation/v1/query", (
            HttpContext context,
            AssetProcessIntelligenceContextualReferenceDeviationV1QueryRequest request,
            AssetProcessIntelligenceAnalyticsService service,
            CancellationToken cancellationToken) =>
            service.ExecuteContextualReferenceDeviationV1Async(context, request, cancellationToken));
        group.MapPost("/contextual-deviation-vector/v1/query", (
            HttpContext context,
            AssetProcessIntelligenceContextualDeviationVectorV1QueryRequest request,
            AssetProcessIntelligenceAnalyticsService service,
            CancellationToken cancellationToken) =>
            service.ExecuteContextualDeviationVectorV1Async(context, request, cancellationToken));
        group.MapPost("/performance/v1/query", (
            HttpContext context,
            AssetProcessIntelligencePerformanceV1BQueryRequest request,
            AssetProcessIntelligenceAnalyticsService service,
            CancellationToken cancellationToken) =>
            service.ExecutePerformanceV1BAsync(context, request, cancellationToken));
        group.MapPost("/profile-coverage", (
            HttpContext context,
            AssetProcessIntelligenceProfileCoverageQueryRequest request,
            AssetProcessIntelligenceAnalyticsService service,
            CancellationToken cancellationToken) =>
            service.ExecuteProfileCoverageAsync(context, request, cancellationToken));
        return endpoints;
    }
}
