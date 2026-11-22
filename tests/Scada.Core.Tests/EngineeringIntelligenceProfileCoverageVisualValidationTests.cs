using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualScripting;

namespace Scada.Core.Tests;

public sealed class EngineeringIntelligenceProfileCoverageVisualValidationTests
{
    [Fact]
    public void ProfileCoverageVisual_ValidatesAndRoundTripsStableReferencesOnCanonicalScreen()
    {
        var element = CreateElement(
            "10000000-0000-0000-0000-000000000001",
            "30000000-0000-0000-0000-000000000003");

        var issues = BuiltinVisualEngineeringValidation.Validate(
            element,
            ImportEntityKind.Screen,
            "intelligence.coverage",
            schemaVersion: int.MaxValue);

        Assert.DoesNotContain(issues, issue => issue.IsError);
        var normalized = VisualEngineeringPropertyMigration.NormalizeCurrentElements([element])!.Single();
        Assert.Equal(
            "10000000-0000-0000-0000-000000000001",
            normalized.Properties![VisualPropertyKeys.IntelligenceScopeId].GetString());
        Assert.Equal(
            "30000000-0000-0000-0000-000000000003",
            normalized.Properties[VisualPropertyKeys.IntelligenceProfileId].GetString());
        var serialized = JsonSerializer.Serialize(normalized);
        var roundTripped = JsonSerializer.Deserialize<VisualElementEngineeringDto>(serialized);
        Assert.NotNull(roundTripped);
        Assert.Equal(element.Type, roundTripped.Type);
        Assert.Equal(
            "10000000-0000-0000-0000-000000000001",
            roundTripped.Properties![VisualPropertyKeys.IntelligenceScopeId].GetString());
        Assert.Equal(
            "30000000-0000-0000-0000-000000000003",
            roundTripped.Properties[VisualPropertyKeys.IntelligenceProfileId].GetString());
        Assert.DoesNotContain("result", roundTripped.Properties.Keys);
        Assert.DoesNotContain("window", roundTripped.Properties.Keys);
    }

    [Theory]
    [InlineData("bad-scope", "30000000-0000-0000-0000-000000000003")]
    [InlineData("10000000-0000-0000-0000-000000000001", "bad-profile")]
    [InlineData("", "30000000-0000-0000-0000-000000000003")]
    public void ProfileCoverageVisual_RejectsMissingOrNonCanonicalReferences(string scopeId, string profileId)
    {
        var issues = BuiltinVisualEngineeringValidation.Validate(
            CreateElement(scopeId, profileId),
            ImportEntityKind.Screen,
            "intelligence.coverage",
            schemaVersion: int.MaxValue);

        Assert.Contains(issues, issue =>
            issue.Code == "VISUAL_INTELLIGENCE_COVERAGE_CONFIG_INVALID" && issue.IsError);
    }

    [Fact]
    public void ProfileCoverageVisual_IsScreenOnly()
    {
        var issues = BuiltinVisualEngineeringValidation.Validate(
            CreateElement(
                "10000000-0000-0000-0000-000000000001",
                "30000000-0000-0000-0000-000000000003"),
            ImportEntityKind.Popup,
            "intelligence.coverage-popup",
            schemaVersion: int.MaxValue);

        Assert.Contains(issues, issue =>
            issue.Code == "VISUAL_INTELLIGENCE_COVERAGE_CONFIG_INVALID" && issue.IsError);
    }

    private static VisualElementEngineeringDto CreateElement(string scopeId, string profileId) => new(
        "coverage",
        BuiltinVisualObjectSchemas.IntelligenceProfileCoverageV1Type,
        Properties: new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [VisualPropertyKeys.IntelligenceScopeId] = Json($"\"{scopeId}\""),
            [VisualPropertyKeys.IntelligenceProfileId] = Json($"\"{profileId}\"")
        });

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
