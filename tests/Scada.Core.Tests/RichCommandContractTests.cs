using Scada.Core.Commands;
using Scada.Core.Interactions;
using Scada.Core.Tags;

namespace Scada.Core.Tests;

public sealed class RichCommandContractTests
{
    [Fact]
    public void DefinitionAndInvocation_ValidateBoundedTypedParameters()
    {
        var commandId = Guid.NewGuid();
        var definition = new RichCommandDefinition(
            commandId,
            "cover.move",
            new[]
            {
                new RichCommandParameterDefinition(
                    "position",
                    new InteractionScalarSchema(
                        InteractionScalarKind.Percentage,
                        Minimum: 0,
                        Maximum: 100),
                    Required: true),
                new RichCommandParameterDefinition(
                    "duration",
                    new InteractionScalarSchema(
                        InteractionScalarKind.Duration,
                        Minimum: 0,
                        Maximum: 30000,
                        Unit: "ms"))
            });

        var requestedAt = new DateTimeOffset(2026, 10, 6, 20, 30, 0, TimeSpan.FromHours(-3));
        var invocation = RichCommandContract.ValidateInvocation(
            definition,
            new RichCommandInvocation(
                Guid.NewGuid(),
                commandId,
                new[]
                {
                    new RichCommandParameterValue("position", InteractionScalarValue.Percentage(75m)),
                    new RichCommandParameterValue("duration", InteractionScalarValue.Duration(TimeSpan.FromSeconds(2)))
                },
                requestedAt));

        Assert.Equal(commandId, invocation.CommandId);
        Assert.Equal(requestedAt.ToUniversalTime(), invocation.RequestedAt);
        Assert.Equal(new[] { "duration", "position" }, invocation.Parameters.Select(parameter => parameter.Key));
        Assert.DoesNotContain(
            typeof(RichCommandInvocation).GetProperties(),
            property => property.Name is "DataSourceId" or "DriverType" or "Principal" or "SecretReference");
    }

    [Fact]
    public void Invocation_RejectsMissingUnexpectedDuplicateAndWrongKindParameters()
    {
        var commandId = Guid.NewGuid();
        var definition = new RichCommandDefinition(
            commandId,
            "scene.invoke",
            new[]
            {
                new RichCommandParameterDefinition(
                    "scene",
                    new InteractionScalarSchema(
                        InteractionScalarKind.Enum,
                        EnumValues: new[] { "away", "home" }),
                    Required: true)
            });

        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => RichCommandContract.ValidateInvocation(
            definition,
            new RichCommandInvocation(Guid.NewGuid(), commandId, Array.Empty<RichCommandParameterValue>(), now)));

        Assert.Throws<ArgumentException>(() => RichCommandContract.ValidateInvocation(
            definition,
            new RichCommandInvocation(
                Guid.NewGuid(),
                commandId,
                new[] { new RichCommandParameterValue("unexpected", InteractionScalarValue.String("value")) },
                now)));

        Assert.Throws<ArgumentException>(() => RichCommandContract.ValidateInvocation(
            definition,
            new RichCommandInvocation(
                Guid.NewGuid(),
                commandId,
                new[]
                {
                    new RichCommandParameterValue("scene", InteractionScalarValue.Enum("home")),
                    new RichCommandParameterValue("scene", InteractionScalarValue.Enum("away"))
                },
                now)));

        Assert.Throws<ArgumentException>(() => RichCommandContract.ValidateInvocation(
            definition,
            new RichCommandInvocation(
                Guid.NewGuid(),
                commandId,
                new[] { new RichCommandParameterValue("scene", InteractionScalarValue.Integer(1)) },
                now)));
    }

    [Fact]
    public void DriverBinding_IsVersionedServerOwnedAndRejectsProtocolOrProtectedMaterialBypass()
    {
        var commandId = Guid.NewGuid();
        var equipmentId = Guid.NewGuid();
        var definition = new RichCommandDefinition(
            commandId,
            "cover.move",
            new[]
            {
                new RichCommandParameterDefinition(
                    "position",
                    new InteractionScalarSchema(InteractionScalarKind.Percentage),
                    Required: true)
            });

        var binding = RichCommandContract.ValidateBinding(
            definition,
            new DriverCommandBinding(
                commandId,
                Guid.NewGuid(),
                "cover-device-01",
                "cover.move",
                new[]
                {
                    new DriverCommandBindingSetting("retry.count", "2"),
                    new DriverCommandBindingSetting("transition.profile", "normal")
                },
                equipmentId,
                "cover-main"));

        Assert.Equal(RichCommandContract.DriverBindingVersion, binding.Version);
        Assert.Equal(equipmentId, binding.EquipmentId);
        Assert.Equal("cover-main", binding.CapabilityId);
        Assert.Equal(new[] { "retry.count", "transition.profile" }, binding.Settings!.Select(setting => setting.Key));

        Assert.Throws<ArgumentException>(() => RichCommandContract.NormalizeBinding(
            binding with
            {
                Settings = new[] { new DriverCommandBindingSetting("secretRef", "credential-1") }
            }));

        Assert.Throws<ArgumentException>(() => RichCommandContract.NormalizeBinding(
            binding with
            {
                Settings = new[] { new DriverCommandBindingSetting("mqtt.topic", "plant/cover/1") }
            }));

        Assert.Throws<ArgumentException>(() => RichCommandContract.NormalizeBinding(
            binding with { SemanticOperationKey = "rpc.method" }));

        Assert.Throws<ArgumentException>(() => RichCommandContract.NormalizeBinding(
            binding with
            {
                Settings = new[] { new DriverCommandBindingSetting("retry.profile", "secret://driver/key") }
            }));

        Assert.Throws<ArgumentException>(() => RichCommandContract.NormalizeBinding(
            binding with { Version = 2 }));
    }

    [Fact]
    public void Result_ProvidesExplicitAcceptedCompletedTimeoutFailureAndUnknownSemantics()
    {
        var invocation = new RichCommandInvocation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Array.Empty<RichCommandParameterValue>(),
            DateTimeOffset.UtcNow);

        foreach (var outcome in Enum.GetValues<RichCommandOutcome>())
        {
            var result = RichCommandContract.ValidateResult(
                invocation,
                new RichCommandResult(
                    invocation.InvocationId,
                    invocation.CommandId,
                    outcome,
                    DateTimeOffset.UtcNow,
                    Code: outcome == RichCommandOutcome.Unknown ? "outcome.ambiguous" : null));

            Assert.Equal(outcome, result.Outcome);
        }

        Assert.Contains(RichCommandOutcome.Rejected, Enum.GetValues<RichCommandOutcome>());
        Assert.Contains(RichCommandOutcome.Accepted, Enum.GetValues<RichCommandOutcome>());
        Assert.Contains(RichCommandOutcome.Completed, Enum.GetValues<RichCommandOutcome>());
        Assert.Contains(RichCommandOutcome.Failed, Enum.GetValues<RichCommandOutcome>());
        Assert.Contains(RichCommandOutcome.TimedOut, Enum.GetValues<RichCommandOutcome>());
        Assert.Contains(RichCommandOutcome.Unknown, Enum.GetValues<RichCommandOutcome>());

        Assert.Throws<ArgumentException>(() => RichCommandContract.ValidateResult(
            invocation,
            new RichCommandResult(
                Guid.NewGuid(),
                invocation.CommandId,
                RichCommandOutcome.Unknown,
                DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void LegacyWriteTagValueCommand_RemainsIndependentAndUnchanged()
    {
        var registry = new InMemoryCommandRegistry();
        var legacy = new CommandDefinition(
            Guid.NewGuid(),
            "plant.p01.start",
            "Start P01",
            CommandKind.WriteTagValue,
            Guid.NewGuid(),
            "Plant.P01.Run",
            true);

        registry.Register(legacy);

        Assert.Equal(CommandKind.WriteTagValue, legacy.Kind);
        Assert.True(registry.TryGet(legacy.Id, out var roundTrip));
        Assert.Equal(legacy, roundTrip);
        Assert.True(CommandValueParser.TryParse(TagDataType.Boolean, "1", out var parsed));
        Assert.Equal(true, parsed);
        Assert.False(typeof(CommandDefinition).IsAssignableFrom(typeof(RichCommandDefinition)));
    }
}
