using Scada.Core.HistoricalQueries;

namespace Scada.Drivers.Tests;

public sealed class HistoricalQueryFailureClassificationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 1, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task QueryAsync_ClassifiesSemanticRangeFailureAsValidation()
    {
        var provider = new StubProvider();
        var service = CreateService(provider);
        var request = new HistoricalQueryRequest(
            HistoricalDatasets.HistorianSamples,
            HistoricalTimeRange.Absolute(
                Now.AddMinutes(-1),
                Now.AddMinutes(-2)));

        var exception = await Assert.ThrowsAsync<HistoricalQueryValidationException>(() =>
            service.QueryAsync(request));

        Assert.Contains("earlier than", exception.Message);
        Assert.Equal(0, provider.QueryCount);
    }

    [Fact]
    public async Task QueryAsync_ClassifiesProviderArgumentFailureAsProviderFailure()
    {
        const string protectedDetail = "provider SQL argument contains protected detail";
        var provider = new StubProvider(new ArgumentException(protectedDetail));
        var service = CreateService(provider);
        var request = new HistoricalQueryRequest(
            HistoricalDatasets.HistorianSamples,
            HistoricalTimeRange.Relative(60));

        var exception = await Assert.ThrowsAsync<HistoricalQueryProviderException>(() =>
            service.QueryAsync(request));

        Assert.Equal(1, provider.QueryCount);
        Assert.IsType<ArgumentException>(exception.InnerException);
        Assert.DoesNotContain(protectedDetail, exception.Message);
    }

    private static HistoricalQueryService CreateService(StubProvider provider) =>
        new(
            new[] { provider },
            new AllowAuthorizer(),
            new HistoricalQueryCursorCodec(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()),
            () => Now);

    private sealed class AllowAuthorizer : IHistoricalQueryAuthorizer
    {
        public ValueTask<HistoricalAuthorizationDecision> AuthorizeAsync(
            string dataset,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(HistoricalAuthorizationDecision.Allow());
        }
    }

    private sealed class StubProvider(Exception? failure = null) : IHistoricalDatasetProvider
    {
        public string Dataset => HistoricalDatasets.HistorianSamples;
        public int QueryCount { get; private set; }

        public Task<HistoricalProviderPage> QueryAsync(
            HistoricalQueryExecution query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            QueryCount++;
            return failure is null
                ? Task.FromResult(new HistoricalProviderPage(
                    Array.Empty<HistoricalQueryRow>(),
                    null))
                : Task.FromException<HistoricalProviderPage>(failure);
        }
    }
}
