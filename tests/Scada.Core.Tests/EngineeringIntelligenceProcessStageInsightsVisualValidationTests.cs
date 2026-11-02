using System.Text.Json;
using System.Text.Json.Nodes;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class EngineeringIntelligenceProcessStageInsightsVisualValidationTests
{
    private const string First = "10000000-0000-0000-0000-000000000001";
    private const string Second = "10000000-0000-0000-0000-000000000002";
    private const string Third = "10000000-0000-0000-0000-000000000003";
    private const string Fourth = "10000000-0000-0000-0000-000000000004";

    [Fact]
    public void RegisteredInsightScreen_PreservesIndependentReportsThroughNormalization()
    {
        var members = new[] { Member(0), Member(1), Member(2) };
        members[1].Remove("contextualDeviation");
        members[2].Remove("performance");
        members[2].Remove("contextualDeviation");
        var element = Element(members);
        Assert.DoesNotContain(Validate(element), issue => issue.IsError);
        var normalized = VisualEngineeringPropertyMigration.NormalizeCurrentElements([element])!.Single();
        var roundTrip = JsonSerializer.Deserialize<VisualElementEngineeringDto>(JsonSerializer.Serialize(normalized))!;
        Assert.Equal(element.Type, roundTrip.Type);
        var persisted = JsonNode.Parse(roundTrip.Properties![VisualPropertyKeys.IntelligenceStageMembers].GetString()!)!.AsArray();
        Assert.Equal(3, persisted.Count);
        Assert.Null(persisted[1]!["contextualDeviation"]);
        Assert.Null(persisted[2]!["performance"]);
        Assert.Equal(2, persisted[2]!["availabilityVersion"]!.GetValue<int>());
    }

    [Fact]
    public void ReferenceOnlyMember_IsValidAndPersistsItsExistingScopeAndProfile()
    {
        var member = Member(0);
        member.Remove("performance"); member.Remove("contextualDeviation"); member["referenceEvidence"] = true;
        var element = Element(member);
        Assert.DoesNotContain(Validate(element), issue => issue.IsError);
        var persisted = VisualEngineeringPropertyMigration.NormalizeCurrentElements([element])!.Single();
        var actual = JsonNode.Parse(persisted.Properties![VisualPropertyKeys.IntelligenceStageMembers].GetString()!)![0]!;
        Assert.True(actual["referenceEvidence"]!.GetValue<bool>());
        Assert.Equal(member["scopeId"]!.GetValue<string>(), actual["scopeId"]!.GetValue<string>());
        Assert.Equal(member["profileId"]!.GetValue<string>(), actual["profileId"]!.GetValue<string>());
        Assert.Null(actual["performance"]);
        Assert.Null(actual["contextualDeviation"]);
    }

    [Fact]
    public void LegacyBothReports_RemainsValidWithoutAvailabilityMarker()
    {
        var member = Member(0);
        member.Remove("availabilityVersion");
        Assert.DoesNotContain(Validate(Element(member)), issue => issue.IsError);
        member.Remove("performance");
        AssertInvalid(Element(member));
    }

    [Theory]
    [InlineData("unknown-version")]
    [InlineData("legacy-reference")]
    [InlineData("false-reference")]
    [InlineData("null-reference")]
    [InlineData("null-performance")]
    [InlineData("null-deviation")]
    [InlineData("duplicate-signals")]
    [InlineData("context-is-target")]
    [InlineData("equal-range")]
    [InlineData("non-numeric-range")]
    [InlineData("rule-missing")]
    [InlineData("malformed-identity")]
    public void InvalidPresentReport_IsRejectedRatherThanTreatedAsUnavailable(string scenario)
    {
        var member = Member(0);
        switch (scenario)
        {
            case "legacy-reference": member.Remove("availabilityVersion"); member["referenceEvidence"] = true; break;
            case "false-reference": member["referenceEvidence"] = false; break;
            case "null-reference": member["referenceEvidence"] = null; break;
            case "unknown-version": member["availabilityVersion"] = 3; break;
            case "null-performance": member["performance"] = null; break;
            case "null-deviation": member["contextualDeviation"] = null; break;
            case "duplicate-signals": member["performance"]!["signalIds"] = new JsonArray(First, First); break;
            case "context-is-target": member["contextualDeviation"]!["firstContextSignalId"] = First; break;
            case "equal-range": member["contextualDeviation"]!["firstContextUpperExclusiveCanonical"] = 0; break;
            case "non-numeric-range": member["contextualDeviation"]!["secondContextLowerInclusiveCanonical"] = "0"; break;
            case "rule-missing": member["performance"]!["ruleId"] = " "; break;
            case "malformed-identity": member["scopeId"] = "wrong"; break;
        }
        AssertInvalid(Element(member));
    }

    [Theory]
    [InlineData(ImportEntityKind.Popup)]
    [InlineData(ImportEntityKind.Template)]
    public void Insights_RemainsScreenOnly(ImportEntityKind kind) =>
        Assert.Contains(Validate(Element(Member(0)), kind), issue => issue.IsError);

    [Fact]
    public void AmbiguousOrUnboundedCatalog_IsRejected()
    {
        AssertInvalid(Element(Member(0), Member(0)));
        AssertInvalid(Element([]));
        AssertInvalid(Element(Enumerable.Range(0, 33).Select(Member).ToArray()));
        var member = Member(0);
        member["padding"] = new string('x', 65536);
        AssertInvalid(Element(member));
    }

    [Fact]
    public void ContextOnlyMember_PreservesVersionedReferencesWithoutPredicateValues()
    {
        var member = Member(0); member.Remove("performance"); member.Remove("contextualDeviation");
        member["operatingContexts"] = Contexts();
        var element = Element(member, Member(1));
        Assert.DoesNotContain(Validate(element), issue => issue.IsError);
        var copy = VisualEngineeringPropertyMigration.NormalizeCurrentElements([element])!.Single();
        var persisted = JsonNode.Parse(copy.Properties![VisualPropertyKeys.IntelligenceStageMembers].GetString()!)!.AsArray();
        Assert.Equal(3, persisted[0]!["operatingContexts"]![0]!["version"]!.GetValue<int>());
        Assert.Null(persisted[1]!["operatingContexts"]);
        Assert.Null(persisted[0]!["operatingContexts"]![0]!["predicates"]);
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("zero-version")]
    [InlineData("duplicate-context")]
    [InlineData("bad-tag")]
    [InlineData("duplicate-tag")]
    [InlineData("predicate-values")]
    [InlineData("too-many")]
    public void InvalidMemberContextCatalog_IsRejected(string scenario)
    {
        var member = Member(0); var contexts = Contexts(); member["operatingContexts"] = contexts;
        switch (scenario)
        {
            case "legacy": member.Remove("availabilityVersion"); break;
            case "null": member["operatingContexts"] = null; break;
            case "empty": contexts.Clear(); break;
            case "zero-version": contexts[0]!["version"] = 0; break;
            case "duplicate-context": contexts.Add(contexts[0]!.DeepClone()); break;
            case "bad-tag": contexts[0]!["predicateTagIds"] = new JsonArray("bad"); break;
            case "duplicate-tag": contexts[0]!["predicateTagIds"] = new JsonArray(First, First); break;
            case "predicate-values": contexts[0]!["minimum"] = 0; break;
            case "too-many":
                contexts.Clear(); for (var i = 0; i < 33; i++) { var item = Contexts()[0]!.DeepClone(); item["id"] = $"40000000-0000-0000-0000-{i:000000000000}"; contexts.Add(item); } break;
        }
        AssertInvalid(Element(member));
    }

    private static JsonArray Contexts() => new(new JsonObject { ["id"] = First, ["displayLabel"] = "Running", ["version"] = 3,
        ["predicateTagIds"] = new JsonArray(Second) });

    private static JsonObject Member(int order) => JsonSerializer.SerializeToNode(new
    {
        assignmentId = $"20000000-0000-0000-0000-{order + 1:000000000000}",
        scopeId = $"30000000-0000-0000-0000-{order + 1:000000000000}",
        profileId = First,
        displayLabel = $"Pump {order + 1}",
        order,
        availabilityVersion = 2,
        performance = new { ruleId = "specific-energy-consumption", ruleVersion = "1", signalIds = new[] { First, Second } },
        contextualDeviation = new
        {
            targetSignalIds = new[] { First, Second },
            firstContextSignalId = Third,
            secondContextSignalId = Fourth,
            firstContextLowerInclusiveCanonical = 0,
            firstContextUpperExclusiveCanonical = 500000,
            secondContextLowerInclusiveCanonical = 0,
            secondContextUpperExclusiveCanonical = 500000
        }
    })!.AsObject();

    private static VisualElementEngineeringDto Element(params JsonObject[] members) => new(
        "stage-insights", BuiltinVisualObjectSchemas.IntelligenceProcessStageInsightsV1Type,
        Properties: new Dictionary<string, JsonElement>
        {
            [VisualPropertyKeys.IntelligenceStageId] = JsonSerializer.SerializeToElement(First),
            [VisualPropertyKeys.IntelligenceStageKey] = JsonSerializer.SerializeToElement("pumping"),
            [VisualPropertyKeys.IntelligenceStageName] = JsonSerializer.SerializeToElement("Pumping"),
            [VisualPropertyKeys.IntelligenceStageMembers] = JsonSerializer.SerializeToElement(JsonSerializer.Serialize(members))
        });

    private static IReadOnlyCollection<ImportIssue> Validate(VisualElementEngineeringDto element, ImportEntityKind kind = ImportEntityKind.Screen) =>
        BuiltinVisualEngineeringValidation.Validate(element, kind, "stage-insights", schemaVersion: int.MaxValue);

    private static void AssertInvalid(VisualElementEngineeringDto element) => Assert.Contains(Validate(element),
        issue => issue.Code == "VISUAL_INTELLIGENCE_PROCESS_STAGE_INSIGHTS_CONFIG_INVALID" && issue.IsError);
}
