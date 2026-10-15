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
        group.MapPost("/query", (
            HttpContext context,
            AssetProcessIntelligenceAnalyticsQueryRequest request,
            AssetProcessIntelligenceAnalyticsService service,
            CancellationToken cancellationToken) =>
            service.ExecuteAsync(context, request, cancellationToken));
        group.MapPost("/profile-coverage", (
            HttpContext context,
            AssetProcessIntelligenceProfileCoverageQueryRequest request,
            AssetProcessIntelligenceAnalyticsService service,
            CancellationToken cancellationToken) =>
            service.ExecuteProfileCoverageAsync(context, request, cancellationToken));
        return endpoints;
    }
}
