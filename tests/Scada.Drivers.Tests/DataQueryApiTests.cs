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

    [Fact]
    public async Task ExecuteTransientDataQueryAsync_UsesCanonicalDataQueryAuthority_AndSanitizesFailures()
    {
        var definition = new DataQueryEngineeringDto(
            Guid.NewGuid(),
            "runtime-playback",
            "Runtime Playback",
            DataQueryExecutionService.HistoricalProviderKey,
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Absolute(
                    DateTimeOffset.UnixEpoch,
                    DateTimeOffset.UnixEpoch.AddHours(1))),
            Version: 1);
        var response = new DataQueryExecutionResponse(
            1,
            definition.Id!.Value,
            definition.Key,
            HistoricalDatasets.HistorianSamples,
            HistorianRetrievalMode.AtOrBefore,
            Array.Empty<HistoricalColumn>(),
            Array.Empty<DataQueryExecutionRow>(),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddHours(1),
            null,
            1);
        var request = new TransientDataQueryExecutionRequest(definition);

        var ok = await HistoricalQueryApi.ExecuteTransientDataQueryAsync(
            request,
            new StubTransientService((actual, _, _) =>
            {
                Assert.Same(definition, actual);
                return Task.FromResult(response);
            }));
        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(ok).StatusCode);

        const string protectedDetail = "provider connection detail";
        var unavailable = await HistoricalQueryApi.ExecuteTransientDataQueryAsync(
            request,
            new StubTransientService((_, _, _) => Task.FromException<DataQueryExecutionResponse>(
                new HistoricalQueryProviderException(
                    "provider",
                    new InvalidOperationException(protectedDetail)))));
        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(unavailable).StatusCode);
        var error = Assert.IsType<HistoricalQueryApiError>(
            Assert.IsAssignableFrom<IValueHttpResult>(unavailable).Value);
        Assert.DoesNotContain(protectedDetail, error.Error);
    }

    private sealed class StubTransientService(
        Func<DataQueryEngineeringDto, DataQueryExecutionRequest, CancellationToken, Task<DataQueryExecutionResponse>> execute)
        : ITransientDataQueryExecutionService
    {
        public Task<DataQueryExecutionResponse> ExecuteTransientAsync(
            DataQueryEngineeringDto definition,
            DataQueryExecutionRequest? request = null,
            CancellationToken cancellationToken = default) =>
            execute(definition, request ?? new DataQueryExecutionRequest(), cancellationToken);
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
