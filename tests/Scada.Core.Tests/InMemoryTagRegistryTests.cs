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
    [Fact]
    public void Snapshot_RemainsStableAcrossProjectEdits()
    {
        var registry = new InMemoryTagRegistry();
        var b = TagDefinition.Create("B", "Plant.B", TagDataType.Double);
        var a = TagDefinition.Create("A", "Plant.A", TagDataType.Double);
        registry.Register(b);
        var first = registry.Snapshot();
        registry.Register(a);
        Assert.Equal(new[] { a, b }, registry.Snapshot());
        var renamed = b with { Path = "Plant.0" };
        registry.Upsert(renamed);
        Assert.Equal(new[] { renamed, a }, registry.Snapshot());
        registry.Remove(a.Id);
        Assert.Equal(new[] { renamed }, registry.Snapshot());
        registry.Clear();
        Assert.Empty(registry.Snapshot());
        Assert.Equal(new[] { b }, first);
    }
}
