using System.Text.Json;
using System.Text.Json.Nodes;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class EngineeringIntelligenceExperimentalJointVisualValidationTests
{
    private const string Id = "10000000-0000-0000-0000-000000000001";
    private static string Key(int i) => $"20000000-0000-0000-0000-{i:000000000000}";
    [Fact]
    public void JointReferences_PersistReadOnlyWithoutAnalyticalConfiguration()
    {
        var element = Element(Signals());
        Assert.DoesNotContain(Validate(element), i => i.IsError);
        Assert.All(BuiltinVisualObjectSchemas.IntelligenceExperimentalJointDeviationV1.Properties.Values.Where(p => p.Key.StartsWith("intelligence", StringComparison.Ordinal)), p =>
        { Assert.False(p.EngineeringEditable); Assert.False(p.RuntimeWritable); Assert.False(p.SupportsBinding); });
        var normalized = VisualEngineeringPropertyMigration.NormalizeCurrentElements([element])!.Single();
        var copy = JsonSerializer.Deserialize<VisualElementEngineeringDto>(JsonSerializer.Serialize(normalized))!;
        Assert.Equal(element.Properties![VisualPropertyKeys.IntelligenceJointSignals].GetString(), copy.Properties![VisualPropertyKeys.IntelligenceJointSignals].GetString());
        Assert.DoesNotContain("referenceWindow", copy.Properties.Keys); Assert.DoesNotContain("scores", copy.Properties.Keys);
    }
    [Theory]
    [InlineData("one")][InlineData("many")][InlineData("duplicate-id")][InlineData("duplicate-tag")]
    [InlineData("bad-id")][InlineData("bad-tag")][InlineData("empty-label")][InlineData("long-label")]
    [InlineData("extra-values")][InlineData("oversized")][InlineData("missing-context")]
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
        var element = Element(signals, scenario == "missing-context" ? "[]" : null);
        Assert.Contains(Validate(element), i => i.IsError && i.Code == "VISUAL_INTELLIGENCE_JOINT_CONFIG_INVALID");
    }
    [Theory]
    [InlineData(ImportEntityKind.Popup)][InlineData(ImportEntityKind.Template)]
    public void JointReport_IsScreenOnly(ImportEntityKind kind) => Assert.Contains(Validate(Element(Signals()),kind), i => i.IsError);
    private static JsonObject Signal(int i) => new() { ["id"] = Key(i), ["tagId"] = Key(i+100), ["label"] = $"Measurement {i}" };
    private static JsonArray Signals() => new(Signal(1),Signal(2));
    private static VisualElementEngineeringDto Element(JsonArray signals, string? contexts = null) => new("joint", BuiltinVisualObjectSchemas.IntelligenceExperimentalJointDeviationV1Type,
        Properties: new Dictionary<string,JsonElement>
        {
            [VisualPropertyKeys.IntelligenceScopeId] = JsonSerializer.SerializeToElement(Id),
            [VisualPropertyKeys.IntelligenceAssignmentId] = JsonSerializer.SerializeToElement(Id),
            [VisualPropertyKeys.IntelligenceProfileId] = JsonSerializer.SerializeToElement(Id),
            [VisualPropertyKeys.IntelligenceJointSignals] = JsonSerializer.SerializeToElement(signals.ToJsonString()),
            [VisualPropertyKeys.IntelligenceOperatingContexts] = JsonSerializer.SerializeToElement(contexts ?? new JsonArray(new JsonObject {
                ["id"] = Id, ["displayLabel"] = "Running", ["version"] = 3, ["predicateTagIds"] = new JsonArray(Key(900)) }).ToJsonString())
        });
    private static IReadOnlyCollection<ImportIssue> Validate(VisualElementEngineeringDto e, ImportEntityKind kind=ImportEntityKind.Screen) => BuiltinVisualEngineeringValidation.Validate(e,kind,"joint",int.MaxValue);
}
