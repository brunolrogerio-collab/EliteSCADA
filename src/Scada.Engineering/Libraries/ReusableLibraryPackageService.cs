using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Scripts;
using Scada.Engineering.VisualAssets;

namespace Scada.Engineering.Libraries;

public static class ReusableLibraryResourceKinds
{
    public const string EquipmentTemplate = "equipment-template";
    public const string Dynamo = "dynamo";
    public const string Screen = "screen";
    public const string Popup = "popup";
    public const string Script = "script";
    public const string VisualAsset = "visual-asset";

    public static IReadOnlySet<string> Supported { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        EquipmentTemplate,
        Dynamo,
        Screen,
        Popup,
        Script,
        VisualAsset
    };

    public static IReadOnlySet<string> ExportEnabled { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        EquipmentTemplate,
        VisualAsset
    };
}

public sealed record ReusableLibraryResourceSelection(string Kind, Guid ResourceId);

public sealed record ReusableLibraryExportRequest(
    Guid LibraryId,
    string Name,
    string Version,
    IReadOnlyCollection<ReusableLibraryResourceSelection> Resources);

public sealed record ReusableLibraryDependency(string Kind, Guid ResourceId);

public sealed record ReusableLibraryFileEntry(
    string Path,
    string MediaType,
    long Length,
    string Sha256);

public sealed record ReusableLibraryResourceEntry(
    Guid ResourceId,
    string Kind,
    string SourceKey,
    string DisplayName,
    string PayloadPath,
    IReadOnlyCollection<ReusableLibraryDependency> Dependencies);

public sealed record ReusableLibraryManifest(
    string Format,
    int FormatVersion,
    Guid LibraryId,
    DateTimeOffset CreatedAtUtc,
    string Product,
    string Name,
    string Version,
    string EngineeringSchema,
    int EngineeringSchemaVersion,
    IReadOnlyCollection<ReusableLibraryResourceEntry> Resources,
    IReadOnlyCollection<ReusableLibraryFileEntry> Files);

public sealed record ReusableLibraryInspection(ReusableLibraryManifest Manifest);

public interface IReusableLibraryPackageService
{
    byte[] Export(ReusableLibraryExportRequest request);
    ReusableLibraryInspection Inspect(ReadOnlyMemory<byte> libraryBytes);
}

public sealed class ReusableLibraryPackageService : IReusableLibraryPackageService
{
    public const string CurrentFormat = "elitescada.resource-library";
    public const int CurrentFormatVersion = 1;
    public const string ManifestPath = "manifest.json";
    public const string PackageExtension = ".escadalib";
    public const int MaximumPackageBytes = 64 * 1024 * 1024;
    public const int MaximumManifestBytes = 1024 * 1024;
    public const int MaximumResourceBytes = 8 * 1024 * 1024;
    public const int MaximumAssetBytes = 32 * 1024 * 1024;
    public const int MaximumPayloadFiles = 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly IEngineeringAssetRegistry _assets;
    private readonly IVisualAssetEngineeringRegistry _visualAssets;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public ReusableLibraryPackageService(
        IEngineeringAssetRegistry assets,
        IVisualAssetEngineeringRegistry visualAssets)
    {
        _assets = assets;
        _visualAssets = visualAssets;
    }

    public byte[] Export(ReusableLibraryExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateExportRequest(request);

        var files = new Dictionary<string, (ReusableLibraryFileEntry Entry, byte[] Bytes)>(StringComparer.Ordinal);
        var resources = new List<ReusableLibraryResourceEntry>();

        foreach (var selection in request.Resources
                     .OrderBy(x => x.Kind, StringComparer.Ordinal)
                     .ThenBy(x => x.ResourceId))
        {
            resources.Add(BuildExportResource(selection, files));
        }

        if (files.Count == 0 || files.Count > MaximumPayloadFiles)
            throw new InvalidDataException("Reusable library contains an invalid number of payload files.");

        var manifest = new ReusableLibraryManifest(
            CurrentFormat,
            CurrentFormatVersion,
            request.LibraryId,
            DateTimeOffset.UtcNow,
            "EliteSCADA",
            request.Name.Trim(),
            request.Version.Trim(),
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            resources,
            files.Values.Select(x => x.Entry).OrderBy(x => x.Path, StringComparer.Ordinal).ToArray());

        ValidateManifest(manifest);
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, _json);
        if (manifestBytes.LongLength > MaximumManifestBytes)
            throw new InvalidDataException("Reusable library manifest exceeds its safety limit.");

        var totalUncompressed = manifestBytes.LongLength;
        foreach (var file in files.Values)
            totalUncompressed = checked(totalUncompressed + file.Bytes.LongLength);
        if (totalUncompressed > MaximumPackageBytes)
            throw new InvalidDataException("Reusable library uncompressed content exceeds its safety limit.");

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, ManifestPath, manifestBytes);
            foreach (var file in files.OrderBy(x => x.Key, StringComparer.Ordinal))
                WriteEntry(archive, file.Key, file.Value.Bytes);
        }

        if (output.Length > MaximumPackageBytes)
            throw new InvalidDataException("Reusable library exceeds its compressed package safety limit.");

        return output.ToArray();
    }

    public ReusableLibraryInspection Inspect(ReadOnlyMemory<byte> libraryBytes)
    {
        if (libraryBytes.IsEmpty)
            throw new InvalidDataException("Reusable library is empty.");
        if (libraryBytes.Length > MaximumPackageBytes)
            throw new InvalidDataException("Reusable library exceeds its package safety limit.");

        try
        {
            using var input = new MemoryStream(libraryBytes.ToArray(), writable: false);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false);
            ValidateBasicArchiveEntries(archive);

            var manifestEntry = archive.GetEntry(ManifestPath)
                ?? throw new InvalidDataException("Reusable library manifest is missing.");
            var manifestBytes = ReadEntry(manifestEntry, MaximumManifestBytes);
            var manifest = JsonSerializer.Deserialize<ReusableLibraryManifest>(manifestBytes, _json)
                ?? throw new InvalidDataException("Reusable library manifest is invalid.");

            ValidateManifest(manifest);
            ValidateArchiveAgainstManifest(archive, manifest);

            var verifiedFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var file in manifest.Files)
            {
                var maximumBytes = file.Path.StartsWith("assets/", StringComparison.Ordinal)
                    ? MaximumAssetBytes
                    : MaximumResourceBytes;
                verifiedFiles[file.Path] = ReadAndVerifyFile(archive, file, maximumBytes);
            }

            ValidateResourcePayloads(manifest, verifiedFiles);
            return new ReusableLibraryInspection(manifest);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException or OverflowException)
        {
            throw new InvalidDataException("Invalid EliteSCADA reusable library.", ex);
        }
    }

    private ReusableLibraryResourceEntry BuildExportResource(
        ReusableLibraryResourceSelection selection,
        IDictionary<string, (ReusableLibraryFileEntry Entry, byte[] Bytes)> files)
    {
        if (!ReusableLibraryResourceKinds.ExportEnabled.Contains(selection.Kind))
            throw new InvalidDataException($"Reusable resource kind '{selection.Kind}' is not export-enabled yet.");

        return selection.Kind switch
        {
            ReusableLibraryResourceKinds.EquipmentTemplate => ExportTemplate(selection.ResourceId, files),
            ReusableLibraryResourceKinds.VisualAsset => ExportVisualAsset(selection.ResourceId, files),
            _ => throw new InvalidDataException($"Unsupported reusable resource kind '{selection.Kind}'.")
        };
    }

    private ReusableLibraryResourceEntry ExportTemplate(
        Guid resourceId,
        IDictionary<string, (ReusableLibraryFileEntry Entry, byte[] Bytes)> files)
    {
        var template = _assets.FindTemplate(resourceId)
            ?? throw new InvalidDataException($"Equipment template '{resourceId:D}' was not found in Working.");
        if (template.Id != resourceId)
            throw new InvalidDataException("Equipment template stable identity is inconsistent.");

        return AddJsonResource(
            ReusableLibraryResourceKinds.EquipmentTemplate,
            resourceId,
            template.Key,
            template.Name,
            template,
            Array.Empty<ReusableLibraryDependency>(),
            files);
    }

    private ReusableLibraryResourceEntry ExportVisualAsset(
        Guid resourceId,
        IDictionary<string, (ReusableLibraryFileEntry Entry, byte[] Bytes)> files)
    {
        var asset = _visualAssets.FindAsset(resourceId)
            ?? throw new InvalidDataException($"Visual asset '{resourceId:D}' was not found in Working.");
        if (asset.Id != resourceId)
            throw new InvalidDataException("Visual asset stable identity is inconsistent.");

        var payload = _visualAssets.FindPayload(asset.Sha256)
            ?? throw new InvalidDataException($"Visual asset '{asset.Key}' payload '{asset.Sha256}' is missing.");
        var actualHash = Sha256(payload.Content);
        if (!actualHash.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase) ||
            payload.ByteLength != asset.ByteLength ||
            !payload.MediaType.Equals(asset.MediaType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Visual asset '{asset.Key}' payload is inconsistent with canonical metadata.");
        if (payload.ByteLength > MaximumAssetBytes)
            throw new InvalidDataException($"Visual asset '{asset.Key}' exceeds the reusable-library asset safety limit.");

        var assetPath = AssetPath(actualHash);
        AddFile(files, assetPath, payload.MediaType, payload.Content);

        return AddJsonResource(
            ReusableLibraryResourceKinds.VisualAsset,
            resourceId,
            asset.Key,
            asset.Name,
            asset,
            Array.Empty<ReusableLibraryDependency>(),
            files);
    }

    private ReusableLibraryResourceEntry AddJsonResource<T>(
        string kind,
        Guid resourceId,
        string sourceKey,
        string displayName,
        T value,
        IReadOnlyCollection<ReusableLibraryDependency> dependencies,
        IDictionary<string, (ReusableLibraryFileEntry Entry, byte[] Bytes)> files)
    {
        var payloadPath = ResourcePath(kind, resourceId);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, _json);
        if (bytes.LongLength > MaximumResourceBytes)
            throw new InvalidDataException($"Reusable resource '{sourceKey}' exceeds its payload safety limit.");

        AddFile(files, payloadPath, "application/json", bytes);
        return new ReusableLibraryResourceEntry(
            resourceId,
            kind,
            sourceKey,
            displayName,
            payloadPath,
            dependencies);
    }

    private static void AddFile(
        IDictionary<string, (ReusableLibraryFileEntry Entry, byte[] Bytes)> files,
        string path,
        string mediaType,
        byte[] bytes)
    {
        var hash = Sha256(bytes);
        var entry = new ReusableLibraryFileEntry(path, mediaType, bytes.LongLength, hash);
        if (files.TryGetValue(path, out var existing))
        {
            if (existing.Entry != entry || !existing.Bytes.AsSpan().SequenceEqual(bytes))
                throw new InvalidDataException($"Reusable library file '{path}' has conflicting content.");
            return;
        }

        files[path] = (entry, bytes.ToArray());
    }

    private static void ValidateExportRequest(ReusableLibraryExportRequest request)
    {
        if (request.LibraryId == Guid.Empty)
            throw new InvalidDataException("Reusable library ID must be a stable non-empty GUID.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidDataException("Reusable library name is required.");
        if (string.IsNullOrWhiteSpace(request.Version))
            throw new InvalidDataException("Reusable library version is required.");
        if (request.Resources is null || request.Resources.Count == 0)
            throw new InvalidDataException("At least one reusable resource must be selected.");

        foreach (var selection in request.Resources)
        {
            if (selection.ResourceId == Guid.Empty)
                throw new InvalidDataException("Reusable resource selection requires a stable non-empty ID.");
            if (!ReusableLibraryResourceKinds.Supported.Contains(selection.Kind))
                throw new InvalidDataException($"Unsupported reusable resource kind '{selection.Kind}'.");
        }

        var duplicate = request.Resources
            .GroupBy(x => (x.Kind, x.ResourceId))
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException(
                $"Reusable resource '{duplicate.Key.Kind}:{duplicate.Key.ResourceId:D}' was selected more than once.");
    }

    private static void ValidateManifest(ReusableLibraryManifest manifest)
    {
        if (!string.Equals(manifest.Format, CurrentFormat, StringComparison.Ordinal))
            throw new InvalidDataException("Unsupported reusable library format.");
        if (manifest.FormatVersion != CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported reusable library format version '{manifest.FormatVersion}'.");
        if (manifest.LibraryId == Guid.Empty)
            throw new InvalidDataException("Reusable library manifest requires a stable non-empty library ID.");
        if (!string.Equals(manifest.Product, "EliteSCADA", StringComparison.Ordinal))
            throw new InvalidDataException("Reusable library product identifier is invalid.");
        if (string.IsNullOrWhiteSpace(manifest.Name) || string.IsNullOrWhiteSpace(manifest.Version))
            throw new InvalidDataException("Reusable library name and version are required.");
        if (!string.Equals(manifest.EngineeringSchema, EngineeringExchangeService.CurrentSchema, StringComparison.Ordinal))
            throw new InvalidDataException("Reusable library Engineering schema is not supported.");
        if (manifest.EngineeringSchemaVersion != EngineeringExchangeService.CurrentSchemaVersion)
            throw new InvalidDataException("Reusable library Engineering schema version is not supported.");
        if (manifest.Resources is null || manifest.Resources.Count == 0)
            throw new InvalidDataException("Reusable library must contain at least one resource.");
        if (manifest.Files is null || manifest.Files.Count == 0 || manifest.Files.Count > MaximumPayloadFiles)
            throw new InvalidDataException("Reusable library contains an invalid number of payload files.");

        var duplicateResourceId = manifest.Resources.GroupBy(x => x.ResourceId).FirstOrDefault(x => x.Count() > 1);
        if (duplicateResourceId is not null)
            throw new InvalidDataException($"Duplicate reusable resource ID '{duplicateResourceId.Key:D}'.");

        var duplicatePayloadPath = manifest.Resources
            .GroupBy(x => x.PayloadPath, StringComparer.Ordinal)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicatePayloadPath is not null)
            throw new InvalidDataException($"Duplicate reusable resource payload path '{duplicatePayloadPath.Key}'.");

        var filesByPath = new Dictionary<string, ReusableLibraryFileEntry>(StringComparer.Ordinal);
        foreach (var file in manifest.Files)
        {
            ValidateArchivePath(file.Path);
            if (file.Path == ManifestPath)
                throw new InvalidDataException("Reusable library manifest cannot declare itself as a payload file.");
            if (string.IsNullOrWhiteSpace(file.MediaType))
                throw new InvalidDataException($"Reusable library file '{file.Path}' requires a media type.");
            if (file.Length < 0)
                throw new InvalidDataException($"Reusable library file '{file.Path}' has an invalid length.");
            ValidateSha256(file.Sha256, $"Reusable library file '{file.Path}'");
            if (!filesByPath.TryAdd(file.Path, file))
                throw new InvalidDataException($"Duplicate reusable library file path '{file.Path}'.");
        }

        var resourcesById = manifest.Resources.ToDictionary(x => x.ResourceId);
        foreach (var resource in manifest.Resources)
        {
            if (resource.ResourceId == Guid.Empty)
                throw new InvalidDataException("Reusable resource ID cannot be empty.");
            if (!ReusableLibraryResourceKinds.Supported.Contains(resource.Kind))
                throw new InvalidDataException($"Unsupported reusable resource kind '{resource.Kind}'.");
            if (string.IsNullOrWhiteSpace(resource.SourceKey) || string.IsNullOrWhiteSpace(resource.DisplayName))
                throw new InvalidDataException($"Reusable resource '{resource.ResourceId:D}' requires source identity and display name.");
            ValidateArchivePath(resource.PayloadPath);
            if (!filesByPath.TryGetValue(resource.PayloadPath, out var payloadFile))
                throw new InvalidDataException($"Reusable resource '{resource.ResourceId:D}' payload is missing from the manifest file table.");
            if (!payloadFile.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Reusable resource '{resource.ResourceId:D}' metadata payload must be JSON.");

            var seenDependencies = new HashSet<(string Kind, Guid ResourceId)>();
            foreach (var dependency in resource.Dependencies ?? Array.Empty<ReusableLibraryDependency>())
            {
                if (dependency.ResourceId == Guid.Empty || !ReusableLibraryResourceKinds.Supported.Contains(dependency.Kind))
                    throw new InvalidDataException($"Reusable resource '{resource.ResourceId:D}' contains an invalid dependency identity.");
                if (!seenDependencies.Add((dependency.Kind, dependency.ResourceId)))
                    throw new InvalidDataException($"Reusable resource '{resource.ResourceId:D}' contains a duplicate dependency.");
                if (!resourcesById.TryGetValue(dependency.ResourceId, out var target) ||
                    !target.Kind.Equals(dependency.Kind, StringComparison.Ordinal))
                    throw new InvalidDataException($"Reusable resource '{resource.ResourceId:D}' dependency does not resolve inside the library.");
            }
        }
    }

    private void ValidateResourcePayloads(
        ReusableLibraryManifest manifest,
        IReadOnlyDictionary<string, byte[]> verifiedFiles)
    {
        var referencedFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in manifest.Resources)
        {
            referencedFiles.Add(resource.PayloadPath);
            ValidateCanonicalResource(resource, verifiedFiles[resource.PayloadPath], manifest, verifiedFiles, referencedFiles);
        }

        var orphan = manifest.Files.FirstOrDefault(x => !referencedFiles.Contains(x.Path));
        if (orphan is not null)
            throw new InvalidDataException($"Reusable library file '{orphan.Path}' is not owned by any declared resource.");
    }

    private void ValidateCanonicalResource(
        ReusableLibraryResourceEntry resource,
        byte[] bytes,
        ReusableLibraryManifest manifest,
        IReadOnlyDictionary<string, byte[]> verifiedFiles,
        ISet<string> referencedFiles)
    {
        try
        {
            switch (resource.Kind)
            {
                case ReusableLibraryResourceKinds.EquipmentTemplate:
                {
                    var value = Deserialize<EquipmentTemplateEngineeringDto>(bytes, resource);
                    ValidateIdentity(resource, value.Id, value.Key, value.Name);
                    break;
                }
                case ReusableLibraryResourceKinds.Dynamo:
                {
                    var value = Deserialize<DynamoEngineeringDto>(bytes, resource);
                    ValidateIdentity(resource, value.Id, value.Key, value.Name);
                    break;
                }
                case ReusableLibraryResourceKinds.Screen:
                {
                    var value = Deserialize<ScreenEngineeringDto>(bytes, resource);
                    ValidateIdentity(resource, value.Id, value.Key, value.Name);
                    break;
                }
                case ReusableLibraryResourceKinds.Popup:
                {
                    var value = Deserialize<PopupEngineeringDto>(bytes, resource);
                    ValidateIdentity(resource, value.Id, value.Key, value.Name);
                    break;
                }
                case ReusableLibraryResourceKinds.Script:
                {
                    var value = Deserialize<ScriptEngineeringDefinition>(bytes, resource);
                    ValidateIdentity(resource, value.Id, value.Path, value.Name);
                    break;
                }
                case ReusableLibraryResourceKinds.VisualAsset:
                {
                    var value = Deserialize<VisualAssetEngineeringDto>(bytes, resource);
                    ValidateIdentity(resource, value.Id, value.Key, value.Name);
                    ValidateSha256(value.Sha256, $"Visual asset '{value.Key}'");
                    if (value.ByteLength < 0 || string.IsNullOrWhiteSpace(value.MediaType))
                        throw new InvalidDataException($"Visual asset '{value.Key}' metadata is invalid.");

                    var assetPath = AssetPath(value.Sha256);
                    var file = manifest.Files.SingleOrDefault(x => x.Path == assetPath)
                        ?? throw new InvalidDataException($"Visual asset '{value.Key}' sidecar '{assetPath}' is missing.");
                    if (!verifiedFiles.TryGetValue(assetPath, out var assetBytes))
                        throw new InvalidDataException($"Visual asset '{value.Key}' sidecar '{assetPath}' was not verified.");
                    if (file.Length != value.ByteLength ||
                        !file.MediaType.Equals(value.MediaType, StringComparison.OrdinalIgnoreCase) ||
                        !file.Sha256.Equals(value.Sha256, StringComparison.OrdinalIgnoreCase) ||
                        assetBytes.LongLength != value.ByteLength)
                        throw new InvalidDataException($"Visual asset '{value.Key}' sidecar is inconsistent with canonical metadata.");
                    referencedFiles.Add(assetPath);
                    break;
                }
                default:
                    throw new InvalidDataException($"Unsupported reusable resource kind '{resource.Kind}'.");
            }
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or FormatException)
        {
            throw new InvalidDataException($"Reusable resource '{resource.Kind}:{resource.ResourceId:D}' payload is invalid.", ex);
        }
    }

    private T Deserialize<T>(byte[] bytes, ReusableLibraryResourceEntry resource)
    {
        string json;
        try
        {
            json = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException($"Reusable resource '{resource.Kind}:{resource.ResourceId:D}' is not valid UTF-8.", ex);
        }

        return JsonSerializer.Deserialize<T>(json, _json)
            ?? throw new InvalidDataException($"Reusable resource '{resource.Kind}:{resource.ResourceId:D}' payload is empty or invalid.");
    }

    private static void ValidateIdentity(
        ReusableLibraryResourceEntry resource,
        Guid? payloadId,
        string payloadKey,
        string payloadName)
    {
        if (!payloadId.HasValue || payloadId.Value == Guid.Empty || payloadId.Value != resource.ResourceId)
            throw new InvalidDataException($"Reusable resource '{resource.ResourceId:D}' stable identity does not match its payload.");
        if (!string.Equals(payloadKey, resource.SourceKey, StringComparison.Ordinal))
            throw new InvalidDataException($"Reusable resource '{resource.ResourceId:D}' source key does not match its payload.");
        if (string.IsNullOrWhiteSpace(payloadName))
            throw new InvalidDataException($"Reusable resource '{resource.ResourceId:D}' payload name is required.");
    }

    private static void ValidateIdentity(
        ReusableLibraryResourceEntry resource,
        Guid payloadId,
        string payloadKey,
        string payloadName) =>
        ValidateIdentity(resource, (Guid?)payloadId, payloadKey, payloadName);

    private static void ValidateBasicArchiveEntries(ZipArchive archive)
    {
        if (archive.Entries.Count == 0 || archive.Entries.Count > MaximumPayloadFiles + 1)
            throw new InvalidDataException("Reusable library contains an invalid number of archive entries.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        long totalLength = 0;
        foreach (var entry in archive.Entries)
        {
            ValidateArchivePath(entry.FullName);
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                throw new InvalidDataException("Reusable library cannot contain directory-only archive entries.");
            if (!seen.Add(entry.FullName))
                throw new InvalidDataException($"Duplicate reusable library archive path '{entry.FullName}'.");
            totalLength = checked(totalLength + entry.Length);
            if (totalLength > MaximumPackageBytes)
                throw new InvalidDataException("Reusable library uncompressed content exceeds its safety limit.");
        }
    }

    private static void ValidateArchiveAgainstManifest(ZipArchive archive, ReusableLibraryManifest manifest)
    {
        var expected = manifest.Files.Select(x => x.Path).Append(ManifestPath).ToHashSet(StringComparer.Ordinal);
        var actual = archive.Entries.Select(x => x.FullName).ToHashSet(StringComparer.Ordinal);
        if (!expected.SetEquals(actual))
            throw new InvalidDataException("Reusable library archive entries do not match the manifest.");
    }

    private static byte[] ReadAndVerifyFile(ZipArchive archive, ReusableLibraryFileEntry file, int maximumBytes)
    {
        var entry = archive.GetEntry(file.Path)
            ?? throw new InvalidDataException($"Reusable library file '{file.Path}' is missing.");
        var bytes = ReadEntry(entry, maximumBytes);
        if (bytes.LongLength != file.Length)
            throw new InvalidDataException($"Reusable library file '{file.Path}' length does not match the manifest.");
        var actualHash = Sha256(bytes);
        if (!actualHash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Reusable library file '{file.Path}' SHA-256 does not match the manifest.");
        return bytes;
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry, int maximumBytes)
    {
        if (entry.Length < 0 || entry.Length > maximumBytes)
            throw new InvalidDataException($"Reusable library entry '{entry.FullName}' exceeds its safety limit.");

        using var source = entry.Open();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            total = checked(total + read);
            if (total > maximumBytes)
                throw new InvalidDataException($"Reusable library entry '{entry.FullName}' exceeds its safety limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static void ValidateArchivePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.Contains('\\') ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            Path.IsPathRooted(path) ||
            path.Contains(':'))
            throw new InvalidDataException($"Reusable library archive path '{path}' is invalid.");

        var segments = path.Split('/');
        if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
            throw new InvalidDataException($"Reusable library archive path '{path}' is invalid.");
    }

    private static void ValidateSha256(string value, string owner)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
            throw new InvalidDataException($"{owner} SHA-256 is invalid.");
        try
        {
            _ = Convert.FromHexString(value);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException($"{owner} SHA-256 is invalid.", ex);
        }
    }

    private static string ResourcePath(string kind, Guid resourceId) =>
        $"resources/{kind}/{resourceId:D}.json";

    private static string AssetPath(string sha256) =>
        $"assets/{sha256.ToLowerInvariant()}";

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void WriteEntry(ZipArchive archive, string path, ReadOnlySpan<byte> bytes)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes);
    }
}