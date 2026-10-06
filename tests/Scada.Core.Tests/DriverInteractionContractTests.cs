using Scada.Core.Events;
using Scada.Core.Interactions;

namespace Scada.Core.Tests;

public sealed class DriverInteractionContractTests
{
    [Fact]
    public void SharedScalar_AcceptsAllFrozenKindsWithCanonicalRepresentations()
    {
        var cases = new[]
        {
            (new InteractionScalarSchema(InteractionScalarKind.Boolean), InteractionScalarValue.Boolean(true), "true"),
            (new InteractionScalarSchema(InteractionScalarKind.Integer, Minimum: -10, Maximum: 10), InteractionScalarValue.Integer(7), "7"),
            (new InteractionScalarSchema(InteractionScalarKind.Number, Minimum: 0, Maximum: 10), InteractionScalarValue.Number(2.5m), "2.5"),
            (new InteractionScalarSchema(InteractionScalarKind.String, MaximumLength: 12), InteractionScalarValue.String("hello"), "hello"),
            (new InteractionScalarSchema(InteractionScalarKind.Enum, EnumValues: new[] { "auto", "manual" }), InteractionScalarValue.Enum("auto"), "auto"),
            (new InteractionScalarSchema(InteractionScalarKind.Duration, Minimum: 0, Maximum: 5000, Unit: "ms"), InteractionScalarValue.Duration(TimeSpan.FromMilliseconds(500)), "00:00:00.5000000"),
            (new InteractionScalarSchema(InteractionScalarKind.Percentage), InteractionScalarValue.Percentage(40m), "40")
        };

        foreach (var (schema, value, expected) in cases)
            Assert.Equal(expected, InteractionScalarContract.Validate(schema, value).SerializedValue);
    }

    [Fact]
    public void SharedScalar_RejectsInvalidEnumPercentageOversizeAndWrongKind()
    {
        var enumSchema = new InteractionScalarSchema(
            InteractionScalarKind.Enum,
            EnumValues: new[] { "open", "closed" });
        Assert.Throws<ArgumentException>(() =>
            InteractionScalarContract.Validate(enumSchema, InteractionScalarValue.Enum("unknown")));

        Assert.Throws<ArgumentException>(() =>
            InteractionScalarContract.Validate(
                new InteractionScalarSchema(InteractionScalarKind.Percentage),
                InteractionScalarValue.Percentage(100.1m)));

        Assert.Throws<ArgumentException>(() =>
            InteractionScalarContract.Validate(
                new InteractionScalarSchema(InteractionScalarKind.String, MaximumLength: 3),
                InteractionScalarValue.String("four")));

        Assert.Throws<ArgumentException>(() =>
            InteractionScalarContract.Validate(
                new InteractionScalarSchema(InteractionScalarKind.Integer),
                InteractionScalarValue.Number(1m)));
    }

    [Fact]
    public void Definition_NormalizesStableIdentitySchemaAndEquipmentScopedCapability()
    {
        var definitionId = Guid.NewGuid();
        var equipmentId = Guid.NewGuid();
        var definition = TransientEventContract.NormalizeDefinition(new TransientEventDefinition(
            definitionId,
            "button.double_click",
            new[]
            {
                new TransientEventFieldDefinition(
                    "button",
                    new InteractionScalarSchema(
                        InteractionScalarKind.Enum,
                        EnumValues: new[] { "right", "left" }),
                    Required: true)
            },
            EquipmentId: equipmentId,
            CapabilityId: "button-main"));

        Assert.Equal(definitionId, definition.DefinitionId);
        Assert.Equal(equipmentId, definition.EquipmentId);
        Assert.Equal("button-main", definition.CapabilityId);
        Assert.Equal(new[] { "left", "right" }, definition.Fields[0].Schema.EnumValues);
    }

    [Fact]
    public void Occurrence_ValidatesTypedPayloadOptionalTimestampAndEvidence()
    {
        var definitionId = Guid.NewGuid();
        var equipmentId = Guid.NewGuid();
        var definition = new TransientEventDefinition(
            definitionId,
            "button.double_click",
            new[]
            {
                new TransientEventFieldDefinition(
                    "button",
                    new InteractionScalarSchema(
                        InteractionScalarKind.Enum,
                        EnumValues: new[] { "left", "right" }),
                    Required: true),
                new TransientEventFieldDefinition(
                    "press_count",
                    new InteractionScalarSchema(InteractionScalarKind.Integer, Minimum: 1, Maximum: 4))
            },
            EquipmentId: equipmentId,
            CapabilityId: "button-main");

        var observed = new DateTimeOffset(2026, 10, 6, 20, 0, 0, TimeSpan.FromHours(-3));
        var deviceTime = new DateTimeOffset(2026, 10, 6, 22, 59, 58, TimeSpan.Zero);
        var occurrence = TransientEventContract.ValidateOccurrence(
            definition,
            new TransientEventOccurrence(
                Guid.NewGuid(),
                definitionId,
                "button.double_click",
                new TransientEventSource(Guid.NewGuid(), "device-01", equipmentId, "button-main"),
                new[]
                {
                    new TransientEventFieldValue("press_count", InteractionScalarValue.Integer(2)),
                    new TransientEventFieldValue("button", InteractionScalarValue.Enum("left"))
                },
                observed,
                new TransientEventTimestamp(deviceTime, TransientEventTimestampOrigin.DeviceClock),
                new TransientEventEvidence(Sequence: 9, Counter: 42)));

        Assert.Equal(observed.ToUniversalTime(), occurrence.ObservedAt);
        Assert.Equal(deviceTime, occurrence.OccurredAt!.Value);
        Assert.Equal(TransientEventTimestampOrigin.DeviceClock, occurrence.OccurredAt.Origin);
        Assert.Equal(9, occurrence.Evidence!.Sequence);
        Assert.Equal(42, occurrence.Evidence.Counter);
        Assert.Equal(new[] { "button", "press_count" }, occurrence.Payload.Select(field => field.Key));
        Assert.False(typeof(IScadaEvent).IsAssignableFrom(typeof(TransientEventOccurrence)));
    }

    [Fact]
    public void Occurrence_RejectsMissingUnexpectedWrongKindDuplicateIdentityAndInvalidEvidence()
    {
        var definitionId = Guid.NewGuid();
        var definition = new TransientEventDefinition(
            definitionId,
            "scene.invoked",
            new[]
            {
                new TransientEventFieldDefinition(
                    "scene",
                    new InteractionScalarSchema(InteractionScalarKind.Integer, Minimum: 1, Maximum: 10),
                    Required: true)
            });
        var source = new TransientEventSource(Guid.NewGuid(), "device-02");
        var observed = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => TransientEventContract.ValidateOccurrence(
            definition,
            new TransientEventOccurrence(
                Guid.NewGuid(), definitionId, "scene.invoked", source,
                Array.Empty<TransientEventFieldValue>(), observed)));

        Assert.Throws<ArgumentException>(() => TransientEventContract.ValidateOccurrence(
            definition,
            new TransientEventOccurrence(
                Guid.NewGuid(), definitionId, "scene.invoked", source,
                new[] { new TransientEventFieldValue("extra", InteractionScalarValue.Integer(1)) }, observed)));

        Assert.Throws<ArgumentException>(() => TransientEventContract.ValidateOccurrence(
            definition,
            new TransientEventOccurrence(
                Guid.NewGuid(), definitionId, "scene.invoked", source,
                new[] { new TransientEventFieldValue("scene", InteractionScalarValue.String("one")) }, observed)));

        Assert.Throws<ArgumentException>(() => TransientEventContract.ValidateOccurrence(
            definition,
            new TransientEventOccurrence(
                definitionId, definitionId, "scene.invoked", source,
                new[] { new TransientEventFieldValue("scene", InteractionScalarValue.Integer(1)) }, observed)));

        Assert.Throws<ArgumentOutOfRangeException>(() => TransientEventContract.ValidateOccurrence(
            definition,
            new TransientEventOccurrence(
                Guid.NewGuid(), definitionId, "scene.invoked", source,
                new[] { new TransientEventFieldValue("scene", InteractionScalarValue.Integer(1)) }, observed,
                Evidence: new TransientEventEvidence(Sequence: -1))));
    }

    [Fact]
    public void Source_RequiresStableDataSourceAndEquipmentForCapability()
    {
        Assert.Throws<ArgumentException>(() =>
            TransientEventContract.NormalizeSource(new TransientEventSource(Guid.Empty, "device")));

        Assert.Throws<ArgumentException>(() =>
            TransientEventContract.NormalizeSource(
                new TransientEventSource(Guid.NewGuid(), "device", CapabilityId: "button-main")));
    }
}
