using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scada.Api.Security;
using Scada.Core.HistoricalQueries;
using Scada.Engineering.Reports;

namespace Scada.Api.Reports;

public static class ReportExecutionApi
{
    public const string PreviewRoute = "/api/reports/preview";

    private static readonly JsonSerializerOptions WireJson = CreateWireJson();

    public static void AddReportExecutionApiCore(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddScoped<IReportExecutionService, ReportExecutionService>();
    }

    public static RouteHandlerBuilder MapReportExecutionEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.MapPost(
                PreviewRoute,
                async (
                    JsonElement requestJson,
                    IReportExecutionService service,
                    CancellationToken cancellationToken) =>
                    await ExecuteAsync(requestJson, service, cancellationToken))
            .RequireWorkspaceEngineeringRead();
    }

    public static async Task<IResult> ExecuteAsync(
        JsonElement requestJson,
        IReportExecutionService service,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        cancellationToken.ThrowIfCancellationRequested();

        ReportExecutionRequest request;
        try
        {
            request = requestJson.Deserialize<ReportExecutionRequest>(WireJson)
                ?? throw new JsonException("Report preview request is empty.");
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new ReportExecutionApiError("invalid_request", ex.Message));
        }

        try
        {
            var result = await service.ExecuteAsync(request, cancellationToken);
            return Results.Json(result, WireJson);
        }
        catch (ReportExecutionValidationException ex)
        {
            return Results.Json(
                new ReportExecutionValidationApiError("invalid_report", ex.Message, ex.Problems),
                WireJson,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ReportExecutionLimitException ex)
        {
            return Results.BadRequest(new ReportExecutionApiError("report_limit", ex.Message));
        }
        catch (HistoricalQueryUnauthorizedException)
        {
            return Results.Unauthorized();
        }
        catch (HistoricalQueryForbiddenException)
        {
            return Results.Json(
                new ReportExecutionApiError("forbidden", "Forbidden."),
                WireJson,
                statusCode: StatusCodes.Status403Forbidden);
        }
        catch (HistoricalQueryCursorException ex)
        {
            return Results.BadRequest(new ReportExecutionApiError("invalid_cursor", ex.Message));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ReportExecutionApiError("invalid_request", ex.Message));
        }
    }

    internal static JsonSerializerOptions CreateWireJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

public sealed record ReportExecutionApiError(string Code, string Error);

public sealed record ReportExecutionValidationApiError(
    string Code,
    string Error,
    IReadOnlyList<ReportEngineeringProblem> Problems);
