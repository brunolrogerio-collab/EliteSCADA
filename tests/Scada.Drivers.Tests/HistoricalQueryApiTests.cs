using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Scada.Api.Historian;
using Scada.Core.HistoricalQueries;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class HistoricalQueryApiTests
{
    private static readonly HistoricalQueryRequest Request = new(
        HistoricalDatasets.HistorianSamples,
        HistoricalTimeRange.Relative(3600));

    [Fact]
    public void PublicJsonContract_UsesVersionedNamesAndStringEnums()
    {
        var request = Request with
        {
            OrderBy =
            [
                new HistoricalSort(
                    "timestamp",
                    HistoricalSortDirection.Descending)
            ],
            Page = new HistoricalPageRequest(25, "opaque-cursor")
        };

        var json = JsonSerializer.Serialize(request);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal(
            HistoricalDatasets.HistorianSamples,
            root.GetProperty("datasetKey").GetString());
        Assert.False(root.TryGetProperty("Dataset", out _));
        Assert.False(root.TryGetProperty("Range", out _));

        var range = root.GetProperty("timeRange");
        Assert.Equal("relative", range.GetProperty("kind").GetString());
        Assert.Equal(3600, range.GetProperty("durationSeconds").GetInt32());
        Assert.Equal("now", range.GetProperty("anchor").GetString());

        var order = root.GetProperty("orderBy")[0];
        Assert.Equal("descending", order.GetProperty("direction").GetString());
        var page = root.GetProperty("page");
        Assert.Equal(25, page.GetProperty("limit").GetInt32());
        Assert.Equal("opaque-cursor", page.GetProperty("cursor").GetString());
    }

    [Fact]
    public void RequiredCapability_UsesExistingReadOnlyProductCapabilities()
    {
        Assert.Equal(
            SecurityCapability.TrendUse,
            HistoricalQueryApi.RequiredCapability(HistoricalDatasets.HistorianSamples));
        Assert.Equal(
            SecurityCapability.View,
            HistoricalQueryApi.RequiredCapability(HistoricalDatasets.AlarmEvents));
        Assert.Throws<ArgumentException>(() =>
            HistoricalQueryApi.RequiredCapability("arbitrary.sql"));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOkForSuccessfulQuery()
    {
        var expected = new HistoricalQueryResponse(
            HistoricalQueryContract.Version,
            HistoricalDatasets.HistorianSamples,
            Array.Empty<HistoricalColumn>(),
            Array.Empty<HistoricalQueryRow>(),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddHours(1),
            null,
            100);

        var result = await HistoricalQueryApi.ExecuteAsync(
            Request,
            new StubService((_, _) => Task.FromResult(expected)));

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, status.StatusCode);
        var value = Assert.IsAssignableFrom<IValueHttpResult>(result);
        Assert.Same(expected, value.Value);
    }

    [Theory]
    [InlineData(FailureKind.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(FailureKind.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(FailureKind.Cursor, StatusCodes.Status400BadRequest)]
    [InlineData(FailureKind.Validation, StatusCodes.Status400BadRequest)]
    public async Task ExecuteAsync_MapsExpectedPublicFailures(
        FailureKind failure,
        int expectedStatus)
    {
        var service = new StubService((_, _) => Task.FromException<HistoricalQueryResponse>(
            failure switch
            {
                FailureKind.Unauthorized => new HistoricalQueryUnauthorizedException("authentication required"),
                FailureKind.Forbidden => new HistoricalQueryForbiddenException("denied"),
                FailureKind.Cursor => new HistoricalQueryCursorException("bad cursor"),
                FailureKind.Validation => new ArgumentException("bad query"),
                _ => throw new ArgumentOutOfRangeException(nameof(failure))
            }));

        var result = await HistoricalQueryApi.ExecuteAsync(Request, service);
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(expectedStatus, status.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCancellation()
    {
        var service = new StubService((_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<HistoricalQueryResponse>(null!);
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HistoricalQueryApi.ExecuteAsync(Request, service, cancellation.Token));
    }

    public enum FailureKind
    {
        Unauthorized,
        Forbidden,
        Cursor,
        Validation
    }

    private sealed class StubService(
        Func<HistoricalQueryRequest, CancellationToken, Task<HistoricalQueryResponse>> execute)
        : IHistoricalQueryService
    {
        public Task<HistoricalQueryResponse> QueryAsync(
            HistoricalQueryRequest request,
            CancellationToken cancellationToken = default) =>
            execute(request, cancellationToken);
    }
}
