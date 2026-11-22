using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualScripting;

namespace Scada.Engineering.ImportExport.Handlers;

internal static class EngineeringHandlerSupport
{
    public static ImportOperation Decide(bool exists, ImportMode mode) => mode switch
    {
        ImportMode.CreateOnly => exists ? ImportOperation.Skip : ImportOperation.Create,
        ImportMode.UpdateExisting => exists ? ImportOperation.Update : ImportOperation.Skip,
        ImportMode.CreateAndUpdate => exists ? ImportOperation.Update : ImportOperation.Create,
        _ => ImportOperation.Error
    };

    public static void AddPreview(
        List<ImportPreviewItem> items,
        ImportEntityKind kind,
        string key,
        bool exists,
        ImportMode mode,
        IReadOnlyCollection<ImportIssue> issues)
    {
        var operation = Decide(exists, mode);
        if (issues.Any(x => x.IsError)) operation = ImportOperation.Error;
        items.Add(new(kind, key, operation, issues));
    }

    public static HashSet<string> Duplicates(IEnumerable<string> keys) =>
        keys.Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool ContainsPlaceholder(string value) =>
        value.Contains('{', StringComparison.Ordinal) || value.Contains('}', StringComparison.Ordinal);

    public static bool TagPathExists(ITagRegistry tags, string path, EngineeringPackage package) =>
        tags.TryGetByPath(path, out _) || package.Tags.Any(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

    public static void ValidateConcreteTagBindings(
        ITagRegistry tags,
        IReadOnlyCollection<EngineeringBindingDto>? bindings,
        ImportEntityKind kind,
        string entityKey,
        EngineeringPackage package,
        List<ImportIssue> issues,
        string? visualObjectType = null)
    {
        foreach (var binding in bindings ?? Array.Empty<EngineeringBindingDto>())
        {
            // Generic validation records null/malformed entries. Reference
            // validation must not turn the same untrusted input into an exception.
            if (binding is null)
                continue;

            if (binding.TagReference is not null && binding.Kind != EngineeringBindingKind.Tag)
            {
                issues.Add(new(
                    "BINDING_TAG_REFERENCE_KIND_INVALID",
                    $"Binding '{binding.Key}' can only declare tagReference when its kind is Tag.",
                    kind,
                    entityKey,
                    true));
                continue;
            }

            if (binding.Kind != EngineeringBindingKind.Tag)
                continue;

            if (binding.TagReference is not null)
            {
                ValidateStableTagReference(tags, binding, kind, entityKey, package, issues);
                ValidateVisualControlTagType(tags, binding, visualObjectType, kind, entityKey, package, issues);
                continue;
            }

            // Legacy/path-only bindings remain readable for compatibility. New
            // concrete authoring should persist TagReference so a rename cannot
            // silently retarget or orphan the binding.
            if (string.IsNullOrWhiteSpace(binding.Target) || ContainsPlaceholder(binding.Target))
                continue;

            if (!TagPathExists(tags, binding.Target, package))
                issues.Add(new(
                    "BINDING_TAG_NOT_FOUND",
                    $"TAG '{binding.Target}' referenced by binding '{binding.Key}' was not found.",
                    kind,
                    entityKey,
                    true));
            else
                ValidateVisualControlTagType(tags, binding, visualObjectType, kind, entityKey, package, issues);
        }
    }

    private static void ValidateVisualControlTagType(
        ITagRegistry tags,
        EngineeringBindingDto binding,
        string? visualObjectType,
        ImportEntityKind kind,
        string entityKey,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        if (binding.Kind != EngineeringBindingKind.Tag ||
            visualObjectType is not (BuiltinVisualObjectSchemas.CheckBoxType or BuiltinVisualObjectSchemas.ComboBoxType))
            return;

        var expectedKey = visualObjectType == BuiltinVisualObjectSchemas.CheckBoxType
            ? VisualPropertyKeys.Checked
            : VisualPropertyKeys.Text;
        if (!binding.Key.Equals(expectedKey, StringComparison.Ordinal)) return;

        TagDataType? dataType = null;
        var readOnly = false;
        if (binding.TagReference is { TagId: var tagId } && tagId != Guid.Empty && tags.TryGet(tagId, out var existing) && existing is not null)
        {
            dataType = existing.DataType;
            readOnly = existing.ReadOnly;
        }
        else
        {
            var candidate = binding.TagReference is { TagId: var candidateId } && candidateId != Guid.Empty
                ? package.Tags.FirstOrDefault(tag => tag is not null && tag.Id == candidateId)
                : package.Tags.FirstOrDefault(tag => tag is not null && tag.Path.Equals(binding.Target, StringComparison.OrdinalIgnoreCase));
            if (candidate is not null)
            {
                dataType = candidate.DataType;
                readOnly = candidate.ReadOnly;
            }
            else if (tags.TryGetByPath(binding.Target, out var pathTag) && pathTag is not null)
            {
                dataType = pathTag.DataType;
                readOnly = pathTag.ReadOnly;
            }
        }

        if (!dataType.HasValue) return; // The general stable-reference validator reports missing identities.

        var compatible = visualObjectType == BuiltinVisualObjectSchemas.CheckBoxType
            ? dataType.Value == TagDataType.Boolean
            : dataType.Value is TagDataType.String or TagDataType.Enum;
        if (!compatible)
        {
            var expected = visualObjectType == BuiltinVisualObjectSchemas.CheckBoxType ? "Boolean" : "String or Enum";
            issues.Add(new(
                visualObjectType == BuiltinVisualObjectSchemas.CheckBoxType ? "VISUAL_CHECKBOX_BOOLEAN_TAG_TYPE_REQUIRED" : "VISUAL_COMBOBOX_TEXT_TAG_TYPE_REQUIRED",
                $"Binding '{binding.Key}' on '{visualObjectType}' requires a {expected} TAG.",
                kind,
                entityKey,
                true));
        }
        else if (readOnly && binding.Direction?.Trim().ToLowerInvariant() is "readwrite" or "write" or "inputoutput")
        {
            issues.Add(new(
                "VISUAL_CONTROL_TAG_READ_ONLY",
                $"Interactive visual control binding '{binding.Key}' targets a read-only TAG.",
                kind,
                entityKey,
                true));
        }
    }

    private static void ValidateStableTagReference(
        ITagRegistry tags,
        EngineeringBindingDto binding,
        ImportEntityKind kind,
        string entityKey,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        var reference = binding.TagReference!;
        if (reference.TagId == Guid.Empty)
        {
            issues.Add(new(
                "BINDING_TAG_REFERENCE_ID_INVALID",
                $"Binding '{binding.Key}' tagReference requires a non-empty TAG identity.",
                kind,
                entityKey,
                true));
            return;
        }

        if (!TryResolveTagDataType(tags, package, reference.TagId, out var dataType))
        {
            issues.Add(new(
                "BINDING_TAG_REFERENCE_NOT_FOUND",
                $"TAG identity '{reference.TagId:D}' referenced by binding '{binding.Key}' was not found in the prospective Engineering model.",
                kind,
                entityKey,
                true));
            return;
        }

        if (reference.Selector is null)
            return;

        if (!TagBitSemantics.TryValidateSelector(dataType, reference.Selector, out var error))
        {
            issues.Add(new(
                "BINDING_TAG_SELECTOR_INVALID",
                $"Binding '{binding.Key}' has an invalid TAG selector: {error}",
                kind,
                entityKey,
                true));
        }
    }

    private static bool TryResolveTagDataType(
        ITagRegistry tags,
        EngineeringPackage package,
        Guid tagId,
        out TagDataType dataType)
    {
        if (tags.TryGet(tagId, out var existing) && existing is not null)
        {
            dataType = existing.DataType;
            return true;
        }

        var candidate = package.Tags.FirstOrDefault(x => x is not null && x.Id == tagId);
        if (candidate is not null)
        {
            dataType = candidate.DataType;
            return true;
        }

        dataType = default;
        return false;
    }
}
