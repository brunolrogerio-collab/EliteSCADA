using Scada.Engineering.Scripts;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class ScriptEngineeringValidationInvariantTests
{
    [Fact]
    public void InvalidDependencyKind_IsReportedWithoutEscapingValidation()
    {
        var script = new ScriptEngineeringDefinition(
            Guid.NewGuid(),
            "scripts/invalid-dependency",
            "Invalid dependency",
            ScriptEngineeringScope.ClientVisual,
            "value = 1",
            dependencies:
            [
                new ScriptEngineeringDependency(
                    (ScriptEngineeringDependencyKind)999,
                    "unknown")
            ]);

        var exception = Record.Exception(() =>
            new ScriptEngineeringValidator().Validate(
                new ScriptEngineeringModel([script])));

        Assert.Null(exception);

        var result = new ScriptEngineeringValidator().Validate(
            new ScriptEngineeringModel([script]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "SCRIPT_DEPENDENCY_KIND_INVALID");
    }
}
