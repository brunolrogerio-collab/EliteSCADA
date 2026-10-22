using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class IntelligenceContextualDeviationVisualValidationTests
{
    private const string ScopeId = "10000000-0000-0000-0000-000000000001";
    private const string AssignmentId = "20000000-0000-0000-0000-000000000002";
    private const string ProfileId = "30000000-0000-0000-0000-000000000003";
    private const string TargetOneId = "40000000-0000-0000-0000-000000000004";
    private const string TargetTwoId = "50000000-0000-0000-0000-000000000005";
    private const string FirstContextId = "60000000-0000-0000-0000-000000000006";
    private const string SecondContextId = "70000000-0000-0000-0000-000000000007";

    [Fact]
    public void ContextualDeviationV2_AcceptsValidStableScreenConfiguration()
    {
        var issues = BuiltinVisualEngineeringValidation.Validate(Visual(), ImportEntityKind.Screen, "deviation", EngineeringExchangeService.CurrentSchemaVersion);

        Assert.DoesNotContain(issues, issue => issue.IsError);
    }

    [Fact]
    public void ContextualDeviationV2_IsScreenOnly()
    {
        var issues = BuiltinVisualEngineeringValidation.Validate(Visual(), ImportEntityKind.Template, "template", EngineeringExchangeService.CurrentSchemaVersion);

        Assert.Contains(issues, issue => issue.Code == "VISUAL_INTELLIGENCE_CONTEXTUAL_DEVIATION_CONFIG_INVALID");
    }

    [Fact]
    public void ContextualDeviationV2_RejectsOverlappingSignalIdentitiesAndInvalidCanonicalRanges()
    {
        var properties = ValidProperties();
        properties[VisualPropertyKeys.IntelligenceSignalIds] = JsonSerializer.SerializeToElement(JsonSerializer.Serialize(new[] { TargetOneId, FirstContextId }));
        properties[VisualPropertyKeys.IntelligenceFirstContextUpperExclusiveCanonical] = JsonSerializer.SerializeToElement(5d);
        var issues = BuiltinVisualEngineeringValidation.Validate(
            Visual(properties), ImportEntityKind.Screen, "deviation", EngineeringExchangeService.CurrentSchemaVersion);

        Assert.Contains(issues, issue => issue.Code == "VISUAL_INTELLIGENCE_CONTEXTUAL_DEVIATION_CONFIG_INVALID");
    }

    private static VisualElementEngineeringDto Visual(Dictionary<string, JsonElement>? properties = null) =>
        new("contextual-deviation", BuiltinVisualObjectSchemas.IntelligenceContextualDeviationV2Type,
            Properties: properties ?? ValidProperties());

    private static Dictionary<string, JsonElement> ValidProperties() => new(StringComparer.Ordinal)
    {
        [VisualPropertyKeys.IntelligenceScopeId] = JsonSerializer.SerializeToElement(ScopeId),
        [VisualPropertyKeys.IntelligenceAssignmentId] = JsonSerializer.SerializeToElement(AssignmentId),
        [VisualPropertyKeys.IntelligenceProfileId] = JsonSerializer.SerializeToElement(ProfileId),
        [VisualPropertyKeys.IntelligenceSignalIds] = JsonSerializer.SerializeToElement(JsonSerializer.Serialize(new[] { TargetOneId, TargetTwoId })),
        [VisualPropertyKeys.IntelligenceFirstContextSignalId] = JsonSerializer.SerializeToElement(FirstContextId),
        [VisualPropertyKeys.IntelligenceFirstContextLowerInclusiveCanonical] = JsonSerializer.SerializeToElement(5d),
        [VisualPropertyKeys.IntelligenceFirstContextUpperExclusiveCanonical] = JsonSerializer.SerializeToElement(25d),
        [VisualPropertyKeys.IntelligenceSecondContextSignalId] = JsonSerializer.SerializeToElement(SecondContextId),
        [VisualPropertyKeys.IntelligenceSecondContextLowerInclusiveCanonical] = JsonSerializer.SerializeToElement(100d),
        [VisualPropertyKeys.IntelligenceSecondContextUpperExclusiveCanonical] = JsonSerializer.SerializeToElement(220d)
    };
}
