using Scada.Engineering.Assets;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Interactions;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class DriverInteractionEngineeringHandler
{
    private readonly IDriverInteractionEngineeringRegistry _registry;
    private readonly IDataSourceEngineeringRegistry _dataSources;
    private readonly IEngineeringAssetRegistry _assets;

    public DriverInteractionEngineeringHandler(
        IDriverInteractionEngineeringRegistry registry,
        IDataSourceEngineeringRegistry dataSources,
        IEngineeringAssetRegistry assets)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _dataSources = dataSources ?? throw new ArgumentNullException(nameof(dataSources));
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
    }

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        ArgumentNullException.ThrowIfNull(package);

        PreviewEventDefinitions(package, mode, items);
        PreviewEventReferences(package, mode, items);
        PreviewRichCommandDefinitions(package, mode, items);
        PreviewDriverCommandBindings(package, mode, items);

        try
        {
            _ = DriverInteractionEngineeringValidator.NormalizeActiveGraph(
                BuildProspectivePackage(package, mode));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
        {
            items.Add(new ImportPreviewItem(
                ImportEntityKind.DriverCommandBinding,
                "driver-interaction-graph",
                ImportOperation.Error,
                [
                    new ImportIssue(
                        "DRIVER_INTERACTION_GRAPH_INVALID",
                        ex.Message,
                        ImportEntityKind.DriverCommandBinding,
                        "driver-interaction-graph",
                        true)
                ]));
        }
    }

    public void Apply(
        EngineeringPackage package,
        ImportMode mode,
        ref int created,
        ref int updated,
        ref int skipped)
    {
        foreach (var dto in package.TransientEventDefinitions ?? Array.Empty<TransientEventDefinitionEngineeringDto>())
            ApplyOne(
                _registry.FindTransientEventDefinition(dto.DefinitionId) is not null,
                mode,
                () => _registry.UpsertTransientEventDefinition(dto),
                ref created,
                ref updated,
                ref skipped);

        foreach (var dto in package.CapabilityEventReferences ?? Array.Empty<CapabilityEventReferenceEngineeringDto>())
            ApplyOne(
                _registry.FindCapabilityEventReference(dto.EquipmentId, dto.CapabilityId, dto.Role) is not null,
                mode,
                () => _registry.UpsertCapabilityEventReference(dto),
                ref created,
                ref updated,
                ref skipped);

        foreach (var dto in package.RichCommandDefinitions ?? Array.Empty<RichCommandDefinitionEngineeringDto>())
            ApplyOne(
                _registry.FindRichCommandDefinition(dto.CommandId) is not null,
                mode,
                () => _registry.UpsertRichCommandDefinition(dto),
                ref created,
                ref updated,
                ref skipped);

        foreach (var dto in package.DriverCommandBindings ?? Array.Empty<DriverCommandBindingEngineeringDto>())
            ApplyOne(
                _registry.FindDriverCommandBinding(dto.CommandId) is not null,
                mode,
                () => _registry.UpsertDriverCommandBinding(dto),
                ref created,
                ref updated,
                ref skipped);
    }

    private void PreviewEventDefinitions(
        EngineeringPackage package,
        ImportMode mode,
        List<ImportPreviewItem> items)
    {
        var incoming = package.TransientEventDefinitions ?? Array.Empty<TransientEventDefinitionEngineeringDto>();
        var duplicateIds = DuplicateValues(incoming.Select(item => item.DefinitionId));
        var duplicateKeys = DuplicateValues(incoming.Select(item => item.SemanticKey), StringComparer.Ordinal);

        foreach (var dto in incoming)
        {
            var issues = Validate(
                () => DriverInteractionEngineeringMapper.ToCore(dto),
                ImportEntityKind.TransientEventDefinition,
                dto.DefinitionId.ToString("D"),
                "TRANSIENT_EVENT_DEFINITION_INVALID");

            if (duplicateIds.Contains(dto.DefinitionId))
                issues.Add(Error(
                    "TRANSIENT_EVENT_DEFINITION_ID_DUPLICATE",
                    $"Transient Event DefinitionId '{dto.DefinitionId:D}' appears more than once in the import package.",
                    ImportEntityKind.TransientEventDefinition,
                    dto.DefinitionId.ToString("D")));

            if (duplicateKeys.Contains(dto.SemanticKey))
                issues.Add(Error(
                    "TRANSIENT_EVENT_SEMANTIC_KEY_DUPLICATE",
                    $"Transient Event semantic key '{dto.SemanticKey}' appears more than once in the import package.",
                    ImportEntityKind.TransientEventDefinition,
                    dto.DefinitionId.ToString("D")));

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.TransientEventDefinition,
                dto.DefinitionId.ToString("D"),
                _registry.FindTransientEventDefinition(dto.DefinitionId) is not null,
                mode,
                issues);
        }
    }

    private void PreviewEventReferences(
        EngineeringPackage package,
        ImportMode mode,
        List<ImportPreviewItem> items)
    {
        var incoming = package.CapabilityEventReferences ?? Array.Empty<CapabilityEventReferenceEngineeringDto>();
        var duplicateIds = DuplicateValues(
            incoming.Select(item => EventReferenceKey(item)),
            StringComparer.Ordinal);

        foreach (var dto in incoming)
        {
            var key = EventReferenceKey(dto);
            var issues = Validate(
                () => DriverInteractionEngineeringMapper.ToCore(dto),
                ImportEntityKind.CapabilityEventReference,
                key,
                "CAPABILITY_EVENT_REFERENCE_INVALID");

            if (duplicateIds.Contains(key))
                issues.Add(Error(
                    "CAPABILITY_EVENT_REFERENCE_DUPLICATE",
                    $"Capability Event Reference '{key}' appears more than once in the import package.",
                    ImportEntityKind.CapabilityEventReference,
                    key));

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.CapabilityEventReference,
                key,
                _registry.FindCapabilityEventReference(dto.EquipmentId, dto.CapabilityId, dto.Role) is not null,
                mode,
                issues);
        }
    }

    private void PreviewRichCommandDefinitions(
        EngineeringPackage package,
        ImportMode mode,
        List<ImportPreviewItem> items)
    {
        var incoming = package.RichCommandDefinitions ?? Array.Empty<RichCommandDefinitionEngineeringDto>();
        var duplicateIds = DuplicateValues(incoming.Select(item => item.CommandId));
        var duplicateKeys = DuplicateValues(incoming.Select(item => item.SemanticKey), StringComparer.Ordinal);

        foreach (var dto in incoming)
        {
            var issues = Validate(
                () => DriverInteractionEngineeringMapper.ToCore(dto),
                ImportEntityKind.RichCommandDefinition,
                dto.CommandId.ToString("D"),
                "RICH_COMMAND_DEFINITION_INVALID");

            if (duplicateIds.Contains(dto.CommandId))
                issues.Add(Error(
                    "RICH_COMMAND_DEFINITION_ID_DUPLICATE",
                    $"Rich Command CommandId '{dto.CommandId:D}' appears more than once in the import package.",
                    ImportEntityKind.RichCommandDefinition,
                    dto.CommandId.ToString("D")));

            if (duplicateKeys.Contains(dto.SemanticKey))
                issues.Add(Error(
                    "RICH_COMMAND_SEMANTIC_KEY_DUPLICATE",
                    $"Rich Command semantic key '{dto.SemanticKey}' appears more than once in the import package.",
                    ImportEntityKind.RichCommandDefinition,
                    dto.CommandId.ToString("D")));

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.RichCommandDefinition,
                dto.CommandId.ToString("D"),
                _registry.FindRichCommandDefinition(dto.CommandId) is not null,
                mode,
                issues);
        }
    }

    private void PreviewDriverCommandBindings(
        EngineeringPackage package,
        ImportMode mode,
        List<ImportPreviewItem> items)
    {
        var incoming = package.DriverCommandBindings ?? Array.Empty<DriverCommandBindingEngineeringDto>();
        var duplicateIds = DuplicateValues(incoming.Select(item => item.CommandId));

        foreach (var dto in incoming)
        {
            var key = dto.CommandId.ToString("D");
            var issues = Validate(
                () => DriverInteractionEngineeringMapper.ToCore(dto),
                ImportEntityKind.DriverCommandBinding,
                key,
                "DRIVER_COMMAND_BINDING_INVALID");

            if (duplicateIds.Contains(dto.CommandId))
                issues.Add(Error(
                    "DRIVER_COMMAND_BINDING_DUPLICATE",
                    $"Driver Command Binding for CommandId '{dto.CommandId:D}' appears more than once in the import package.",
                    ImportEntityKind.DriverCommandBinding,
                    key));

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.DriverCommandBinding,
                key,
                _registry.FindDriverCommandBinding(dto.CommandId) is not null,
                mode,
                issues);
        }
    }

    private EngineeringPackage BuildProspectivePackage(EngineeringPackage package, ImportMode mode)
    {
        var dataSources = Overlay(
            _dataSources.Snapshot(),
            package.DataSources ?? Array.Empty<DataSourceEngineeringDto>(),
            item => item.Id?.ToString("D"),
            mode,
            ignoreNullKey: true);

        var equipment = Overlay(
            _assets.SnapshotEquipment(),
            package.Equipment ?? Array.Empty<EquipmentEngineeringDto>(),
            item => item.Id?.ToString("D"),
            mode,
            ignoreNullKey: true);

        var events = Overlay(
            _registry.SnapshotTransientEventDefinitions(),
            package.TransientEventDefinitions ?? Array.Empty<TransientEventDefinitionEngineeringDto>(),
            item => item.DefinitionId.ToString("D"),
            mode);

        var references = Overlay(
            _registry.SnapshotCapabilityEventReferences(),
            package.CapabilityEventReferences ?? Array.Empty<CapabilityEventReferenceEngineeringDto>(),
            item => EventReferenceKey(item),
            mode);

        var commands = Overlay(
            _registry.SnapshotRichCommandDefinitions(),
            package.RichCommandDefinitions ?? Array.Empty<RichCommandDefinitionEngineeringDto>(),
            item => item.CommandId.ToString("D"),
            mode);

        var bindings = Overlay(
            _registry.SnapshotDriverCommandBindings(),
            package.DriverCommandBindings ?? Array.Empty<DriverCommandBindingEngineeringDto>(),
            item => item.CommandId.ToString("D"),
            mode);

        return package with
        {
            DataSources = dataSources,
            Equipment = equipment,
            TransientEventDefinitions = events,
            CapabilityEventReferences = references,
            RichCommandDefinitions = commands,
            DriverCommandBindings = bindings
        };
    }

    private static IReadOnlyCollection<T> Overlay<T>(
        IReadOnlyCollection<T> current,
        IReadOnlyCollection<T> incoming,
        Func<T, string?> keySelector,
        ImportMode mode,
        bool ignoreNullKey = false)
        where T : class
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        var unkeyed = new List<T>();

        foreach (var item in current)
        {
            if (item is null) continue;
            var key = keySelector(item);
            if (key is null)
            {
                if (!ignoreNullKey) throw new InvalidDataException("Interaction graph identity is required.");
                unkeyed.Add(item);
                continue;
            }
            result[key] = item;
        }

        foreach (var item in incoming)
        {
            if (item is null) continue;
            var key = keySelector(item);
            if (key is null)
            {
                if (!ignoreNullKey) throw new InvalidDataException("Interaction graph identity is required.");
                unkeyed.Add(item);
                continue;
            }

            var exists = result.ContainsKey(key);
            if (EngineeringHandlerSupport.Decide(exists, mode) == ImportOperation.Skip)
                continue;
            result[key] = item;
        }

        return result.Values.Concat(unkeyed).ToArray();
    }

    private static List<ImportIssue> Validate<T>(
        Func<T> validate,
        ImportEntityKind kind,
        string key,
        string code)
    {
        try
        {
            _ = validate();
            return [];
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
        {
            return [Error(code, ex.Message, kind, key)];
        }
    }

    private static HashSet<T> DuplicateValues<T>(
        IEnumerable<T> values,
        IEqualityComparer<T>? comparer = null)
        where T : notnull =>
        values.GroupBy(value => value, comparer ?? EqualityComparer<T>.Default)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(comparer ?? EqualityComparer<T>.Default);

    private static ImportIssue Error(
        string code,
        string message,
        ImportEntityKind kind,
        string key) =>
        new(code, message, kind, key, true);

    private static string EventReferenceKey(CapabilityEventReferenceEngineeringDto dto) =>
        $"{dto.EquipmentId:D}/{dto.CapabilityId}/{dto.Role}";

    private static void ApplyOne(
        bool exists,
        ImportMode mode,
        Action apply,
        ref int created,
        ref int updated,
        ref int skipped)
    {
        var operation = EngineeringHandlerSupport.Decide(exists, mode);
        if (operation == ImportOperation.Skip)
        {
            skipped++;
            return;
        }

        apply();
        if (operation == ImportOperation.Create) created++;
        else updated++;
    }
}
