using Scada.Core.Interactions;

namespace Scada.Core.Events;

public sealed record TransientEventSource(
    Guid DataSourceId,
    string StableDeviceIdentity,
    Guid? EquipmentId = null,
    string? CapabilityId = null);

public sealed record TransientEventFieldDefinition(
    string Key,
    InteractionScalarSchema Schema,
    bool Required = false);

public sealed record TransientEventDefinition(
    Guid DefinitionId,
    string SemanticKey,
    IReadOnlyList<TransientEventFieldDefinition> Fields,
    Guid? EquipmentId = null,
    string? CapabilityId = null,
    string? Description = null);

public enum TransientEventTimestampOrigin
{
    DeviceClock,
    ProtocolMetadata,
    BridgeClock
}

public sealed record TransientEventTimestamp(
    DateTimeOffset Value,
    TransientEventTimestampOrigin Origin);

public sealed record TransientEventEvidence(
    long? Sequence = null,
    long? Counter = null);

public sealed record TransientEventFieldValue(
    string Key,
    InteractionScalarValue Value);

/// <summary>
/// Protocol-neutral transient occurrence contract. It deliberately does not
/// implement IScadaEvent in S0: publication/dispatch belongs to the future
/// bounded Runtime slice rather than this shared-contract lock.
/// </summary>
public sealed record TransientEventOccurrence(
    Guid EventId,
    Guid DefinitionId,
    string SemanticKey,
    TransientEventSource Source,
    IReadOnlyList<TransientEventFieldValue> Payload,
    DateTimeOffset ObservedAt,
    TransientEventTimestamp? OccurredAt = null,
    TransientEventEvidence? Evidence = null);

public static class TransientEventContract
{
    public const int Version = 1;
    public const int MaximumFieldCount = 64;
    public const int MaximumKeyLength = 160;
    public const int MaximumDescriptionLength = 1000;
    public const int MaximumSourceIdentityLength = 500;
    public const int MaximumCapabilityIdLength = 160;

    public static TransientEventDefinition NormalizeDefinition(TransientEventDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.DefinitionId == Guid.Empty)
            throw new ArgumentException("Transient Event DefinitionId is required.", nameof(definition));

        var semanticKey = SemanticKey(definition.SemanticKey, nameof(definition));
        var capabilityId = Optional(definition.CapabilityId, MaximumCapabilityIdLength, "CapabilityId");
        if (capabilityId is not null && definition.EquipmentId is null)
            throw new ArgumentException("Transient Event CapabilityId requires EquipmentId.", nameof(definition));

        if (definition.Fields is null)
            throw new ArgumentException("Transient Event field schema is required.", nameof(definition));
        if (definition.Fields.Count > MaximumFieldCount)
            throw new ArgumentException($"Transient Event supports at most {MaximumFieldCount} fields.", nameof(definition));

        var fields = new List<TransientEventFieldDefinition>(definition.Fields.Count);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in definition.Fields)
        {
            ArgumentNullException.ThrowIfNull(field);
            var key = FieldKey(field.Key, nameof(definition));
            if (!keys.Add(key))
                throw new ArgumentException($"Transient Event field '{key}' is duplicated.", nameof(definition));

            fields.Add(field with
            {
                Key = key,
                Schema = InteractionScalarContract.NormalizeSchema(field.Schema)
            });
        }

        fields.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));

        return definition with
        {
            SemanticKey = semanticKey,
            Fields = fields,
            CapabilityId = capabilityId,
            Description = Optional(definition.Description, MaximumDescriptionLength, "description")
        };
    }

    public static TransientEventOccurrence ValidateOccurrence(
        TransientEventDefinition definition,
        TransientEventOccurrence occurrence)
    {
        var normalized = NormalizeDefinition(definition);
        ArgumentNullException.ThrowIfNull(occurrence);

        if (occurrence.EventId == Guid.Empty)
            throw new ArgumentException("Transient Event EventId is required.", nameof(occurrence));
        if (occurrence.EventId == normalized.DefinitionId)
            throw new ArgumentException("Transient Event occurrence identity must be distinct from DefinitionId.", nameof(occurrence));
        if (occurrence.DefinitionId != normalized.DefinitionId)
            throw new ArgumentException("Transient Event DefinitionId does not match the definition.", nameof(occurrence));
        if (!StringComparer.Ordinal.Equals(SemanticKey(occurrence.SemanticKey, nameof(occurrence)), normalized.SemanticKey))
            throw new ArgumentException("Transient Event semantic key does not match the definition.", nameof(occurrence));

        var source = NormalizeSource(occurrence.Source);
        if (normalized.EquipmentId is not null && source.EquipmentId != normalized.EquipmentId)
            throw new ArgumentException("Transient Event source EquipmentId does not match the definition.", nameof(occurrence));
        if (normalized.CapabilityId is not null &&
            !StringComparer.Ordinal.Equals(source.CapabilityId, normalized.CapabilityId))
            throw new ArgumentException("Transient Event source CapabilityId does not match the definition.", nameof(occurrence));

        if (occurrence.ObservedAt == default)
            throw new ArgumentException("Transient Event ObservedAt is required.", nameof(occurrence));

        ValidateEvidence(occurrence.Evidence);

        var supplied = new Dictionary<string, TransientEventFieldValue>(StringComparer.Ordinal);
        foreach (var fieldValue in occurrence.Payload ?? Array.Empty<TransientEventFieldValue>())
        {
            ArgumentNullException.ThrowIfNull(fieldValue);
            var key = FieldKey(fieldValue.Key, nameof(occurrence));
            if (!supplied.TryAdd(key, fieldValue with { Key = key }))
                throw new ArgumentException($"Transient Event payload field '{key}' is duplicated.", nameof(occurrence));
        }

        var schemaByKey = normalized.Fields.ToDictionary(field => field.Key, StringComparer.Ordinal);
        foreach (var suppliedKey in supplied.Keys)
        {
            if (!schemaByKey.ContainsKey(suppliedKey))
                throw new ArgumentException($"Transient Event payload field '{suppliedKey}' is not defined.", nameof(occurrence));
        }

        foreach (var field in normalized.Fields)
        {
            if (!supplied.TryGetValue(field.Key, out var value))
            {
                if (field.Required)
                    throw new ArgumentException($"Transient Event required payload field '{field.Key}' is missing.", nameof(occurrence));
                continue;
            }

            supplied[field.Key] = value with
            {
                Value = InteractionScalarContract.Validate(field.Schema, value.Value)
            };
        }

        var payload = supplied.Values
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToArray();

        return occurrence with
        {
            SemanticKey = normalized.SemanticKey,
            Source = source,
            Payload = payload,
            ObservedAt = occurrence.ObservedAt.ToUniversalTime(),
            OccurredAt = occurrence.OccurredAt is null
                ? null
                : occurrence.OccurredAt with { Value = occurrence.OccurredAt.Value.ToUniversalTime() }
        };
    }

    public static TransientEventSource NormalizeSource(TransientEventSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.DataSourceId == Guid.Empty)
            throw new ArgumentException("Transient Event source DataSourceId is required.", nameof(source));

        var stableDeviceIdentity = Required(
            source.StableDeviceIdentity,
            MaximumSourceIdentityLength,
            "stable device identity");
        var capabilityId = Optional(source.CapabilityId, MaximumCapabilityIdLength, "CapabilityId");
        if (capabilityId is not null && source.EquipmentId is null)
            throw new ArgumentException("Transient Event source CapabilityId requires EquipmentId.", nameof(source));

        return source with
        {
            StableDeviceIdentity = stableDeviceIdentity,
            CapabilityId = capabilityId
        };
    }

    public static void ValidateEvidence(TransientEventEvidence? evidence)
    {
        if (evidence is null)
            return;
        if (evidence.Sequence is < 0)
            throw new ArgumentOutOfRangeException(nameof(evidence), "Transient Event sequence evidence cannot be negative.");
        if (evidence.Counter is < 0)
            throw new ArgumentOutOfRangeException(nameof(evidence), "Transient Event counter evidence cannot be negative.");
    }

    private static string SemanticKey(string? value, string parameterName)
    {
        var key = Required(value, MaximumKeyLength, "semantic key");
        if (!IsBoundedKey(key))
            throw new ArgumentException("Transient Event semantic key contains unsupported characters.", parameterName);
        return key;
    }

    private static string FieldKey(string? value, string parameterName)
    {
        var key = Required(value, MaximumKeyLength, "field key");
        if (!IsBoundedKey(key))
            throw new ArgumentException("Transient Event field key contains unsupported characters.", parameterName);
        return key;
    }

    private static bool IsBoundedKey(string value)
        => value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '.' or '_' or '-' or ':');

    private static string Required(string? value, int maximumLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Transient Event {field} is required.");
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"Transient Event {field} exceeds {maximumLength} characters.");
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return Required(value, maximumLength, field);
    }
}
