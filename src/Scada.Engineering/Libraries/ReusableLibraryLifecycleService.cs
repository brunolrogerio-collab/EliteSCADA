using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Scripts;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;

namespace Scada.Engineering.Libraries;

public sealed record ReusableLibraryLifecycleStatus(
    string Kind,
    Guid ProjectResourceId,
    ReusableLibraryResourceUpdateEngineeringDto Update,
    string? Reason = null);

public sealed record ReusableLibraryComparisonChange(
    string Property,
    string? CurrentValue,
    string? SourceValue);

public sealed record ReusableLibraryComparison(
    ReusableLibraryLifecycleStatus Status,
    IReadOnlyCollection<ReusableLibraryComparisonChange> Changes);

public sealed record ReusableLibraryUpgradePlan(
    ReusableLibraryManifest Manifest,
    ReusableLibraryResourceEntry Root,
    IReadOnlyCollection<ReusableLibraryResourceEntry> DependencyClosure,
    IReadOnlyCollection<ReusableLibraryLifecycleStatus> ResourceStates,
    EngineeringPackage Engineering,
    EngineeringImportContext ImportContext,
    ImportPreview CanonicalPreview,
    string TargetFingerprint)
{
    public bool CanApply =>
        CanonicalPreview.CanApply &&
        ResourceStates.All(state =>
            state.Update.State is ReusableLibraryUpdateState.UpToDate
                or ReusableLibraryUpdateState.UpdateAvailable);
}

/// <summary>
/// R2 backend lifecycle for project-owned definitions that retain reusable-library
/// provenance. Runtime never reads this service or the external .escadalib.
/// Upgrade preserves stable project identities and refuses to overwrite locally
/// divergent definitions.
/// </summary>
public sealed class ReusableLibraryLifecycleService
{
    private readonly IReusableLibraryPackageService _packages;
    private readonly IEngineeringAssetRegistry _assets;
    private readonly IEngineeringViewRegistry _views;
    private readonly IScriptEngineeringRegistry _scripts;
    private readonly IVisualAssetEngineeringRegistry _visualAssets;
    private readonly IEngineeringExchangeService _exchange;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    private readonly JsonSerializerOptions _fingerprintJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public ReusableLibraryLifecycleService(
        IReusableLibraryPackageService packages,
        IEngineeringAssetRegistry assets,
        IEngineeringViewRegistry views,
        IScriptEngineeringRegistry scripts,
        IVisualAssetEngineeringRegistry visualAssets,
        IEngineeringExchangeService exchange)
    {
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _views = views ?? throw new ArgumentNullException(nameof(views));
        _scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
        _visualAssets = visualAssets ?? throw new ArgumentNullException(nameof(visualAssets));
        _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
    }

    public ReusableLibraryLifecycleStatus Evaluate(
        string kind,
        Guid projectResourceId,
        ReadOnlyMemory<byte>? sourceLibraryBytes)
    {
        var current = GetProjectResource(kind, projectResourceId)
            ?? throw new KeyNotFoundException(
                $"Project reusable resource '{kind}:{projectResourceId:D}' was not found.");

        if (!ReusableLibraryProvenance.TryRead(GetMetadata(current), out var provenance))
        {
            return Status(
                kind,
                projectResourceId,
                new ReusableLibrarySourceProvenanceEngineeringDto(
                    Guid.Empty,
                    projectResourceId,
                    string.Empty,
                    string.Empty),
                ReusableLibraryUpdateState.Incompatible,
                CurrentContentHash(current),
                "Project resource is detached or has no valid reusable-library provenance.");
        }

        var currentHash = CurrentContentHash(current);
        if (!sourceLibraryBytes.HasValue || sourceLibraryBytes.Value.IsEmpty)
        {
            return Status(
                kind,
                projectResourceId,
                provenance,
                ReusableLibraryUpdateState.SourceMissing,
                currentHash,
                "Source library is not currently available.");
        }

        var inspection = _packages.Inspect(sourceLibraryBytes.Value);
        if (inspection.Manifest.LibraryId != provenance.SourceLibraryId)
        {
            return Status(
                kind,
                projectResourceId,
                provenance,
                ReusableLibraryUpdateState.Incompatible,
                currentHash,
                "Associated library identity does not match persisted provenance.");
        }

        var sourceResource = inspection.Manifest.Resources.SingleOrDefault(resource =>
            resource.Kind == kind && resource.ResourceId == provenance.SourceResourceId);
        if (sourceResource is null)
        {
            return Status(
                kind,
                projectResourceId,
                provenance,
                ReusableLibraryUpdateState.SourceMissing,
                currentHash,
                "Source resource no longer exists in the associated library.");
        }

        if (!currentHash.Equals(provenance.SourceContentHash, StringComparison.OrdinalIgnoreCase))
        {
            return Status(
                kind,
                projectResourceId,
                provenance,
                ReusableLibraryUpdateState.LocallyModified,
                currentHash,
                "Project-owned definition diverged from the last incorporated source content.");
        }

        var sourceHash = PayloadHash(inspection.Manifest, sourceResource);
        var state = sourceHash.Equals(provenance.SourceContentHash, StringComparison.OrdinalIgnoreCase)
            ? ReusableLibraryUpdateState.UpToDate
            : ReusableLibraryUpdateState.UpdateAvailable;

        return Status(
            kind,
            projectResourceId,
            provenance,
            state,
            currentHash,
            state == ReusableLibraryUpdateState.UpdateAvailable
                ? $"Source library version '{inspection.Manifest.Version}' exposes different canonical content."
                : null);
    }

    public ReusableLibraryComparison Compare(
        string kind,
        Guid projectResourceId,
        ReadOnlyMemory<byte> sourceLibraryBytes)
    {
        var status = Evaluate(kind, projectResourceId, sourceLibraryBytes);
        if (status.Update.Source.SourceLibraryId == Guid.Empty)
            return new ReusableLibraryComparison(status, Array.Empty<ReusableLibraryComparisonChange>());

        var inspection = _packages.Inspect(sourceLibraryBytes);
        var resource = inspection.Manifest.Resources.SingleOrDefault(item =>
            item.Kind == kind &&
            item.ResourceId == status.Update.Source.SourceResourceId);
        if (resource is null)
            return new ReusableLibraryComparison(status, Array.Empty<ReusableLibraryComparisonChange>());

        var current = GetProjectResource(kind, projectResourceId)!;
        var currentJson = JsonSerializer.SerializeToElement(WithoutOrigin(current), current.GetType(), _json);
        var sourceBytes = ReadResourcePayload(sourceLibraryBytes, inspection.Manifest, resource);
        using var sourceDocument = JsonDocument.Parse(sourceBytes);

        return new ReusableLibraryComparison(
            status,
            CompareTopLevel(currentJson, sourceDocument.RootElement));
    }

    public ReusableLibraryLifecycleStatus KeepCurrent(
        string kind,
        Guid projectResourceId,
        ReadOnlyMemory<byte>? sourceLibraryBytes) =>
        Evaluate(kind, projectResourceId, sourceLibraryBytes);

    public ReusableLibraryLifecycleStatus ForkDetach(string kind, Guid projectResourceId)
    {
        var current = GetProjectResource(kind, projectResourceId)
            ?? throw new KeyNotFoundException(
                $"Project reusable resource '{kind}:{projectResourceId:D}' was not found.");

        var detached = WithoutOrigin(current);
        UpsertProjectResource(kind, detached);

        return Status(
            kind,
            projectResourceId,
            new ReusableLibrarySourceProvenanceEngineeringDto(
                Guid.Empty,
                projectResourceId,
                string.Empty,
                string.Empty),
            ReusableLibraryUpdateState.Incompatible,
            CurrentContentHash(detached),
            "Fork/Detach preserved project content and removed the update relationship.");
    }

    public ReusableLibraryUpgradePlan PreviewUpgrade(
        ReadOnlyMemory<byte> sourceLibraryBytes,
        ReusableLibraryIncorporationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var inspection = _packages.Inspect(sourceLibraryBytes);
        var closure = ResolveClosure(inspection.Manifest, selection);
        var target = _exchange.ExportPackage();

        using var input = new MemoryStream(sourceLibraryBytes.ToArray(), writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false);

        var templates = new List<EquipmentTemplateEngineeringDto>();
        var dynamos = new List<DynamoEngineeringDto>();
        var screens = new List<ScreenEngineeringDto>();
        var popups = new List<PopupEngineeringDto>();
        var scripts = new List<ScriptEngineeringDefinition>();
        var visualAssets = new List<VisualAssetEngineeringDto>();
        var visualPayloads = new Dictionary<string, VisualAssetPayload>(StringComparer.OrdinalIgnoreCase);
        var states = new List<ReusableLibraryLifecycleStatus>();

        foreach (var resource in closure)
        {
            var incoming = ReadAndStampResource(sourceLibraryBytes, inspection.Manifest, resource);
            var existing = GetProjectResource(resource.Kind, resource.ResourceId);

            if (existing is not null)
            {
                var state = Evaluate(resource.Kind, resource.ResourceId, sourceLibraryBytes);
                states.Add(state);
                if (state.Update.State == ReusableLibraryUpdateState.UpToDate)
                    continue;
                if (state.Update.State != ReusableLibraryUpdateState.UpdateAvailable)
                    continue;
            }
            else
            {
                var collision = FindByKey(resource.Kind, resource.SourceKey);
                if (collision is not null)
                {
                    states.Add(Status(
                        resource.Kind,
                        resource.ResourceId,
                        Provenance(inspection.Manifest, resource),
                        ReusableLibraryUpdateState.Incompatible,
                        null,
                        $"Target key/path '{resource.SourceKey}' is already owned by a different stable identity."));
                    continue;
                }

                states.Add(Status(
                    resource.Kind,
                    resource.ResourceId,
                    Provenance(inspection.Manifest, resource),
                    ReusableLibraryUpdateState.UpdateAvailable,
                    null,
                    "New dependency will be incorporated as project-owned content."));
            }

            switch (incoming)
            {
                case EquipmentTemplateEngineeringDto value:
                    templates.Add(value);
                    break;
                case DynamoEngineeringDto value:
                    dynamos.Add(value);
                    break;
                case ScreenEngineeringDto value:
                    screens.Add(value);
                    break;
                case PopupEngineeringDto value:
                    popups.Add(value);
                    break;
                case ScriptEngineeringDefinition value:
                    scripts.Add(value);
                    break;
                case VisualAssetEngineeringDto value:
                {
                    visualAssets.Add(value);
                    var sidecar = inspection.Manifest.Files.Single(file =>
                        file.Path == $"assets/{value.Sha256.ToLowerInvariant()}");
                    var bytes = ReadVerifiedEntry(
                        archive,
                        sidecar,
                        ReusableLibraryPackageService.MaximumAssetBytes);
                    var payload = VisualAssetPayload.Create(value.MediaType, bytes);
                    if (!payload.Sha256.Equals(value.Sha256, StringComparison.OrdinalIgnoreCase) ||
                        payload.ByteLength != value.ByteLength)
                        throw new InvalidDataException(
                            $"Reusable visual asset '{value.Key}' sidecar does not match canonical metadata.");
                    visualPayloads[payload.Sha256] = payload;
                    break;
                }
                default:
                    states.Add(Status(
                        resource.Kind,
                        resource.ResourceId,
                        Provenance(inspection.Manifest, resource),
                        ReusableLibraryUpdateState.Incompatible,
                        null,
                        "Reusable resource kind is not upgrade-enabled."));
                    break;
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
            Screens: screens,
            Popups: popups,
            Scripts: scripts,
            VisualAssets: visualAssets,
            StartupScreenId: target.StartupScreenId,
            EngineeringLock: target.EngineeringLock,
            AuthorityPolicyReference: target.AuthorityPolicyReference);

        var context = new EngineeringImportContext(visualPayloads);
        var preview = _exchange.Preview(package, ImportMode.CreateAndUpdate, context);

        return new ReusableLibraryUpgradePlan(
            inspection.Manifest,
            closure.Last(),
            closure,
            states,
            package,
            context,
            preview,
            TargetFingerprint(target));
    }

    public ImportResult ApplyUpgrade(ReusableLibraryUpgradePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.CanApply)
        {
            var stateIssues = plan.ResourceStates
                .Where(state => state.Update.State is not (
                    ReusableLibraryUpdateState.UpToDate or ReusableLibraryUpdateState.UpdateAvailable))
                .Select(state => new ImportIssue(
                    "LIBRARY_UPGRADE_BLOCKED",
                    state.Reason ?? $"Library resource state '{state.Update.State}' blocks upgrade.",
                    ToImportKind(state.Kind),
                    state.ProjectResourceId.ToString("D"),
                    true));

            return new ImportResult(
                ImportMode.CreateAndUpdate,
                0,
                0,
                plan.CanonicalPreview.SkipCount,
                stateIssues
                    .Concat(plan.CanonicalPreview.Items.SelectMany(item => item.Issues).Where(issue => issue.IsError))
                    .ToArray());
        }

        if (!TargetFingerprint(_exchange.ExportPackage()).Equals(plan.TargetFingerprint, StringComparison.Ordinal))
        {
            return new ImportResult(
                ImportMode.CreateAndUpdate,
                0,
                0,
                0,
                [new ImportIssue(
                    "LIBRARY_UPGRADE_PREVIEW_STALE",
                    "Engineering Working changed after Library Upgrade Preview. Re-run Preview before Apply.",
                    ToImportKind(plan.Root.Kind),
                    plan.Root.ResourceId.ToString("D"),
                    true)]);
        }

        return _exchange.Apply(plan.Engineering, ImportMode.CreateAndUpdate, plan.ImportContext);
    }

    private object ReadAndStampResource(
        ReadOnlyMemory<byte> libraryBytes,
        ReusableLibraryManifest manifest,
        ReusableLibraryResourceEntry resource)
    {
        var payload = ReadResourcePayload(libraryBytes, manifest, resource);
        var provenance = Provenance(manifest, resource);

        return resource.Kind switch
        {
            ReusableLibraryResourceKinds.EquipmentTemplate =>
                Stamp(JsonSerializer.Deserialize<EquipmentTemplateEngineeringDto>(payload, _json)!, provenance, resource.Kind),
            ReusableLibraryResourceKinds.Dynamo =>
                Stamp(JsonSerializer.Deserialize<DynamoEngineeringDto>(payload, _json)!, provenance, resource.Kind),
            ReusableLibraryResourceKinds.Screen =>
                Stamp(JsonSerializer.Deserialize<ScreenEngineeringDto>(payload, _json)!, provenance, resource.Kind),
            ReusableLibraryResourceKinds.Popup =>
                Stamp(JsonSerializer.Deserialize<PopupEngineeringDto>(payload, _json)!, provenance, resource.Kind),
            ReusableLibraryResourceKinds.Script =>
                Stamp(JsonSerializer.Deserialize<ScriptEngineeringDefinition>(payload, _json)!, provenance, resource.Kind),
            ReusableLibraryResourceKinds.VisualAsset =>
                Stamp(JsonSerializer.Deserialize<VisualAssetEngineeringDto>(payload, _json)!, provenance, resource.Kind),
            _ => throw new InvalidDataException(
                $"Reusable resource kind '{resource.Kind}' is not upgrade-enabled.")
        };
    }

    private static EquipmentTemplateEngineeringDto Stamp(
        EquipmentTemplateEngineeringDto value,
        ReusableLibrarySourceProvenanceEngineeringDto provenance,
        string kind) =>
        value with { Metadata = ReusableLibraryProvenance.Stamp(value.Metadata, provenance, kind) };

    private static DynamoEngineeringDto Stamp(
        DynamoEngineeringDto value,
        ReusableLibrarySourceProvenanceEngineeringDto provenance,
        string kind) =>
        value with { Metadata = ReusableLibraryProvenance.Stamp(value.Metadata, provenance, kind) };

    private static ScreenEngineeringDto Stamp(
        ScreenEngineeringDto value,
        ReusableLibrarySourceProvenanceEngineeringDto provenance,
        string kind) =>
        value with { Metadata = ReusableLibraryProvenance.Stamp(value.Metadata, provenance, kind) };

    private static PopupEngineeringDto Stamp(
        PopupEngineeringDto value,
        ReusableLibrarySourceProvenanceEngineeringDto provenance,
        string kind) =>
        value with { Metadata = ReusableLibraryProvenance.Stamp(value.Metadata, provenance, kind) };

    private static VisualAssetEngineeringDto Stamp(
        VisualAssetEngineeringDto value,
        ReusableLibrarySourceProvenanceEngineeringDto provenance,
        string kind) =>
        value with { Metadata = ReusableLibraryProvenance.Stamp(value.Metadata, provenance, kind) };

    private static ScriptEngineeringDefinition Stamp(
        ScriptEngineeringDefinition value,
        ReusableLibrarySourceProvenanceEngineeringDto provenance,
        string kind) =>
        ReusableScriptDependencyAnalyzer.WithMetadata(
            value,
            ReusableLibraryProvenance.Stamp(value.Metadata, provenance, kind));

    private object? GetProjectResource(string kind, Guid id) =>
        kind switch
        {
            ReusableLibraryResourceKinds.EquipmentTemplate => _assets.FindTemplate(id),
            ReusableLibraryResourceKinds.Dynamo => _assets.FindDynamo(id),
            ReusableLibraryResourceKinds.Screen => _views.FindScreen(id),
            ReusableLibraryResourceKinds.Popup => _views.FindPopup(id),
            ReusableLibraryResourceKinds.Script => _scripts.Find(id),
            ReusableLibraryResourceKinds.VisualAsset => _visualAssets.FindAsset(id),
            _ => null
        };

    private object? FindByKey(string kind, string key) =>
        kind switch
        {
            ReusableLibraryResourceKinds.EquipmentTemplate => _assets.FindTemplateByKey(key),
            ReusableLibraryResourceKinds.Dynamo => _assets.FindDynamoByKey(key),
            ReusableLibraryResourceKinds.Screen => _views.FindScreenByKey(key),
            ReusableLibraryResourceKinds.Popup => _views.FindPopupByKey(key),
            ReusableLibraryResourceKinds.Script => _scripts.FindByPath(key),
            ReusableLibraryResourceKinds.VisualAsset => _visualAssets.FindAssetByKey(key),
            _ => null
        };

    private void UpsertProjectResource(string kind, object value)
    {
        switch (kind)
        {
            case ReusableLibraryResourceKinds.EquipmentTemplate:
                _assets.UpsertTemplate((EquipmentTemplateEngineeringDto)value);
                return;
            case ReusableLibraryResourceKinds.Dynamo:
                _assets.UpsertDynamo((DynamoEngineeringDto)value);
                return;
            case ReusableLibraryResourceKinds.Screen:
                _views.UpsertScreen((ScreenEngineeringDto)value);
                return;
            case ReusableLibraryResourceKinds.Popup:
                _views.UpsertPopup((PopupEngineeringDto)value);
                return;
            case ReusableLibraryResourceKinds.Script:
                _scripts.Upsert((ScriptEngineeringDefinition)value);
                return;
            case ReusableLibraryResourceKinds.VisualAsset:
                _visualAssets.UpsertAsset((VisualAssetEngineeringDto)value);
                return;
            default:
                throw new InvalidDataException($"Unsupported reusable resource kind '{kind}'.");
        }
    }

    private static IReadOnlyDictionary<string, string>? GetMetadata(object value) =>
        value switch
        {
            EquipmentTemplateEngineeringDto item => item.Metadata,
            DynamoEngineeringDto item => item.Metadata,
            ScreenEngineeringDto item => item.Metadata,
            PopupEngineeringDto item => item.Metadata,
            ScriptEngineeringDefinition item => item.Metadata,
            VisualAssetEngineeringDto item => item.Metadata,
            _ => null
        };

    private static object WithoutOrigin(object value) =>
        value switch
        {
            EquipmentTemplateEngineeringDto item => item with { Metadata = ReusableLibraryProvenance.WithoutOrigin(item.Metadata) },
            DynamoEngineeringDto item => item with { Metadata = ReusableLibraryProvenance.WithoutOrigin(item.Metadata) },
            ScreenEngineeringDto item => item with { Metadata = ReusableLibraryProvenance.WithoutOrigin(item.Metadata) },
            PopupEngineeringDto item => item with { Metadata = ReusableLibraryProvenance.WithoutOrigin(item.Metadata) },
            ScriptEngineeringDefinition item => ReusableScriptDependencyAnalyzer.WithMetadata(
                item,
                ReusableLibraryProvenance.WithoutOrigin(item.Metadata)),
            VisualAssetEngineeringDto item => item with { Metadata = ReusableLibraryProvenance.WithoutOrigin(item.Metadata) },
            _ => value
        };

    private string CurrentContentHash(object value) =>
        Hash(JsonSerializer.SerializeToUtf8Bytes(WithoutOrigin(value), value.GetType(), _json));

    private string TargetFingerprint(EngineeringPackage target)
    {
        var canonical = target with { ExportedAt = default };
        return Hash(JsonSerializer.SerializeToUtf8Bytes(canonical, _fingerprintJson));
    }

    private static ReusableLibraryLifecycleStatus Status(
        string kind,
        Guid projectResourceId,
        ReusableLibrarySourceProvenanceEngineeringDto provenance,
        ReusableLibraryUpdateState state,
        string? currentHash,
        string? reason) =>
        new(
            kind,
            projectResourceId,
            new ReusableLibraryResourceUpdateEngineeringDto(
                provenance,
                state,
                currentHash),
            reason);

    private static ReusableLibrarySourceProvenanceEngineeringDto Provenance(
        ReusableLibraryManifest manifest,
        ReusableLibraryResourceEntry resource) =>
        new(
            manifest.LibraryId,
            resource.ResourceId,
            manifest.Version,
            PayloadHash(manifest, resource));

    private static string PayloadHash(
        ReusableLibraryManifest manifest,
        ReusableLibraryResourceEntry resource) =>
        manifest.Files.Single(file => file.Path == resource.PayloadPath).Sha256.ToLowerInvariant();

    private static IReadOnlyCollection<ReusableLibraryResourceEntry> ResolveClosure(
        ReusableLibraryManifest manifest,
        ReusableLibraryIncorporationSelection selection)
    {
        var byIdentity = manifest.Resources.ToDictionary(
            resource => (resource.Kind, resource.ResourceId),
            resource => resource);
        if (!byIdentity.TryGetValue((selection.Kind, selection.ResourceId), out var selected))
            throw new KeyNotFoundException(
                $"Reusable resource '{selection.Kind}:{selection.ResourceId:D}' was not found in the associated library.");

        var state = new Dictionary<(string Kind, Guid ResourceId), byte>();
        var ordered = new List<ReusableLibraryResourceEntry>();

        void Visit(ReusableLibraryResourceEntry resource)
        {
            var identity = (resource.Kind, resource.ResourceId);
            if (state.TryGetValue(identity, out var current))
            {
                if (current == 1)
                    throw new InvalidDataException(
                        $"Reusable library dependency cycle detected at '{resource.Kind}:{resource.ResourceId:D}'.");
                if (current == 2)
                    return;
            }

            state[identity] = 1;
            foreach (var dependency in resource.Dependencies
                         .OrderBy(item => item.Kind, StringComparer.Ordinal)
                         .ThenBy(item => item.ResourceId))
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

    private byte[] ReadResourcePayload(
        ReadOnlyMemory<byte> libraryBytes,
        ReusableLibraryManifest manifest,
        ReusableLibraryResourceEntry resource)
    {
        var file = manifest.Files.Single(entry => entry.Path == resource.PayloadPath);
        using var input = new MemoryStream(libraryBytes.ToArray(), writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: false);
        return ReadVerifiedEntry(
            archive,
            file,
            ReusableLibraryPackageService.MaximumResourceBytes);
    }

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
        var hash = Hash(bytes);
        if (!hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Reusable library file '{file.Path}' SHA-256 does not match the manifest.");
        return bytes;
    }

    private static IReadOnlyCollection<ReusableLibraryComparisonChange> CompareTopLevel(
        JsonElement current,
        JsonElement source)
    {
        var currentProperties = current.ValueKind == JsonValueKind.Object
            ? current.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.Clone(), StringComparer.Ordinal)
            : new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var sourceProperties = source.ValueKind == JsonValueKind.Object
            ? source.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.Clone(), StringComparer.Ordinal)
            : new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        return currentProperties.Keys
            .Concat(sourceProperties.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .Where(key =>
                !currentProperties.TryGetValue(key, out var left) ||
                !sourceProperties.TryGetValue(key, out var right) ||
                !JsonElement.DeepEquals(left, right))
            .Select(key => new ReusableLibraryComparisonChange(
                key,
                currentProperties.TryGetValue(key, out var left) ? left.GetRawText() : null,
                sourceProperties.TryGetValue(key, out var right) ? right.GetRawText() : null))
            .ToArray();
    }

    private static ImportEntityKind ToImportKind(string kind) =>
        kind switch
        {
            ReusableLibraryResourceKinds.EquipmentTemplate => ImportEntityKind.Template,
            ReusableLibraryResourceKinds.Dynamo => ImportEntityKind.Dynamo,
            ReusableLibraryResourceKinds.Screen => ImportEntityKind.Screen,
            ReusableLibraryResourceKinds.Popup => ImportEntityKind.Popup,
            ReusableLibraryResourceKinds.Script => ImportEntityKind.Script,
            ReusableLibraryResourceKinds.VisualAsset => ImportEntityKind.VisualAsset,
            _ => ImportEntityKind.Template
        };

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
