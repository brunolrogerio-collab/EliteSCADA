using Scada.Engineering.Contracts;

namespace Scada.Engineering.Views;

/// <summary>
/// Assigns stable identities to visual Engineering elements while preserving
/// legacy schema-v10 compatibility. A legacy element that arrives without an Id
/// reuses the Id of the existing sibling with the same developer key when one
/// exists; otherwise it receives a new Id. Once exported, that Id becomes the
/// authoritative identity and developer-key renames no longer redefine it.
/// </summary>
internal static class VisualElementIdentity
{
    public static IReadOnlyCollection<VisualElementEngineeringDto>? Normalize(
        IReadOnlyCollection<VisualElementEngineeringDto>? incoming,
        IReadOnlyCollection<VisualElementEngineeringDto>? existing = null)
    {
        if (incoming is null)
            return null;

        var usedIds = new HashSet<Guid>();
        return NormalizeLevel(incoming, existing, usedIds);
    }

    private static IReadOnlyCollection<VisualElementEngineeringDto> NormalizeLevel(
        IReadOnlyCollection<VisualElementEngineeringDto> incoming,
        IReadOnlyCollection<VisualElementEngineeringDto>? existing,
        HashSet<Guid> usedIds)
    {
        var existingByKey = BuildUniqueKeyIndex(existing);
        var normalized = new List<VisualElementEngineeringDto>(incoming.Count);

        foreach (var element in incoming)
        {
            existingByKey.TryGetValue(element.Key ?? string.Empty, out var previous);

            var id = ResolveId(element.Id, previous?.Id, usedIds, element.Key);
            var children = NormalizeLevel(
                element.Children ?? Array.Empty<VisualElementEngineeringDto>(),
                previous?.Children,
                usedIds);

            normalized.Add(element with
            {
                Id = id,
                Children = element.Children is null ? null : children
            });
        }

        return normalized.ToArray();
    }

    private static Dictionary<string, VisualElementEngineeringDto> BuildUniqueKeyIndex(
        IReadOnlyCollection<VisualElementEngineeringDto>? elements)
    {
        if (elements is null || elements.Count == 0)
            return new Dictionary<string, VisualElementEngineeringDto>(StringComparer.OrdinalIgnoreCase);

        return elements
            .Where(element => !string.IsNullOrWhiteSpace(element.Key))
            .GroupBy(element => element.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
    }

    private static Guid ResolveId(Guid? supplied, Guid? previous, HashSet<Guid> usedIds, string? key)
    {
        if (supplied == Guid.Empty)
            throw new ArgumentException($"Visual element '{key}' cannot use an empty Id.");

        var candidate = supplied
            ?? (previous.HasValue && previous.Value != Guid.Empty ? previous.Value : Guid.NewGuid());

        if (!usedIds.Add(candidate))
            throw new ArgumentException($"Visual element Id '{candidate:D}' is duplicated in the same visual definition.");

        return candidate;
    }
}
