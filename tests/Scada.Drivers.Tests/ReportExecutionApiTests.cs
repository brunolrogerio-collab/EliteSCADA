using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Scada.Api.Reports;
using Scada.Core.HistoricalQueries;
using Scada.Engineering.Reports;

namespace Scada.Drivers.Tests;

public sealed class ReportExecutionApiTests
{
    [Fact]
    public async Task ExecuteAsync_AcceptsCanonicalStringEnumsAndExactInt64Text()
    {
        var request = Request() with
        {
            Parameters = new Dictionary<string, ReportParameterValue>
            {
                ["counter"] = ReportParameterValue.FromInt64(long.MaxValue)
            }
        };
        var expected = new ReportExecutionResult(
            null,
            request.Report.Key,
            request.Parameters!,
            Array.Empty<ReportQueryExecutionResult>());
        ReportExecutionRequest? captured = null;
        var service = new StubService((value, _) =>
        {
            captured = value;
            return Task.FromResult(expected);
        });

        var json = JsonSerializer.SerializeToElement(request, WireJson());
        var result = await ReportExecutionApi.ExecuteAsync(json, service);

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, status.StatusCode);
        Assert.NotNull(captured);
        Assert.Equal(ReportPageOrientation.Portrait, captured.Report.Page!.Orientation);
        Assert.Equal(long.MaxValue.ToString(), captured.Parameters!["counter"].Value);
        Assert.Equal(ReportParameterType.Int64, captured.Parameters["counter"].Type);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequestForUnknownReportEnumText()
    {
        using var document = JsonDocument.Parse("""
            {
              "report": {
                "key": "bad",
                "name": "Bad",
                "page": { "orientation": "sideways" },
                "queries": [],
                "sections": []
              }
            }
            """);

        var result = await ReportExecutionApi.ExecuteAsync(document.RootElement, new StubService((_, _) =>
            throw new InvalidOperationException("Service must not run for invalid JSON.")));

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, status.StatusCode);
    }

    [Theory]
    [InlineData(FailureKind.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(FailureKind.Limit, StatusCodes.Status400BadRequest)]
    [InlineData(FailureKind.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(FailureKind.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(FailureKind.Cursor, StatusCodes.Status400BadRequest)]
    [InlineData(FailureKind.Argument, StatusCodes.Status400BadRequest)]
    public async Task ExecuteAsync_MapsExpectedReportAndHistoricalFailures(
        FailureKind failure,
        int expectedStatus)
    {
        var service = new StubService((_, _) => Task.FromException<ReportExecutionResult>(failure switch
        {
            FailureKind.Validation => new ReportExecutionValidationException(
                [new ReportEngineeringProblem("REPORT_INVALID", "invalid")]),
            FailureKind.Limit => new ReportExecutionLimitException("too many rows"),
            FailureKind.Unauthorized => new HistoricalQueryUnauthorizedException("authentication required"),
            FailureKind.Forbidden => new HistoricalQueryForbiddenException("forbidden"),
            FailureKind.Cursor => new HistoricalQueryCursorException("bad cursor"),
            FailureKind.Argument => new ArgumentException("bad request"),
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        }));

        var result = await ReportExecutionApi.ExecuteAsync(
            JsonSerializer.SerializeToElement(Request(), WireJson()),
            service);
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(expectedStatus, status.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ReportExecutionApi.ExecuteAsync(
                JsonSerializer.SerializeToElement(Request(), WireJson()),
                new StubService((_, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult<ReportExecutionResult>(null!);
                }),
                cancellation.Token));
    }

    private static ReportExecutionRequest Request()
    {
        var query = new ReportQueryEngineeringDto(
            "main",
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Relative(3600),
                OrderBy: [new HistoricalSort("timestamp", HistoricalSortDirection.Descending)],
                Page: new HistoricalPageRequest(50)));
        var report = new ReportEngineeringDto(
            null,
            "report-api-test",
            "Report API Test",
            Page: new ReportPageEngineeringDto(),
            Queries: [query],
            Sections:
            [
                new ReportSectionEngineeringDto(
                    null,
                    "detail",
                    ReportSectionKind.Detail,
                    10,
                    QueryKey: "main",
                    Controls:
                    [
                        new ReportControlEngineeringDto(
                            null,
                            "value",
                            ReportControlKind.DataField,
                            0,
                            0,
                            50,
                            6,
                            QueryKey: "main",
                            Field: "value")
                    ])
            ]);
        return new ReportExecutionRequest(report);
    }

    private static JsonSerializerOptions WireJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    public enum FailureKind
    {
        Validation,
        Limit,
        Unauthorized,
        Forbidden,
        Cursor,
        Argument
    }

    private sealed class StubService(
        Func<ReportExecutionRequest, CancellationToken, Task<ReportExecutionResult>> execute)
        : IReportExecutionService
    {
        public Task<ReportExecutionResult> ExecuteAsync(
            ReportExecutionRequest request,
            CancellationToken cancellationToken = default) =>
            execute(request, cancellationToken);
    }
}
