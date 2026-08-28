using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.VisualAssets;

namespace Scada.Engineering.ProjectPackages;

public sealed record ProjectPackageFileEntry(
    string Path,
    string MediaType,
    long Length,
    string Sha256);

public sealed record ProjectPackageManifest(
    string Format,
    int FormatVersion,
    Guid PackageId,
    DateTimeOffset CreatedAtUtc,
    string Product,
    string ProjectKey,
    string ProjectName,
    string EngineeringSchema,
    int EngineeringSchemaVersion,
    IReadOnlyCollection<ProjectPackageFileEntry> Files);

public sealed record ProjectPackageInspection(
    ProjectPackageManifest Manifest,
    EngineeringPackage Engineering);

public interface IProjectPackageService
{
    byte[] Export(string projectKey, string projectName);
    ProjectPackageInspection Inspect(ReadOnlyMemory<byte> packageBytes);
    ImportPreview Preview(ReadOnlyMemory<byte> packageBytes, ImportMode mode);
    ImportResult Apply(ReadOnlyMemory<byte> packageBytes, ImportMode mode);
}

public sealed class ProjectPackageService : IProjectPackageService
{
    public const string CurrentFormat = "elitescada.project-package";
    public const int CurrentFormatVersion = 2;
    public const string ManifestPath = "manifest.json";
    public const string EngineeringPath = "engineering.json";
    public const string AssetDirectory = "assets/";
    public const string PackageExtension = ".escadapkg";

    private const int MaximumManifestBytes = 1024 * 1024;
    private const int MaximumEngineeringBytes = 50 * 1024 * 1024;
    private const int MaximumPackageBytes = 256 * 1024 * 1024;
    private const int MaximumPayloadFiles = 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly IEngineeringExchangeService _engineering;
    private readonly IVisualAssetEngineeringRegistry? _visualAssets;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public ProjectPackageService(
        IEngineeringExchangeService engineering,
        IVisualAssetEngineeringRegistry? visualAssets = null)
    {
        _engineering = engineering;
        _visualAssets = visualAssets;
    }

    public byte[] Export(string projectKey, string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ArgumentException("Project key is required.", nameof(projectKey));
        if (string.IsNullOrWhiteSpace(projectName))
            throw new ArgumentException("Project name is required.", nameof(projectName));

        var engineeringJson = _engineering.ExportJson(indented: true);
        var engineeringBytes = Encoding.UTF8.GetBytes(engineeringJson);
        var engineeringPackage = _engineering.ParseJson(engineeringJson);
        var fileEntries = new List<ProjectPackageFileEntry>
        {
            new(
                EngineeringPath,
                "application/json",
                engineeringBytes.LongLength,
                Sha256(engineeringBytes))
        };
        var assetPayloads = ResolveExportPayloads(engineeringPackage);

        foreach (var payload in assetPayloads.Values.OrderBy(x => x.Sha256, StringComparer.Ordinal))
        {
            fileEntries.Add(new ProjectPackageFileEntry(
                AssetPath(payload.Sha256),
                payload.MediaType,
                payload.ByteLength,
                payload.Sha256));
        }

        var manifest = new ProjectPackageManifest(
            CurrentFormat,
            CurrentFormatVersion,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "EliteSCADA",
            projectKey.Trim(),
            projectName.Trim(),
            engineeringPackage.Schema,
            engineeringPackage.SchemaVersion,
            fileEntries);

        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, _json);
        if (manifestBytes.Length > MaximumManifestBytes)
            throw new InvalidDataException("Project package manifest exceeds its safety limit.");

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, ManifestPath, manifestBytes);
            WriteEntry(archive, EngineeringPath, engineeringBytes);
            foreach (var payload in assetPayloads.Values.OrderBy(x => x.Sha256, StringComparer.Ordinal))
                WriteEntry(archive, AssetPath(payload.Sha256), payload.Content);
        }

        if (output.Length > MaximumPackageBytes)
            throw new InvalidDataException($"Project package exceeds the {MaximumPackageBytes} byte safety limit.");

        return output.ToArray();
    }

    public ProjectPackageInspection Inspect(ReadOnlyMemory<byte> packageBytes)
    {
        var parsed = ParsePackage(packageBytes);
        return new ProjectPackageInspection(parsed.Manifest, parsed.Engineering);
    }

    public ImportPreview Preview(ReadOnlyMemory<byte> packageBytes, ImportMode mode)
    {
        var parsed = ParsePackage(packageBytes);
        return _engineering.Preview(parsed.Engineering, mode, parsed.ImportContext);
    }

    public ImportResult Apply(ReadOnlyMemory<byte> packageBytes, ImportMode mode)
    {
        var parsed = ParsePackage(packageBytes);
        var preview = _engineering.Preview(parsed.Engineering, mode, parsed.ImportContext);
        if (!preview.CanApply)
            return new ImportResult(mode, 0, 0, preview.SkipCount, preview.Items.SelectMany(x => x.Issues).ToArray());
        return _engineering.Apply(parsed.Engineering, mode, parsed.ImportContext);
    }

    private ParsedProjectPackage ParsePackage(ReadOnlyMemory<byte> packageBytes)
    {
        if (packageBytes.IsEmpty)
            throw new InvalidDataException("Project package is empty.");
        if (packageBytes.Length > MaximumPackageBytes)
            throw new InvalidDataException($"Project package exceeds the {MaximumPackageBytes} byte safety limit.");

        try
        {
            using var input = new MemoryStream(packageBytes.ToArray(), writable: false);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false);
            ValidateBasicArchiveEntries(archive);

            var manifestBytes = ReadEntry(archive.GetEntry(ManifestPath)!, MaximumManifestBytes);
            var manifest = JsonSerializer.Deserialize<ProjectPackageManifest>(manifestBytes, _json)
                ?? throw new InvalidDataException("Project package manifest is invalid.");
            ValidateManifest(manifest);
            ValidateArchiveAgainstManifest(archive, manifest);

            var engineeringBytes = ReadAndVerifyManifestEntry(
                archive,
                manifest.Files.Single(x => string.Equals(x.Path, EngineeringPath, StringComparison.Ordinal)),
                MaximumEngineeringBytes);

            string engineeringJson;
            try
            {
                engineeringJson = StrictUtf8.GetString(engineeringBytes);
            }
            catch (DecoderFallbackException ex)
            {
                throw new InvalidDataException("Engineering payload is not valid UTF-8.", ex);
            }

            var engineering = _engineering.ParseJson(engineeringJson);
            if (!string.Equals(manifest.EngineeringSchema, engineering.Schema, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Engineering schema does not match the project package manifest.");
            if (manifest.EngineeringSchemaVersion != engineering.SchemaVersion)
                throw new InvalidDataException("Engineering schema version does not match the project package manifest.");

            var importContext = BuildImportContext(archive, manifest, engineering);
            return new ParsedProjectPackage(manifest, engineering, importContext);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException or OverflowException)
        {
            throw new InvalidDataException("Invalid EliteSCADA project package.", ex);
        }
    }

    private IReadOnlyDictionary<string, VisualAssetPayload> ResolveExportPayloads(EngineeringPackage engineering)
    {
        var metadata = engineering.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>();
        if (metadata.Count == 0)
            return new Dictionary<string, VisualAssetPayload>(StringComparer.OrdinalIgnoreCase);
        if (_visualAssets is null)
            throw new InvalidOperationException("Visual asset registry is required to export a project containing image assets.");

        var payloads = new Dictionary<string, VisualAssetPayload>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in metadata)
        {
            var issues = VisualAssetEngineeringValidator.Validate(asset, _visualAssets);
            if (issues.Any(x => x.IsError))
                throw new InvalidDataException(
                    $"Visual asset '{asset.Key}' cannot be exported: {string.Join("; ", issues.Where(x => x.IsError).Select(x => x.Code))}.");

            var payload = _visualAssets.FindPayload(asset.Sha256)
                ?? throw new InvalidDataException($"Visual asset '{asset.Key}' payload '{asset.Sha256}' is unavailable.");
            var normalizedHash = asset.Sha256.ToLowerInvariant();
            if (payloads.TryGetValue(normalizedHash, out var existing) &&
                (!existing.MediaType.Equals(payload.MediaType, StringComparison.OrdinalIgnoreCase) ||
                 !existing.Content.AsSpan().SequenceEqual(payload.Content)))
                throw new InvalidDataException($"Visual asset hash '{normalizedHash}' maps to conflicting payloads.");
            payloads[normalizedHash] = payload with { Sha256 = normalizedHash, Content = payload.Content.ToArray() };
        }

        return payloads;
    }

    private EngineeringImportContext BuildImportContext(
        ZipArchive archive,
        ProjectPackageManifest manifest,
        EngineeringPackage engineering)
    {
        var metadata = engineering.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>();
        if (manifest.FormatVersion == 1)
        {
            if (metadata.Count != 0)
                throw new InvalidDataException("Project package v1 cannot carry first-class visual asset payloads.");
            return EngineeringImportContext.Empty;
        }

        var assetEntries = manifest.Files
            .Where(x => x.Path.StartsWith(AssetDirectory, StringComparison.Ordinal))
            .ToArray();
        var requiredHashes = metadata
            .Select(x => x.Sha256.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);
        var manifestHashes = assetEntries
            .Select(x => ParseAssetPath(x.Path))
            .ToHashSet(StringComparer.Ordinal);

        if (!requiredHashes.SetEquals(manifestHashes))
            throw new InvalidDataException("Project package visual asset sidecars do not exactly match canonical Engineering metadata.");

        var payloads = new Dictionary<string, VisualAssetPayload>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in assetEntries)
        {
            var hashFromPath = ParseAssetPath(entry.Path);
            if (!entry.Sha256.Equals(hashFromPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Project asset entry '{entry.Path}' path hash does not match its manifest SHA-256.");
            if (entry.Length <= 0 || entry.Length > VisualAssetEngineeringValidator.MaximumPayloadBytes)
                throw new InvalidDataException($"Project asset entry '{entry.Path}' length is outside the supported range.");

            var bytes = ReadAndVerifyManifestEntry(
                archive,
                entry,
                checked((int)VisualAssetEngineeringValidator.MaximumPayloadBytes));
            var payload = new VisualAssetPayload(hashFromPath, entry.MediaType, bytes);
            payloads.Add(hashFromPath, payload);
        }

        var context = new EngineeringImportContext(payloads);
        var scratchRegistry = new InMemoryVisualAssetEngineeringRegistry();
        foreach (var asset in metadata)
        {
            var issues = VisualAssetEngineeringValidator.Validate(asset, scratchRegistry, context);
            if (issues.Any(x => x.IsError))
                throw new InvalidDataException(
                    $"Project package visual asset '{asset.Key}' is invalid: {string.Join("; ", issues.Where(x => x.IsError).Select(x => x.Code))}.");
        }

        return context;
    }

    private static void ValidateBasicArchiveEntries(ZipArchive archive)
    {
        var entries = archive.Entries.ToArray();
        if (entries.Length < 2 || entries.Length > MaximumPayloadFiles + 1)
            throw new InvalidDataException("Project package contains an unsupported number of entries.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        long totalUncompressed = 0;
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FullName) ||
                entry.FullName.Contains("..", StringComparison.Ordinal) ||
                entry.FullName.StartsWith("/", StringComparison.Ordinal) ||
                entry.FullName.StartsWith("\\", StringComparison.Ordinal) ||
                entry.FullName.Contains('\\'))
                throw new InvalidDataException($"Unsafe package entry path '{entry.FullName}'.");
            if (!seen.Add(entry.FullName))
                throw new InvalidDataException($"Project package contains duplicate entry '{entry.FullName}'.");
            if (entry.FullName.EndsWith('/', StringComparison.Ordinal))
                throw new InvalidDataException($"Project package directory entry '{entry.FullName}' is not allowed.");

            totalUncompressed = checked(totalUncompressed + entry.Length);
            if (totalUncompressed > MaximumPackageBytes)
                throw new InvalidDataException("Project package uncompressed content exceeds its safety limit.");
        }

        if (entries.Count(x => x.FullName.Equals(ManifestPath, StringComparison.Ordinal)) != 1 ||
            entries.Count(x => x.FullName.Equals(EngineeringPath, StringComparison.Ordinal)) != 1)
            throw new InvalidDataException("Project package is missing manifest.json or engineering.json.");
    }

    private static void ValidateArchiveAgainstManifest(ZipArchive archive, ProjectPackageManifest manifest)
    {
        var actualPaths = archive.Entries
            .Select(x => x.FullName)
            .Where(x => !x.Equals(ManifestPath, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var manifestPaths = manifest.Files.Select(x => x.Path).ToHashSet(StringComparer.Ordinal);

        if (!actualPaths.SetEquals(manifestPaths))
            throw new InvalidDataException("Project package archive entries do not exactly match the manifest.");

        if (manifest.FormatVersion == 1 &&
            (archive.Entries.Count != 2 || manifest.Files.Count != 1))
            throw new InvalidDataException("Project package v1 must contain only manifest.json and engineering.json.");
    }

    private static void ValidateManifest(ProjectPackageManifest manifest)
    {
        if (!string.Equals(manifest.Format, CurrentFormat, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported project package format '{manifest.Format}'.");
        if (manifest.FormatVersion is not (1 or CurrentFormatVersion))
            throw new InvalidDataException($"Unsupported project package format version {manifest.FormatVersion}.");
        if (!string.Equals(manifest.Product, "EliteSCADA", StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported project package product '{manifest.Product}'.");
        if (manifest.PackageId == Guid.Empty)
            throw new InvalidDataException("Project package ID is invalid.");
        if (string.IsNullOrWhiteSpace(manifest.ProjectKey) || string.IsNullOrWhiteSpace(manifest.ProjectName))
            throw new InvalidDataException("Project identity is missing from the project package manifest.");
        if (string.IsNullOrWhiteSpace(manifest.EngineeringSchema) || manifest.EngineeringSchemaVersion < 1)
            throw new InvalidDataException("Engineering schema information is invalid in the project package manifest.");
        if (manifest.Files is null || manifest.Files.Count is < 1 or > MaximumPayloadFiles)
            throw new InvalidDataException("Project package manifest file count is invalid.");

        var duplicatePath = manifest.Files
            .Where(x => x is not null)
            .GroupBy(x => x!.Path, StringComparer.Ordinal)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicatePath is not null)
            throw new InvalidDataException($"Project package manifest contains duplicate path '{duplicatePath.Key}'.");

        foreach (var file in manifest.Files)
        {
            if (file is null || string.IsNullOrWhiteSpace(file.Path))
                throw new InvalidDataException("Project package manifest contains an invalid file entry.");
            if (file.Path.Equals(ManifestPath, StringComparison.Ordinal))
                throw new InvalidDataException("Project package manifest cannot describe itself as a payload entry.");
            if (file.Length < 0 || file.Length > MaximumPackageBytes)
                throw new InvalidDataException($"Project package entry '{file.Path}' length is invalid.");
            if (string.IsNullOrWhiteSpace(file.MediaType) || file.MediaType.Length > 100)
                throw new InvalidDataException($"Project package entry '{file.Path}' media type is invalid.");
            if (!IsSha256(file.Sha256))
                throw new InvalidDataException($"Project package entry '{file.Path}' SHA-256 is invalid.");
        }

        var engineeringEntry = manifest.Files.SingleOrDefault(x =>
            x is not null && string.Equals(x.Path, EngineeringPath, StringComparison.Ordinal));
        if (engineeringEntry is null)
            throw new InvalidDataException("Project package manifest does not describe engineering.json.");
        if (!string.Equals(engineeringEntry.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("engineering.json has an unsupported media type in the project package manifest.");
        if (engineeringEntry.Length < 0 || engineeringEntry.Length > MaximumEngineeringBytes)
            throw new InvalidDataException("engineering.json length is invalid in the project package manifest.");

        if (manifest.FormatVersion == 1)
        {
            if (manifest.Files.Count != 1)
                throw new InvalidDataException("Project package v1 manifest must describe exactly engineering.json.");
            return;
        }

        foreach (var assetEntry in manifest.Files.Where(x => !x.Path.Equals(EngineeringPath, StringComparison.Ordinal)))
        {
            var hash = ParseAssetPath(assetEntry.Path);
            if (!assetEntry.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Project asset entry '{assetEntry.Path}' path does not match its SHA-256.");
            if (assetEntry.Length <= 0 || assetEntry.Length > VisualAssetEngineeringValidator.MaximumPayloadBytes)
                throw new InvalidDataException($"Project asset entry '{assetEntry.Path}' length is invalid.");
            if (assetEntry.MediaType is not ("image/png" or "image/jpeg" or "image/bmp"))
                throw new InvalidDataException($"Project asset entry '{assetEntry.Path}' media type is unsupported.");
        }
    }

    private static byte[] ReadAndVerifyManifestEntry(
        ZipArchive archive,
        ProjectPackageFileEntry expected,
        int maximumBytes)
    {
        var entry = archive.GetEntry(expected.Path)
            ?? throw new InvalidDataException($"Project package is missing '{expected.Path}'.");
        var bytes = ReadEntry(entry, maximumBytes);
        if (expected.Length != bytes.LongLength)
            throw new InvalidDataException($"Package entry '{expected.Path}' length does not match the manifest.");
        if (!expected.Sha256.Equals(Sha256(bytes), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Package entry '{expected.Path}' checksum does not match the manifest.");
        return bytes;
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry, int maximumBytes)
    {
        if (entry.Length > maximumBytes)
            throw new InvalidDataException($"Package entry '{entry.FullName}' exceeds its safety limit.");
        using var stream = entry.Open();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        var total = 0;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            total = checked(total + read);
            if (total > maximumBytes)
                throw new InvalidDataException($"Package entry '{entry.FullName}' exceeds its safety limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static string AssetPath(string sha256) => $"{AssetDirectory}{sha256.ToLowerInvariant()}";

    private static string ParseAssetPath(string path)
    {
        if (!path.StartsWith(AssetDirectory, StringComparison.Ordinal) || path.Length != AssetDirectory.Length + 64)
            throw new InvalidDataException($"Invalid project asset sidecar path '{path}'.");
        var hash = path[AssetDirectory.Length..];
        if (!IsSha256(hash) || hash.Any(char.IsUpper))
            throw new InvalidDataException($"Project asset sidecar path '{path}' must use lowercase SHA-256 identity.");
        return hash;
    }

    private static bool IsSha256(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length == 64 && value.All(Uri.IsHexDigit);

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record ParsedProjectPackage(
        ProjectPackageManifest Manifest,
        EngineeringPackage Engineering,
        EngineeringImportContext ImportContext);
}
