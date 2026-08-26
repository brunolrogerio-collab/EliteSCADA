using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

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
    public const int CurrentFormatVersion = 1;
    public const string ManifestPath = "manifest.json";
    public const string EngineeringPath = "engineering.json";
    public const string PackageExtension = ".escadapkg";

    private const int MaximumManifestBytes = 1024 * 1024;
    private const int MaximumEngineeringBytes = 50 * 1024 * 1024;
    private const int MaximumPackageBytes = 64 * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly IEngineeringExchangeService _engineering;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public ProjectPackageService(IEngineeringExchangeService engineering)
    {
        _engineering = engineering;
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
        var engineeringEntry = new ProjectPackageFileEntry(
            EngineeringPath,
            "application/json",
            engineeringBytes.LongLength,
            Sha256(engineeringBytes));

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
            new[] { engineeringEntry });

        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, _json);

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, ManifestPath, manifestBytes);
            WriteEntry(archive, EngineeringPath, engineeringBytes);
        }
        return output.ToArray();
    }

    public ProjectPackageInspection Inspect(ReadOnlyMemory<byte> packageBytes)
    {
        if (packageBytes.IsEmpty)
            throw new InvalidDataException("Project package is empty.");
        if (packageBytes.Length > MaximumPackageBytes)
            throw new InvalidDataException($"Project package exceeds the {MaximumPackageBytes} byte safety limit.");

        try
        {
            using var input = new MemoryStream(packageBytes.ToArray(), writable: false);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false);
            ValidateArchiveEntries(archive);

            var manifestBytes = ReadEntry(archive.GetEntry(ManifestPath)!, MaximumManifestBytes);
            var manifest = JsonSerializer.Deserialize<ProjectPackageManifest>(manifestBytes, _json)
                ?? throw new InvalidDataException("Project package manifest is invalid.");
            ValidateManifest(manifest);

            var engineeringBytes = ReadEntry(archive.GetEntry(EngineeringPath)!, MaximumEngineeringBytes);
            var expectedEntry = manifest.Files.Single(x => x.Path.Equals(EngineeringPath, StringComparison.Ordinal));
            if (expectedEntry.Length != engineeringBytes.LongLength)
                throw new InvalidDataException("Engineering payload length does not match the project package manifest.");
            if (!expectedEntry.Sha256.Equals(Sha256(engineeringBytes), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Engineering payload checksum does not match the project package manifest.");

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
            if (!manifest.EngineeringSchema.Equals(engineering.Schema, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Engineering schema does not match the project package manifest.");
            if (manifest.EngineeringSchemaVersion != engineering.SchemaVersion)
                throw new InvalidDataException("Engineering schema version does not match the project package manifest.");

            return new ProjectPackageInspection(manifest, engineering);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            throw new InvalidDataException("Invalid EliteSCADA project package.", ex);
        }
    }

    public ImportPreview Preview(ReadOnlyMemory<byte> packageBytes, ImportMode mode)
    {
        var inspection = Inspect(packageBytes);
        return _engineering.Preview(inspection.Engineering, mode);
    }

    public ImportResult Apply(ReadOnlyMemory<byte> packageBytes, ImportMode mode)
    {
        var inspection = Inspect(packageBytes);
        var preview = _engineering.Preview(inspection.Engineering, mode);
        if (!preview.CanApply)
            return new ImportResult(mode, 0, 0, preview.SkipCount, preview.Items.SelectMany(x => x.Issues).ToArray());
        return _engineering.Apply(inspection.Engineering, mode);
    }

    private static void ValidateArchiveEntries(ZipArchive archive)
    {
        var entries = archive.Entries.ToArray();
        if (entries.Length != 2)
            throw new InvalidDataException("Project package v1 must contain exactly manifest.json and engineering.json.");

        foreach (var entry in entries)
        {
            if (entry.FullName.Contains("..", StringComparison.Ordinal) ||
                entry.FullName.StartsWith('/', StringComparison.Ordinal) ||
                entry.FullName.StartsWith('\\'))
                throw new InvalidDataException($"Unsafe package entry path '{entry.FullName}'.");
        }

        if (entries.Count(x => x.FullName.Equals(ManifestPath, StringComparison.Ordinal)) != 1 ||
            entries.Count(x => x.FullName.Equals(EngineeringPath, StringComparison.Ordinal)) != 1)
            throw new InvalidDataException("Project package v1 is missing a required entry or contains duplicate entry names.");
    }

    private static void ValidateManifest(ProjectPackageManifest manifest)
    {
        if (!manifest.Format.Equals(CurrentFormat, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported project package format '{manifest.Format}'.");
        if (manifest.FormatVersion != CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported project package format version {manifest.FormatVersion}.");
        if (manifest.PackageId == Guid.Empty)
            throw new InvalidDataException("Project package ID is invalid.");
        if (string.IsNullOrWhiteSpace(manifest.ProjectKey) || string.IsNullOrWhiteSpace(manifest.ProjectName))
            throw new InvalidDataException("Project identity is missing from the project package manifest.");
        if (string.IsNullOrWhiteSpace(manifest.EngineeringSchema) || manifest.EngineeringSchemaVersion < 1)
            throw new InvalidDataException("Engineering schema information is invalid in the project package manifest.");
        if (manifest.Files is null || manifest.Files.Count != 1)
            throw new InvalidDataException("Project package v1 manifest must describe exactly one engineering payload.");

        var engineeringEntry = manifest.Files.SingleOrDefault(x => x.Path.Equals(EngineeringPath, StringComparison.Ordinal));
        if (engineeringEntry is null)
            throw new InvalidDataException("Project package manifest does not describe engineering.json.");
        if (!engineeringEntry.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("engineering.json has an unsupported media type in the project package manifest.");
        if (engineeringEntry.Length < 0 || engineeringEntry.Length > MaximumEngineeringBytes)
            throw new InvalidDataException("engineering.json length is invalid in the project package manifest.");
        if (engineeringEntry.Sha256.Length != 64 || !engineeringEntry.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("engineering.json SHA-256 is invalid in the project package manifest.");
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
            total += read;
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

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
