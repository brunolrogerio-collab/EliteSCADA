using Microsoft.Extensions.DependencyInjection.Extensions;
using Scada.Api.Reports;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Security.Authorization;

namespace Scada.Api.Historian;

public static class HistoricalQueryApi
{
    public const string Route = "/api/historical/query";
    private const string LoggerCategory = "Scada.Api.Historian.HistoricalQueryApi";

    public static void AddHistoricalQueryApiCore(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddHttpContextAccessor();
        builder.Services.TryAddScoped<IHistoricalQueryAuthorizer, ApiHistoricalQueryAuthorizer>();
        builder.Services.TryAddScoped<IHistoricalQueryService, HistoricalQueryService>();
    }

    public static RouteHandlerBuilder MapHistoricalQueryEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var historicalQuery = app.MapPost(
            Route,
            async (
                HistoricalQueryRequest request,
                IHistoricalQueryService service,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
                await ExecuteAsync(
                    request,
                    service,
                    cancellationToken,
                    loggerFactory.CreateLogger(LoggerCategory)));

        // Report Preview executes only through the accepted Historical Query service,
        // so it is mounted with the same explicitly enabled historical feature bundle.
        app.MapReportExecutionEndpoints();
        return historicalQuery;
    }

    public static async Task<IResult> ExecuteAsync(
        HistoricalQueryRequest request,
        IHistoricalQueryService service,
        CancellationToken cancellationToken = default,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(service);

        try
        {
            var response = await service.QueryAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HistoricalQueryUnauthorizedException)
        {
            return Results.Unauthorized();
        }
        catch (HistoricalQueryForbiddenException)
        {
            return Results.Json(
                new HistoricalQueryApiError("forbidden", "Forbidden."),
                statusCode: StatusCodes.Status403Forbidden);
        }
        catch (HistoricalQueryCursorException)
        {
            return Results.BadRequest(
                new HistoricalQueryApiError(
                    "invalid_cursor",
                    "Historical query cursor is invalid, expired, or does not match this request."));
        }
        catch (HistoricalQueryValidationException ex)
        {
            return Results.BadRequest(
                new HistoricalQueryApiError("invalid_query", ex.Message));
        }
        catch (HistoricalQueryProviderException ex)
        {
            logger?.LogError(
                ex,
                "Historical query provider failure for dataset {Dataset}.",
                request.Dataset);
            return Results.Json(
                new HistoricalQueryApiError(
                    "historical_unavailable",
                    "Historical query provider is unavailable."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception ex)
        {
            logger?.LogError(
                ex,
                "Unexpected historical query failure for dataset {Dataset}.",
                request.Dataset);
            return Results.Json(
                new HistoricalQueryApiError(
                    "historical_query_failed",
                    "Historical query execution failed."),
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public static SecurityCapability RequiredCapability(string dataset) => dataset switch
    {
        HistoricalDatasets.HistorianSamples => SecurityCapability.TrendUse,
        HistoricalDatasets.AlarmEvents => SecurityCapability.View,
        HistoricalDatasets.OperationalEvents => SecurityCapability.View,
        _ => throw new ArgumentException(
            "Historical dataset is not allowlisted for API authorization.",
            nameof(dataset))
    };
}

public sealed record HistoricalQueryApiError(string Code, string Error);

public sealed class ApiHistoricalQueryAuthorizer(
    IHttpContextAccessor contextAccessor,
    ApiAuthorizationService security,
    ScadaRuntimeFacade runtime) : IHistoricalQueryAuthorizer
{
    public async ValueTask<HistoricalAuthorizationDecision> AuthorizeAsync(
        string dataset,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!security.AuthenticationEnabled)
            return HistoricalAuthorizationDecision.Allow("Authentication is disabled for this deployment.");

        var context = contextAccessor.HttpContext;
        if (context is null)
            return HistoricalAuthorizationDecision.Unauthenticated(
                "Historical query requires an authenticated HTTP request context.");

        SecurityCapability capability;
        try
        {
            capability = HistoricalQueryApi.RequiredCapability(dataset);
        }
        catch (ArgumentException)
        {
            return HistoricalAuthorizationDecision.Forbid(
                "Historical dataset has no API authorization policy.");
        }

        var authorization = await security.CheckRuntimeAsync(
            context,
            runtime,
            capability,
            cancellationToken: cancellationToken);
        if (!authorization.IsAuthenticated)
            return HistoricalAuthorizationDecision.Unauthenticated();
        if (!authorization.Allowed)
            return HistoricalAuthorizationDecision.Forbid();
        return HistoricalAuthorizationDecision.Allow();
    }
}

/// <summary>
/// Read-only ITagRegistry projection over the currently active runtime. It exists so
/// historical providers can resolve stable TAG IDs to the active revision's public
/// TAG paths without reading mutable Engineering draft state.
/// </summary>
public sealed class RuntimeTagRegistryView(ScadaRuntimeFacade runtime) : ITagRegistry
{
    public TagDefinition Register(TagDefinition tag) =>
        throw new NotSupportedException("The runtime TAG registry view is read-only.");

    public TagDefinition Upsert(TagDefinition tag) =>
        throw new NotSupportedException("The runtime TAG registry view is read-only.");

    public bool TryGet(Guid tagId, out TagDefinition? tag) =>
        runtime.TryGetTag(tagId, out tag);

    public bool TryGetByPath(string path, out TagDefinition? tag) =>
        runtime.TryGetTagByPath(path, out tag);

    public IReadOnlyCollection<TagDefinition> Snapshot() => runtime.Tags();
}