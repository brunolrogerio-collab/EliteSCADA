using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class VisualPropertyDefaultSourceConvergenceTests
{
    [Fact]
    public void Runtime_DistinguishesRegistryDefaultFromExplicitEngineeringBase()
    {
        var schema = new VisualPropertySchemaBuilder("core.rectangle")
            .Include(CommonVisualPropertyDefinitions.Geometry)
            .Build();
        var engineering = new VisualEngineeringPropertySet(
            schema,
            new Dictionary<string, VisualPropertyValue>
            {
                [VisualPropertyKeys.X] = new VisualNumberValue(15)
            });
        var runtime = new VisualRuntimePropertyState("client-a/screen-1/object-1", engineering);

        var explicitX = runtime.Resolve(VisualPropertyKeys.X);
        var defaultY = runtime.Resolve(VisualPropertyKeys.Y);

        Assert.Equal(VisualPropertyRuntimeSource.EngineeringBase, explicitX.Source);
        Assert.Equal(15d, Assert.IsType<VisualNumberValue>(explicitX.Value).Value);
        Assert.Equal(VisualPropertyRuntimeSource.Default, defaultY.Source);
        Assert.Equal(0d, Assert.IsType<VisualNumberValue>(defaultY.Value).Value);
        Assert.True(engineering.TryGetEngineeredValue(VisualPropertyKeys.X, out _));
        Assert.False(engineering.TryGetEngineeredValue(VisualPropertyKeys.Y, out _));

        // Compatibility API still exposes the effective design-time value.
        Assert.Equal(0d, Assert.IsType<VisualNumberValue>(engineering.GetBaseValue(VisualPropertyKeys.Y)).Value);
    }

    [Fact]
    public void RuntimeOverridePrecedence_StillFallsBackThroughEngineeringThenDefault()
    {
        var schema = new VisualPropertySchemaBuilder("core.rectangle")
            .Include(CommonVisualPropertyDefinitions.Geometry)
            .Build();
        var runtime = new VisualRuntimePropertyState(
            "client-a/screen-1/object-1",
            new VisualEngineeringPropertySet(schema));

        runtime.SetBindingOverride(VisualPropertyKeys.X, new VisualNumberValue(20));
        runtime.SetScriptOverride(VisualPropertyKeys.X, new VisualNumberValue(30));
        runtime.SetAnimationOverride(VisualPropertyKeys.X, new VisualNumberValue(40));
        Assert.Equal(VisualPropertyRuntimeSource.Animation, runtime.Resolve(VisualPropertyKeys.X).Source);

        runtime.ClearAnimationOverride(VisualPropertyKeys.X);
        Assert.Equal(VisualPropertyRuntimeSource.Script, runtime.Resolve(VisualPropertyKeys.X).Source);

        runtime.ClearScriptOverride(VisualPropertyKeys.X);
        Assert.Equal(VisualPropertyRuntimeSource.BindingOrExpression, runtime.Resolve(VisualPropertyKeys.X).Source);

        runtime.ClearBindingOverride(VisualPropertyKeys.X);
        Assert.Equal(VisualPropertyRuntimeSource.Default, runtime.Resolve(VisualPropertyKeys.X).Source);
    }
}
