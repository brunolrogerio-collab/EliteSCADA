using Scada.Engineering.Contracts;
using Scada.Engineering.Validation;
using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class VisualEngineeringMalformedInputTests
{
    [Fact]
    public void ScreenValidation_ReportsNullVisualNodesInsteadOfThrowing()
    {
        var screen = new ScreenEngineeringDto(
            null,
            "overview",
            "Overview",
            Elements: new VisualElementEngineeringDto[] { null! });

        var issues = EngineeringValidator.ValidateScreen(screen);

        Assert.Contains(issues, issue => issue.Code == "VISUAL_ELEMENT_NULL" && issue.IsError);
    }

    [Fact]
    public void ScreenValidation_ReportsNullAndMalformedBindingsInsteadOfThrowing()
    {
        var malformed = new VisualElementEngineeringDto(
            "pump",
            BuiltinVisualObjectSchemas.RectangleType,
            Bindings:
            [
                null!,
                new EngineeringBindingDto(null!, EngineeringBindingKind.Tag, null!),
                new EngineeringBindingDto(VisualPropertyKeys.X, EngineeringBindingKind.Tag, null!)
            ]);
        var screen = new ScreenEngineeringDto(null, "overview", "Overview", Elements: [malformed]);

        var issues = EngineeringValidator.ValidateScreen(screen);
        var builtinIssues = BuiltinVisualEngineeringValidation.Validate(
            malformed,
            ImportEntityKind.Screen,
            screen.Key,
            VisualEngineeringPropertyCodec.TypedSchemaVersion);

        Assert.Contains(issues, issue => issue.Code == "BINDING_NULL" && issue.IsError);
        Assert.Contains(issues, issue => issue.Code == "BINDING_KEY_REQUIRED" && issue.IsError);
        Assert.Contains(issues, issue => issue.Code == "BINDING_TARGET_REQUIRED" && issue.IsError);
        Assert.DoesNotContain(builtinIssues, issue => issue.Code == "VISUAL_BINDING_PROPERTY_UNKNOWN");
    }
}
