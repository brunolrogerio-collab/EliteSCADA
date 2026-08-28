using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.Scripts;

namespace Scada.Core.Tests;

public sealed class ScriptEngineeringVisualObjectReferenceResolverTests
{
    [Fact]
    public void FromEngineeringPackage_CatalogsNestedWave11VisualObjectIds()
    {
        var screenId = Guid.Parse("93000000-0000-0000-0000-000000000001");
        var parentId = Guid.Parse("93000000-0000-0000-0000-000000000002");
        var childId = Guid.Parse("93000000-0000-0000-0000-000000000003");
        var package = new EngineeringPackage(
            "scada.engineering",
            11,
            DateTimeOffset.UnixEpoch,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Screens:
            [
                new ScreenEngineeringDto(
                    screenId,
                    "overview",
                    "Overview",
                    Elements:
                    [
                        new VisualElementEngineeringDto(
                            "parent",
                            "core.group",
                            Children:
                            [
                                new VisualElementEngineeringDto(
                                    "child",
                                    "core.rectangle",
                                    Id: childId)
                            ],
                            Id: parentId)
                    ])
            ]);

        var resolver = ScriptEngineeringReferenceResolver.FromEngineeringPackage(package);

        Assert.True(resolver.Resolve(
            ScriptEngineeringDependencyKind.VisualDefinition,
            screenId.ToString("D")).IsResolved);
        Assert.True(resolver.Resolve(
            ScriptEngineeringDependencyKind.VisualObject,
            ScriptEngineeringReferenceKeys.VisualObject(screenId, parentId)).IsResolved);
        Assert.True(resolver.Resolve(
            ScriptEngineeringDependencyKind.VisualObject,
            ScriptEngineeringReferenceKeys.VisualObject(screenId, childId)).IsResolved);
        Assert.Empty(resolver.CatalogIssues);
    }
}
