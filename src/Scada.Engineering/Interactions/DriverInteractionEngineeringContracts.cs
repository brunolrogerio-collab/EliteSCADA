using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.Interactions;

public sealed record TransientEventDefinitionEngineeringDto(
    Guid DefinitionId,
    string SemanticKey,
    IReadOnlyCollection<TransientEventFieldDefinition> Fields,
    Guid? EquipmentId = null,
    string? CapabilityId = null,
    string? Description = null);

public sealed record CapabilityEventReferenceEngineeringDto(
    Guid EquipmentId,
    string CapabilityId,
    string Role,
    Guid EventDefinitionId,
    string SemanticEventKey,
    int Version = CapabilityEventReferenceContract.Version);

public sealed record RichCommandDefinitionEngineeringDto(
    Guid CommandId,
    string SemanticKey,
    IReadOnlyCollection<RichCommandParameterDefinition> Parameters,
    string? Description = null);

public sealed record DriverCommandBindingEngineeringDto(
    Guid CommandId,
    Guid DataSourceId,
    string StableDeviceIdentity,
    string SemanticOperationKey,
    IReadOnlyCollection<DriverCommandBindingSetting>? Settings = null,
    Guid? EquipmentId = null,
    string? CapabilityId = null,
    int Version = RichCommandContract.DriverBindingVersion);

public static class DriverInteractionEngineeringMapper
{
    public static TransientEventDefinition ToCore(TransientEventDefinitionEngineeringDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return TransientEventContract.NormalizeDefinition(new TransientEventDefinition(
            dto.DefinitionId,
            dto.SemanticKey,
            dto.Fields?.ToArray() ?? throw new ArgumentException("Transient Event Engineering fields are required.", nameof(dto)),
            dto.EquipmentId,
            dto.CapabilityId,
            dto.Description));
    }

    public static TransientEventDefinitionEngineeringDto ToEngineering(TransientEventDefinition definition)
    {
        var normalized = TransientEventContract.NormalizeDefinition(definition);
        return new(
            normalized.DefinitionId,
            normalized.SemanticKey,
            normalized.Fields.ToArray(),
            normalized.EquipmentId,
            normalized.CapabilityId,
            normalized.Description);
    }

    public static CapabilityEventReference ToCore(CapabilityEventReferenceEngineeringDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return CapabilityEventReferenceContract.Validate(new CapabilityEventReference(
            dto.EquipmentId,
            dto.CapabilityId,
            dto.Role,
            dto.EventDefinitionId,
            dto.SemanticEventKey,
            dto.Version));
    }

    public static CapabilityEventReferenceEngineeringDto ToEngineering(CapabilityEventReference reference)
    {
        var normalized = CapabilityEventReferenceContract.Validate(reference);
        return new(
            normalized.EquipmentId,
            normalized.CapabilityId,
            normalized.Role,
            normalized.EventDefinitionId,
            normalized.SemanticEventKey,
            normalized.Version);
    }

    public static RichCommandDefinition ToCore(RichCommandDefinitionEngineeringDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return RichCommandContract.NormalizeDefinition(new RichCommandDefinition(
            dto.CommandId,
            dto.SemanticKey,
            dto.Parameters?.ToArray() ?? throw new ArgumentException("Rich Command Engineering parameters are required.", nameof(dto)),
            dto.Description));
    }

    public static RichCommandDefinitionEngineeringDto ToEngineering(RichCommandDefinition definition)
    {
        var normalized = RichCommandContract.NormalizeDefinition(definition);
        return new(
            normalized.CommandId,
            normalized.SemanticKey,
            normalized.Parameters.ToArray(),
            normalized.Description);
    }

    public static DriverCommandBinding ToCore(DriverCommandBindingEngineeringDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return RichCommandContract.NormalizeBinding(new DriverCommandBinding(
            dto.CommandId,
            dto.DataSourceId,
            dto.StableDeviceIdentity,
            dto.SemanticOperationKey,
            dto.Settings?.ToArray(),
            dto.EquipmentId,
            dto.CapabilityId,
            dto.Version));
    }

    public static DriverCommandBindingEngineeringDto ToEngineering(DriverCommandBinding binding)
    {
        var normalized = RichCommandContract.NormalizeBinding(binding);
        return new(
            normalized.CommandId,
            normalized.DataSourceId,
            normalized.StableDeviceIdentity,
            normalized.SemanticOperationKey,
            normalized.Settings?.ToArray(),
            normalized.EquipmentId,
            normalized.CapabilityId,
            normalized.Version);
    }
}

public sealed record DriverInteractionCoreGraph(
    IReadOnlyCollection<TransientEventDefinition> EventDefinitions,
    IReadOnlyCollection<CapabilityEventReference> EventReferences,
    IReadOnlyCollection<RichCommandDefinition> RichCommandDefinitions,
    IReadOnlyCollection<DriverCommandBinding> DriverCommandBindings);

public static class DriverInteractionEngineeringValidator
{
    public static DriverInteractionCoreGraph NormalizeActiveGraph(EngineeringPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var eventDefinitions = (package.TransientEventDefinitions ?? Array.Empty<TransientEventDefinitionEngineeringDto>())
            .Select(DriverInteractionEngineeringMapper.ToCore)
            .ToArray();
        EnsureUnique(
            eventDefinitions.Select(definition => definition.DefinitionId),
            "Transient Event DefinitionId");
        EnsureUnique(
            eventDefinitions.Select(definition => definition.SemanticKey),
            "Transient Event semantic key",
            StringComparer.Ordinal);

        var richCommandDefinitions = (package.RichCommandDefinitions ?? Array.Empty<RichCommandDefinitionEngineeringDto>())
            .Select(DriverInteractionEngineeringMapper.ToCore)
            .ToArray();
        EnsureUnique(
            richCommandDefinitions.Select(definition => definition.CommandId),
            "Rich Command CommandId");
        EnsureUnique(
            richCommandDefinitions.Select(definition => definition.SemanticKey),
            "Rich Command semantic key",
            StringComparer.Ordinal);

        var equipment = BuildEquipmentIndex(package.Equipment);
        var dataSources = BuildDataSourceIndex(package.DataSources);

        foreach (var definition in eventDefinitions)
            ValidateEquipmentReference(equipment, definition.EquipmentId, definition.CapabilityId, "Transient Event definition");

        var eventById = eventDefinitions.ToDictionary(definition => definition.DefinitionId);
        var eventReferences = (package.CapabilityEventReferences ?? Array.Empty<CapabilityEventReferenceEngineeringDto>())
            .Select(DriverInteractionEngineeringMapper.ToCore)
            .ToArray();
        EnsureUnique(
            eventReferences.Select(reference => new EventReferenceIdentity(
                reference.EquipmentId,
                reference.CapabilityId,
                reference.Role)),
            "Capability Event Reference identity");

        foreach (var reference in eventReferences)
        {
            ValidateEquipmentReference(
                equipment,
                reference.EquipmentId,
                reference.CapabilityId,
                "Capability Event Reference");

            if (!eventById.TryGetValue(reference.EventDefinitionId, out var definition))
                throw new InvalidDataException(
                    $"Capability Event Reference '{reference.EquipmentId:D}/{reference.CapabilityId}/{reference.Role}' points to unknown Event Definition '{reference.EventDefinitionId:D}'.");

            _ = CapabilityEventReferenceContract.Validate(definition, reference);
        }

        var commandById = richCommandDefinitions.ToDictionary(definition => definition.CommandId);
        var bindings = (package.DriverCommandBindings ?? Array.Empty<DriverCommandBindingEngineeringDto>())
            .Select(DriverInteractionEngineeringMapper.ToCore)
            .ToArray();
        EnsureUnique(
            bindings.Select(binding => binding.CommandId),
            "Driver Command Binding CommandId");

        foreach (var binding in bindings)
        {
            if (!commandById.TryGetValue(binding.CommandId, out var definition))
                throw new InvalidDataException(
                    $"Driver Command Binding for '{binding.CommandId:D}' points to an unknown Rich Command definition.");

            _ = RichCommandContract.ValidateBinding(definition, binding);

            if (!dataSources.ContainsKey(binding.DataSourceId))
                throw new InvalidDataException(
                    $"Driver Command Binding for '{binding.CommandId:D}' points to unknown Data Source '{binding.DataSourceId:D}'.");

            ValidateEquipmentReference(
                equipment,
                binding.EquipmentId,
                binding.CapabilityId,
                "Driver Command Binding");
        }

        return new DriverInteractionCoreGraph(
            eventDefinitions,
            eventReferences,
            richCommandDefinitions,
            bindings);
    }

    private static Dictionary<Guid, EquipmentEngineeringDto> BuildEquipmentIndex(
        IReadOnlyCollection<EquipmentEngineeringDto>? candidates)
    {
        var result = new Dictionary<Guid, EquipmentEngineeringDto>();
        foreach (var equipment in candidates ?? Array.Empty<EquipmentEngineeringDto>())
        {
            if (equipment is null || !equipment.Id.HasValue)
                continue;
            if (equipment.Id == Guid.Empty)
                throw new InvalidDataException("Equipment identity cannot be empty.");
            if (!result.TryAdd(equipment.Id.Value, equipment))
                throw new InvalidDataException($"Equipment identity '{equipment.Id.Value:D}' is duplicated.");

            var capabilityIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var capability in equipment.Capabilities ?? Array.Empty<EquipmentCapabilityEngineeringDto>())
            {
                if (capability is null || string.IsNullOrWhiteSpace(capability.Id))
                    throw new InvalidDataException($"Equipment '{equipment.Id.Value:D}' contains an invalid Capability identity.");
                if (!capabilityIds.Add(capability.Id))
                    throw new InvalidDataException(
                        $"Equipment '{equipment.Id.Value:D}' contains duplicate Capability '{capability.Id}'.");
            }
        }

        return result;
    }

    private static Dictionary<Guid, DataSourceEngineeringDto> BuildDataSourceIndex(
        IReadOnlyCollection<DataSourceEngineeringDto>? candidates)
    {
        var result = new Dictionary<Guid, DataSourceEngineeringDto>();
        foreach (var dataSource in candidates ?? Array.Empty<DataSourceEngineeringDto>())
        {
            if (dataSource is null || !dataSource.Id.HasValue)
                continue;
            if (dataSource.Id == Guid.Empty)
                throw new InvalidDataException("Data Source identity cannot be empty.");
            if (!result.TryAdd(dataSource.Id.Value, dataSource))
                throw new InvalidDataException($"Data Source identity '{dataSource.Id.Value:D}' is duplicated.");
        }

        return result;
    }

    private static void ValidateEquipmentReference(
        IReadOnlyDictionary<Guid, EquipmentEngineeringDto> equipment,
        Guid? equipmentId,
        string? capabilityId,
        string entity)
    {
        if (!equipmentId.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(capabilityId))
                throw new InvalidDataException($"{entity} CapabilityId requires EquipmentId.");
            return;
        }

        if (!equipment.TryGetValue(equipmentId.Value, out var target))
            throw new InvalidDataException($"{entity} points to unknown Equipment '{equipmentId.Value:D}'.");

        if (string.IsNullOrWhiteSpace(capabilityId))
            return;

        if (!(target.Capabilities ?? Array.Empty<EquipmentCapabilityEngineeringDto>())
            .Any(capability => capability is not null && StringComparer.Ordinal.Equals(capability.Id, capabilityId)))
        {
            throw new InvalidDataException(
                $"{entity} points to unknown Capability '{capabilityId}' on Equipment '{equipmentId.Value:D}'.");
        }
    }

    private static void EnsureUnique<T>(IEnumerable<T> values, string field)
        where T : notnull =>
        EnsureUnique(values, field, EqualityComparer<T>.Default);

    private static void EnsureUnique<T>(
        IEnumerable<T> values,
        string field,
        IEqualityComparer<T> comparer)
        where T : notnull
    {
        var seen = new HashSet<T>(comparer);
        foreach (var value in values)
        {
            if (!seen.Add(value))
                throw new InvalidDataException($"{field} '{value}' is duplicated.");
        }
    }

    private readonly record struct EventReferenceIdentity(
        Guid EquipmentId,
        string CapabilityId,
        string Role);
}
