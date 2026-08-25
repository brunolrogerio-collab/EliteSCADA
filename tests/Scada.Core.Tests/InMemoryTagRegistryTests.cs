using Scada.Core.Tags;

namespace Scada.Core.Tests;

public sealed class InMemoryTagRegistryTests
{
    [Fact]
    public void Register_AllowsLookupByPath()
    {
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("Pressure", "Demo.Pressure", TagDataType.Double, engineeringUnit: "bar");
        registry.Register(tag);
        Assert.True(registry.TryGetByPath("demo.pressure", out var found));
        Assert.Equal(tag.Id, found!.Id);
    }

    [Fact]
    public void Register_RejectsDuplicatePathWithDifferentId()
    {
        var registry = new InMemoryTagRegistry();
        registry.Register(TagDefinition.Create("A", "Demo.A", TagDataType.Double));
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(TagDefinition.Create("A2", "Demo.A", TagDataType.Double)));
    }
}
