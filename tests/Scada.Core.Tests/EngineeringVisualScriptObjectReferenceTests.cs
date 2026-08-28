using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Scripts;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class EngineeringVisualScriptObjectReferenceTests
{
    [Fact]
    public void Preview_AcceptsClientScriptDependencyOnCanonicalVisualObjectInSamePackage()
    {
        using var harness = new Harness();
        var screenId = Guid.Parse("91000000-0000-0000-0000-000000000001");
        var objectId = Guid.Parse("91000000-0000-0000-0000-000000000002");
        var scriptId = Guid.Parse("91000000-0000-0000-0000-000000000003");
        var package = Package(screenId, objectId, scriptId, objectId);

        var preview = harness.Exchange.Preview(package, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.DoesNotContain(
            preview.Items.SelectMany(item => item.Issues),
            issue => issue.Code == "SCRIPT_DEPENDENCY_REFERENCE_MISSING");
    }

    [Fact]
    public void Preview_RejectsClientScriptDependencyOnMissingVisualObject()
    {
        using var harness = new Harness();
        var screenId = Guid.Parse("92000000-0000-0000-0000-000000000001");
        var objectId = Guid.Parse("92000000-0000-0000-0000-000000000002");
        var missingObjectId = Guid.Parse("92000000-0000-0000-0000-000000000099");
        var scriptId = Guid.Parse("92000000-0000-0000-0000-000000000003");
        var package = Package(screenId, objectId, scriptId, missingObjectId);

        var preview = harness.Exchange.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(item => item.Issues),
            issue => issue.Code == "SCRIPT_DEPENDENCY_REFERENCE_MISSING" && issue.IsError);
    }

    private static EngineeringPackage Package(
        Guid screenId,
        Guid objectId,
        Guid scriptId,
        Guid dependencyObjectId)
    {
        var screen = new ScreenEngineeringDto(
            screenId,
            "screen.object-reference",
            "Object Reference",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "rectangle",
                    BuiltinVisualObjectSchemas.RectangleType,
                    Id: objectId)
            ]);
        var script = new ScriptEngineeringDefinition(
            scriptId,
            "scripts/client/object-reference",
            "Object reference",
            ScriptEngineeringScope.ClientVisual,
            "value = 1",
            dependencies:
            [
                new ScriptEngineeringDependency(
                    ScriptEngineeringDependencyKind.VisualObject,
                    ScriptEngineeringReferenceKeys.VisualObject(screenId, dependencyObjectId))
            ]);

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Screens: [screen],
            Scripts: [script]);
    }

    private sealed class Harness : IDisposable
    {
        private readonly InMemoryScadaEventBus _eventBus = new();

        public Harness()
        {
            var tags = new InMemoryTagRegistry();
            Alarms = new InMemoryAlarmEngine(_eventBus);
            Exchange = new EngineeringExchangeService(
                tags,
                Alarms,
                new InMemoryDataSourceEngineeringRegistry(),
                new InMemoryEngineeringAssetRegistry(),
                new InMemoryEngineeringViewRegistry(),
                new InMemorySecurityPolicyEngineeringRegistry(),
                new InMemoryCommandEngineeringRegistry(),
                new InMemoryGatewayEngineeringRegistry(),
                new InMemoryScriptEngineeringRegistry());
        }

        public InMemoryAlarmEngine Alarms { get; }
        public EngineeringExchangeService Exchange { get; }

        public void Dispose() => Alarms.Dispose();
    }
}
