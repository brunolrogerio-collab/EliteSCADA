namespace Scada.Core.Events;

/// <summary>
/// Stable, protocol-neutral Engineering definition for one class of operational
/// process event. This is intentionally distinct from Alarm and security Audit.
/// </summary>
public sealed record OperationalEventDefinition(
    Guid Id,
    string Key,
    string Name,
    string Type,
    string Category,
    string Source,
    string? Area = null,
    string? EquipmentPath = null,
    Guid? TagId = null,
    string? TagPath = null,
    string? Message = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>
/// Dynamic context supplied by the Active Runtime at emission time. Operator and
/// command fields are optional because many legitimate process events are produced
/// autonomously rather than by a human action.
/// </summary>
public sealed record OperationalEventEmissionContext(
    string? Operator = null,
    string? Operation = null,
    Guid? CommandId = null,
    string? CommandKey = null,
    string? Message = null,
    IReadOnlyDictionary<string, string>? Context = null);

/// <summary>
/// Immutable occurrence published on the Runtime event bus and persisted by the
/// operational-event history subscriber.
/// </summary>
public sealed record OperationalEventOccurred(
    Guid EventId,
    Guid DefinitionId,
    string DefinitionKey,
    string Type,
    string Category,
    string Source,
    string? Area,
    string? EquipmentPath,
    Guid? TagId,
    string? TagPath,
    string? Operator,
    string? Operation,
    Guid? CommandId,
    string? CommandKey,
    string? Message,
    IReadOnlyDictionary<string, string> Context,
    DateTimeOffset OccurredAt) : IScadaEvent;

public static class OperationalEventContract
{
    public const int Version = 1;

    public static OperationalEventDefinition Normalize(OperationalEventDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Id == Guid.Empty)
            throw new ArgumentException("Operational Event stable ID is required.", nameof(definition));

        return definition with
        {
            Key = Required(definition.Key, "key", 160),
            Name = Required(definition.Name, "name", 240),
            Type = Required(definition.Type, "type", 120),
            Category = Required(definition.Category, "category", 120),
            Source = Required(definition.Source, "source", 240),
            Area = Optional(definition.Area, 240),
            EquipmentPath = Optional(definition.EquipmentPath, 500),
            TagPath = Optional(definition.TagPath, 500),
            Message = Optional(definition.Message, 4000),
            Metadata = Copy(definition.Metadata)
        };
    }

    public static OperationalEventOccurred CreateOccurrence(
        OperationalEventDefinition definition,
        OperationalEventEmissionContext? emission = null,
        DateTimeOffset? occurredAt = null,
        Guid? eventId = null)
    {
        var normalized = Normalize(definition);
        emission ??= new OperationalEventEmissionContext();
        var timestamp = (occurredAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var id = eventId ?? Guid.NewGuid();
        if (id == Guid.Empty)
            throw new ArgumentException("Operational Event occurrence ID cannot be empty.", nameof(eventId));

        var commandId = emission.CommandId == Guid.Empty ? null : emission.CommandId;
        return new OperationalEventOccurred(
            id,
            normalized.Id,
            normalized.Key,
            normalized.Type,
            normalized.Category,
            normalized.Source,
            normalized.Area,
            normalized.EquipmentPath,
            normalized.TagId,
            normalized.TagPath,
            Optional(emission.Operator, 240),
            Optional(emission.Operation, 240),
            commandId,
            Optional(emission.CommandKey, 240),
            Optional(emission.Message, 4000) ?? normalized.Message,
            MergeContext(normalized.Metadata, emission.Context),
            timestamp);
    }

    private static string Required(string? value, string field, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Operational Event {field} is required.");
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"Operational Event {field} exceeds {maximumLength} characters.");
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"Operational Event text exceeds {maximumLength} characters.");
        return normalized;
    }

    private static IReadOnlyDictionary<string, string>? Copy(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0) return null;
        if (values.Count > 128)
            throw new ArgumentException("Operational Event context/metadata supports at most 128 entries.");

        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            var key = Required(pair.Key, "context key", 160);
            var value = pair.Value ?? string.Empty;
            if (value.Length > 4000)
                throw new ArgumentException($"Operational Event context value '{key}' exceeds 4000 characters.");
            copy[key] = value;
        }
        return copy;
    }

    private static IReadOnlyDictionary<string, string> MergeContext(
        IReadOnlyDictionary<string, string>? metadata,
        IReadOnlyDictionary<string, string>? dynamicContext)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        var authored = Copy(metadata);
        if (authored is not null)
        {
            foreach (var pair in authored)
                merged[pair.Key] = pair.Value;
        }

        var runtime = Copy(dynamicContext);
        if (runtime is not null)
        {
            foreach (var pair in runtime)
                merged[pair.Key] = pair.Value;
        }

        if (merged.Count > 128)
            throw new ArgumentException("Operational Event merged context supports at most 128 entries.");

        return merged;
    }
}