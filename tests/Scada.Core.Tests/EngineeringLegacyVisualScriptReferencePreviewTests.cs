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

public sealed class EngineeringLegacyVisualScriptReferencePreviewTests
{
    [Fact]
    public void Preview_V10ScreenUpdatePreservesExistingVisualObjectIdentityByKey()
    {
        using var harness = new Harness();
        var fixture = harness.SeedScreenAndDependentScript();
        var package = LegacyScreenPackage(
            fixture.ScreenKey,
            [new VisualElementEngineeringDto("rectangle", BuiltinVisualObjectSchemas.RectangleType)]);

        var preview = harness.Exchange.Preview(package, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.DoesNotContain(
            preview.Items.SelectMany(item => item.Issues),
            issue => issue.Code == "SCRIPT_DEPENDENCY_REFERENCE_MISSING");
    }

    [Fact]
    public void Preview_V10ScreenUpdateRejectsRemovalOfObjectReferencedByExistingScript()
    {
        using var harness = new Harness();
        var fixture = harness.SeedScreenAndDependentScript();
        var package = LegacyScreenPackage(fixture.ScreenKey, Array.Empty<VisualElementEngineeringDto>());

        var preview = harness.Exchange.Preview(package, ImportMode.CreateAndUpdate);
        var issues = preview.Items.SelectMany(item => item.Issues).ToArray();

        Assert.False(preview.CanApply);
        Assert.Contains(
            issues,
            issue => issue.Code == "SCRIPT_DEPENDENCY_REFERENCE_MISSING" && issue.IsError);
    }

    private static EngineeringPackage LegacyScreenPackage(
        string screenKey,
        IReadOnlyCollection<VisualElementEngineeringDto> elements) =>
        new(
            EngineeringExchangeService.CurrentSchema,
            10,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Screens:
            [
                new ScreenEngineeringDto(
                    null,
                    screenKey,
                    "Legacy Overview",
                    Elements: elements)
            ]);

    private sealed class Harness : IDisposable
    {
        private readonly InMemoryScadaEventBus _eventBus = new();

        public Harness()
        {
            Tags = new InMemoryTagRegistry();
            Alarms = new InMemoryAlarmEngine(_eventBus);
            Assets = new InMemoryEngineeringAssetRegistry();
            Views = new InMemoryEngineeringViewRegistry();
            Scripts = new InMemoryScriptEngineeringRegistry();
            Exchange = new EngineeringExchangeService(
                Tags,
                Alarms,
                new InMemoryDataSourceEngineeringRegistry(),
                Assets,
                Views,
                new InMemorySecurityPolicyEngineeringRegistry(),
                new InMemoryCommandEngineeringRegistry(),
                new InMemoryGatewayEngineeringRegistry(),
                Scripts);
        }

        public InMemoryTagRegistry Tags { get; }
        public InMemoryAlarmEngine Alarms { get; }
        public InMemoryEngineeringAssetRegistry Assets { get; }
        public InMemoryEngineeringViewRegistry Views { get; }
        public InMemoryScriptEngineeringRegistry Scripts { get; }
        public EngineeringExchangeService Exchange { get; }

        public (string ScreenKey, Guid ScreenId, Guid ObjectId, Guid ScriptId) SeedScreenAndDependentScript()
        {
            const string screenKey = "screen.legacy-reference";
            var screenId = Guid.Parse("94000000-0000-0000-0000-000000000001");
            var objectId = Guid.Parse("94000000-0000-0000-0000-000000000002");
            var scriptId = Guid.Parse("94000000-0000-0000-0000-000000000003");

            Views.UpsertScreen(new ScreenEngineeringDto(
                screenId,
                screenKey,
                "Overview",
                Elements:
                [
                    new VisualElementEngineeringDto(
                        "rectangle",
                        BuiltinVisualObjectSchemas.RectangleType,
                        Id: objectId)
                ]));

            Scripts.Upsert(new ScriptEngineeringDefinition(
                scriptId,
                "scripts/client/legacy-reference",
                "Legacy reference",
                ScriptEngineeringScope.ClientVisual,
                "value = 1",
                dependencies:
                [
                    new ScriptEngineeringDependency(
                        ScriptEngineeringDependencyKind.VisualObject,
                        ScriptEngineeringReferenceKeys.VisualObject(screenId, objectId))
                ]));

            return (screenKey, screenId, objectId, scriptId);
        }

        public void Dispose() => Alarms.Dispose();
    }
}
