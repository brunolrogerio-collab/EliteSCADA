using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class EngineeringVisualMalformedPreviewTests
{
    [Fact]
    public void Preview_ReturnsIssuesForMalformedVisualTreeInsteadOfThrowing()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new EngineeringExchangeService(tags, alarms);

        var malformedElement = new VisualElementEngineeringDto(
            "pump",
            BuiltinVisualObjectSchemas.RectangleType,
            Bindings:
            [
                null!,
                new EngineeringBindingDto(null!, EngineeringBindingKind.Tag, null!),
                new EngineeringBindingDto(VisualPropertyKeys.X, EngineeringBindingKind.Tag, null!)
            ]);
        var screen = new ScreenEngineeringDto(
            null,
            "overview",
            "Overview",
            Elements: [null!, malformedElement]);
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Screens: [screen]);

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var issues = preview.Items.SelectMany(item => item.Issues).ToArray();

        Assert.False(preview.CanApply);
        Assert.Contains(issues, issue => issue.Code == "VISUAL_ELEMENT_NULL" && issue.IsError);
        Assert.Contains(issues, issue => issue.Code == "BINDING_NULL" && issue.IsError);
        Assert.Contains(issues, issue => issue.Code == "BINDING_KEY_REQUIRED" && issue.IsError);
        Assert.Contains(issues, issue => issue.Code == "BINDING_TARGET_REQUIRED" && issue.IsError);
    }
}
