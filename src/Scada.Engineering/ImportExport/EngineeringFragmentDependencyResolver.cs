using System.Text.Json;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.Interactions;
using Scada.Engineering.Reports;
using Scada.Engineering.Scripts;

namespace Scada.Engineering.ImportExport;

internal sealed class EngineeringFragmentDependencyResolver
{
    private readonly EngineeringPackage _package;
    private readonly Dictionary<(ImportEntityKind Kind, Guid Id), EngineeringFragmentEntityReference> _byId = new();
    private readonly Dictionary<(ImportEntityKind Kind, string Key), EngineeringFragmentEntityReference> _byKey =
        new(new KindKeyComparer());

    public EngineeringFragmentDependencyResolver(EngineeringPackage package)
    {
        _package = package ?? throw new ArgumentNullException(nameof(package));

        foreach (var reference in EnumerateEntityReferences(package))
        {
            if (reference.EntityId is not { } id || id == Guid.Empty)
                continue;
            if (!_byId.TryAdd((reference.EntityKind, id), reference))
                throw new InvalidDataException(
                    $"Engineering contains duplicate stable identity '{reference.EntityKind}:{id:D}'.");
            if (!_byKey.TryAdd((reference.EntityKind, reference.EntityKey), reference))
                throw new InvalidDataException(
                    $"Engineering contains duplicate canonical key/path '{reference.EntityKind}:{reference.EntityKey}'.");
        }
    }

    public EngineeringFragmentEntityReference ResolveRoot(EngineeringFragmentEntityReference requested)
    {
        ArgumentNullException.ThrowIfNull(requested);
        EnsurePortableKind(requested.EntityKind);

        if (requested.EntityId is not { } id || id == Guid.Empty)
            throw new InvalidDataException(
                $"Engineering Fragment root '{requested.EntityKind}:{requested.EntityKey}' requires stable canonical identity.");

        if (!_byId.TryGetValue((requested.EntityKind, id), out var actual))
            throw new KeyNotFoundException(
                $"Engineering Fragment root '{requested.EntityKind}:{id:D}' was not found in Working.");

        if (!string.IsNullOrWhiteSpace(requested.EntityKey) &&
            !string.Equals(requested.EntityKey, actual.EntityKey, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Engineering Fragment root '{requested.EntityKind}:{id:D}' key/path is stale. Expected '{actual.EntityKey}'.");

        return actual;
    }

    public IReadOnlyCollection<EngineeringFragmentEntityReference> ResolveClosure(
        IReadOnlyCollection<EngineeringFragmentEntityReference> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        var state = new Dictionary<(ImportEntityKind Kind, Guid Id), byte>();
        var closure = new Dictionary<(ImportEntityKind Kind, Guid Id), EngineeringFragmentEntityReference>();

        void Visit(EngineeringFragmentEntityReference reference)
        {
            EnsurePortableKind(reference.EntityKind);
            var id = RequireId(reference);
            var identity = (reference.EntityKind, id);
            if (state.TryGetValue(identity, out var current))
            {
                if (current is 1 or 2)
                    return;
            }

            if (!_byId.TryGetValue(identity, out var canonical))
                throw new InvalidDataException(
                    $"Engineering Fragment dependency '{reference.EntityKind}:{id:D}' does not resolve in Working.");

            state[identity] = 1;
            foreach (var dependency in DirectDependencies(canonical)
                         .OrderBy(item => item, EngineeringFragmentReferenceComparer.Instance))
                Visit(dependency);
            state[identity] = 2;
            closure[identity] = canonical;
        }

        foreach (var root in roots.OrderBy(item => item, EngineeringFragmentReferenceComparer.Instance))
            Visit(root);

        return closure.Values
            .OrderBy(item => item, EngineeringFragmentReferenceComparer.Instance)
            .ToArray();
    }

    private IReadOnlyCollection<EngineeringFragmentEntityReference> DirectDependencies(
        EngineeringFragmentEntityReference reference)
    {
        var ownerId = RequireId(reference);
        var dependencies = new Dictionary<(ImportEntityKind Kind, Guid Id), EngineeringFragmentEntityReference>();

        void Add(EngineeringFragmentEntityReference? dependency)
        {
            if (dependency?.EntityId is not { } id || id == Guid.Empty)
                return;
            dependencies.TryAdd((dependency.EntityKind, id), dependency);
        }

        void AddById(ImportEntityKind kind, Guid? id, string owner)
        {
            if (id is { } value && value != Guid.Empty)
                Add(Require(kind, value, owner));
        }

        void AddByKey(ImportEntityKind kind, string? key, string owner)
        {
            if (!string.IsNullOrWhiteSpace(key))
                Add(Require(kind, key, owner));
        }

        switch (reference.EntityKind)
        {
            case ImportEntityKind.Tag:
            {
                var item = Find<TagEngineeringDto>(reference);
                if (item.DataSourceId.HasValue)
                    AddById(ImportEntityKind.DataSource, item.DataSourceId, reference.EntityKey);
                else
                    AddByKey(ImportEntityKind.DataSource, item.Source, reference.EntityKey);

                if (item.HistorianCaptureProfileId.HasValue)
                    AddById(ImportEntityKind.HistorianCaptureProfile, item.HistorianCaptureProfileId, reference.EntityKey);
                break;
            }
            case ImportEntityKind.Alarm:
            {
                var item = Find<AlarmEngineeringDto>(reference);
                if (item.TagId.HasValue)
                    AddById(ImportEntityKind.Tag, item.TagId, reference.EntityKey);
                else
                    AddByKey(ImportEntityKind.Tag, item.TagPath, reference.EntityKey);
                break;
            }
            case ImportEntityKind.OperationalEvent:
            {
                var item = Find<OperationalEventEngineeringDto>(reference);
                if (item.TagId.HasValue)
                    AddById(ImportEntityKind.Tag, item.TagId, reference.EntityKey);
                else
                    AddByKey(ImportEntityKind.Tag, item.TagPath, reference.EntityKey);
                break;
            }
            case ImportEntityKind.DataSource:
            case ImportEntityKind.Template:
            case ImportEntityKind.VisualAsset:
            case ImportEntityKind.HistorianCaptureProfile:
            case ImportEntityKind.DataQuery:
            case ImportEntityKind.AlarmView:
                break;
            case ImportEntityKind.Equipment:
            {
                var item = Find<EquipmentEngineeringDto>(reference);
                if (item.TemplateId.HasValue)
                    AddById(ImportEntityKind.Template, item.TemplateId, reference.EntityKey);
                else
                    AddByKey(ImportEntityKind.Template, item.TemplateKey, reference.EntityKey);
                AddBindingDependencies(item.Bindings, AddById, AddByKey, reference.EntityKey);
                break;
            }
            case ImportEntityKind.Dynamo:
            {
                var item = Find<DynamoEngineeringDto>(reference);
                if (item.TemplateId.HasValue)
                    AddById(ImportEntityKind.Template, item.TemplateId, reference.EntityKey);
                else
                    AddByKey(ImportEntityKind.Template, item.TemplateKey, reference.EntityKey);
                AddBindingDependencies(item.Bindings, AddById, AddByKey, reference.EntityKey);
                AddVisualDependencies(item.Elements, AddById, AddByKey, reference.EntityKey);
                AddAttachedScripts(ownerId, AddById);
                break;
            }
            case ImportEntityKind.Screen:
            {
                var item = Find<ScreenEngineeringDto>(reference);
                AddVisualDependencies(item.Elements, AddById, AddByKey, reference.EntityKey);
                AddAttachedScripts(ownerId, AddById);
                break;
            }
            case ImportEntityKind.Popup:
            {
                var item = Find<PopupEngineeringDto>(reference);
                if (item.TemplateId.HasValue)
                    AddById(ImportEntityKind.Template, item.TemplateId, reference.EntityKey);
                else
                    AddByKey(ImportEntityKind.Template, item.TemplateKey, reference.EntityKey);
                AddVisualDependencies(item.Elements, AddById, AddByKey, reference.EntityKey);
                AddAttachedScripts(ownerId, AddById);
                break;
            }
            case ImportEntityKind.Command:
            {
                var item = Find<CommandEngineeringDto>(reference);
                if (item.TargetTagId.HasValue)
                    AddById(ImportEntityKind.Tag, item.TargetTagId, reference.EntityKey);
                else
                    AddByKey(ImportEntityKind.Tag, item.TargetTagPath, reference.EntityKey);
                break;
            }
            case ImportEntityKind.RichCommandDefinition:
            {
                var item = Find<RichCommandDefinitionEngineeringDto>(reference);
                AddById(ImportEntityKind.DriverCommandBinding, item.CommandId, reference.EntityKey);
                break;
            }
            case ImportEntityKind.DriverCommandBinding:
            {
                var item = Find<DriverCommandBindingEngineeringDto>(reference);
                AddById(ImportEntityKind.RichCommandDefinition, item.CommandId, reference.EntityKey);
                AddById(ImportEntityKind.DataSource, item.DataSourceId, reference.EntityKey);
                AddById(ImportEntityKind.Equipment, item.EquipmentId, reference.EntityKey);
                break;
            }
            case ImportEntityKind.Gateway:
            {
                var item = Find<GatewayRouteEngineeringDto>(reference);
                if (item.SourceTagId.HasValue)
                    AddById(ImportEntityKind.Tag, item.SourceTagId, reference.EntityKey);
                else
                    AddByKey(ImportEntityKind.Tag, item.SourceTagPath, reference.EntityKey);
                if (item.DestinationTagId.HasValue)
                    AddById(ImportEntityKind.Tag, item.DestinationTagId, reference.EntityKey);
                else
                    AddByKey(ImportEntityKind.Tag, item.DestinationTagPath, reference.EntityKey);
                break;
            }
            case ImportEntityKind.Script:
            {
                var item = Find<ScriptEngineeringDefinition>(reference);
                foreach (var entryPoint in item.EntryPoints)
                    AddTagReference(entryPoint.TagReference, AddById, reference.EntityKey);

                foreach (var dependency in item.Dependencies)
                {
                    switch (dependency.Kind)
                    {
                        case ScriptEngineeringDependencyKind.Script:
                            AddById(
                                ImportEntityKind.Script,
                                ParseGuid(dependency.StableReference, reference.EntityKey),
                                reference.EntityKey);
                            break;
                        case ScriptEngineeringDependencyKind.Tag:
                        case ScriptEngineeringDependencyKind.ClientMemoryTag:
                        case ScriptEngineeringDependencyKind.ServerMemoryTag:
                            if (dependency.TagBinding?.Expected is { } expected)
                                AddById(ImportEntityKind.Tag, expected.TagId, reference.EntityKey);
                            else
                                AddById(
                                    ImportEntityKind.Tag,
                                    ParseGuid(dependency.StableReference, reference.EntityKey),
                                    reference.EntityKey);
                            break;
                        case ScriptEngineeringDependencyKind.VisualDefinition:
                            Add(RequireVisualDefinition(
                                ParseGuid(dependency.StableReference, reference.EntityKey),
                                reference.EntityKey));
                            break;
                        case ScriptEngineeringDependencyKind.VisualObject:
                        {
                            var separator = dependency.StableReference.IndexOf('/');
                            var definitionText = separator > 0
                                ? dependency.StableReference[..separator]
                                : dependency.StableReference;
                            Add(RequireVisualDefinition(
                                ParseGuid(definitionText, reference.EntityKey),
                                reference.EntityKey));
                            break;
                        }
                        case ScriptEngineeringDependencyKind.Resource:
                            AddById(
                                ImportEntityKind.VisualAsset,
                                ParseGuid(dependency.StableReference, reference.EntityKey),
                                reference.EntityKey);
                            break;
                        case ScriptEngineeringDependencyKind.RichCommand:
                            AddById(
                                ImportEntityKind.RichCommandDefinition,
                                ParseGuid(dependency.StableReference, reference.EntityKey),
                                reference.EntityKey);
                            break;
                    }
                }

                foreach (var attached in (_package.ScriptVisualEventReferences ?? Array.Empty<ScriptVisualEventReference>())
                             .Where(item => item.ScriptId == ownerId))
                {
                    Add(RequireVisualDefinition(attached.VisualDefinitionId, reference.EntityKey));
                    AddTagReference(attached.TagReference, AddById, reference.EntityKey);
                }
                break;
            }
            case ImportEntityKind.Report:
            {
                var item = Find<ReportEngineeringDto>(reference);
                foreach (var section in item.Sections ?? Array.Empty<ReportSectionEngineeringDto>())
                {
                    foreach (var control in section.Controls ?? Array.Empty<ReportControlEngineeringDto>())
                        AddById(ImportEntityKind.VisualAsset, control.AssetId, reference.EntityKey);
                }
                break;
            }
            case ImportEntityKind.SecurityRole:
            case ImportEntityKind.SecurityScope:
                throw new InvalidDataException(
                    $"'{reference.EntityKind}' is Security Authority content and cannot be exported as an Engineering Fragment.");
            default:
                throw new InvalidDataException(
                    $"Engineering Fragment dependency resolver does not support '{reference.EntityKind}'.");
        }

        return dependencies.Values.ToArray();
    }

    private void AddAttachedScripts(
        Guid visualDefinitionId,
        Action<ImportEntityKind, Guid?, string> addById)
    {
        foreach (var reference in _package.ScriptVisualEventReferences ?? Array.Empty<ScriptVisualEventReference>())
        {
            if (reference.VisualDefinitionId == visualDefinitionId)
                addById(ImportEntityKind.Script, reference.ScriptId, visualDefinitionId.ToString("D"));
        }
    }

    private static void AddBindingDependencies(
        IReadOnlyCollection<EngineeringBindingDto>? bindings,
        Action<ImportEntityKind, Guid?, string> addById,
        Action<ImportEntityKind, string?, string> addByKey,
        string owner)
    {
        foreach (var binding in bindings ?? Array.Empty<EngineeringBindingDto>())
        {
            if (binding is null ||
                binding.Kind is not (EngineeringBindingKind.Tag or EngineeringBindingKind.ClientMemory))
                continue;

            if (binding.TagReference is { } stable)
                addById(ImportEntityKind.Tag, stable.TagId, owner);
            else
                addByKey(ImportEntityKind.Tag, binding.Target, owner);
        }
    }

    private static void AddVisualDependencies(
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        Action<ImportEntityKind, Guid?, string> addById,
        Action<ImportEntityKind, string?, string> addByKey,
        string owner)
    {
        foreach (var element in elements ?? Array.Empty<VisualElementEngineeringDto>())
        {
            if (element is null)
                continue;

            if (element.DynamoDefinitionId.HasValue)
                addById(ImportEntityKind.Dynamo, element.DynamoDefinitionId, owner);
            else
                addByKey(ImportEntityKind.Dynamo, element.DynamoKey, owner);

            if (element.EquipmentId.HasValue)
                addById(ImportEntityKind.Equipment, element.EquipmentId, owner);
            else
                addByKey(ImportEntityKind.Equipment, element.EquipmentPath, owner);

            AddBindingDependencies(element.Bindings, addById, addByKey, owner);

            foreach (var parameter in element.DynamoParameters ?? Array.Empty<DynamoParameterValueEngineeringDto>())
                AddTagReference(parameter?.TagReference, addById, owner);

            foreach (var expression in element.PropertyExpressions ?? Array.Empty<VisualPropertyExpressionEngineeringDto>())
                AddExpressionDependencies(expression?.Expression, addById, owner);
            foreach (var condition in element.BooleanConditions ?? Array.Empty<VisualBooleanConditionEngineeringDto>())
                AddValueSourceDependencies(condition?.Source, addById, addByKey, owner);
            if (element.AnalogFill is not null)
                AddValueSourceDependencies(element.AnalogFill.Source, addById, addByKey, owner);
            foreach (var map in element.PropertyMaps ?? Array.Empty<VisualPropertyMapEngineeringDto>())
                AddValueSourceDependencies(map?.Source, addById, addByKey, owner);

            foreach (var action in element.Actions ?? Array.Empty<VisualNavigationActionEngineeringDto>())
            {
                if (action is null)
                    continue;
                if (action.CommandId.HasValue)
                    addById(ImportEntityKind.Command, action.CommandId, owner);
                if (action.Kind == VisualNavigationActionKind.NavigateScreen)
                    addByKey(ImportEntityKind.Screen, action.TargetKey, owner);
                if (action.Kind == VisualNavigationActionKind.OpenPopup)
                    addByKey(ImportEntityKind.Popup, action.TargetKey, owner);
                if ((action.Kind is VisualNavigationActionKind.SetTagValue or VisualNavigationActionKind.ToggleTagBoolean) &&
                    Guid.TryParse(action.TargetKey, out var tagId))
                    addById(ImportEntityKind.Tag, tagId, owner);
            }

            if (string.Equals(element.Type, "core.image", StringComparison.Ordinal) &&
                element.Properties is not null &&
                element.Properties.TryGetValue("assetRef", out var assetReference) &&
                TryParseAssetReference(assetReference, out var assetId))
            {
                addById(ImportEntityKind.VisualAsset, assetId, owner);
            }

            AddVisualDependencies(element.Children, addById, addByKey, owner);
        }
    }

    private static void AddValueSourceDependencies(
        VisualValueSourceEngineeringDto? source,
        Action<ImportEntityKind, Guid?, string> addById,
        Action<ImportEntityKind, string?, string> addByKey,
        string owner)
    {
        if (source is null)
            return;
        if (source.TagReference is { } reference)
            addById(ImportEntityKind.Tag, reference.TagId, owner);
        else if (source.Kind is VisualValueSourceKind.Tag or VisualValueSourceKind.ClientMemory)
            addByKey(ImportEntityKind.Tag, source.Target, owner);
        AddExpressionDependencies(source.Expression, addById, owner);
    }

    private static void AddExpressionDependencies(
        VisualExpressionEngineeringDto? expression,
        Action<ImportEntityKind, Guid?, string> addById,
        string owner)
    {
        foreach (var dependency in expression?.Dependencies ?? Array.Empty<VisualExpressionDependencyEngineeringDto>())
            addById(ImportEntityKind.Tag, dependency.TagReference.TagId, owner);
    }

    private static void AddTagReference(
        TagValueReference? reference,
        Action<ImportEntityKind, Guid?, string> addById,
        string owner)
    {
        if (reference is not null)
            addById(ImportEntityKind.Tag, reference.TagId, owner);
    }

    private EngineeringFragmentEntityReference Require(ImportEntityKind kind, Guid id, string owner)
    {
        if (_byId.TryGetValue((kind, id), out var reference))
            return reference;
        throw new InvalidDataException(
            $"Engineering Fragment dependency '{kind}:{id:D}' required by '{owner}' was not found.");
    }

    private EngineeringFragmentEntityReference Require(ImportEntityKind kind, string key, string owner)
    {
        if (_byKey.TryGetValue((kind, key), out var reference))
            return reference;
        throw new InvalidDataException(
            $"Engineering Fragment dependency '{kind}:{key}' required by '{owner}' was not found.");
    }

    private EngineeringFragmentEntityReference RequireVisualDefinition(Guid id, string owner)
    {
        foreach (var kind in new[] { ImportEntityKind.Screen, ImportEntityKind.Popup, ImportEntityKind.Dynamo })
        {
            if (_byId.TryGetValue((kind, id), out var reference))
                return reference;
        }

        throw new InvalidDataException(
            $"Engineering Fragment visual definition dependency '{id:D}' required by '{owner}' was not found.");
    }

    private T Find<T>(EngineeringFragmentEntityReference reference)
    {
        var id = RequireId(reference);
        object? value = reference.EntityKind switch
        {
            ImportEntityKind.Tag => _package.Tags.SingleOrDefault(item => item.Id == id),
            ImportEntityKind.Alarm => _package.Alarms.SingleOrDefault(item => item.Id == id),
            ImportEntityKind.OperationalEvent => (_package.OperationalEvents ?? Array.Empty<OperationalEventEngineeringDto>()).SingleOrDefault(item => item.Id == id),
            ImportEntityKind.DataSource => (_package.DataSources ?? Array.Empty<DataSourceEngineeringDto>()).SingleOrDefault(item => item.Id == id),
            ImportEntityKind.Template => (_package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>()).SingleOrDefault(item => item.Id == id),
            ImportEntityKind.Equipment => (_package.Equipment ?? Array.Empty<EquipmentEngineeringDto>()).SingleOrDefault(item => item.Id == id),
            ImportEntityKind.Dynamo => (_package.Dynamos ?? Array.Empty<DynamoEngineeringDto>()).SingleOrDefault(item => item.Id == id),
            ImportEntityKind.Screen => (_package.Screens ?? Array.Empty<ScreenEngineeringDto>()).SingleOrDefault(item => item.Id == id),
            ImportEntityKind.Popup => (_package.Popups ?? Array.Empty<PopupEngineeringDto>()).SingleOrDefault(item => item.Id == id),
            ImportEntityKind.Command => (_package.Commands ?? Array.Empty<CommandEngineeringDto>()).SingleOrDefault(item => item.Id == id),
            ImportEntityKind.RichCommandDefinition => (_package.RichCommandDefinitions ?? Array.Empty<RichCommandDefinitionEngineeringDto>()).SingleOrDefault(item => item.CommandId == id),
            ImportEntityKind.DriverCommandBinding => (_package.DriverCommandBindings ?? Array.Empty<DriverCommandBindingEngineeringDto>()).SingleOrDefault(item => item.CommandId == id),
            ImportEntityKind.Gateway => (_package.Gateways ?? Array.Empty<GatewayRouteEngineeringDto>()).SingleOrDefault(item => item.Id == id),
            ImportEntityKind.Script => (_package.Scripts ?? Array.Empty<ScriptEngineeringDefinition>()).SingleOrDefault(item => item.Id == id),
            ImportEntityKind.VisualAsset => (_package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>()).SingleOrDefault(item => item.Id == id),
            ImportEntityKind.Report => (_package.Reports ?? Array.Empty<ReportEngineeringDto>()).SingleOrDefault(item => item.Id == id),
            ImportEntityKind.HistorianCaptureProfile => (_package.HistorianCaptureProfiles ?? Array.Empty<HistorianCaptureProfileEngineeringDto>()).SingleOrDefault(item => item.Id == id),
            ImportEntityKind.DataQuery => (_package.DataQueries ?? Array.Empty<DataQueryEngineeringDto>()).SingleOrDefault(item => item.Id == id),
            ImportEntityKind.AlarmView => (_package.AlarmViews ?? Array.Empty<AlarmViewEngineeringDto>()).SingleOrDefault(item => item.Id == id),
            _ => null
        };

        return value is T typed
            ? typed
            : throw new InvalidDataException(
                $"Engineering Fragment entity '{reference.EntityKind}:{id:D}' could not be resolved.");
    }

    private static Guid RequireId(EngineeringFragmentEntityReference reference) =>
        reference.EntityId is { } id && id != Guid.Empty
            ? id
            : throw new InvalidDataException(
                $"Engineering Fragment entity '{reference.EntityKind}:{reference.EntityKey}' requires stable canonical identity.");

    private static Guid ParseGuid(string value, string owner) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw new InvalidDataException(
                $"Engineering Fragment dependency '{value}' required by '{owner}' is not a stable GUID.");

    private static bool TryParseAssetReference(JsonElement reference, out Guid assetId)
    {
        assetId = Guid.Empty;
        if (reference.ValueKind != JsonValueKind.Object ||
            !reference.TryGetProperty("assetId", out var value) ||
            value.ValueKind != JsonValueKind.String)
            return false;

        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return false;
        if (text.StartsWith("asset:", StringComparison.Ordinal))
            text = text["asset:".Length..];
        return Guid.TryParse(text, out assetId) && assetId != Guid.Empty;
    }

    private static void EnsurePortableKind(ImportEntityKind kind)
    {
        if (kind is ImportEntityKind.SecurityRole or ImportEntityKind.SecurityScope)
            throw new InvalidDataException(
                $"'{kind}' is Security Authority content and cannot be exported as an Engineering Fragment.");
    }

    public static IEnumerable<EngineeringFragmentEntityReference> EnumerateEntityReferences(EngineeringPackage package)
    {
        foreach (var item in package.Tags)
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.Tag, item.Path, id);
        foreach (var item in package.Alarms)
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.Alarm, item.Name, id);
        foreach (var item in package.OperationalEvents ?? Array.Empty<OperationalEventEngineeringDto>())
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.OperationalEvent, item.Key, id);
        foreach (var item in package.DataSources ?? Array.Empty<DataSourceEngineeringDto>())
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.DataSource, item.Key, id);
        foreach (var item in package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>())
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.Template, item.Key, id);
        foreach (var item in package.Equipment ?? Array.Empty<EquipmentEngineeringDto>())
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.Equipment, item.Path, id);
        foreach (var item in package.Dynamos ?? Array.Empty<DynamoEngineeringDto>())
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.Dynamo, item.Key, id);
        foreach (var item in package.Screens ?? Array.Empty<ScreenEngineeringDto>())
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.Screen, item.Key, id);
        foreach (var item in package.Popups ?? Array.Empty<PopupEngineeringDto>())
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.Popup, item.Key, id);
        foreach (var item in package.Commands ?? Array.Empty<CommandEngineeringDto>())
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.Command, item.Key, id);
        foreach (var item in package.RichCommandDefinitions ?? Array.Empty<RichCommandDefinitionEngineeringDto>())
            if (item.CommandId != Guid.Empty)
                yield return new(ImportEntityKind.RichCommandDefinition, item.SemanticKey, item.CommandId);
        foreach (var item in package.DriverCommandBindings ?? Array.Empty<DriverCommandBindingEngineeringDto>())
            if (item.CommandId != Guid.Empty)
                yield return new(ImportEntityKind.DriverCommandBinding, item.CommandId.ToString("D"), item.CommandId);
        foreach (var item in package.Gateways ?? Array.Empty<GatewayRouteEngineeringDto>())
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.Gateway, item.Key, id);
        foreach (var item in package.Scripts ?? Array.Empty<ScriptEngineeringDefinition>())
            if (item.Id != Guid.Empty)
                yield return new(ImportEntityKind.Script, item.Path, item.Id);
        foreach (var item in package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>())
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.VisualAsset, item.Key, id);
        foreach (var item in package.Reports ?? Array.Empty<ReportEngineeringDto>())
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.Report, item.Key, id);
        foreach (var item in package.HistorianCaptureProfiles ?? Array.Empty<HistorianCaptureProfileEngineeringDto>())
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.HistorianCaptureProfile, item.Key, id);
        foreach (var item in package.DataQueries ?? Array.Empty<DataQueryEngineeringDto>())
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.DataQuery, item.Key, id);
        foreach (var item in package.AlarmViews ?? Array.Empty<AlarmViewEngineeringDto>())
            if (item.Id is { } id && id != Guid.Empty)
                yield return new(ImportEntityKind.AlarmView, item.Key, id);
    }

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

internal sealed class EngineeringFragmentReferenceComparer :
    IComparer<EngineeringFragmentEntityReference>,
    IEqualityComparer<EngineeringFragmentEntityReference>
{
    public static EngineeringFragmentReferenceComparer Instance { get; } = new();

    public int Compare(EngineeringFragmentEntityReference? left, EngineeringFragmentEntityReference? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return -1;
        if (right is null) return 1;

        var kind = left.EntityKind.CompareTo(right.EntityKind);
        if (kind != 0) return kind;
        var id = Nullable.Compare(left.EntityId, right.EntityId);
        if (id != 0) return id;
        return StringComparer.OrdinalIgnoreCase.Compare(left.EntityKey, right.EntityKey);
    }

    public bool Equals(EngineeringFragmentEntityReference? left, EngineeringFragmentEntityReference? right) =>
        left is not null &&
        right is not null &&
        left.EntityKind == right.EntityKind &&
        left.EntityId == right.EntityId &&
        string.Equals(left.EntityKey, right.EntityKey, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode(EngineeringFragmentEntityReference value) =>
        HashCode.Combine(
            value.EntityKind,
            value.EntityId,
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.EntityKey ?? string.Empty));

    public static (ImportEntityKind Kind, Guid Id) Identity(EngineeringFragmentEntityReference reference) =>
        (reference.EntityKind, reference.EntityId!.Value);
}
