using System.Security.Cryptography;
using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.Interactions;
using Scada.Engineering.Libraries;
using Scada.Engineering.Reports;
using Scada.Engineering.Scripts;
using Scada.Engineering.VisualAssets;

namespace Scada.Engineering.ImportExport;

public sealed record EngineeringFragmentPlan(
    EngineeringFragmentEnvelope Source,
    IReadOnlyCollection<EngineeringFragmentPreviewItem> Items,
    EngineeringPackage Engineering,
    EngineeringImportContext ImportContext,
    ImportPreview CanonicalPreview,
    string TargetFingerprint,
    string PlanFingerprint)
{
    public bool CanApply =>
        CanonicalPreview.CanApply &&
        Items.All(item => item.Operation is not EngineeringFragmentPlanOperation.Conflict
            and not EngineeringFragmentPlanOperation.Unsupported);
}

internal static class EngineeringFragmentPlanBuilder
{
    private static readonly HashSet<ImportEntityKind> UnsupportedMutationKinds =
    [
        ImportEntityKind.SecurityRole,
        ImportEntityKind.SecurityScope
    ];

    public static EngineeringFragmentPlan Build(
        EngineeringFragmentInspection inspection,
        EngineeringPackage target,
        IEngineeringExchangeService exchange,
        JsonSerializerOptions json)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(exchange);
        ArgumentNullException.ThrowIfNull(json);

        var source = inspection.Envelope.Engineering;
        var sourceReferences = EngineeringFragmentDependencyResolver
            .EnumerateEntityReferences(source)
            .OrderBy(reference => reference, EngineeringFragmentReferenceComparer.Instance)
            .ToArray();
        var targetReferences = EngineeringFragmentDependencyResolver
            .EnumerateEntityReferences(target)
            .ToArray();

        var targetById = targetReferences.ToDictionary(
            reference => (reference.EntityKind, reference.EntityId!.Value),
            reference => reference);
        var targetByKey = targetReferences.ToDictionary(
            reference => (reference.EntityKind, reference.EntityKey),
            reference => reference,
            new KindKeyComparer());

        var remaps = new Dictionary<(ImportEntityKind Kind, Guid SourceId), Guid>();
        var preConflicts = new Dictionary<(ImportEntityKind Kind, Guid Id), string>();

        foreach (var sourceReference in sourceReferences)
        {
            var sourceId = sourceReference.EntityId!.Value;
            if (targetById.TryGetValue((sourceReference.EntityKind, sourceId), out var sameId))
            {
                if (!string.Equals(
                        sourceReference.EntityKey,
                        sameId.EntityKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    preConflicts[(sourceReference.EntityKind, sourceId)] =
                        $"Stable identity '{sourceId:D}' already belongs to target '{sameId.EntityKey}'.";
                }
                continue;
            }

            if (targetByKey.TryGetValue(
                    (sourceReference.EntityKind, sourceReference.EntityKey),
                    out var sameKey))
            {
                remaps[(sourceReference.EntityKind, sourceId)] = sameKey.EntityId!.Value;
            }
        }

        var remappedSource = EngineeringFragmentRemapper.Apply(source, remaps);
        var items = new List<EngineeringFragmentPreviewItem>();
        var mutationIdentities = new HashSet<(ImportEntityKind Kind, Guid Id)>();

        foreach (var sourceReference in sourceReferences)
        {
            var sourceId = sourceReference.EntityId!.Value;
            var sourceIdentity = (sourceReference.EntityKind, sourceId);

            if (UnsupportedMutationKinds.Contains(sourceReference.EntityKind))
            {
                items.Add(new EngineeringFragmentPreviewItem(
                    sourceReference,
                    EngineeringFragmentPlanOperation.Unsupported,
                    null,
                    "The shared wire exists, but this lane does not own canonical mutation for this entity kind."));
                continue;
            }

            if (preConflicts.TryGetValue(sourceIdentity, out var preConflict))
            {
                items.Add(new EngineeringFragmentPreviewItem(
                    sourceReference,
                    EngineeringFragmentPlanOperation.Conflict,
                    targetById[(sourceReference.EntityKind, sourceId)],
                    preConflict));
                continue;
            }

            if (targetById.TryGetValue(sourceIdentity, out var sameIdTarget))
            {
                var sourceEntity = GetEntity(
                    remappedSource,
                    sourceReference.EntityKind,
                    sourceId);
                var targetEntity = GetEntity(
                    target,
                    sourceReference.EntityKind,
                    sourceId);

                if (SemanticEquals(sourceEntity, targetEntity, json))
                {
                    items.Add(new EngineeringFragmentPreviewItem(
                        sourceReference,
                        EngineeringFragmentPlanOperation.ReuseIdentical,
                        sameIdTarget,
                        "Stable identity and canonical content already match."));
                }
                else
                {
                    mutationIdentities.Add(sourceIdentity);
                    items.Add(new EngineeringFragmentPreviewItem(
                        sourceReference,
                        EngineeringFragmentPlanOperation.Update,
                        sameIdTarget,
                        "Stable identity matches but canonical content differs."));
                }
                continue;
            }

            if (remaps.TryGetValue(sourceIdentity, out var targetId))
            {
                var remappedReference = targetById[(sourceReference.EntityKind, targetId)];
                var remappedEntity = GetEntity(
                    remappedSource,
                    sourceReference.EntityKind,
                    targetId);
                var targetEntity = GetEntity(
                    target,
                    sourceReference.EntityKind,
                    targetId);

                if (SemanticEquals(remappedEntity, targetEntity, json))
                {
                    items.Add(new EngineeringFragmentPreviewItem(
                        sourceReference,
                        EngineeringFragmentPlanOperation.Remap,
                        remappedReference,
                        "Canonical key/path matches identical target content; stable references are remapped to the target-owned identity."));
                }
                else
                {
                    items.Add(new EngineeringFragmentPreviewItem(
                        sourceReference,
                        EngineeringFragmentPlanOperation.Conflict,
                        remappedReference,
                        "Canonical key/path is already owned by a different stable identity with different content."));
                }
                continue;
            }

            mutationIdentities.Add(sourceIdentity);
            items.Add(new EngineeringFragmentPreviewItem(
                sourceReference,
                EngineeringFragmentPlanOperation.Create,
                null,
                "No compatible target identity exists."));
        }

        var mappedMutationIdentities = mutationIdentities
            .Select(identity =>
            {
                var mapped = remaps.TryGetValue(identity, out var targetId) ? targetId : identity.Id;
                return (identity.Kind, Id: mapped);
            })
            .ToHashSet();

        var engineering = FilterForMutation(remappedSource, mappedMutationIdentities) with
        {
            StartupScreenId = target.StartupScreenId,
            EngineeringLock = target.EngineeringLock,
            AuthorityPolicyReference = target.AuthorityPolicyReference
        };

        var canonicalPreview = exchange.Preview(
            engineering,
            ImportMode.CreateAndUpdate,
            inspection.ImportContext);

        var targetFingerprint = Fingerprint(target, json);
        var planFingerprint = FingerprintPlan(
            inspection.Envelope,
            items,
            engineering,
            targetFingerprint,
            json);

        return new EngineeringFragmentPlan(
            inspection.Envelope,
            items,
            engineering,
            inspection.ImportContext,
            canonicalPreview,
            targetFingerprint,
            planFingerprint);
    }

    public static string Fingerprint(EngineeringPackage package, JsonSerializerOptions json)
    {
        var canonical = package with { ExportedAt = default };
        return Hash(JsonSerializer.SerializeToUtf8Bytes(canonical, json));
    }

    private static string FingerprintPlan(
        EngineeringFragmentEnvelope envelope,
        IReadOnlyCollection<EngineeringFragmentPreviewItem> items,
        EngineeringPackage engineering,
        string targetFingerprint,
        JsonSerializerOptions json)
    {
        var canonicalEnvelope = envelope with
        {
            ExportedAt = default,
            Engineering = envelope.Engineering with { ExportedAt = default }
        };
        var canonicalEngineering = engineering with { ExportedAt = default };
        return Hash(JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                envelope = canonicalEnvelope,
                items,
                engineering = canonicalEngineering,
                targetFingerprint
            },
            json));
    }

    private static EngineeringPackage FilterForMutation(
        EngineeringPackage package,
        IReadOnlySet<(ImportEntityKind Kind, Guid Id)> include)
    {
        bool Included(ImportEntityKind kind, Guid? id) =>
            id is { } value && value != Guid.Empty && include.Contains((kind, value));

        var scripts = (package.Scripts ?? Array.Empty<ScriptEngineeringDefinition>())
            .Where(script => Included(ImportEntityKind.Script, script.Id))
            .ToArray();
        var scriptIds = scripts.Select(script => script.Id).ToHashSet();

        return package with
        {
            Tags = package.Tags.Where(item => Included(ImportEntityKind.Tag, item.Id)).ToArray(),
            Alarms = package.Alarms.Where(item => Included(ImportEntityKind.Alarm, item.Id)).ToArray(),
            DataSources = (package.DataSources ?? Array.Empty<DataSourceEngineeringDto>())
                .Where(item => Included(ImportEntityKind.DataSource, item.Id)).ToArray(),
            Templates = (package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Template, item.Id)).ToArray(),
            Equipment = (package.Equipment ?? Array.Empty<EquipmentEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Equipment, item.Id)).ToArray(),
            Dynamos = (package.Dynamos ?? Array.Empty<DynamoEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Dynamo, item.Id)).ToArray(),
            Screens = (package.Screens ?? Array.Empty<ScreenEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Screen, item.Id)).ToArray(),
            Popups = (package.Popups ?? Array.Empty<PopupEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Popup, item.Id)).ToArray(),
            SecurityRoles = Array.Empty<SecurityRoleEngineeringDto>(),
            Commands = (package.Commands ?? Array.Empty<CommandEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Command, item.Id)).ToArray(),
            Gateways = (package.Gateways ?? Array.Empty<GatewayRouteEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Gateway, item.Id)).ToArray(),
            Scripts = scripts,
            ScriptVisualEventReferences = (package.ScriptVisualEventReferences ?? Array.Empty<ScriptVisualEventReference>())
                .Where(reference => scriptIds.Contains(reference.ScriptId)).ToArray(),
            VisualAssets = (package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>())
                .Where(item => Included(ImportEntityKind.VisualAsset, item.Id)).ToArray(),
            Reports = (package.Reports ?? Array.Empty<ReportEngineeringDto>())
                .Where(item => Included(ImportEntityKind.Report, item.Id)).ToArray(),
            OperationalEvents = (package.OperationalEvents ?? Array.Empty<OperationalEventEngineeringDto>())
                .Where(item => Included(ImportEntityKind.OperationalEvent, item.Id)).ToArray(),
            SecurityScopes = Array.Empty<SecurityScopeEngineeringDto>(),
            HistorianCaptureProfiles = (package.HistorianCaptureProfiles ?? Array.Empty<HistorianCaptureProfileEngineeringDto>())
                .Where(item => Included(ImportEntityKind.HistorianCaptureProfile, item.Id)).ToArray(),
            DataQueries = (package.DataQueries ?? Array.Empty<DataQueryEngineeringDto>())
                .Where(item => Included(ImportEntityKind.DataQuery, item.Id)).ToArray(),
            AlarmViews = (package.AlarmViews ?? Array.Empty<AlarmViewEngineeringDto>())
                .Where(item => Included(ImportEntityKind.AlarmView, item.Id)).ToArray(),
            RichCommandDefinitions = (package.RichCommandDefinitions ?? Array.Empty<RichCommandDefinitionEngineeringDto>())
                .Where(item => Included(ImportEntityKind.RichCommandDefinition, item.CommandId)).ToArray(),
            DriverCommandBindings = (package.DriverCommandBindings ?? Array.Empty<DriverCommandBindingEngineeringDto>())
                .Where(item => Included(ImportEntityKind.DriverCommandBinding, item.CommandId)).ToArray()
        };
    }

    private static object GetEntity(
        EngineeringPackage package,
        ImportEntityKind kind,
        Guid id) =>
        kind switch
        {
            ImportEntityKind.Tag => package.Tags.Single(item => item.Id == id),
            ImportEntityKind.Alarm => package.Alarms.Single(item => item.Id == id),
            ImportEntityKind.OperationalEvent => (package.OperationalEvents ?? Array.Empty<OperationalEventEngineeringDto>()).Single(item => item.Id == id),
            ImportEntityKind.DataSource => (package.DataSources ?? Array.Empty<DataSourceEngineeringDto>()).Single(item => item.Id == id),
            ImportEntityKind.Template => (package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>()).Single(item => item.Id == id),
            ImportEntityKind.Equipment => (package.Equipment ?? Array.Empty<EquipmentEngineeringDto>()).Single(item => item.Id == id),
            ImportEntityKind.Dynamo => (package.Dynamos ?? Array.Empty<DynamoEngineeringDto>()).Single(item => item.Id == id),
            ImportEntityKind.Screen => (package.Screens ?? Array.Empty<ScreenEngineeringDto>()).Single(item => item.Id == id),
            ImportEntityKind.Popup => (package.Popups ?? Array.Empty<PopupEngineeringDto>()).Single(item => item.Id == id),
            ImportEntityKind.Command => (package.Commands ?? Array.Empty<CommandEngineeringDto>()).Single(item => item.Id == id),
            ImportEntityKind.Gateway => (package.Gateways ?? Array.Empty<GatewayRouteEngineeringDto>()).Single(item => item.Id == id),
            ImportEntityKind.Script => (package.Scripts ?? Array.Empty<ScriptEngineeringDefinition>()).Single(item => item.Id == id),
            ImportEntityKind.VisualAsset => (package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>()).Single(item => item.Id == id),
            ImportEntityKind.Report => (package.Reports ?? Array.Empty<ReportEngineeringDto>()).Single(item => item.Id == id),
            ImportEntityKind.HistorianCaptureProfile => (package.HistorianCaptureProfiles ?? Array.Empty<HistorianCaptureProfileEngineeringDto>()).Single(item => item.Id == id),
            ImportEntityKind.DataQuery => (package.DataQueries ?? Array.Empty<DataQueryEngineeringDto>()).Single(item => item.Id == id),
            ImportEntityKind.AlarmView => (package.AlarmViews ?? Array.Empty<AlarmViewEngineeringDto>()).Single(item => item.Id == id),
            ImportEntityKind.RichCommandDefinition => (package.RichCommandDefinitions ?? Array.Empty<RichCommandDefinitionEngineeringDto>()).Single(item => item.CommandId == id),
            ImportEntityKind.DriverCommandBinding => (package.DriverCommandBindings ?? Array.Empty<DriverCommandBindingEngineeringDto>()).Single(item => item.CommandId == id),
            _ => throw new InvalidDataException($"Unsupported Fragment entity kind '{kind}'.")
        };

    private static bool SemanticEquals(object source, object target, JsonSerializerOptions json)
    {
        source = RemoveReusableOrigin(source);
        target = RemoveReusableOrigin(target);
        return JsonElement.DeepEquals(
            JsonSerializer.SerializeToElement(source, source.GetType(), json),
            JsonSerializer.SerializeToElement(target, target.GetType(), json));
    }

    private static object RemoveReusableOrigin(object value) =>
        value switch
        {
            EquipmentTemplateEngineeringDto item => item with { Metadata = ReusableLibraryProvenance.WithoutOrigin(item.Metadata) },
            DynamoEngineeringDto item => item with { Metadata = ReusableLibraryProvenance.WithoutOrigin(item.Metadata) },
            ScreenEngineeringDto item => item with { Metadata = ReusableLibraryProvenance.WithoutOrigin(item.Metadata) },
            PopupEngineeringDto item => item with { Metadata = ReusableLibraryProvenance.WithoutOrigin(item.Metadata) },
            VisualAssetEngineeringDto item => item with { Metadata = ReusableLibraryProvenance.WithoutOrigin(item.Metadata) },
            ScriptEngineeringDefinition item => ReusableScriptDependencyAnalyzer.WithMetadata(
                item,
                ReusableLibraryProvenance.WithoutOrigin(item.Metadata)),
            _ => value
        };

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class KindKeyComparer : IEqualityComparer<(ImportEntityKind Kind, string Key)>
    {
        public bool Equals(
            (ImportEntityKind Kind, string Key) left,
            (ImportEntityKind Kind, string Key) right) =>
            left.Kind == right.Kind &&
            string.Equals(left.Key, right.Key, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((ImportEntityKind Kind, string Key) value) =>
            HashCode.Combine(
                value.Kind,
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Key ?? string.Empty));
    }
}
