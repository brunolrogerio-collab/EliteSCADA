using Scada.Engineering.Contracts;
using Scada.Engineering.Events;

namespace Scada.Engineering.ImportExport.Handlers;

internal sealed class OperationalEventEngineeringHandler
{
    private readonly IOperationalEventEngineeringRegistry _registry;

    public OperationalEventEngineeringHandler(IOperationalEventEngineeringRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public void Preview(EngineeringPackage package, ImportMode mode, List<ImportPreviewItem> items)
    {
        var definitions = package.OperationalEvents ?? Array.Empty<OperationalEventEngineeringDto>();
        var duplicateKeys = EngineeringHandlerSupport.Duplicates(definitions.Select(item => item.Key));

        foreach (var definition in definitions)
        {
            var issues = Validate(package, definition).ToList();
            if (duplicateKeys.Contains(definition.Key))
            {
                issues.Add(new ImportIssue(
                    "OPERATIONAL_EVENT_DUPLICATE_IN_FILE",
                    $"Operational Event key '{definition.Key}' appears more than once in the import package.",
                    ImportEntityKind.OperationalEvent,
                    definition.Key,
                    true));
            }

            EngineeringHandlerSupport.AddPreview(
                items,
                ImportEntityKind.OperationalEvent,
                definition.Key,
                ResolveExisting(definition) is not null,
                mode,
                issues);
        }
    }

    public void Apply(EngineeringPackage package, ImportMode mode, ref int created, ref int updated, ref int skipped)
    {
        foreach (var definition in package.OperationalEvents ?? Array.Empty<OperationalEventEngineeringDto>())
        {
            var existing = ResolveExisting(definition);
            var operation = EngineeringHandlerSupport.Decide(existing is not null, mode);
            if (operation == ImportOperation.Skip)
            {
                skipped++;
                continue;
            }

            _registry.UpsertOperationalEvent(definition with
            {
                Id = existing?.Id ?? definition.Id ?? Guid.NewGuid()
            });
            if (existing is null) created++; else updated++;
        }
    }

    private OperationalEventEngineeringDto? ResolveExisting(OperationalEventEngineeringDto definition)
    {
        if (definition.Id.HasValue)
        {
            var byId = _registry.FindOperationalEvent(definition.Id.Value);
            if (byId is not null) return byId;
        }

        return _registry.FindOperationalEventByKey(definition.Key);
    }

    private static IEnumerable<ImportIssue> Validate(
        EngineeringPackage package,
        OperationalEventEngineeringDto definition)
    {
        var key = definition.Key ?? string.Empty;
        if (definition.Id == Guid.Empty)
            yield return Issue("OPERATIONAL_EVENT_ID_EMPTY", "Stable ID cannot be empty.", key);
        if (string.IsNullOrWhiteSpace(definition.Key))
            yield return Issue("OPERATIONAL_EVENT_KEY_REQUIRED", "Key is required.", key);
        if (string.IsNullOrWhiteSpace(definition.Name))
            yield return Issue("OPERATIONAL_EVENT_NAME_REQUIRED", "Name is required.", key);
        if (string.IsNullOrWhiteSpace(definition.Type))
            yield return Issue("OPERATIONAL_EVENT_TYPE_REQUIRED", "Type is required.", key);
        if (string.IsNullOrWhiteSpace(definition.Category))
            yield return Issue("OPERATIONAL_EVENT_CATEGORY_REQUIRED", "Category is required.", key);
        if (string.IsNullOrWhiteSpace(definition.Source))
            yield return Issue("OPERATIONAL_EVENT_SOURCE_REQUIRED", "Source/origin is required.", key);

        if (definition.Key?.Length > 160 || definition.Name?.Length > 240 ||
            definition.Type?.Length > 120 || definition.Category?.Length > 120 ||
            definition.Source?.Length > 240 || definition.Area?.Length > 240 ||
            definition.EquipmentPath?.Length > 500 || definition.TagPath?.Length > 500 ||
            definition.Message?.Length > 4000)
        {
            yield return Issue(
                "OPERATIONAL_EVENT_FIELD_TOO_LONG",
                "One or more Operational Event fields exceed the canonical length limit.",
                key);
        }

        if (!definition.TagId.HasValue && string.IsNullOrWhiteSpace(definition.TagPath))
            yield break;

        var byId = definition.TagId.HasValue
            ? package.Tags.FirstOrDefault(tag => tag.Id == definition.TagId.Value)
            : null;
        var byPath = !string.IsNullOrWhiteSpace(definition.TagPath)
            ? package.Tags.FirstOrDefault(tag => tag.Path.Equals(definition.TagPath, StringComparison.OrdinalIgnoreCase))
            : null;

        if (byId is not null && byPath is not null &&
            byId.Id != byPath.Id &&
            !byId.Path.Equals(byPath.Path, StringComparison.OrdinalIgnoreCase))
        {
            yield return Issue(
                "OPERATIONAL_EVENT_TAG_MISMATCH",
                "TagId and TagPath resolve to different TAGs.",
                key);
            yield break;
        }

        if (byId is null && byPath is null)
        {
            yield return Issue(
                "OPERATIONAL_EVENT_TAG_NOT_FOUND",
                "The scoped TAG is not present in the imported Engineering package.",
                key);
            yield break;
        }

        var resolved = byId ?? byPath!;
        if (!resolved.Id.HasValue || resolved.Id == Guid.Empty)
        {
            yield return Issue(
                "OPERATIONAL_EVENT_STABLE_TAG_ID_REQUIRED",
                $"Scoped TAG '{resolved.Path}' requires a stable non-empty ID.",
                key);
        }
    }

    private static ImportIssue Issue(string code, string message, string key) =>
        new(code, message, ImportEntityKind.OperationalEvent, key, true);
}