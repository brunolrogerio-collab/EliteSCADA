using Scada.Core.Commands;
using Scada.Core.Tags;

namespace Scada.Core.Tests;

public sealed class CommandDomainTests
{
    [Fact]
    public void Registry_IndexesCommandsByStableIdAndKey()
    {
        var registry = new InMemoryCommandRegistry();
        var id = Guid.NewGuid();
        var command = new CommandDefinition(
            id,
            "plant.p01.start",
            "Start P01",
            CommandKind.WriteTagValue,
            Guid.NewGuid(),
            "Plant.P01.Run",
            true);

        registry.Register(command);

        Assert.True(registry.TryGet(id, out var byId));
        Assert.True(registry.TryGetByKey("PLANT.P01.START", out var byKey));
        Assert.Same(byId, byKey);
        Assert.Equal(command, Assert.Single(registry.Snapshot()));
    }

    [Theory]
    [InlineData(TagDataType.Boolean, "true", true)]
    [InlineData(TagDataType.Boolean, "0", false)]
    [InlineData(TagDataType.Int16, "123", (short)123)]
    [InlineData(TagDataType.Int32, "456", 456)]
    [InlineData(TagDataType.Int64, "789", 789L)]
    [InlineData(TagDataType.Float, "12.5", 12.5f)]
    [InlineData(TagDataType.Double, "18.75", 18.75d)]
    [InlineData(TagDataType.String, "AUTO", "AUTO")]
    [InlineData(TagDataType.Enum, "RUN", "RUN")]
    public void ConfiguredValueParser_UsesTargetTagDataType(TagDataType dataType, string configured, object expected)
    {
        var parsed = CommandValueParser.TryParse(dataType, configured, out var value);

        Assert.True(parsed);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void ConfiguredValueParser_RejectsInvalidBoolean()
    {
        Assert.False(CommandValueParser.TryParse(TagDataType.Boolean, "start-ish", out _));
    }
}
