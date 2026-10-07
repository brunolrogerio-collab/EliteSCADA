using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Interactions;

namespace Scada.Core.Tests;

public sealed class InteractionConvergenceContractTests
{
    [Fact]
    public void Causality_ValidatesCorrelationCausationOriginAndHopBounds()
    {
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();

        var context = InteractionCausalityContract.Validate(
            new InteractionCausalityContext(
                correlationId,
                causationId,
                InteractionOrigin.Automation,
                HopCount: 3,
                MaximumHops: 8));

        Assert.Equal(correlationId, context.CorrelationId);
        Assert.Equal(causationId, context.CausationId);
        Assert.Equal(3, context.HopCount);
        Assert.Equal(8, context.MaximumHops);

        Assert.Throws<ArgumentException>(() =>
            InteractionCausalityContract.Validate(
                new InteractionCausalityContext(
                    Guid.Empty,
                    null,
                    InteractionOrigin.System)));

        Assert.Throws<ArgumentException>(() =>
            InteractionCausalityContract.Validate(
                new InteractionCausalityContext(
                    Guid.NewGuid(),
                    Guid.Empty,
                    InteractionOrigin.System)));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InteractionCausalityContract.Validate(
                new InteractionCausalityContext(
                    Guid.NewGuid(),
                    null,
                    InteractionOrigin.Automation,
                    HopCount: 5,
                    MaximumHops: 4)));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InteractionCausalityContract.Validate(
                new InteractionCausalityContext(
                    Guid.NewGuid(),
                    null,
                    InteractionOrigin.System,
                    MaximumHops: InteractionCausalityContract.AbsoluteMaximumHops + 1)));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InteractionCausalityContract.Validate(
                new InteractionCausalityContext(
                    Guid.NewGuid(),
                    null,
                    (InteractionOrigin)999)));
    }

    [Fact]
    public void EventToCommand_PreservesBoundedDiagnosticLineageAcrossSeparateContracts()
    {
        var correlationId = Guid.NewGuid();
        var eventDefinitionId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var eventDefinition = new TransientEventDefinition(
            eventDefinitionId,
            "button.double_click",
            Array.Empty<TransientEventFieldDefinition>());

        var occurrence = TransientEventContract.ValidateOccurrence(
            eventDefinition,
            new TransientEventOccurrence(
                eventId,
                eventDefinitionId,
                "button.double_click",
                new TransientEventSource(Guid.NewGuid(), "device-01"),
                Array.Empty<TransientEventFieldValue>(),
                DateTimeOffset.UtcNow,
                Causality: new InteractionCausalityContext(
                    correlationId,
                    null,
                    InteractionOrigin.Driver,
                    HopCount: 0,
                    MaximumHops: 8)));

        var commandDefinition = new RichCommandDefinition(
            Guid.NewGuid(),
            "scene.invoke",
            Array.Empty<RichCommandParameterDefinition>());
        var invocation = RichCommandContract.ValidateInvocation(
            commandDefinition,
            new RichCommandInvocation(
                Guid.NewGuid(),
                commandDefinition.CommandId,
                Array.Empty<RichCommandParameterValue>(),
                DateTimeOffset.UtcNow,
                new InteractionCausalityContext(
                    correlationId,
                    occurrence.EventId,
                    InteractionOrigin.ServerScript,
                    HopCount: 1,
                    MaximumHops: 8)));

        Assert.Equal(correlationId, occurrence.Causality!.CorrelationId);
        Assert.Null(occurrence.Causality.CausationId);
        Assert.Equal(correlationId, invocation.Causality!.CorrelationId);
        Assert.Equal(occurrence.EventId, invocation.Causality.CausationId);
        Assert.Equal(InteractionOrigin.ServerScript, invocation.Causality.Origin);
        Assert.NotEqual(occurrence.EventId, invocation.InvocationId);
    }

    [Fact]
    public void CapabilityEventReference_BindsSemanticRoleToExactEventDefinitionIdentity()
    {
        var equipmentId = Guid.NewGuid();
        var definition = new TransientEventDefinition(
            Guid.NewGuid(),
            "button.double_click",
            Array.Empty<TransientEventFieldDefinition>(),
            EquipmentId: equipmentId,
            CapabilityId: "button-main");

        var reference = CapabilityEventReferenceContract.Validate(
            definition,
            new CapabilityEventReference(
                equipmentId,
                "button-main",
                "doubleClick",
                definition.DefinitionId,
                definition.SemanticKey));

        Assert.Equal("doubleClick", reference.Role);
        Assert.Equal(definition.DefinitionId, reference.EventDefinitionId);
        Assert.Equal(CapabilityEventReferenceContract.Version, reference.Version);

        Assert.Throws<ArgumentException>(() =>
            CapabilityEventReferenceContract.Validate(
                definition,
                reference with { EventDefinitionId = Guid.NewGuid() }));

        Assert.Throws<ArgumentException>(() =>
            CapabilityEventReferenceContract.Validate(
                definition,
                reference with { CapabilityId = "different-capability" }));

        Assert.Throws<ArgumentException>(() =>
            CapabilityEventReferenceContract.Validate(
                definition,
                reference with { SemanticEventKey = "button.long_press" }));
    }

    [Fact]
    public void Causality_RemainsOptionalForIndependentEventAndCommandRoots()
    {
        var eventDefinition = new TransientEventDefinition(
            Guid.NewGuid(),
            "device.rejoined",
            Array.Empty<TransientEventFieldDefinition>());
        var occurrence = TransientEventContract.ValidateOccurrence(
            eventDefinition,
            new TransientEventOccurrence(
                Guid.NewGuid(),
                eventDefinition.DefinitionId,
                eventDefinition.SemanticKey,
                new TransientEventSource(Guid.NewGuid(), "device-02"),
                Array.Empty<TransientEventFieldValue>(),
                DateTimeOffset.UtcNow));

        var commandDefinition = new RichCommandDefinition(
            Guid.NewGuid(),
            "device.identify",
            Array.Empty<RichCommandParameterDefinition>());
        var invocation = RichCommandContract.ValidateInvocation(
            commandDefinition,
            new RichCommandInvocation(
                Guid.NewGuid(),
                commandDefinition.CommandId,
                Array.Empty<RichCommandParameterValue>(),
                DateTimeOffset.UtcNow));

        Assert.Null(occurrence.Causality);
        Assert.Null(invocation.Causality);
    }
}
