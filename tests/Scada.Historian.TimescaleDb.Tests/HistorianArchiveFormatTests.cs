using Scada.Historian.TimescaleDb.Archives;

namespace Scada.Historian.TimescaleDb.Tests;

public sealed class HistorianArchiveFormatTests
{
    [Fact]
    public async Task Versioned_archive_round_trips_and_verifies_each_compressed_segment()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"scadivo-historian-archive-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var segmentDirectory = Path.Combine(directory, "segments");
            Directory.CreateDirectory(segmentDirectory);
            var from = new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
            var to = from.AddDays(1);
            var samples = new[]
            {
                new HistorianArchiveSample(
                    Guid.NewGuid(), from.AddMinutes(1), 0, "simulação-α", "12.5", 4,
                    from.AddMinutes(1), 1, from.AddMinutes(2), 1, Guid.NewGuid(), 1,
                    Convert.ToBase64String([1, 2, 3])),
                new HistorianArchiveSample(
                    Guid.NewGuid(), from.AddHours(4), 2, null, "\"valor com acento\"", null,
                    null, null, null, null, null, null, null)
            };

            var segment = await HistorianArchiveFormat.WriteSegmentAsync(
                Path.Combine(segmentDirectory, "segment-000000.jsonl.gz"),
                0,
                from,
                to,
                AsAsync(samples));
            var archiveId = Guid.NewGuid();
            var manifest = new HistorianArchiveManifest(
                HistorianArchiveFormat.FormatName,
                HistorianArchiveFormat.CurrentVersion,
                archiveId,
                DateTimeOffset.UtcNow,
                from,
                to,
                [segment]);
            var packagePath = Path.Combine(directory, "samples.scadhist");

            var created = await HistorianArchiveFormat.CreatePackageAsync(packagePath, manifest, segmentDirectory);
            var verified = await HistorianArchiveFormat.VerifyPackageAsync(packagePath);

            Assert.Equal(archiveId, verified.Manifest.ArchiveId);
            Assert.Equal(samples.Length, verified.Manifest.Segments.Single().SampleCount);
            Assert.Equal(segment.Sha256, verified.Manifest.Segments.Single().Sha256);
            Assert.Equal(created.ArchiveSha256, verified.ArchiveSha256);
            Assert.True(verified.ArchiveLengthBytes > 0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Archive_verification_rejects_corrupted_segment_payload()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"scadivo-historian-archive-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var segmentPath = Path.Combine(directory, "segment-000000.jsonl.gz");
            var from = DateTimeOffset.UtcNow.AddDays(-1);
            var to = from.AddHours(1);
            var segment = await HistorianArchiveFormat.WriteSegmentAsync(
                segmentPath,
                0,
                from,
                to,
                AsAsync([new HistorianArchiveSample(
                    Guid.NewGuid(), from.AddMinutes(1), 0, null, "3", 4,
                    null, null, null, null, null, null, null)]));
            var manifest = new HistorianArchiveManifest(
                HistorianArchiveFormat.FormatName,
                HistorianArchiveFormat.CurrentVersion,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                from,
                to,
                [segment]);
            var archivePath = Path.Combine(directory, "corrupt.scadhist");
            await HistorianArchiveFormat.CreatePackageAsync(archivePath, manifest, directory);

            var bytes = await File.ReadAllBytesAsync(archivePath);
            bytes[bytes.Length / 2] ^= 0x40;
            await File.WriteAllBytesAsync(archivePath, bytes);

            await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
                HistorianArchiveFormat.VerifyPackageAsync(archivePath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Archive_verification_rejects_null_manifest_segment_as_invalid_data()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"scadivo-historian-archive-null-segment-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var archivePath = Path.Combine(directory, "null-segment.scadhist");
        try
        {
            await using (var file = new FileStream(archivePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new System.IO.Compression.ZipArchive(file, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            await using (var writer = new StreamWriter(archive.CreateEntry(HistorianArchiveFormat.ManifestEntryName).Open()))
            {
                await writer.WriteAsync("""
                    {"format":"scadivo.historian.raw-archive","formatVersion":1,"archiveId":"38c90b40-1368-4cfd-a84f-e240aeb0a26a","createdAtUtc":"2026-10-29T00:00:00Z","fromUtc":"2026-10-28T00:00:00Z","toUtc":"2026-10-29T00:00:00Z","segments":[null]}
                    """);
            }

            await Assert.ThrowsAsync<InvalidDataException>(() => HistorianArchiveFormat.VerifyPackageAsync(archivePath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Segment_paths_reject_traversal_and_other_archive_names()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scadivo-archive-safe");

        Assert.Throws<InvalidDataException>(() => HistorianArchiveFormat.ResolveSegmentPath(directory, "../payload.gz"));
        Assert.Throws<InvalidDataException>(() => HistorianArchiveFormat.ResolveSegmentPath(directory, "manifest.json"));
    }

    private static async IAsyncEnumerable<HistorianArchiveSample> AsAsync(
        IEnumerable<HistorianArchiveSample> samples)
    {
        foreach (var sample in samples)
        {
            yield return sample;
            await Task.Yield();
        }
    }
}
