using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class EngineeringIntelligenceProcessStageCoverageVisualValidationTests
{
    private const string StageId = "10000000-0000-0000-0000-000000000001";
    private const string AssignmentOne = "20000000-0000-0000-0000-000000000002";
    private const string AssignmentTwo = "30000000-0000-0000-0000-000000000003";
    private const string ScopeOne = "40000000-0000-0000-0000-000000000004";
    private const string ScopeTwo = "50000000-0000-0000-0000-000000000005";
    private const string ProfileOne = "60000000-0000-0000-0000-000000000006";
    private const string ProfileTwo = "70000000-0000-0000-0000-000000000007";

    [Fact]
    public void ProcessStageCoverageVisual_AcceptsAndRoundTripsStableMemberReferencesOnScreen()
    {
        var element = CreateElement();
        var issues = BuiltinVisualEngineeringValidation.Validate(
            element,
            ImportEntityKind.Screen,
            "intelligence.process-stage-coverage",
            schemaVersion: int.MaxValue);

        Assert.DoesNotContain(issues, issue => issue.IsError);
        var normalized = VisualEngineeringPropertyMigration.NormalizeCurrentElements([element])!.Single();
        var serialized = JsonSerializer.Serialize(normalized);
        var roundTripped = JsonSerializer.Deserialize<VisualElementEngineeringDto>(serialized);
        Assert.NotNull(roundTripped);
        Assert.Equal(element.Type, roundTripped.Type);
        Assert.Equal(StageId, roundTripped.Properties![VisualPropertyKeys.IntelligenceStageId].GetString());
        var membersJson = roundTripped.Properties[VisualPropertyKeys.IntelligenceStageMembers].GetString();
        using var members = JsonDocument.Parse(membersJson!);
        Assert.Equal(2, members.RootElement.GetArrayLength());
        Assert.DoesNotContain("result", roundTripped.Properties.Keys);
        Assert.DoesNotContain("window", roundTripped.Properties.Keys);
    }

    [Theory]
    [InlineData(ImportEntityKind.Popup)]
    [InlineData(ImportEntityKind.Template)]
    public void ProcessStageCoverageVisual_IsScreenOnly(ImportEntityKind entityKind)
    {
        var issues = BuiltinVisualEngineeringValidation.Validate(
            CreateElement(), entityKind, "intelligence.process-stage-coverage", schemaVersion: int.MaxValue);

        Assert.Contains(issues, issue =>
            issue.Code == "VISUAL_INTELLIGENCE_PROCESS_STAGE_COVERAGE_CONFIG_INVALID" && issue.IsError);
    }

    [Fact]
    public void ProcessStageCoverageVisual_RejectsInvalidOrUnboundedMemberCatalog()
    {
        var properties = ValidProperties();
        properties[VisualPropertyKeys.IntelligenceStageMembers] = JsonSerializer.SerializeToElement("[]");

        AssertInvalid(properties);

        properties = ValidProperties();
        properties[VisualPropertyKeys.IntelligenceStageMembers] = JsonSerializer.SerializeToElement("not-json");
        AssertInvalid(properties);

        properties = ValidProperties();
        var duplicate = JsonSerializer.Deserialize<JsonElement[]>(properties[VisualPropertyKeys.IntelligenceStageMembers].GetString()!)!;
        properties[VisualPropertyKeys.IntelligenceStageMembers] = JsonSerializer.SerializeToElement(JsonSerializer.Serialize(new[] { duplicate[0], duplicate[0] }));
        AssertInvalid(properties);

        properties = ValidProperties();
        var tooMany = Enumerable.Range(0, 33).Select(index => new
        {
            assignmentId = Guid.NewGuid(),
            scopeId = Guid.NewGuid(),
            profileId = Guid.NewGuid(),
            displayLabel = $"Equipment {index + 1}",
            order = index
        });
        properties[VisualPropertyKeys.IntelligenceStageMembers] = JsonSerializer.SerializeToElement(JsonSerializer.Serialize(tooMany));
        AssertInvalid(properties);
    }

    [Fact]
    public void ProcessStageCoverageVisual_RejectsMalformedStageIdentityAndMemberOrder()
    {
        var properties = ValidProperties();
        properties[VisualPropertyKeys.IntelligenceStageId] = JsonSerializer.SerializeToElement("not-a-guid");
        AssertInvalid(properties);

        properties = ValidProperties();
        properties[VisualPropertyKeys.IntelligenceStageMembers] = JsonSerializer.SerializeToElement(JsonSerializer.Serialize(new[]
        {
            new { assignmentId = AssignmentOne, scopeId = ScopeOne, profileId = ProfileOne, displayLabel = "Pump 1", order = 1 },
            new { assignmentId = AssignmentTwo, scopeId = ScopeTwo, profileId = ProfileTwo, displayLabel = "Pump 2", order = 1 }
        }));
        AssertInvalid(properties);
    }

    private static void AssertInvalid(Dictionary<string, JsonElement> properties)
    {
        var issues = BuiltinVisualEngineeringValidation.Validate(
            new VisualElementEngineeringDto("stage-coverage", BuiltinVisualObjectSchemas.IntelligenceProcessStageCoverageV1Type, Properties: properties),
            ImportEntityKind.Screen,
            "intelligence.process-stage-coverage",
            schemaVersion: int.MaxValue);

        Assert.Contains(issues, issue =>
            issue.Code == "VISUAL_INTELLIGENCE_PROCESS_STAGE_COVERAGE_CONFIG_INVALID" && issue.IsError);
    }

    private static VisualElementEngineeringDto CreateElement() => new(
        "stage-coverage",
        BuiltinVisualObjectSchemas.IntelligenceProcessStageCoverageV1Type,
        Properties: ValidProperties());

    private static Dictionary<string, JsonElement> ValidProperties() => new(StringComparer.Ordinal)
    {
        [VisualPropertyKeys.IntelligenceStageId] = JsonSerializer.SerializeToElement(StageId),
        [VisualPropertyKeys.IntelligenceStageKey] = JsonSerializer.SerializeToElement("pumping-stage"),
        [VisualPropertyKeys.IntelligenceStageName] = JsonSerializer.SerializeToElement("Pumping stage"),
        [VisualPropertyKeys.IntelligenceStageMembers] = JsonSerializer.SerializeToElement(JsonSerializer.Serialize(new[]
        {
            new { assignmentId = AssignmentOne, scopeId = ScopeOne, profileId = ProfileOne, displayLabel = "Pump P-101 · Pump profile", order = 0 },
            new { assignmentId = AssignmentTwo, scopeId = ScopeTwo, profileId = ProfileTwo, displayLabel = "Pump P-102 · Pump profile", order = 1 }
        }))
    };
}
