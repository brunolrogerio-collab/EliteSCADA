using System.Text.Json;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.HomeAssistant;

public sealed record HomeAssistantRegistryDisplayEntry(
    string EntityId,
    string Platform,
    string? AreaId,
    string? DeviceId,
    string? DisplayName,
    string? EntityCategory,
    bool Hidden);

public sealed record HomeAssistantSelectedEntity(
    HomeAssistantState State,
    HomeAssistantRegistryDisplayEntry? Registry,
    string Domain,
    string DisplayName)
{
    public string EntityId => State.EntityId;
    public string? DeviceId => Registry?.DeviceId;
    public string? AreaId => Registry?.AreaId;
    public string? Platform => Registry?.Platform;
    public string SourceAddress => State.EntityId;
    public bool RenameStable => false;
}

public sealed record HomeAssistantSelectedInventory(
    IReadOnlyCollection<HomeAssistantSelectedEntity> Entities,
    IReadOnlyCollection<DriverEngineeringIssue> Issues);

public static class HomeAssistantRegistryDisplayParser
{
    public static IReadOnlyList<HomeAssistantRegistryDisplayEntry> Parse(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object)
            throw new FormatException("Home Assistant entity registry display result must be an object.");

        var categories = new Dictionary<int, string>();
        if (result.TryGetProperty("entity_categories", out var categoryElement) &&
            categoryElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in categoryElement.EnumerateObject())
            {
                if (int.TryParse(property.Name, out var index) &&
                    property.Value.ValueKind == JsonValueKind.String &&
                    property.Value.GetString() is { } category)
                {
                    categories[index] = category;
                }
            }
        }

        if (!result.TryGetProperty("entities", out var entities) || entities.ValueKind != JsonValueKind.Array)
            throw new FormatException("Home Assistant entity registry display result is missing entities.");

        var entries = new List<HomeAssistantRegistryDisplayEntry>();
        foreach (var entity in entities.EnumerateArray())
        {
            if (entity.ValueKind != JsonValueKind.Object)
                throw new FormatException("Home Assistant entity registry display entry must be an object.");

            var entityId = RequireString(entity, "ei");
            var platform = RequireString(entity, "pl");
            string? category = null;
            if (entity.TryGetProperty("ec", out var categoryIndex) &&
                categoryIndex.TryGetInt32(out var index))
            {
                category = categories.TryGetValue(index, out var resolved)
                    ? resolved
                    : $"unknown:{index}";
            }

            entries.Add(new HomeAssistantRegistryDisplayEntry(
                entityId,
                platform,
                GetString(entity, "ai"),
                GetString(entity, "di"),
                GetString(entity, "en"),
                category,
                entity.TryGetProperty("hb", out var hidden) && hidden.ValueKind == JsonValueKind.True));
        }

        return entries;
    }

    private static string RequireString(JsonElement obj, string name) =>
        GetString(obj, name) ?? throw new FormatException($"Home Assistant entity registry display entry is missing '{name}'.");

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public static class HomeAssistantSelectedInventoryBuilder
{
    public static HomeAssistantSelectedInventory Build(
        IReadOnlyCollection<HomeAssistantState> states,
        IReadOnlyCollection<HomeAssistantRegistryDisplayEntry> registry,
        IReadOnlyCollection<string> selectedEntityIds)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(selectedEntityIds);

        var issues = new List<DriverEngineeringIssue>();
        if (selectedEntityIds.Count == 0)
        {
            issues.Add(new DriverEngineeringIssue(
                "HA_SELECTION_REQUIRED",
                DriverEngineeringIssueSeverity.Information,
                "Home Assistant discovery requires explicit entity selection; whole-instance auto import is disabled."));
            return new HomeAssistantSelectedInventory(Array.Empty<HomeAssistantSelectedEntity>(), issues);
        }

        var stateById = states
            .GroupBy(x => x.EntityId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.Ordinal);
        var registryById = registry
            .GroupBy(x => x.EntityId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.Ordinal);

        var selected = new List<HomeAssistantSelectedEntity>();
        foreach (var entityId in selectedEntityIds
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Select(x => x.Trim())
                     .Distinct(StringComparer.Ordinal))
        {
            if (!stateById.TryGetValue(entityId, out var state))
            {
                issues.Add(new DriverEngineeringIssue(
                    "HA_SELECTED_ENTITY_NOT_FOUND",
                    DriverEngineeringIssueSeverity.Warning,
                    $"Selected Home Assistant entity '{entityId}' is not present in the current state inventory.",
                    entityId));
                continue;
            }

            var separator = entityId.IndexOf('.');
            if (separator <= 0 || separator == entityId.Length - 1)
            {
                issues.Add(new DriverEngineeringIssue(
                    "HA_ENTITY_ID_INVALID",
                    DriverEngineeringIssueSeverity.Warning,
                    $"Home Assistant entity id '{entityId}' is malformed.",
                    entityId));
                continue;
            }

            registryById.TryGetValue(entityId, out var registryEntry);
            var displayName = registryEntry?.DisplayName
                ?? FriendlyName(state.Attributes)
                ?? entityId;
            selected.Add(new HomeAssistantSelectedEntity(
                state,
                registryEntry,
                entityId[..separator],
                displayName));
        }

        return new HomeAssistantSelectedInventory(selected, issues);
    }

    private static string? FriendlyName(JsonElement attributes) =>
        attributes.ValueKind == JsonValueKind.Object &&
        attributes.TryGetProperty("friendly_name", out var name) &&
        name.ValueKind == JsonValueKind.String
            ? name.GetString()
            : null;
}