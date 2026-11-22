using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class EngineeringIntelligencePerformanceVisualValidationTests
{
    [Fact]
    public void PerformanceIndicator_ValidatesAndRoundTripsStableReferences()
    {
        var element = CreateElement(
            scopeId: "10000000-0000-0000-0000-000000000001",
            assignmentId: "20000000-0000-0000-0000-000000000002",
            profileId: "30000000-0000-0000-0000-000000000003",
            ruleId: "performance.energy-per-output",
            ruleVersion: "1.0",
            signalIds: [
                "40000000-0000-0000-0000-000000000004",
                "50000000-0000-0000-0000-000000000005"]);

        var issues = BuiltinVisualEngineeringValidation.Validate(
            element,
            ImportEntityKind.Screen,
            "intelligence.performance",
            schemaVersion: int.MaxValue);

        Assert.DoesNotContain(issues, issue => issue.IsError);

        var normalized = VisualEngineeringPropertyMigration.NormalizeCurrentElements([element])!.Single();
        Assert.Equal(
            "performance.energy-per-output",
            normalized.Properties![VisualPropertyKeys.IntelligenceRuleId].GetString());
        Assert.Equal(
            "1.0",
            normalized.Properties[VisualPropertyKeys.IntelligenceRuleVersion].GetString());
        Assert.Equal(
            2,
            JsonSerializer.Deserialize<string[]>(normalized.Properties[VisualPropertyKeys.IntelligenceSignalIds].GetString()!)!.Length);
    }

    [Theory]
    [InlineData("not-a-guid", "20000000-0000-0000-0000-000000000002", "30000000-0000-0000-0000-000000000003", "performance.energy-per-output", "1.0", "[\"40000000-0000-0000-0000-000000000004\",\"50000000-0000-0000-0000-000000000005\"]")]
    [InlineData("10000000-0000-0000-0000-000000000001", "20000000-0000-0000-0000-000000000002", "30000000-0000-0000-0000-000000000003", "", "1.0", "[\"40000000-0000-0000-0000-000000000004\",\"50000000-0000-0000-0000-000000000005\"]")]
    [InlineData("10000000-0000-0000-0000-000000000001", "20000000-0000-0000-0000-000000000002", "30000000-0000-0000-0000-000000000003", " performance.energy-per-output", "1.0", "[\"40000000-0000-0000-0000-000000000004\",\"50000000-0000-0000-0000-000000000005\"]")]
    [InlineData("10000000-0000-0000-0000-000000000001", "20000000-0000-0000-0000-000000000002", "30000000-0000-0000-0000-000000000003", "performance.energy-per-output", "1.0 ", "[\"40000000-0000-0000-0000-000000000004\",\"50000000-0000-0000-0000-000000000005\"]")]
    [InlineData("10000000-0000-0000-0000-000000000001", "20000000-0000-0000-0000-000000000002", "30000000-0000-0000-0000-000000000003", "performance.energy-per-output", "1.0", "[\"40000000-0000-0000-0000-000000000004\",\"40000000-0000-0000-0000-000000000004\"]")]
    [InlineData("10000000-0000-0000-0000-000000000001", "20000000-0000-0000-0000-000000000002", "30000000-0000-0000-0000-000000000003", "performance.energy-per-output", "1.0", "[]")]
    public void PerformanceIndicator_RejectsIncompleteOrAmbiguousReferences(
        string scopeId,
        string assignmentId,
        string profileId,
        string ruleId,
        string ruleVersion,
        string signalIds)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [VisualPropertyKeys.IntelligenceScopeId] = Json($"\"{scopeId}\""),
            [VisualPropertyKeys.IntelligenceAssignmentId] = Json($"\"{assignmentId}\""),
            [VisualPropertyKeys.IntelligenceProfileId] = Json($"\"{profileId}\""),
            [VisualPropertyKeys.IntelligenceRuleId] = Json($"\"{ruleId}\""),
            [VisualPropertyKeys.IntelligenceRuleVersion] = Json($"\"{ruleVersion}\""),
            [VisualPropertyKeys.IntelligenceSignalIds] = Json(JsonSerializer.Serialize(JsonSerializer.Serialize(signalIds)))
        };
        var element = new VisualElementEngineeringDto(
            "performance",
            BuiltinVisualObjectSchemas.IntelligencePerformanceV1BType,
            Properties: properties);

        var issues = BuiltinVisualEngineeringValidation.Validate(
            element,
            ImportEntityKind.Screen,
            "intelligence.performance",
            schemaVersion: int.MaxValue);

        Assert.Contains(issues, issue => issue.Code == "VISUAL_INTELLIGENCE_PERFORMANCE_CONFIG_INVALID" && issue.IsError);
    }

    [Fact]
    public void PerformanceIndicator_RejectsOversizedSignalReferencePayload()
    {
        var element = CreateElement(
            scopeId: "10000000-0000-0000-0000-000000000001",
            assignmentId: "20000000-0000-0000-0000-000000000002",
            profileId: "30000000-0000-0000-0000-000000000003",
            ruleId: "performance.energy-per-output",
            ruleVersion: "1.0",
            signalIds: [
                "40000000-0000-0000-0000-000000000004",
                "50000000-0000-0000-0000-000000000005"]);
        var properties = new Dictionary<string, JsonElement>(element.Properties!, StringComparer.Ordinal)
        {
            [VisualPropertyKeys.IntelligenceSignalIds] = Json(JsonSerializer.Serialize(new string('x', 513)))
        };

        var issues = BuiltinVisualEngineeringValidation.Validate(
            element with { Properties = properties },
            ImportEntityKind.Screen,
            "intelligence.performance",
            schemaVersion: int.MaxValue);

        Assert.Contains(issues, issue => issue.Code == "VISUAL_INTELLIGENCE_PERFORMANCE_CONFIG_INVALID" && issue.IsError);
    }

    private static VisualElementEngineeringDto CreateElement(
        string scopeId,
        string assignmentId,
        string profileId,
        string ruleId,
        string ruleVersion,
        IReadOnlyCollection<string> signalIds) => new(
        "performance",
        BuiltinVisualObjectSchemas.IntelligencePerformanceV1BType,
        Properties: new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [VisualPropertyKeys.IntelligenceScopeId] = Json($"\"{scopeId}\""),
            [VisualPropertyKeys.IntelligenceAssignmentId] = Json($"\"{assignmentId}\""),
            [VisualPropertyKeys.IntelligenceProfileId] = Json($"\"{profileId}\""),
            [VisualPropertyKeys.IntelligenceRuleId] = Json($"\"{ruleId}\""),
            [VisualPropertyKeys.IntelligenceRuleVersion] = Json($"\"{ruleVersion}\""),
            [VisualPropertyKeys.IntelligenceSignalIds] = Json(JsonSerializer.Serialize(JsonSerializer.Serialize(signalIds)))
        });

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
