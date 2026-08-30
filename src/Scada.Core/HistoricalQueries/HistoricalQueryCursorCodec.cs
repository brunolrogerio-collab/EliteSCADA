using System.Security.Cryptography;
using System.Text.Json;

namespace Scada.Core.HistoricalQueries;

public sealed class HistoricalQueryCursorCodec
{
    private const int Version = 1;
    private const int MaximumCursorLength = 4096;
    private readonly byte[] _key;

    public HistoricalQueryCursorCodec(ReadOnlySpan<byte> key)
    {
        if (key.Length < 32)
            throw new ArgumentException("Historical cursor key must contain at least 32 bytes.", nameof(key));
        _key = key.ToArray();
    }

    public string Encode(
        string dataset,
        string fingerprint,
        HistoricalResolvedRange range,
        HistoricalSort sort,
        HistoricalQueryPosition position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(sort);
        ArgumentNullException.ThrowIfNull(position);

        var payload = new CursorPayload(
            Version,
            dataset,
            fingerprint,
            range.FromUtc.UtcTicks,
            range.ToUtc.UtcTicks,
            sort.Field,
            sort.Direction,
            position.Primary.Kind,
            position.Primary.Value,
            position.TimestampUtc.UtcTicks,
            position.TieBreaker);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var signature = HMACSHA256.HashData(_key, bytes);
        return $"{Base64Url(bytes)}.{Base64Url(signature)}";
    }

    public HistoricalDecodedCursor Decode(string cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > MaximumCursorLength)
            throw new HistoricalQueryCursorException("Historical cursor is missing or exceeds the supported size.");

        var parts = cursor.Split('.', StringSplitOptions.None);
        if (parts.Length != 2)
            throw new HistoricalQueryCursorException("Historical cursor format is invalid.");

        byte[] payloadBytes;
        byte[] suppliedSignature;
        try
        {
            payloadBytes = FromBase64Url(parts[0]);
            suppliedSignature = FromBase64Url(parts[1]);
        }
        catch (FormatException ex)
        {
            throw new HistoricalQueryCursorException($"Historical cursor encoding is invalid: {ex.Message}");
        }

        var expectedSignature = HMACSHA256.HashData(_key, payloadBytes);
        if (suppliedSignature.Length != expectedSignature.Length ||
            !CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
            throw new HistoricalQueryCursorException("Historical cursor signature is invalid.");

        CursorPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<CursorPayload>(payloadBytes)
                ?? throw new JsonException("Cursor payload is empty.");
        }
        catch (JsonException ex)
        {
            throw new HistoricalQueryCursorException($"Historical cursor payload is invalid: {ex.Message}");
        }

        if (payload.Version != Version ||
            string.IsNullOrWhiteSpace(payload.Dataset) ||
            string.IsNullOrWhiteSpace(payload.Fingerprint) ||
            string.IsNullOrWhiteSpace(payload.SortField) ||
            string.IsNullOrWhiteSpace(payload.TieBreaker))
            throw new HistoricalQueryCursorException("Historical cursor payload is incomplete or unsupported.");

        try
        {
            return new HistoricalDecodedCursor(
                payload.Dataset,
                payload.Fingerprint,
                new HistoricalResolvedRange(
                    new DateTimeOffset(payload.FromUtcTicks, TimeSpan.Zero),
                    new DateTimeOffset(payload.ToUtcTicks, TimeSpan.Zero)),
                new HistoricalSort(payload.SortField, payload.Direction),
                new HistoricalQueryPosition(
                    new HistoricalQueryValue(payload.PrimaryKind, payload.PrimaryValue),
                    new DateTimeOffset(payload.TimestampUtcTicks, TimeSpan.Zero),
                    payload.TieBreaker));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new HistoricalQueryCursorException($"Historical cursor time value is invalid: {ex.Message}");
        }
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized += (normalized.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            0 => string.Empty,
            _ => throw new FormatException("Invalid base64url length.")
        };
        return Convert.FromBase64String(normalized);
    }

    private sealed record CursorPayload(
        int Version,
        string Dataset,
        string Fingerprint,
        long FromUtcTicks,
        long ToUtcTicks,
        string SortField,
        HistoricalSortDirection Direction,
        HistoricalValueKind PrimaryKind,
        string? PrimaryValue,
        long TimestampUtcTicks,
        string TieBreaker);
}

public sealed record HistoricalDecodedCursor(
    string Dataset,
    string Fingerprint,
    HistoricalResolvedRange Range,
    HistoricalSort Sort,
    HistoricalQueryPosition Position);
