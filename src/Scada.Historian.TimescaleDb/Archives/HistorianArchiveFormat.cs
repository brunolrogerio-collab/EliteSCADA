using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Scada.Historian.TimescaleDb.Archives;

public sealed record HistorianArchiveSample(
    Guid TagId,
    DateTimeOffset Timestamp,
    int Quality,
    string? Source,
    string ValueJson,
    short? DataType,
    DateTimeOffset? SourceTimestamp,
    short? SourceTimestampEvidence,
    DateTimeOffset? ServerTimestamp,
    short? ServerTimestampEvidence,
    Guid? CaptureProfileId,
    short? CapturePolicyState,
    string? CapturePolicyFingerprintBase64);

public sealed record HistorianArchiveSegment(
    int Sequence,
    string FileName,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    long SampleCount,
    string Sha256);

public sealed record HistorianArchiveManifest(
    string Format,
    int FormatVersion,
    Guid ArchiveId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    IReadOnlyList<HistorianArchiveSegment> Segments);

public sealed record HistorianArchiveVerification(
    HistorianArchiveManifest Manifest,
    string ArchiveSha256,
    long ArchiveLengthBytes);

/// <summary>
/// Versioned, portable archive format for raw Historian samples. Each independently
/// compressed time segment is checkpointable; the manifest binds each segment to its
/// range, row count, and SHA-256 of the uncompressed JSON-lines payload.
/// </summary>
public static class HistorianArchiveFormat
{
    public const string FormatName = "scadivo.historian.raw-archive";
    public const int CurrentVersion = 1;
    public const string ManifestEntryName = "manifest.json";
    private const int MaximumManifestBytes = 4 * 1024 * 1024;
    private const int MaximumArchiveSegments = 100_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<HistorianArchiveSegment> WriteSegmentAsync(
        string path,
        int sequence,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        IAsyncEnumerable<HistorianArchiveSample> samples,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(samples);
        ValidateRange(fromUtc, toUtc);
        if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long count = 0;
        await using (var output = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            await foreach (var sample in samples.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                ValidateSample(sample, fromUtc, toUtc);
                var json = JsonSerializer.SerializeToUtf8Bytes(sample, JsonOptions);
                hash.AppendData(json);
                hash.AppendData([0x0A]);
                await gzip.WriteAsync(json, cancellationToken).ConfigureAwait(false);
                await gzip.WriteAsync(new byte[] { 0x0A }, cancellationToken).ConfigureAwait(false);
                count++;
            }

            await gzip.DisposeAsync().ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            output.Flush(flushToDisk: true);
        }

        var fileName = $"segment-{sequence:D6}.jsonl.gz";
        return new HistorianArchiveSegment(
            sequence,
            fileName,
            fromUtc.ToUniversalTime(),
            toUtc.ToUniversalTime(),
            count,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    public static async Task<HistorianArchiveVerification> CreatePackageAsync(
        string packagePath,
        HistorianArchiveManifest manifest,
        string segmentDirectory,
        CancellationToken cancellationToken = default)
    {
        ValidateManifest(manifest);
        var fullPath = Path.GetFullPath(packagePath);
        var tempPath = fullPath + ".partial";
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        try
        {
            await using (var file = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var segment in manifest.Segments.OrderBy(x => x.Sequence))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var sourcePath = ResolveSegmentPath(segmentDirectory, segment.FileName);
                    await using var source = new FileStream(
                        sourcePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 64 * 1024,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                    var entry = archive.CreateEntry($"segments/{segment.FileName}", CompressionLevel.NoCompression);
                    await using var destination = entry.Open();
                    await source.CopyToAsync(destination, 64 * 1024, cancellationToken).ConfigureAwait(false);
                }

                var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                await using var manifestStream = manifestEntry.Open();
                await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            await using (var file = new FileStream(
                tempPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                file.Flush(flushToDisk: true);
            }

            var verification = await VerifyPackageAsync(tempPath, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, fullPath, overwrite: true);
            return verification with { ArchiveLengthBytes = new FileInfo(fullPath).Length };
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    public static async Task<HistorianArchiveVerification> VerifyPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        var fullPath = Path.GetFullPath(packagePath);
        await using var file = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var archiveHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var archiveSha256 = await ComputeHashAsync(file, archiveHash, cancellationToken).ConfigureAwait(false);
        file.Position = 0;

        using var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntry = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException("Historian archive manifest is missing.");
        if (manifestEntry.Length is <= 0 or > MaximumManifestBytes)
            throw new InvalidDataException("Historian archive manifest size is outside the supported range.");

        HistorianArchiveManifest? manifest;
        await using (var stream = manifestEntry.Open())
            manifest = await JsonSerializer.DeserializeAsync<HistorianArchiveManifest>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        if (manifest is null) throw new InvalidDataException("Historian archive manifest is invalid.");
        ValidateManifest(manifest);

        var expectedNames = manifest.Segments.Select(x => $"segments/{x.FileName}")
            .Append(ManifestEntryName)
            .ToHashSet(StringComparer.Ordinal);
        if (archive.Entries.Count != expectedNames.Count || archive.Entries.Any(x => !expectedNames.Contains(x.FullName)))
            throw new InvalidDataException("Historian archive contains missing or unexpected entries.");

        foreach (var segment in manifest.Segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = archive.GetEntry($"segments/{segment.FileName}")
                ?? throw new InvalidDataException("Historian archive segment is missing.");
            await using var compressed = entry.Open();
            await VerifySegmentContentAsync(compressed, segment, cancellationToken).ConfigureAwait(false);
        }

        return new HistorianArchiveVerification(
            manifest,
            archiveSha256,
            new FileInfo(fullPath).Length);
    }

    public static async Task VerifySegmentContentAsync(
        Stream compressedSegment,
        HistorianArchiveSegment expected,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compressedSegment);
        ArgumentNullException.ThrowIfNull(expected);
        var actual = await InspectSegmentContentAsync(
            compressedSegment,
            expected.Sequence,
            expected.FromUtc,
            expected.ToUtc,
            cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actual.FileName, expected.FileName, StringComparison.Ordinal) ||
            actual.SampleCount != expected.SampleCount ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(actual.Sha256), Encoding.ASCII.GetBytes(expected.Sha256)))
            throw new InvalidDataException("Historian archive segment failed its row-count or SHA-256 check.");
    }

    public static async Task<HistorianArchiveSegment> InspectSegmentContentAsync(
        Stream compressedSegment,
        int sequence,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compressedSegment);
        ValidateRange(fromUtc, toUtc);
        if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));

        using var gzip = new GZipStream(compressedSegment, CompressionMode.Decompress, leaveOpen: true);
        using var reader = new StreamReader(gzip, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, 64 * 1024, leaveOpen: true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long count = 0;
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HistorianArchiveSample? sample;
            try { sample = JsonSerializer.Deserialize<HistorianArchiveSample>(line, JsonOptions); }
            catch (JsonException ex) { throw new InvalidDataException("Historian archive segment contains invalid sample JSON.", ex); }
            if (sample is null) throw new InvalidDataException("Historian archive segment contains an empty sample.");
            ValidateSample(sample, fromUtc, toUtc);
            var bytes = Encoding.UTF8.GetBytes(line);
            hash.AppendData(bytes);
            hash.AppendData([0x0A]);
            count++;
        }

        return new HistorianArchiveSegment(
            sequence,
            $"segment-{sequence:D6}.jsonl.gz",
            fromUtc.ToUniversalTime(),
            toUtc.ToUniversalTime(),
            count,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    public static string ResolveSegmentPath(string segmentDirectory, string fileName)
    {
        if (!IsSafeSegmentFileName(fileName))
            throw new InvalidDataException("Historian archive segment filename is unsafe.");
        var directory = Path.GetFullPath(segmentDirectory);
        var candidate = Path.GetFullPath(Path.Combine(directory, fileName));
        var prefix = directory.EndsWith(Path.DirectorySeparatorChar)
            ? directory
            : directory + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Historian archive segment path escapes its storage directory.");
        return candidate;
    }

    private static void ValidateManifest(HistorianArchiveManifest manifest)
    {
        if (!string.Equals(manifest.Format, FormatName, StringComparison.Ordinal) || manifest.FormatVersion != CurrentVersion)
            throw new InvalidDataException("Historian archive format or version is not supported.");
        ValidateRange(manifest.FromUtc, manifest.ToUtc);
        if (manifest.ArchiveId == Guid.Empty || manifest.CreatedAtUtc.Offset != TimeSpan.Zero ||
            manifest.Segments is null || manifest.Segments.Count is < 1 or > MaximumArchiveSegments)
            throw new InvalidDataException("Historian archive manifest metadata is invalid.");
        if (manifest.Segments.Any(x => x is null))
            throw new InvalidDataException("Historian archive manifest contains an empty segment entry.");

        var ordered = manifest.Segments.OrderBy(x => x.Sequence).ToArray();
        if (ordered.Select(x => x.Sequence).Where((sequence, index) => sequence != index).Any() ||
            ordered.Select(x => x.FileName).Distinct(StringComparer.Ordinal).Count() != ordered.Length ||
            ordered[0].FromUtc != manifest.FromUtc || ordered[^1].ToUtc != manifest.ToUtc)
            throw new InvalidDataException("Historian archive manifest has missing, duplicate, or out-of-range segments.");
        foreach (var segment in ordered)
        {
            if (segment.Sequence < 0 || !IsSafeSegmentFileName(segment.FileName) ||
                segment.SampleCount < 0 || segment.Sha256 is null || segment.Sha256.Length != 64 ||
                !segment.Sha256.All(Uri.IsHexDigit))
                throw new InvalidDataException("Historian archive segment metadata is invalid.");
            ValidateRange(segment.FromUtc, segment.ToUtc);
            if (segment.FromUtc < manifest.FromUtc || segment.ToUtc > manifest.ToUtc)
                throw new InvalidDataException("Historian archive segment is outside the manifest range.");
        }
        for (var index = 1; index < ordered.Length; index++)
        {
            if (ordered[index - 1].ToUtc != ordered[index].FromUtc)
                throw new InvalidDataException("Historian archive segments must cover one contiguous time range.");
        }
    }

    private static bool IsSafeSegmentFileName(string? name) =>
        name is { Length: 23 } && name.StartsWith("segment-", StringComparison.Ordinal) &&
        name.EndsWith(".jsonl.gz", StringComparison.Ordinal) &&
        name.AsSpan(8, 6).ToString().All(char.IsAsciiDigit);

    private static void ValidateRange(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        if (fromUtc.Offset != TimeSpan.Zero || toUtc.Offset != TimeSpan.Zero || toUtc <= fromUtc)
            throw new InvalidDataException("Historian archive time range must be a non-empty half-open UTC interval.");
    }

    private static void ValidateSample(HistorianArchiveSample sample, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        if (sample.TagId == Guid.Empty || sample.Timestamp.Offset != TimeSpan.Zero ||
            sample.Timestamp < fromUtc || sample.Timestamp >= toUtc ||
            string.IsNullOrWhiteSpace(sample.ValueJson))
            throw new InvalidDataException("Historian archive sample metadata or timestamp is invalid.");
        try { using var _ = JsonDocument.Parse(sample.ValueJson); }
        catch (JsonException ex) { throw new InvalidDataException("Historian archive sample value is not valid JSON.", ex); }
        if (sample.CapturePolicyFingerprintBase64 is { } fingerprint)
        {
            try { _ = Convert.FromBase64String(fingerprint); }
            catch (FormatException ex) { throw new InvalidDataException("Historian archive sample fingerprint is invalid.", ex); }
        }
    }

    private static async Task<string> ComputeHashAsync(
        Stream stream,
        IncrementalHash hash,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            hash.AppendData(buffer, 0, read);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
