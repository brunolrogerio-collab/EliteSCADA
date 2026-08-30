using Scada.Core.HistoricalQueries;

namespace Scada.Core.Tests;

public sealed class HistoricalQueryCoreTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 29, 20, 0, 0, TimeSpan.Zero);
    private static readonly byte[] CursorKey =
        Enumerable.Range(1, 32).Select(static value => (byte)value).ToArray();

    [Fact]
    public void Validator_RejectsUnknownVersionDatasetFieldAndMaliciousIdentifier()
    {
        Assert.Throws<ArgumentException>(() => HistoricalQueryValidator.Validate(
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Relative(3600),
                Version: 2)));

        Assert.Throws<ArgumentException>(() => HistoricalQueryValidator.Validate(
            new HistoricalQueryRequest(
                "raw.sql",
                HistoricalTimeRange.Relative(3600))));

        var request = new HistoricalQueryRequest(
            HistoricalDatasets.HistorianSamples,
            HistoricalTimeRange.Relative(3600),
            Filters:
            [
                new HistoricalFilter(
                    "timestamp); DROP TABLE elitescada.tag_history; --",
                    HistoricalFilterOperator.Eq,
                    [HistoricalQueryValue.FromDateTime(Now.AddMinutes(-1))])
            ]);
        Assert.Throws<ArgumentException>(() => HistoricalQueryValidator.Validate(request));
    }

    [Fact]
    public void Validator_EnforcesTypedOperatorsBoundedPageAndOrderTerms()
    {
        var wrongType = new HistoricalQueryRequest(
            HistoricalDatasets.AlarmEvents,
            HistoricalTimeRange.Relative(3600),
            Filters:
            [
                new HistoricalFilter(
                    "priority",
                    HistoricalFilterOperator.GreaterThan,
                    [HistoricalQueryValue.FromString("4")])
            ]);
        Assert.Throws<ArgumentException>(() => HistoricalQueryValidator.Validate(wrongType));

        var unsupportedOperator = new HistoricalQueryRequest(
            HistoricalDatasets.HistorianSamples,
            HistoricalTimeRange.Relative(3600),
            Filters:
            [
                new HistoricalFilter(
                    "tag.id",
                    HistoricalFilterOperator.Contains,
                    [HistoricalQueryValue.FromGuid(Guid.NewGuid())])
            ]);
        Assert.Throws<ArgumentException>(() => HistoricalQueryValidator.Validate(unsupportedOperator));

        var oversized = new HistoricalQueryRequest(
            HistoricalDatasets.HistorianSamples,
            HistoricalTimeRange.Relative(3600),
            Page: new HistoricalPageRequest(201));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HistoricalQueryValidator.Validate(oversized));

        var multipleSorts = new HistoricalQueryRequest(
            HistoricalDatasets.AlarmEvents,
            HistoricalTimeRange.Relative(3600),
            OrderBy:
            [
                new HistoricalSort("priority"),
                new HistoricalSort("timestamp")
            ]);
        Assert.Throws<ArgumentException>(() =>
            HistoricalQueryValidator.Validate(multipleSorts));
    }

    [Fact]
    public void Validator_ResolvesConfigurableRelativeRangeAndRejectsInvalidAbsoluteRange()
    {
        var resolved = HistoricalQueryValidator.ResolveRange(
            HistoricalTimeRange.Relative(37 * 60),
            Now);
        Assert.Equal(Now.AddMinutes(-37), resolved.FromUtc);
        Assert.Equal(Now, resolved.ToUtc);

        Assert.Throws<ArgumentOutOfRangeException>(() => HistoricalQueryValidator.Validate(
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Relative(
                    HistoricalQueryValidator.MaximumRelativeDurationSeconds + 1))));

        Assert.Throws<ArgumentException>(() => HistoricalQueryValidator.ResolveRange(
            HistoricalTimeRange.Absolute(Now.AddHours(-1), Now.AddMinutes(1)),
            Now));

        var nonUtc = Now.ToOffset(TimeSpan.FromHours(-3));
        Assert.Throws<ArgumentException>(() => HistoricalQueryValidator.Validate(
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Absolute(nonUtc.AddHours(-1), nonUtc))));
    }

    [Fact]
    public void Validator_PreservesSupportedScalarKinds()
    {
        var values = new[]
        {
            HistoricalQueryValue.FromInt16(short.MaxValue),
            HistoricalQueryValue.FromInt32(int.MaxValue),
            HistoricalQueryValue.FromInt64(long.MaxValue),
            HistoricalQueryValue.FromFloat(1.25f),
            HistoricalQueryValue.FromDouble(2.5d),
            HistoricalQueryValue.FromBoolean(true),
            HistoricalQueryValue.FromString("AUTO")
        };

        foreach (var value in values)
        {
            var validated = HistoricalQueryValidator.Validate(
                new HistoricalQueryRequest(
                    HistoricalDatasets.HistorianSamples,
                    HistoricalTimeRange.Relative(3600),
                    Filters:
                    [
                        new HistoricalFilter(
                            "value",
                            HistoricalFilterOperator.Eq,
                            [value])
                    ]));
            Assert.Equal(value.Kind, validated.Filters.Single().Values.Single().Kind);
            Assert.Equal(value.Value, validated.Filters.Single().Values.Single().Value);
        }
    }

    [Fact]
    public void Cursor_IsOpaqueSignedAndRejectsTampering()
    {
        var codec = new HistoricalQueryCursorCodec(CursorKey);
        var range = new HistoricalResolvedRange(Now.AddHours(-1), Now);
        var sort = new HistoricalSort();
        var position = new HistoricalQueryPosition(
            HistoricalQueryValue.FromDateTime(Now.AddMinutes(-1)),
            Now.AddMinutes(-1),
            "42");

        var cursor = codec.Encode(
            HistoricalDatasets.HistorianSamples,
            "ABC",
            range,
            sort,
            position);
        Assert.DoesNotContain("historian.samples", cursor, StringComparison.Ordinal);
        var decoded = codec.Decode(cursor);
        Assert.Equal(HistoricalDatasets.HistorianSamples, decoded.Dataset);
        Assert.Equal("ABC", decoded.Fingerprint);
        Assert.Equal("42", decoded.Position.TieBreaker);

        var replacement = cursor[^1] == 'A' ? 'B' : 'A';
        Assert.Throws<HistoricalQueryCursorException>(() =>
            codec.Decode(cursor[..^1] + replacement));
    }

    [Fact]
    public async Task Service_FailsClosedBeforeProviderWhenAuthorizationDenies()
    {
        var provider = new RecordingProvider(HistoricalDatasets.HistorianSamples);
        var service = new HistoricalQueryService(
            [provider],
            new FixedAuthorizer(HistoricalAuthorizationDecision.Forbid()),
            new HistoricalQueryCursorCodec(CursorKey),
            () => Now);

        await Assert.ThrowsAsync<HistoricalQueryForbiddenException>(() => service.QueryAsync(
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Relative(3600))));
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task Service_PropagatesCancellationBeforeProviderExecution()
    {
        var provider = new RecordingProvider(HistoricalDatasets.HistorianSamples);
        var service = new HistoricalQueryService(
            [provider],
            new FixedAuthorizer(HistoricalAuthorizationDecision.Allow()),
            new HistoricalQueryCursorCodec(CursorKey),
            () => Now);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.QueryAsync(
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Relative(3600)),
            cancellation.Token));
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task Service_BindsCursorToQueryAndPreservesResolvedRelativeRangeAcrossPages()
    {
        var firstPosition = new HistoricalQueryPosition(
            HistoricalQueryValue.FromDateTime(Now.AddMinutes(-10)),
            Now.AddMinutes(-10),
            "1");
        var provider = new RecordingProvider(
            HistoricalDatasets.HistorianSamples,
            new HistoricalProviderPage(
                [Row("timestamp", HistoricalQueryValue.FromDateTime(Now.AddMinutes(-10)))],
                firstPosition));
        var clock = Now;
        var service = new HistoricalQueryService(
            [provider],
            new FixedAuthorizer(HistoricalAuthorizationDecision.Allow()),
            new HistoricalQueryCursorCodec(CursorKey),
            () => clock);
        var baseRequest = new HistoricalQueryRequest(
            HistoricalDatasets.HistorianSamples,
            HistoricalTimeRange.Relative(3600),
            Page: new HistoricalPageRequest(20));

        var first = await service.QueryAsync(baseRequest);
        Assert.Equal(HistoricalQueryContract.Version, first.Version);
        Assert.NotNull(first.NextCursor);
        Assert.Equal(Now.AddHours(-1), first.FromUtc);
        Assert.Equal(Now, first.ToUtc);

        clock = Now.AddMinutes(5);
        provider.Page = new HistoricalProviderPage(
            Array.Empty<HistoricalQueryRow>(),
            null);
        var second = await service.QueryAsync(baseRequest with
        {
            Page = new HistoricalPageRequest(20, first.NextCursor)
        });
        Assert.Equal(first.FromUtc, second.FromUtc);
        Assert.Equal(first.ToUtc, second.ToUtc);
        Assert.Equal(firstPosition, provider.LastQuery!.After);

        var changedQuery = baseRequest with
        {
            Search = "Pump",
            Page = new HistoricalPageRequest(20, first.NextCursor)
        };
        await Assert.ThrowsAsync<HistoricalQueryCursorException>(() =>
            service.QueryAsync(changedQuery));
    }

    [Fact]
    public async Task Service_RejectsCrossDatasetCursorAndProviderOverflow()
    {
        var historian = new RecordingProvider(
            HistoricalDatasets.HistorianSamples,
            new HistoricalProviderPage(
                [Row("timestamp", HistoricalQueryValue.FromDateTime(Now.AddMinutes(-1)))],
                new HistoricalQueryPosition(
                    HistoricalQueryValue.FromDateTime(Now.AddMinutes(-1)),
                    Now.AddMinutes(-1),
                    "7")));
        var alarms = new RecordingProvider(HistoricalDatasets.AlarmEvents);
        var service = new HistoricalQueryService(
            [historian, alarms],
            new FixedAuthorizer(HistoricalAuthorizationDecision.Allow()),
            new HistoricalQueryCursorCodec(CursorKey),
            () => Now);

        var first = await service.QueryAsync(new HistoricalQueryRequest(
            HistoricalDatasets.HistorianSamples,
            HistoricalTimeRange.Relative(3600)));
        Assert.NotNull(first.NextCursor);
        await Assert.ThrowsAsync<HistoricalQueryCursorException>(() => service.QueryAsync(
            new HistoricalQueryRequest(
                HistoricalDatasets.AlarmEvents,
                HistoricalTimeRange.Relative(3600),
                Page: new HistoricalPageRequest(100, first.NextCursor))));

        historian.Page = new HistoricalProviderPage(
            Enumerable.Range(0, 101)
                .Select(_ => Row("timestamp", HistoricalQueryValue.FromDateTime(Now)))
                .ToArray(),
            null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.QueryAsync(
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Relative(3600),
                Page: new HistoricalPageRequest(100))));
    }

    private static HistoricalQueryRow Row(
        string field,
        HistoricalQueryValue value) =>
        new(new Dictionary<string, HistoricalQueryValue>(StringComparer.Ordinal)
        {
            [field] = value
        });

    private sealed class FixedAuthorizer(
        HistoricalAuthorizationDecision decision) : IHistoricalQueryAuthorizer
    {
        public ValueTask<HistoricalAuthorizationDecision> AuthorizeAsync(
            string dataset,
            CancellationToken cancellationToken = default)
        {
            _ = dataset;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(decision);
        }
    }

    private sealed class RecordingProvider : IHistoricalDatasetProvider
    {
        public RecordingProvider(
            string dataset,
            HistoricalProviderPage? page = null)
        {
            Dataset = dataset;
            Page = page ?? new HistoricalProviderPage(
                Array.Empty<HistoricalQueryRow>(),
                null);
        }

        public string Dataset { get; }
        public int CallCount { get; private set; }
        public HistoricalQueryExecution? LastQuery { get; private set; }
        public HistoricalProviderPage Page { get; set; }

        public Task<HistoricalProviderPage> QueryAsync(
            HistoricalQueryExecution query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastQuery = query;
            return Task.FromResult(Page);
        }
    }
}
