using System.Text.Json;
using System.Text.Json.Nodes;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class EngineeringIntelligenceContextEligibilityVisualValidationTests
{
    private const string Id = "10000000-0000-0000-0000-000000000001";
    private const string TagId = "20000000-0000-0000-0000-000000000002";

    [Fact]
    public void SavedContextReferences_RoundTripWithReadOnlyIdentityAndVersion()
    {
        var element = Element(Contexts());
        Assert.DoesNotContain(Validate(element), issue => issue.IsError);
        var schema = BuiltinVisualObjectSchemas.IntelligenceContextEligibilityV1;
        Assert.All(schema.Properties.Values.Where(property => property.Key.StartsWith("intelligence", StringComparison.Ordinal)), property =>
        {
            Assert.False(property.EngineeringEditable);
            Assert.False(property.RuntimeWritable);
            Assert.False(property.SupportsBinding);
        });
        var normalized = VisualEngineeringPropertyMigration.NormalizeCurrentElements([element])!.Single();
        var copy = JsonSerializer.Deserialize<VisualElementEngineeringDto>(JsonSerializer.Serialize(normalized))!;
        Assert.Equal(element.Properties![VisualPropertyKeys.IntelligenceOperatingContexts].GetString(),
            copy.Properties![VisualPropertyKeys.IntelligenceOperatingContexts].GetString());
        Assert.DoesNotContain("referenceWindow", copy.Properties.Keys);
        Assert.DoesNotContain("result", copy.Properties.Keys);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("duplicate-context")]
    [InlineData("bad-id")]
    [InlineData("zero-version")]
    [InlineData("fractional-version")]
    [InlineData("empty-label")]
    [InlineData("long-label")]
    [InlineData("duplicate-tag")]
    [InlineData("bad-tag")]
    [InlineData("empty-tags")]
    [InlineData("too-many-tags")]
    [InlineData("too-many-contexts")]
    [InlineData("predicate-values")]
    [InlineData("too-long")]
    public void InvalidReferenceCatalog_IsRejected(string scenario)
    {
        var contexts = Contexts(); var first = contexts[0]!;
        switch (scenario)
        {
            case "empty": contexts.Clear(); break;
            case "duplicate-context": contexts.Add(first.DeepClone()); break;
            case "bad-id": first["id"] = "bad"; break;
            case "zero-version": first["version"] = 0; break;
            case "fractional-version": first["version"] = 1.5; break;
            case "empty-label": first["displayLabel"] = " "; break;
            case "long-label": first["displayLabel"] = new string('a', 129); break;
            case "duplicate-tag": first["predicateTagIds"] = new JsonArray(TagId, TagId); break;
            case "bad-tag": first["predicateTagIds"] = new JsonArray("bad"); break;
            case "empty-tags": first["predicateTagIds"] = new JsonArray(); break;
            case "too-many-tags": first["predicateTagIds"] = new JsonArray(Enumerable.Range(1, 17).Select(i => JsonValue.Create($"30000000-0000-0000-0000-{i:000000000000}")).ToArray()); break;
            case "too-many-contexts":
                contexts.Clear();
                for (var i = 1; i <= 33; i++) { var context = Contexts()[0]!.DeepClone(); context["id"] = $"30000000-0000-0000-0000-{i:000000000000}"; contexts.Add(context); }
                break;
            case "predicate-values": first["predicates"] = new JsonArray(new JsonObject { ["minimum"] = 20 }); break;
            case "too-long": first["displayLabel"] = new string('a', 32768); break;
        }
        AssertInvalid(Element(contexts));
    }

    [Theory]
    [InlineData(ImportEntityKind.Popup)]
    [InlineData(ImportEntityKind.Template)]
    public void ContextReport_IsScreenOnly(ImportEntityKind kind) => Assert.Contains(Validate(Element(Contexts()), kind), issue => issue.IsError);

    [Theory]
    [InlineData(VisualPropertyKeys.IntelligenceScopeId)]
    [InlineData(VisualPropertyKeys.IntelligenceAssignmentId)]
    [InlineData(VisualPropertyKeys.IntelligenceProfileId)]
    public void InvalidIdentity_IsRejected(string property)
    {
        var element = Element(Contexts());
        var properties = element.Properties!.ToDictionary(item => item.Key, item => item.Value);
        properties[property] = JsonSerializer.SerializeToElement("bad");
        AssertInvalid(element with { Properties = properties });
    }

    private static JsonArray Contexts() => new(new JsonObject { ["id"] = Id, ["displayLabel"] = "Normal operation", ["version"] = 3,
        ["predicateTagIds"] = new JsonArray(TagId) });
    private static VisualElementEngineeringDto Element(JsonArray contexts) => new("conditions", BuiltinVisualObjectSchemas.IntelligenceContextEligibilityV1Type,
        Properties: new Dictionary<string, JsonElement>
        {
            [VisualPropertyKeys.IntelligenceScopeId] = JsonSerializer.SerializeToElement(Id),
            [VisualPropertyKeys.IntelligenceAssignmentId] = JsonSerializer.SerializeToElement(Id),
            [VisualPropertyKeys.IntelligenceProfileId] = JsonSerializer.SerializeToElement(Id),
            [VisualPropertyKeys.IntelligenceOperatingContexts] = JsonSerializer.SerializeToElement(contexts.ToJsonString())
        });
    private static IReadOnlyCollection<ImportIssue> Validate(VisualElementEngineeringDto element, ImportEntityKind kind = ImportEntityKind.Screen) =>
        BuiltinVisualEngineeringValidation.Validate(element, kind, "conditions", schemaVersion: int.MaxValue);
    private static void AssertInvalid(VisualElementEngineeringDto element) => Assert.Contains(Validate(element),
        issue => issue.IsError && issue.Code == "VISUAL_INTELLIGENCE_CONTEXT_ELIGIBILITY_CONFIG_INVALID");
}
