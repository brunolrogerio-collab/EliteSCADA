using System.Text.Json;
using System.Text.Json.Nodes;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class EngineeringIntelligenceSingleTargetVisualValidationTests
{
    private const string Id = "10000000-0000-0000-0000-000000000001";
    private static string Key(int i) => $"20000000-0000-0000-0000-{i:000000000000}";
    [Fact]
    public void SingleTargetReferences_PersistReadOnlyWithoutAnalyticalConfiguration()
    {
        var element = Element(Signals());
        Assert.DoesNotContain(Validate(element), i => i.IsError);
        Assert.All(BuiltinVisualObjectSchemas.IntelligenceSingleTargetDeviationV1.Properties.Values.Where(p => p.Key.StartsWith("intelligence", StringComparison.Ordinal)), p =>
        { Assert.False(p.EngineeringEditable); Assert.False(p.RuntimeWritable); Assert.False(p.SupportsBinding); });
        var normalized = VisualEngineeringPropertyMigration.NormalizeCurrentElements([element])!.Single();
        var copy = JsonSerializer.Deserialize<VisualElementEngineeringDto>(JsonSerializer.Serialize(normalized))!;
        Assert.Equal(element.Properties![VisualPropertyKeys.IntelligenceSingleTargetSignals].GetString(), copy.Properties![VisualPropertyKeys.IntelligenceSingleTargetSignals].GetString());
        Assert.DoesNotContain("referenceWindow", copy.Properties.Keys); Assert.DoesNotContain("scores", copy.Properties.Keys);
    }
    [Theory]
    [InlineData("one")][InlineData("many")][InlineData("duplicate-id")][InlineData("duplicate-tag")]
    [InlineData("bad-id")][InlineData("bad-tag")][InlineData("empty-label")][InlineData("long-label")]
    [InlineData("extra-values")][InlineData("oversized")][InlineData("missing-unit-code")][InlineData("partial-unit")][InlineData("no-condition")][InlineData("empty-guid")]
    public void InvalidReferenceConfiguration_FailsClosed(string scenario)
    {
        var signals = Signals();
        switch(scenario)
        {
            case "one": signals.RemoveAt(1); break;
            case "many": for(var i=3;i<=17;i++) signals.Add(Signal(i)); break;
            case "duplicate-id": signals[1]!["id"] = signals[0]!["id"]!.GetValue<string>(); break;
            case "duplicate-tag": signals[1]!["tagId"] = signals[0]!["tagId"]!.GetValue<string>(); break;
            case "bad-id": signals[0]!["id"] = "bad"; break;
            case "bad-tag": signals[0]!["tagId"] = "bad"; break;
            case "empty-label": signals[0]!["label"] = " "; break;
            case "long-label": signals[0]!["label"] = new string('a',321); break;
            case "extra-values": signals[0]!["minimum"] = 0; break;
            case "oversized": signals[0]!["label"] = new string('a',16384); break;
        }
        if (scenario == "missing-unit-code") signals[1]!.AsObject().Remove("unitCode");
        if (scenario == "partial-unit") signals[1]!["unitCode"] = null;
        if (scenario == "no-condition") { signals[1]!["unitCode"] = null; signals[1]!["canonicalUnit"] = null; }
        if (scenario == "empty-guid") signals[0]!["id"] = Guid.Empty.ToString();
        var element = Element(signals);
        Assert.Contains(Validate(element), i => i.IsError && i.Code == "VISUAL_INTELLIGENCE_SINGLE_TARGET_CONFIG_INVALID");
    }
    [Theory]
    [InlineData(ImportEntityKind.Popup)][InlineData(ImportEntityKind.Template)]
    public void SingleTargetReport_IsScreenOnly(ImportEntityKind kind) => Assert.Contains(Validate(Element(Signals()),kind), i => i.IsError);
    private static JsonObject Signal(int i) => new() { ["id"] = Key(i), ["tagId"] = Key(i+100), ["label"] = $"Measurement {i}", ["unit"] = i == 1 ? "A" : "Hz", ["canonicalUnit"] = i == 1 ? null : "Hz", ["unitCode"] = i == 1 ? null : "unit.frequency.hertz" };
    private static JsonArray Signals() => new(Signal(1),Signal(2));
    private static VisualElementEngineeringDto Element(JsonArray signals) => new("joint", BuiltinVisualObjectSchemas.IntelligenceSingleTargetDeviationV1Type,
        Properties: new Dictionary<string,JsonElement>
        {
            [VisualPropertyKeys.IntelligenceScopeId] = JsonSerializer.SerializeToElement(Id),
            [VisualPropertyKeys.IntelligenceAssignmentId] = JsonSerializer.SerializeToElement(Id),
            [VisualPropertyKeys.IntelligenceProfileId] = JsonSerializer.SerializeToElement(Id),
            [VisualPropertyKeys.IntelligenceSingleTargetSignals] = JsonSerializer.SerializeToElement(signals.ToJsonString())

        });
    private static IReadOnlyCollection<ImportIssue> Validate(VisualElementEngineeringDto e, ImportEntityKind kind=ImportEntityKind.Screen) => BuiltinVisualEngineeringValidation.Validate(e,kind,"joint",int.MaxValue);
}
