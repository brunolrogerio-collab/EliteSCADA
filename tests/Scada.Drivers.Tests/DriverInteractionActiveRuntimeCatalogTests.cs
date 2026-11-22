using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Interactions;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.Interactions;

namespace Scada.Drivers.Tests;

public sealed class DriverInteractionActiveRuntimeCatalogTests
{
    [Fact]
    public void DraftAndNonActivePreparation_DoNotChangeActiveResolvers()
    {
        var catalog = new ActiveDriverInteractionRuntimeCatalog();
        var active = Package("active");
        var draft = Package("draft");

        catalog.Commit(catalog.Prepare(active));
        var draftPrepared = catalog.Prepare(draft);

        AssertResolves(catalog, active);
        AssertDoesNotResolve(catalog, draft);

        _ = draftPrepared;
        AssertResolves(catalog, active);
    }

    [Fact]
    public void Commit_SwapsEventDefinitionRichCommandAndBindingAsOnePreparedGraph()
    {
        var catalog = new ActiveDriverInteractionRuntimeCatalog();
        var first = Package("first");
        var second = Package("second");

        catalog.Commit(catalog.Prepare(first));
        var preparedSecond = catalog.Prepare(second);

        AssertResolves(catalog, first);
        catalog.Commit(preparedSecond);

        AssertDoesNotResolve(catalog, first);
        AssertResolves(catalog, second);
        var descriptor = catalog.Describe();
        Assert.Equal(1, descriptor.EventDefinitionCount);
        Assert.Equal(1, descriptor.CapabilityEventReferenceCount);
        Assert.Equal(1, descriptor.RichCommandDefinitionCount);
        Assert.Equal(1, descriptor.DriverCommandBindingCount);
    }

    [Fact]
    public void InvalidPreparation_FailsClosedAndLeavesActiveGraphUntouched()
    {
        var catalog = new ActiveDriverInteractionRuntimeCatalog();
        var active = Package("active");
        catalog.Commit(catalog.Prepare(active));

        var invalid = Package("invalid") with
        {
            DriverCommandBindings =
            [
                Binding(
                    CommandId("invalid"),
                    Guid.NewGuid(),
                    EquipmentId("invalid"),
                    "cover.move.invalid")
            ]
        };

        Assert.Throws<InvalidDataException>(() => catalog.Prepare(invalid));
        AssertResolves(catalog, active);
        AssertDoesNotResolve(catalog, invalid);
    }

    [Fact]
    public void Rollback_RecommittingPriorPreparedGraphRestoresResolvers()
    {
        var catalog = new ActiveDriverInteractionRuntimeCatalog();
        var prior = Package("prior");
        var next = Package("next");
        var priorPrepared = catalog.Prepare(prior);
        var nextPrepared = catalog.Prepare(next);

        catalog.Commit(priorPrepared);
        catalog.Commit(nextPrepared);
        AssertResolves(catalog, next);

        catalog.Commit(priorPrepared);

        AssertResolves(catalog, prior);
        AssertDoesNotResolve(catalog, next);
    }

    [Fact]
    public void CapabilityEventReferences_AreCarriedBySameActiveGraph()
    {
        var catalog = new ActiveDriverInteractionRuntimeCatalog();
        var package = Package("refs");

        catalog.Commit(catalog.Prepare(package));

        var reference = Assert.Single(catalog.CapabilityEventReferences());
        Assert.Equal(EventId("refs"), reference.EventDefinitionId);
        Assert.Equal(EquipmentId("refs"), reference.EquipmentId);
        Assert.Equal("cover.main", reference.CapabilityId);
    }

    private static void AssertResolves(
        ActiveDriverInteractionRuntimeCatalog catalog,
        EngineeringPackage package)
    {
        var eventResolver = (ITransientEventDefinitionResolver)catalog;
        var commandResolver = (IRichCommandDefinitionResolver)catalog;
        var bindingResolver = (IRichCommandBindingResolver)catalog;

        Assert.True(eventResolver.TryResolve(EventId(Key(package)), out var eventDefinition));
        Assert.NotNull(eventDefinition);
        Assert.True(commandResolver.TryResolve(CommandId(Key(package)), out var commandDefinition));
        Assert.NotNull(commandDefinition);
        Assert.True(bindingResolver.TryResolve(CommandId(Key(package)), out var binding));
        Assert.NotNull(binding);
    }

    private static void AssertDoesNotResolve(
        ActiveDriverInteractionRuntimeCatalog catalog,
        EngineeringPackage package)
    {
        var eventResolver = (ITransientEventDefinitionResolver)catalog;
        var commandResolver = (IRichCommandDefinitionResolver)catalog;
        var bindingResolver = (IRichCommandBindingResolver)catalog;

        Assert.False(eventResolver.TryResolve(EventId(Key(package)), out _));
        Assert.False(commandResolver.TryResolve(CommandId(Key(package)), out _));
        Assert.False(bindingResolver.TryResolve(CommandId(Key(package)), out _));
    }

    private static string Key(EngineeringPackage package) =>
        package.RichCommandDefinitions!.Single().SemanticKey.Split('.').Last();

    private static EngineeringPackage Package(string key)
    {
        var equipmentId = EquipmentId(key);
        var dataSourceId = DataSourceId(key);
        var eventId = EventId(key);
        var commandId = CommandId(key);

        return new EngineeringPackage(
            "scada.engineering",
            23,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            DataSources:
            [
                new DataSourceEngineeringDto(
                    dataSourceId,
                    $"source.{key}",
                    $"Source {key}",
                    "test.driver")
            ],
            Equipment:
            [
                new EquipmentEngineeringDto(
                    equipmentId,
                    $"Plant.{key}",
                    $"Equipment {key}",
                    Capabilities:
                    [
                        new EquipmentCapabilityEngineeringDto(
                            "cover.main",
                            EquipmentCapabilityKinds.Cover)
                    ])
            ],
            TransientEventDefinitions:
            [
                new TransientEventDefinitionEngineeringDto(
                    eventId,
                    $"cover.stopped.{key}",
                    [
                        new TransientEventFieldDefinition(
                            "position",
                            new InteractionScalarSchema(
                                InteractionScalarKind.Percentage,
                                Minimum: 0,
                                Maximum: 100))
                    ],
                    equipmentId,
                    "cover.main")
            ],
            CapabilityEventReferences:
            [
                new CapabilityEventReferenceEngineeringDto(
                    equipmentId,
                    "cover.main",
                    "stopped",
                    eventId,
                    $"cover.stopped.{key}")
            ],
            RichCommandDefinitions:
            [
                new RichCommandDefinitionEngineeringDto(
                    commandId,
                    $"cover.move.{key}",
                    [
                        new RichCommandParameterDefinition(
                            "position",
                            new InteractionScalarSchema(
                                InteractionScalarKind.Percentage,
                                Minimum: 0,
                                Maximum: 100))
                    ])
            ],
            DriverCommandBindings:
            [
                Binding(commandId, dataSourceId, equipmentId, $"cover.move.{key}")
            ]);
    }

    private static DriverCommandBindingEngineeringDto Binding(
        Guid commandId,
        Guid dataSourceId,
        Guid equipmentId,
        string semanticOperationKey) =>
        new(
            commandId,
            dataSourceId,
            "cover-01",
            SemanticOperationKey: semanticOperationKey,
            Settings: [new DriverCommandBindingSetting("transition.profile", "normal")],
            EquipmentId: equipmentId,
            CapabilityId: "cover.main");

    private static Guid EquipmentId(string key) => StableGuid($"equipment:{key}");
    private static Guid DataSourceId(string key) => StableGuid($"source:{key}");
    private static Guid EventId(string key) => StableGuid($"event:{key}");
    private static Guid CommandId(string key) => StableGuid($"command:{key}");

    private static Guid StableGuid(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
