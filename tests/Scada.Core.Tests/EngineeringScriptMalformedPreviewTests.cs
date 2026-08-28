using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class EngineeringScriptMalformedPreviewTests
{
    [Fact]
    public void Preview_RejectsNullScriptAndVisualReferenceInsteadOfDeferringFailureToApply()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        using var alarms = new InMemoryAlarmEngine(bus);
        var service = new EngineeringExchangeService(tags, alarms);
        const string json = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 11,
          "exportedAt": "2026-08-28T00:00:00Z",
          "tags": [],
          "alarms": [],
          "scripts": [null],
          "scriptVisualEventReferences": [null]
        }
        """;

        var package = service.ParseJson(json);
        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var issues = preview.Items.SelectMany(item => item.Issues).ToArray();

        Assert.False(preview.CanApply);
        Assert.Contains(issues, issue => issue.Code == "SCRIPT_NULL" && issue.IsError);
        Assert.Contains(issues, issue => issue.Code == "SCRIPT_VISUAL_REFERENCE_NULL" && issue.IsError);

        var apply = service.Apply(package, ImportMode.CreateAndUpdate);
        Assert.Contains(apply.Issues, issue => issue.Code == "SCRIPT_NULL");
        Assert.Contains(apply.Issues, issue => issue.Code == "SCRIPT_VISUAL_REFERENCE_NULL");
    }
}
