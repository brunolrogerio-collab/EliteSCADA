namespace Scada.Core.Events;

/// <summary>
/// Protocol-neutral semantic role reference from one Equipment Capability to a
/// first-class Transient Event definition. It is an S0 reference envelope only;
/// Engineering persistence/version migration belongs to a future slice.
/// </summary>
public sealed record CapabilityEventReference(
    Guid EquipmentId,
    string CapabilityId,
    string Role,
    Guid EventDefinitionId,
    string SemanticEventKey,
    int Version = CapabilityEventReferenceContract.Version);

public static class CapabilityEventReferenceContract
{
    public const int Version = 1;
    public const int MaximumCapabilityIdLength = 160;
    public const int MaximumRoleLength = 128;
    public const int MaximumSemanticEventKeyLength = 160;

    public static CapabilityEventReference Validate(CapabilityEventReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (reference.Version != Version)
            throw new ArgumentException(
                $"Capability Event Reference version '{reference.Version}' is unsupported; expected {Version}.",
                nameof(reference));
        if (reference.EquipmentId == Guid.Empty)
            throw new ArgumentException("Capability Event Reference EquipmentId is required.", nameof(reference));
        if (reference.EventDefinitionId == Guid.Empty)
            throw new ArgumentException("Capability Event Reference EventDefinitionId is required.", nameof(reference));

        return reference with
        {
            CapabilityId = BoundedKey(
                reference.CapabilityId,
                MaximumCapabilityIdLength,
                "CapabilityId",
                nameof(reference)),
            Role = BoundedKey(
                reference.Role,
                MaximumRoleLength,
                "role",
                nameof(reference)),
            SemanticEventKey = BoundedKey(
                reference.SemanticEventKey,
                MaximumSemanticEventKeyLength,
                "semantic event key",
                nameof(reference))
        };
    }

    public static CapabilityEventReference Validate(
        TransientEventDefinition definition,
        CapabilityEventReference reference)
    {
        var normalizedDefinition = TransientEventContract.NormalizeDefinition(definition);
        var normalizedReference = Validate(reference);

        if (normalizedReference.EventDefinitionId != normalizedDefinition.DefinitionId)
            throw new ArgumentException(
                "Capability Event Reference EventDefinitionId does not match the Transient Event definition.",
                nameof(reference));
        if (!StringComparer.Ordinal.Equals(
                normalizedReference.SemanticEventKey,
                normalizedDefinition.SemanticKey))
        {
            throw new ArgumentException(
                "Capability Event Reference semantic event key does not match the Transient Event definition.",
                nameof(reference));
        }
        if (normalizedDefinition.EquipmentId is null ||
            normalizedReference.EquipmentId != normalizedDefinition.EquipmentId)
        {
            throw new ArgumentException(
                "Capability Event Reference EquipmentId does not match the Transient Event definition.",
                nameof(reference));
        }
        if (normalizedDefinition.CapabilityId is null ||
            !StringComparer.Ordinal.Equals(
                normalizedReference.CapabilityId,
                normalizedDefinition.CapabilityId))
        {
            throw new ArgumentException(
                "Capability Event Reference CapabilityId does not match the Transient Event definition.",
                nameof(reference));
        }

        return normalizedReference;
    }

    private static string BoundedKey(
        string? value,
        int maximumLength,
        string field,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Capability Event Reference {field} is required.", parameterName);

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException(
                $"Capability Event Reference {field} exceeds {maximumLength} characters.",
                parameterName);
        if (!normalized.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '.' or '_' or '-' or ':'))
        {
            throw new ArgumentException(
                $"Capability Event Reference {field} contains unsupported characters.",
                parameterName);
        }

        return normalized;
    }
}
