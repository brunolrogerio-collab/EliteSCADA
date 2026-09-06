using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.VisualAssets;

namespace Scada.Engineering.Libraries;

public sealed record ReusableLibraryIncorporationSelection(string Kind, Guid ResourceId);

public sealed record ReusableLibraryIncorporationPlan(
    ReusableLibraryManifest Manifest,
    ReusableLibraryIncorporationSelection Selection,
    IReadOnlyCollection<ReusableLibraryResourceEntry> DependencyClosure,
    EngineeringPackage Engineering,
    EngineeringImportContext ImportContext,
    ImportPreview Preview,
    int DeduplicatedCount)
{
    public bool RequiresMutation => Preview.CreateCount > 0;
}

public sealed class ReusableLibraryIncorporationConflictException : InvalidOperationException
{
    public ReusableLibraryIncorporationConflictException(string kind, Guid resourceId, string sourceKey, string reason)
        : base($"Reusable resource '{kind}:{resourceId:D}' ('{sourceKey}') conflicts with existing project content: {reason}")
    {
        Kind = kind;
        ResourceId = resourceId;
        SourceKey = sourceKey;
        Reason = reason;
    }

    public string Kind { get; }
    public Guid ResourceId { get; }
    public string SourceKey { get; }
    public string Reason { get; }
}

/// <summary>
/// Builds a complete, non-mutating incorporation plan from an already associated
/// .escadalib. The service re-inspects the package, resolves dependency closure,
/// verifies selected payload bytes again and performs explicit identity/content
/// collision checks before canonical Engineering Preview is allowed to run.
/// </summary>
public sealed class ReusableLibraryIncorporationService
{
    private static readonly IReadOnlySet<string> IncorporationEnabled = new HashSet<string>(StringComparer.Ordinal)
    {
        ReusableLibraryResourceKinds.EquipmentTemplate,
        ReusableLibraryResourceKinds.Dynamo,
        ReusableLibraryResourceKinds.VisualAsset
    };

    private readonly IReusableLibraryPackageService _packages;
    private readonly IEngineeringAssetRegistry _assets;
    private readonly IVisualAssetEngineeringRegistry _visualAssets;
    private readonly IEngineeringExchangeService _exchange;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public ReusableLibraryIncorporationService(
        IReusableLibraryPackageService packages,
        IEngineeringAssetRegistry assets,
        IVisualAssetEngineeringRegistry visualAssets,
        IEngineeringExchangeService exchange)
    {
        _packages = packages;
        _assets = assets;
        _visualAssets = visualAssets;
        _exchange = exchange;
    }

    public ReusableLibraryIncorporationPlan Plan(
        ReadOnlyMemory<byte> libraryBytes,
        ReusableLibraryIncorporationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.ResourceId == Guid.Empty)
            throw new InvalidDataException("Reusable resource incorporation requires a stable non-empty resource ID.");
        if (!ReusableLibraryResourceKinds.Supported.Contains(selection.Kind))
            throw new InvalidDataException($"Unsupported reusable resource kind '{selection.Kind}'.");

        var inspection = _packages.Inspect(libraryBytes);
        var closure = ResolveClosure(inspection.Manifest, selection);

        using var input = new MemoryStream(libraryBytes.ToArray(), writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false);

        var templates = new List<EquipmentTemplateEngineeringDto>();
        var dynamos = new List<DynamoEngineeringDto>();
        var visualAssets = new List<VisualAssetEngineeringDto>();
        var visualPayloads = new Dictionary<string, VisualAssetPayload>(StringComparer.OrdinalIgnoreCase);
        var deduplicated = 0;

        foreach (var resource in closure)
        {
            if (!IncorporationEnabled.Contains(resource.Kind))
                throw new InvalidDataException(
                    $"Reusable resource kind '{resource.Kind}' is not incorporation-enabled yet.");

            var payloadFile = inspection.Manifest.Files.SingleOrDefault(file => file.Path == resource.PayloadPath)
                ?? throw new InvalidDataException(
                    $"Reusable resource '{resource.Kind}:{resource.ResourceId:D}' payload is missing from the manifest.");
            var payloadBytes = ReadVerifiedEntry(archive, payloadFile, ReusableLibraryPackageService.MaximumResourceBytes);

            switch (resource.Kind)
            {
                case ReusableLibraryResourceKinds.EquipmentTemplate:
                {
                    RequireNoDependencies(resource);
                    var template = Deserialize<EquipmentTemplateEngineeringDto>(payloadBytes, resource);
                    if (ShouldCreateTemplate(resource, template))
                    {
                        templates.Add(template with
                        {
                            Metadata = ReusableLibraryProvenance.Stamp(
                                template.Metadata,
                                inspection.Manifest,
                                resource)
                        });
                    }
                    else
                    {
                        deduplicated++;
                    }
                    break;
                }
                case ReusableLibraryResourceKinds.Dynamo:
                {
                    var dynamo = Deserialize<DynamoEngineeringDto>(payloadBytes, resource);
                    ReusableDynamoDependencyAnalyzer.ValidateDeclaredDependencies(
                        dynamo,
                        resource,
                        inspection.Manifest);
                    if (ShouldCreateDynamo(resource, dynamo))
                    {
                        dynamos.Add(dynamo with
                        {
                            Metadata = ReusableLibraryProvenance.Stamp(
                                dynamo.Metadata,
                                inspection.Manifest,
                                resource)
                        });
                    }
                    else
                    {
                        deduplicated++;
                    }
                    break;
                }
                case ReusableLibraryResourceKinds.VisualAsset:
                {
                    RequireNoDependencies(resource);
                    var asset = Deserialize<VisualAssetEngineeringDto>(payloadBytes, resource);
                    var sidecarPath = $"assets/{asset.Sha256.ToLowerInvariant()}";
                    var sidecarFile = inspection.Manifest.Files.SingleOrDefault(file => file.Path == sidecarPath)
                        ?? throw new InvalidDataException($"Visual asset '{asset.Key}' sidecar '{sidecarPath}' is missing.");
                    var sidecarBytes = ReadVerifiedEntry(
                        archive,
                        sidecarFile,
                        ReusableLibraryPackageService.MaximumAssetBytes);
                    var visualPayload = VisualAssetPayload.Create(asset.MediaType, sidecarBytes);
                    if (!visualPayload.Sha256.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase) ||
                        visualPayload.ByteLength != asset.ByteLength)
                        throw new InvalidDataException($"Visual asset '{asset.Key}' sidecar does not match canonical metadata.");

                    if (ShouldCreateVisualAsset(resource, asset, visualPayload))
                    {
                        visualAssets.Add(asset with
                        {
                            Sha256 = asset.Sha256.ToLowerInvariant(),
                            Metadata = ReusableLibraryProvenance.Stamp(
                                asset.Metadata,
                                inspection.Manifest,
                                resource)
                        });
                        visualPayloads[visualPayload.Sha256] = visualPayload;
                    }
                    else
                    {
                        deduplicated++;
                    }
                    break;
                }
                default:
                    throw new InvalidDataException($"Unsupported reusable resource kind '{resource.Kind}'.");
            }
        }

        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Templates: templates,
            Dynamos: dynamos,
            VisualAssets: visualAssets);
        var context = new EngineeringImportContext(visualPayloads);
        var preview = _exchange.Preview(package, ImportMode.CreateOnly, context);
        if (!preview.CanApply)
        {
            var firstError = preview.Items
                .SelectMany(item => item.Issues)
                .FirstOrDefault(issue => issue.IsError);
            throw new InvalidDataException(
                firstError is null
                    ? "Reusable resource incorporation failed canonical Engineering validation."
                    : $"Reusable resource incorporation failed canonical Engineering validation: {firstError.Code}: {firstError.Message}");
        }

        return new ReusableLibraryIncorporationPlan(
            inspection.Manifest,
            selection,
            closure,
            package,
            context,
            preview,
            deduplicated);
    }

    private static void RequireNoDependencies(ReusableLibraryResourceEntry resource)
    {
        if ((resource.Dependencies?.Count ?? 0) != 0)
            throw new InvalidDataException(
                $"Reusable resource '{resource.Kind}:{resource.ResourceId:D}' cannot declare dependencies in its canonical library model.");
    }

    private IReadOnlyCollection<ReusableLibraryResourceEntry> ResolveClosure(
        ReusableLibraryManifest manifest,
        ReusableLibraryIncorporationSelection selection)
    {
        var byIdentity = manifest.Resources.ToDictionary(
            resource => (resource.Kind, resource.ResourceId),
            resource => resource);
        if (!byIdentity.TryGetValue((selection.Kind, selection.ResourceId), out var selected))
            throw new KeyNotFoundException(
                $"Reusable resource '{selection.Kind}:{selection.ResourceId:D}' was not found in the associated library.");

        var state = new Dictionary<(string Kind, Guid ResourceId), int>();
        var ordered = new List<ReusableLibraryResourceEntry>();

        void Visit(ReusableLibraryResourceEntry resource)
        {
            var identity = (resource.Kind, resource.ResourceId);
            if (state.TryGetValue(identity, out var existingState))
            {
                if (existingState == 1)
                    throw new InvalidDataException(
                        $"Reusable library dependency cycle detected at '{resource.Kind}:{resource.ResourceId:D}'.");
                if (existingState == 2) return;
            }

            state[identity] = 1;
            foreach (var dependency in resource.Dependencies ?? Array.Empty<ReusableLibraryDependency>())
            {
                if (!byIdentity.TryGetValue((dependency.Kind, dependency.ResourceId), out var target))
                    throw new InvalidDataException(
                        $"Reusable dependency '{dependency.Kind}:{dependency.ResourceId:D}' does not resolve inside the library.");
                Visit(target);
            }
            state[identity] = 2;
            ordered.Add(resource);
        }

        Visit(selected);
        return ordered;
    }

    private bool ShouldCreateTemplate(
        ReusableLibraryResourceEntry resource,
        EquipmentTemplateEngineeringDto incoming)
    {
        var byId = _assets.FindTemplate(resource.ResourceId);
        var byKey = _assets.FindTemplateByKey(incoming.Key);
        if (byId is null && byKey is null) return true;

        if (byId is null || byKey is null || byId.Id != incoming.Id || byKey.Id != incoming.Id)
            throw Conflict(resource, "stable ID/key collision");
        if (!SemanticEquals(
                byId with { Metadata = ReusableLibraryProvenance.WithoutOrigin(byId.Metadata) },
                incoming with { Metadata = ReusableLibraryProvenance.WithoutOrigin(incoming.Metadata) }))
            throw Conflict(resource, "same identity has different canonical content");
        return false;
    }

    private bool ShouldCreateDynamo(
        ReusableLibraryResourceEntry resource,
        DynamoEngineeringDto incoming)
    {
        var byId = _assets.FindDynamo(resource.ResourceId);
        var byKey = _assets.FindDynamoByKey(incoming.Key);
        if (byId is null && byKey is null) return true;

        if (byId is null || byKey is null || byId.Id != incoming.Id || byKey.Id != incoming.Id)
            throw Conflict(resource, "stable ID/key collision");
        if (!SemanticEquals(
                byId with { Metadata = ReusableLibraryProvenance.WithoutOrigin(byId.Metadata) },
                incoming with { Metadata = ReusableLibraryProvenance.WithoutOrigin(incoming.Metadata) }))
            throw Conflict(resource, "same identity has different canonical content");
        return false;
    }

    private bool ShouldCreateVisualAsset(
        ReusableLibraryResourceEntry resource,
        VisualAssetEngineeringDto incoming,
        VisualAssetPayload incomingPayload)
    {
        var byId = _visualAssets.FindAsset(resource.ResourceId);
        var byKey = _visualAssets.FindAssetByKey(incoming.Key);
        if (byId is null && byKey is null) return true;

        if (byId is null || byKey is null || byId.Id != incoming.Id || byKey.Id != incoming.Id)
            throw Conflict(resource, "stable ID/key collision");
        if (!SemanticEquals(
                byId with { Metadata = ReusableLibraryProvenance.WithoutOrigin(byId.Metadata) },
                incoming with { Metadata = ReusableLibraryProvenance.WithoutOrigin(incoming.Metadata) }))
            throw Conflict(resource, "same identity has different canonical metadata");

        var existingPayload = _visualAssets.FindPayload(incoming.Sha256);
        if (existingPayload is null ||
            !existingPayload.MediaType.Equals(incomingPayload.MediaType, StringComparison.OrdinalIgnoreCase) ||
            existingPayload.ByteLength != incomingPayload.ByteLength ||
            !existingPayload.Content.AsSpan().SequenceEqual(incomingPayload.Content))
            throw Conflict(resource, "same identity does not own the same verified visual payload");
        return false;
    }

    private ReusableLibraryIncorporationConflictException Conflict(
        ReusableLibraryResourceEntry resource,
        string reason) =>
        new(resource.Kind, resource.ResourceId, resource.SourceKey, reason);

    private bool SemanticEquals<T>(T left, T right)
    {
        var leftElement = JsonSerializer.SerializeToElement(left, _json);
        var rightElement = JsonSerializer.SerializeToElement(right, _json);
        return JsonElement.DeepEquals(leftElement, rightElement);
    }

    private T Deserialize<T>(byte[] bytes, ReusableLibraryResourceEntry resource) =>
        JsonSerializer.Deserialize<T>(bytes, _json)
        ?? throw new InvalidDataException(
            $"Reusable resource '{resource.Kind}:{resource.ResourceId:D}' payload is empty or invalid.");

    private static byte[] ReadVerifiedEntry(
        ZipArchive archive,
        ReusableLibraryFileEntry file,
        int maximumBytes)
    {
        var entry = archive.GetEntry(file.Path)
            ?? throw new InvalidDataException($"Reusable library file '{file.Path}' is missing.");
        if (entry.Length < 0 || entry.Length > maximumBytes || file.Length > maximumBytes)
            throw new InvalidDataException($"Reusable library file '{file.Path}' exceeds its safety limit.");

        using var source = entry.Open();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            total = checked(total + read);
            if (total > maximumBytes)
                throw new InvalidDataException($"Reusable library file '{file.Path}' exceeds its safety limit.");
            output.Write(buffer, 0, read);
        }

        var bytes = output.ToArray();
        if (bytes.LongLength != file.Length)
            throw new InvalidDataException($"Reusable library file '{file.Path}' length does not match the manifest.");
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Reusable library file '{file.Path}' SHA-256 does not match the manifest.");
        return bytes;
    }
}
