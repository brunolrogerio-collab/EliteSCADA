using Scada.Core.Tags;
using Scada.Engineering.Contracts;

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
        List<ImportIssue> issues)
    {
        foreach (var binding in bindings ?? Array.Empty<EngineeringBindingDto>())
        {
            if (binding.Kind != EngineeringBindingKind.Tag ||
                string.IsNullOrWhiteSpace(binding.Target) ||
                ContainsPlaceholder(binding.Target))
                continue;

            if (!TagPathExists(tags, binding.Target, package))
                issues.Add(new(
                    "BINDING_TAG_NOT_FOUND",
                    $"TAG '{binding.Target}' referenced by binding '{binding.Key}' was not found.",
                    kind,
                    entityKey,
                    true));
        }
    }
}
