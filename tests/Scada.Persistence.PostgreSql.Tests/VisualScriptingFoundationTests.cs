using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class VisualScriptingFoundationTests
{
    [Fact]
    public void RuntimePropertyResolution_UsesDeterministicPrecedence()
    {
        var schema = new VisualPropertySchemaBuilder("core.rectangle")
            .Include(CommonVisualPropertyDefinitions.Geometry)
            .Include(CommonVisualPropertyDefinitions.Visibility)
            .Include(CommonVisualPropertyDefinitions.Fill)
            .Build();

        var engineering = new VisualEngineeringPropertySet(
            schema,
            new Dictionary<string, VisualPropertyValue>
            {
                [VisualPropertyKeys.X] = new VisualNumberValue(10)
            });
        var runtime = new VisualRuntimePropertyState("client-a/screen-1/object-1", engineering);

        Assert.Equal(10d, Assert.IsType<VisualNumberValue>(runtime.Resolve(VisualPropertyKeys.X).Value).Value);
        Assert.Equal(VisualPropertyRuntimeSource.EngineeringBase, runtime.Resolve(VisualPropertyKeys.X).Source);

        runtime.SetBindingOverride(VisualPropertyKeys.X, new VisualNumberValue(20));
        Assert.Equal(20d, Assert.IsType<VisualNumberValue>(runtime.Resolve(VisualPropertyKeys.X).Value).Value);
        Assert.Equal(VisualPropertyRuntimeSource.BindingOrExpression, runtime.Resolve(VisualPropertyKeys.X).Source);

        runtime.SetScriptOverride(VisualPropertyKeys.X, new VisualNumberValue(30));
        Assert.Equal(30d, Assert.IsType<VisualNumberValue>(runtime.Resolve(VisualPropertyKeys.X).Value).Value);
        Assert.Equal(VisualPropertyRuntimeSource.Script, runtime.Resolve(VisualPropertyKeys.X).Source);

        runtime.SetAnimationOverride(VisualPropertyKeys.X, new VisualNumberValue(40));
        Assert.Equal(40d, Assert.IsType<VisualNumberValue>(runtime.Resolve(VisualPropertyKeys.X).Value).Value);
        Assert.Equal(VisualPropertyRuntimeSource.Animation, runtime.Resolve(VisualPropertyKeys.X).Source);

        runtime.ClearAnimationOverride(VisualPropertyKeys.X);
        Assert.Equal(30d, Assert.IsType<VisualNumberValue>(runtime.Resolve(VisualPropertyKeys.X).Value).Value);

        runtime.ClearScriptOverride(VisualPropertyKeys.X);
        Assert.Equal(20d, Assert.IsType<VisualNumberValue>(runtime.Resolve(VisualPropertyKeys.X).Value).Value);

        runtime.ClearBindingOverride(VisualPropertyKeys.X);
        Assert.Equal(10d, Assert.IsType<VisualNumberValue>(runtime.Resolve(VisualPropertyKeys.X).Value).Value);
    }

    [Fact]
    public void RuntimeOverrides_DoNotMutateEngineeringBaseValues()
    {
        var schema = new VisualPropertySchemaBuilder("core.rectangle")
            .Include(CommonVisualPropertyDefinitions.Geometry)
            .Include(CommonVisualPropertyDefinitions.Visibility)
            .Build();

        var engineering = new VisualEngineeringPropertySet(
            schema,
            new Dictionary<string, VisualPropertyValue>
            {
                [VisualPropertyKeys.Width] = new VisualNumberValue(125)
            });
        var runtime = new VisualRuntimePropertyState("client-a/screen-1/object-1", engineering);

        runtime.SetScriptOverride(VisualPropertyKeys.Width, new VisualNumberValue(300));

        Assert.Equal(300d, Assert.IsType<VisualNumberValue>(runtime.Resolve(VisualPropertyKeys.Width).Value).Value);
        Assert.Equal(125d, Assert.IsType<VisualNumberValue>(engineering.GetBaseValue(VisualPropertyKeys.Width)).Value);

        runtime.ClearAllRuntimeOverrides();

        Assert.Equal(125d, Assert.IsType<VisualNumberValue>(runtime.Resolve(VisualPropertyKeys.Width).Value).Value);
    }

    [Fact]
    public void ObjectSpecificProperties_MustBeExplicitlyDeclared()
    {
        var schema = new VisualPropertySchemaBuilder("core.gauge")
            .Include(CommonVisualPropertyDefinitions.Geometry)
            .Add(new VisualPropertyDefinition(
                "gauge.minimum",
                new VisualNumberValue(0),
                animatable: false))
            .Build();

        var runtime = new VisualRuntimePropertyState(
            "client-a/screen-1/gauge-1",
            new VisualEngineeringPropertySet(schema));

        runtime.SetScriptOverride("gauge.minimum", new VisualNumberValue(-10));

        Assert.Throws<KeyNotFoundException>(() =>
            runtime.SetScriptOverride("gauge.maximum", new VisualNumberValue(100)));
        Assert.False(schema.Declares(VisualPropertyKeys.Text));
        Assert.False(schema.Declares(VisualPropertyKeys.ImageResourceId));
    }

    [Fact]
    public void PropertySchema_EnforcesTypeAndConstraints()
    {
        var schema = new VisualPropertySchemaBuilder("core.rectangle")
            .Include(CommonVisualPropertyDefinitions.Visibility)
            .Include(CommonVisualPropertyDefinitions.Fill)
            .Build();

        var runtime = new VisualRuntimePropertyState(
            "client-a/screen-1/object-1",
            new VisualEngineeringPropertySet(schema));

        Assert.Throws<ArgumentException>(() =>
            runtime.SetScriptOverride(VisualPropertyKeys.Opacity, new VisualStringValue("opaque")));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            runtime.SetScriptOverride(VisualPropertyKeys.Opacity, new VisualNumberValue(1.1)));

        Assert.Throws<ArgumentException>(() =>
            runtime.SetScriptOverride(VisualPropertyKeys.FillColor, new VisualColorValue("red")));
    }

    [Fact]
    public void TweenContract_RequiresAnimatablePropertyAndBoundedRequest()
    {
        var schema = new VisualPropertySchemaBuilder("core.rectangle")
            .Include(CommonVisualPropertyDefinitions.Geometry)
            .Include(CommonVisualPropertyDefinitions.Visibility)
            .Build();

        var valid = new VisualTweenRequest(
            VisualPropertyKeys.X,
            new VisualNumberValue(250),
            TimeSpan.FromMilliseconds(500),
            VisualTweenEasing.EaseInOut,
            RepeatCount: 2,
            PingPong: true,
            ConflictBehavior: VisualTweenConflictBehavior.ReplaceExisting);

        valid.ValidateFor(schema);

        Assert.Throws<InvalidOperationException>(() =>
            new VisualTweenRequest(
                VisualPropertyKeys.Visible,
                new VisualBooleanValue(false),
                TimeSpan.FromMilliseconds(100))
            .ValidateFor(schema));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VisualTweenRequest(
                VisualPropertyKeys.X,
                new VisualNumberValue(50),
                TimeSpan.Zero)
            .ValidateFor(schema));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VisualTweenRequest(
                VisualPropertyKeys.X,
                new VisualNumberValue(50),
                TimeSpan.FromMilliseconds(100),
                RepeatCount: -1)
            .ValidateFor(schema));
    }

    [Fact]
    public void ScriptApiSurfaces_KeepClientAndServerScopesSeparated()
    {
        var client = ScriptApiSurface.ClientVisual();
        var server = ScriptApiSurface.Server();

        Assert.True(client.Allows(ScriptApiCapability.ReadClientMemory));
        Assert.True(client.Allows(ScriptApiCapability.WriteVisualProperties));
        Assert.True(client.Allows(ScriptApiCapability.RequestVisualTween));
        Assert.False(client.Allows(ScriptApiCapability.ReadServerMemory));
        Assert.False(client.Allows(ScriptApiCapability.WriteSharedTags));

        Assert.True(server.Allows(ScriptApiCapability.ReadServerMemory));
        Assert.True(server.Allows(ScriptApiCapability.WriteServerMemory));
        Assert.True(server.Allows(ScriptApiCapability.WriteSharedTags));
        Assert.False(server.Allows(ScriptApiCapability.ReadClientMemory));
        Assert.False(server.Allows(ScriptApiCapability.ReadVisualProperties));

        foreach (var denied in Enum.GetValues<ScriptSandboxDeniedBoundary>())
        {
            Assert.True(client.Denies(denied));
            Assert.True(server.Denies(denied));
        }

        Assert.Throws<ArgumentException>(() =>
            ScriptApiSurface.CreateValidated(
                PythonScriptScope.ClientVisual,
                ScriptApiCapability.ReadServerMemory));

        Assert.Throws<ArgumentException>(() =>
            ScriptApiSurface.CreateValidated(
                PythonScriptScope.Server,
                ScriptApiCapability.WriteVisualProperties));
    }

    [Fact]
    public void ExecutionPolicy_IsExplicitlyBoundedAndCreatesCancellableLease()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScriptExecutionPolicy(
                TimeSpan.Zero,
                1,
                TimeSpan.FromMilliseconds(10),
                1));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScriptExecutionPolicy(
                TimeSpan.FromMilliseconds(100),
                0,
                TimeSpan.FromMilliseconds(10),
                1));

        using var cancellation = new CancellationTokenSource();
        var policy = new ScriptExecutionPolicy(
            TimeSpan.FromMilliseconds(200),
            maxQueuedEvents: 32,
            minimumTimerInterval: TimeSpan.FromMilliseconds(25),
            maxConsecutiveFailuresBeforeThrottle: 3,
            queueOverflowStrategy: ScriptQueueOverflowStrategy.RejectNewest);

        var lease = ScriptExecutionLease.Create(
            Guid.NewGuid(),
            "client-a/screen-1/script-1",
            "on_click",
            policy,
            cancellation.Token);

        Assert.True(lease.Deadline > lease.StartedAt);
        Assert.Equal(cancellation.Token, lease.CancellationToken);
        Assert.Equal(ScriptFaultIsolationScope.ScriptRuntimeInstance, policy.FaultIsolationScope);
    }

    [Fact]
    public void PythonPreflight_ReportsSandboxViolationWithLineAndColumn()
    {
        var script = new PythonScriptDefinition(
            Guid.NewGuid(),
            "screens/main/scripts/unsafe",
            "Unsafe",
            PythonScriptScope.ClientVisual,
            """
            value = 1
            import os
            open("secret.txt")
            """);

        var result = new PythonPreflightValidator().Validate(script);

        Assert.False(result.IsValid);

        var importDiagnostic = Assert.Single(
            result.Diagnostics.Where(diagnostic => diagnostic.Code == "PY_SANDBOX_IMPORT_DENIED"));
        Assert.Equal(2, importDiagnostic.Span.Start.Line);
        Assert.Equal(1, importDiagnostic.Span.Start.Column);

        var callDiagnostic = Assert.Single(
            result.Diagnostics.Where(diagnostic => diagnostic.Code == "PY_SANDBOX_CALL_DENIED"));
        Assert.Equal(3, callDiagnostic.Span.Start.Line);
        Assert.Equal(1, callDiagnostic.Span.Start.Column);
    }

    [Fact]
    public void PythonPreflight_ValidatesScopeEntryPointsAndStructuralDelimiter()
    {
        var script = new PythonScriptDefinition(
            Guid.NewGuid(),
            "server/scripts/bad-scope",
            "Bad scope",
            PythonScriptScope.Server,
            "value = (1 + 2",
            entryPoints:
            [
                new PythonScriptEntryPoint(PythonScriptEventKind.FrameTick, "1bad")
            ]);

        var result = new PythonPreflightValidator().Validate(script);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PY_DELIMITER_UNCLOSED");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PY_ENTRYPOINT_IDENTIFIER_INVALID");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PY_SCOPE_EVENT_INVALID");
    }
}
