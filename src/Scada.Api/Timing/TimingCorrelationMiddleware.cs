using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Scada.Api.Timing;

public sealed record TimingRequestMetadata(
    string RequestId,
    string? Operation,
    string? Category,
    int Attempt);

public sealed partial class TimingCorrelationMiddleware(
    RequestDelegate next,
    ILogger<TimingCorrelationMiddleware> logger)
{
    public const string CorrelationResponseHeader = "X-EliteSCADA-Correlation-Id";
    public const string RequestIdHeader = "X-EliteSCADA-Request-Id";
    public const string OperationHeader = "X-EliteSCADA-Operation";
    public const string CategoryHeader = "X-EliteSCADA-Request-Category";
    public const string AttemptHeader = "X-EliteSCADA-Attempt";

    private static readonly HashSet<string> Categories = new(
        TimingPolicyV1Taxonomy.AdaptiveCategories,
        StringComparer.Ordinal);

    public async Task InvokeAsync(HttpContext context)
    {
        var metadata = ReadMetadata(context);
        context.Response.Headers[CorrelationResponseHeader] = context.TraceIdentifier;

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = context.TraceIdentifier,
            ["RequestId"] = metadata.RequestId,
            ["RemoteOperation"] = metadata.Operation,
            ["RemoteTimingCategory"] = metadata.Category,
            ["RemoteAttempt"] = metadata.Attempt
        });

        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            logger.LogDebug(
                "Remote request completed with status {StatusCode} in {DurationMilliseconds:F3} ms.",
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    internal static TimingRequestMetadata ReadMetadata(HttpContext context)
    {
        var requestId = SafeHeader(context, RequestIdHeader, 64) ?? context.TraceIdentifier;
        var operation = SafeHeader(context, OperationHeader, 96);
        var category = SafeHeader(context, CategoryHeader, 40);
        if (category is not null && !Categories.Contains(category)) category = null;

        var attemptValue = SafeHeader(context, AttemptHeader, 2);
        var attempt = int.TryParse(attemptValue, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) &&
                      parsed is >= 1 and <= 4
            ? parsed
            : 1;
        return new TimingRequestMetadata(requestId, operation, category, attempt);
    }

    private static string? SafeHeader(HttpContext context, string name, int maximumLength)
    {
        if (!context.Request.Headers.TryGetValue(name, out var values) || values.Count != 1) return null;
        var value = values[0]?.Trim();
        return value is not null && value.Length is > 0 && value.Length <= maximumLength && SafeMetadataValue().IsMatch(value)
            ? value
            : null;
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:/-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeMetadataValue();
}
