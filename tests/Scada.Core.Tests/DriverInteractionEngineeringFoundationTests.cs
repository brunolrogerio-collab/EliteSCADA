using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Interactions;
using Scada.Engineering.Contracts;
using Scada.Engineering.Interactions;

namespace Scada.Core.Tests;

public sealed class DriverInteractionEngineeringFoundationTests
{
    [Fact]
    public void Mapper_RoundTripsCanonicalEventAndRichCommandShapes()
    {
        var equipmentId = Guid.NewGuid();
        var eventDefinition = new TransientEventDefinitionEngineeringDto(
            Guid.NewGuid(),
            "cover.stopped",
            [
                new TransientEventFieldDefinition(
                    "position",
                    new InteractionScalarSchema(
                        InteractionScalarKind.Percentage,
                        Minimum: 0,
                        Maximum: 100),
                    Required: true)
            ],
            equipmentId,
            "cover.main",
            "Cover stopped event");

        var normalizedEvent = DriverInteractionEngineeringMapper.ToCore(eventDefinition);
        var eventRoundTrip = DriverInteractionEngineeringMapper.ToEngineering(normalizedEvent);

        Assert.Equal(eventDefinition.DefinitionId, eventRoundTrip.DefinitionId);
        Assert.Equal("cover.stopped", eventRoundTrip.SemanticKey);
        Assert.Single(eventRoundTrip.Fields);

        var command = new RichCommandDefinitionEngineeringDto(
            Guid.NewGuid(),
            "cover.move",
            [
                new RichCommandParameterDefinition(
                    "position",
                    new InteractionScalarSchema(
                        InteractionScalarKind.Percentage,
                        Minimum: 0,
                        Maximum: 100),
                    Required: true)
            ],
            "Move cover");

        var normalizedCommand = DriverInteractionEngineeringMapper.ToCore(command);
        var commandRoundTrip = DriverInteractionEngineeringMapper.ToEngineering(normalizedCommand);

        Assert.Equal(command.CommandId, commandRoundTrip.CommandId);
        Assert.Equal("cover.move", commandRoundTrip.SemanticKey);
        Assert.Single(commandRoundTrip.Parameters);
    }

    [Fact]
    public void Registry_NormalizesAndOrdersInteractionCollections()
    {
        var registry = new InMemoryDriverInteractionEngineeringRegistry();
        var first = new RichCommandDefinitionEngineeringDto(
            Guid.NewGuid(),
            "a.command",
            Array.Empty<RichCommandParameterDefinition>());
        var second = new RichCommandDefinitionEngineeringDto(
            Guid.NewGuid(),
            "b.command",
            Array.Empty<RichCommandParameterDefinition>());

        registry.UpsertRichCommandDefinition(second);
        registry.UpsertRichCommandDefinition(first);

        var snapshot = registry.SnapshotRichCommandDefinitions().ToArray();

        Assert.Equal(2, snapshot.Length);
        Assert.Equal("a.command", snapshot[0].SemanticKey);
        Assert.Equal("b.command", snapshot[1].SemanticKey);
    }

    [Fact]
    public void ActiveGraph_RejectsDanglingCapabilityEventReference()
    {
        var equipmentId = Guid.NewGuid();
        var package = CreatePackage(
            equipmentId,
            eventReferences:
            [
                new CapabilityEventReferenceEngineeringDto(
                    equipmentId,
                    "cover.main",
                    "stopped",
                    Guid.NewGuid(),
                    "cover.stopped")
            ]);

        var exception = Assert.Throws<InvalidDataException>(
            () => DriverInteractionEngineeringValidator.NormalizeActiveGraph(package));

        Assert.Contains("unknown Event Definition", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveGraph_RejectsBindingWithUnknownDataSource()
    {
        var equipmentId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var package = CreatePackage(
            equipmentId,
            commands:
            [
                new RichCommandDefinitionEngineeringDto(
                    commandId,
                    "cover.move",
                    Array.Empty<RichCommandParameterDefinition>())
            ],
            bindings:
            [
                new DriverCommandBindingEngineeringDto(
                    commandId,
                    Guid.NewGuid(),
                    "cover-01",
                    "cover.move",
                    EquipmentId: equipmentId,
                    CapabilityId: "cover.main")
            ]);

        var exception = Assert.Throws<InvalidDataException>(
            () => DriverInteractionEngineeringValidator.NormalizeActiveGraph(package));

        Assert.Contains("unknown Data Source", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveGraph_RejectsRawProtocolOrProtectedMaterialBindingSettingsThroughCoreContract()
    {
        var equipmentId = Guid.NewGuid();
        var dataSourceId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var package = CreatePackage(
            equipmentId,
            dataSourceId,
            commands:
            [
                new RichCommandDefinitionEngineeringDto(
                    commandId,
                    "cover.move",
                    Array.Empty<RichCommandParameterDefinition>())
            ],
            bindings:
            [
                new DriverCommandBindingEngineeringDto(
                    commandId,
                    dataSourceId,
                    "cover-01",
                    "cover.move",
                    [new DriverCommandBindingSetting("mqtt.topic", "plant/raw")],
                    equipmentId,
                    "cover.main")
            ]);

        Assert.Throws<ArgumentException>(
            () => DriverInteractionEngineeringValidator.NormalizeActiveGraph(package));
    }

    private static EngineeringPackage CreatePackage(
        Guid equipmentId,
        Guid? dataSourceId = null,
        IReadOnlyCollection<TransientEventDefinitionEngineeringDto>? events = null,
        IReadOnlyCollection<CapabilityEventReferenceEngineeringDto>? eventReferences = null,
        IReadOnlyCollection<RichCommandDefinitionEngineeringDto>? commands = null,
        IReadOnlyCollection<DriverCommandBindingEngineeringDto>? bindings = null)
    {
        return new EngineeringPackage(
            "scada.engineering",
            23,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            DataSources: dataSourceId.HasValue
                ? [new DataSourceEngineeringDto(dataSourceId, "test.source", "Test Source", "test.driver")]
                : Array.Empty<DataSourceEngineeringDto>(),
            Equipment:
            [
                new EquipmentEngineeringDto(
                    equipmentId,
                    "Plant.Cover01",
                    "Cover 01",
                    Capabilities:
                    [
                        new EquipmentCapabilityEngineeringDto(
                            "cover.main",
                            EquipmentCapabilityKinds.Cover)
                    ])
            ],
            TransientEventDefinitions: events ?? Array.Empty<TransientEventDefinitionEngineeringDto>(),
            CapabilityEventReferences: eventReferences ?? Array.Empty<CapabilityEventReferenceEngineeringDto>(),
            RichCommandDefinitions: commands ?? Array.Empty<RichCommandDefinitionEngineeringDto>(),
            DriverCommandBindings: bindings ?? Array.Empty<DriverCommandBindingEngineeringDto>());
    }
}
