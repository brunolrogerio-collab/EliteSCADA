namespace Scada.Core.HistoricalQueries;

public interface IHistoricalQueryService
{
    Task<HistoricalQueryResponse> QueryAsync(
        HistoricalQueryRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class HistoricalQueryService : IHistoricalQueryService
{
    private readonly IReadOnlyDictionary<string, IHistoricalDatasetProvider> _providers;
    private readonly IHistoricalQueryAuthorizer _authorizer;
    private readonly HistoricalQueryCursorCodec _cursorCodec;
    private readonly Func<DateTimeOffset> _utcNow;

    public HistoricalQueryService(
        IEnumerable<IHistoricalDatasetProvider> providers,
        IHistoricalQueryAuthorizer authorizer,
        HistoricalQueryCursorCodec cursorCodec,
        Func<DateTimeOffset>? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        _cursorCodec = cursorCodec ?? throw new ArgumentNullException(nameof(cursorCodec));
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

        var materialized = providers.ToArray();
        if (materialized.Any(static provider => provider is null))
            throw new ArgumentException(
                "Historical dataset provider collection cannot contain null entries.",
                nameof(providers));
        if (materialized.Any(static provider => string.IsNullOrWhiteSpace(provider.Dataset)))
            throw new ArgumentException(
                "Historical dataset providers must declare a dataset ID.",
                nameof(providers));
        if (materialized
            .GroupBy(static provider => provider.Dataset, StringComparer.Ordinal)
            .Any(static group => group.Count() > 1))
            throw new ArgumentException(
                "Historical dataset provider IDs must be unique.",
                nameof(providers));

        _providers = materialized.ToDictionary(static provider => provider.Dataset, StringComparer.Ordinal);
    }

    public async Task<HistoricalQueryResponse> QueryAsync(
        HistoricalQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validated = ValidateRequest(request);
        if (!_providers.TryGetValue(validated.Dataset.Id, out var provider))
            throw new HistoricalQueryProviderException(
                "Historical dataset provider is unavailable.");

        var decision = await _authorizer.AuthorizeAsync(validated.Dataset.Id, cancellationToken);
        switch (decision.Outcome)
        {
            case HistoricalAuthorizationOutcome.Unauthenticated:
                throw new HistoricalQueryUnauthorizedException(decision.Reason);
            case HistoricalAuthorizationOutcome.Forbidden:
                throw new HistoricalQueryForbiddenException(decision.Reason);
            case HistoricalAuthorizationOutcome.Allowed:
                break;
            default:
                throw new HistoricalQueryForbiddenException(
                    "Historical authorization returned an unknown decision and failed closed.");
        }

        var now = _utcNow();
        HistoricalResolvedRange range;
        HistoricalQueryPosition? after = null;
        if (string.IsNullOrWhiteSpace(validated.Cursor))
        {
            range = ResolveRequestRange(validated.RequestedRange, now);
        }
        else
        {
            var cursor = _cursorCodec.Decode(validated.Cursor);
            if (!string.Equals(cursor.Dataset, validated.Dataset.Id, StringComparison.Ordinal) ||
                !string.Equals(cursor.Fingerprint, validated.Fingerprint, StringComparison.Ordinal) ||
                cursor.Sort != validated.Sort)
                throw new HistoricalQueryCursorException(
                    "Historical cursor does not belong to this dataset/query/sort.");

            HistoricalQueryValidator.ValidateResolvedRange(cursor.Range, now);
            range = cursor.Range;
            after = cursor.Position;
        }

        var execution = new HistoricalQueryExecution(
            validated.Dataset,
            range,
            validated.Filters,
            validated.Search,
            validated.Sort,
            validated.PageSize,
            after);

        HistoricalProviderPage page;
        try
        {
            page = await provider.QueryAsync(execution, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HistoricalQueryProviderException(
                "Historical dataset provider execution failed.",
                ex);
        }

        if (page.Rows.Count > validated.PageSize)
            throw new HistoricalQueryProviderException(
                "Historical dataset provider returned an invalid page.");

        var nextCursor = page.NextPosition is null
            ? null
            : _cursorCodec.Encode(
                validated.Dataset.Id,
                validated.Fingerprint,
                range,
                validated.Sort,
                page.NextPosition);

        return new HistoricalQueryResponse(
            HistoricalQueryContract.Version,
            validated.Dataset.Id,
            validated.Dataset.Columns,
            page.Rows,
            range.FromUtc,
            range.ToUtc,
            nextCursor,
            validated.PageSize);
    }

    private static HistoricalValidatedRequest ValidateRequest(HistoricalQueryRequest request)
    {
        try
        {
            return HistoricalQueryValidator.Validate(request);
        }
        catch (ArgumentException ex)
        {
            throw ToValidationException(ex);
        }
    }

    private static HistoricalResolvedRange ResolveRequestRange(
        HistoricalTimeRange range,
        DateTimeOffset nowUtc)
    {
        try
        {
            return HistoricalQueryValidator.ResolveRange(range, nowUtc);
        }
        catch (ArgumentException ex)
        {
            throw ToValidationException(ex);
        }
    }

    private static HistoricalQueryValidationException ToValidationException(ArgumentException exception)
    {
        var message = exception.Message;
        var firstLineEnd = message.IndexOfAny(['\r', '\n']);
        if (firstLineEnd >= 0) message = message[..firstLineEnd];
        return new HistoricalQueryValidationException(message, exception);
    }
}
