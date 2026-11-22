using Microsoft.AspNetCore.Http;
using Scada.Api.Historian;
using Scada.Core.HistoricalQueries;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataQueries;

namespace Scada.Drivers.Tests;

public sealed class DataQueryApiTests
{
    [Fact]
    public async Task ExecuteDataQueryAsync_MapsSuccessNotFoundAndCancellation()
    {
        var response = new DataQueryExecutionResponse(
            1,
            Guid.NewGuid(),
            "q",
            HistoricalDatasets.HistorianSamples,
            HistorianRetrievalMode.Raw,
            Array.Empty<HistoricalColumn>(),
            Array.Empty<DataQueryExecutionRow>(),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            null,
            100);

        var ok = await HistoricalQueryApi.ExecuteDataQueryAsync(
            "q",
            new DataQueryExecutionRequest(),
            new StubService((_, _, _) => Task.FromResult(response)));
        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(ok).StatusCode);

        var missing = await HistoricalQueryApi.ExecuteDataQueryAsync(
            "missing",
            new DataQueryExecutionRequest(),
            new StubService((_, _, _) => Task.FromException<DataQueryExecutionResponse>(
                new DataQueryDefinitionNotFoundException("internal definition detail"))));
        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsAssignableFrom<IStatusCodeHttpResult>(missing).StatusCode);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HistoricalQueryApi.ExecuteDataQueryAsync(
                "q",
                new DataQueryExecutionRequest(),
                new StubService((_, _, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult(response);
                }),
                cancellation.Token));
    }

    [Fact]
    public async Task ExecuteDataQueryAsync_SanitizesProviderFailure()
    {
        const string protectedDetail = "database connection detail";
        var result = await HistoricalQueryApi.ExecuteDataQueryAsync(
            "q",
            new DataQueryExecutionRequest(),
            new StubService((_, _, _) => Task.FromException<DataQueryExecutionResponse>(
                new HistoricalQueryProviderException(
                    "provider",
                    new InvalidOperationException(protectedDetail)))));

        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        var error = Assert.IsType<HistoricalQueryApiError>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal("historical_unavailable", error.Code);
        Assert.DoesNotContain(protectedDetail, error.Error);
    }

    private sealed class StubService(
        Func<string, DataQueryExecutionRequest, CancellationToken, Task<DataQueryExecutionResponse>> execute)
        : IDataQueryExecutionService
    {
        public Task<DataQueryExecutionResponse> ExecuteAsync(
            string queryKey,
            DataQueryExecutionRequest? request = null,
            CancellationToken cancellationToken = default) =>
            execute(queryKey, request ?? new DataQueryExecutionRequest(), cancellationToken);

        public Task<DataQueryExecutionResponse> ExecuteAsync(
            Guid queryId,
            DataQueryExecutionRequest? request = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
