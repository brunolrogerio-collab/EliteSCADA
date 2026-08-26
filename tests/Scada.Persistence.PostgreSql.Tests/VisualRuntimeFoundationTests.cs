using Scada.Engineering.VisualScripting;

namespace Scada.Persistence.PostgreSql.Tests;

public sealed class VisualRuntimeFoundationTests
{
    [Fact]
    public void VisualRuntimeDefinition_EnforcesStableUniqueObjectIdentityAndAcyclicParents()
    {
        var schema = CreateRectangleSchema();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        var first = new VisualObjectRuntimeDefinition(
            firstId,
            "pump",
            new VisualEngineeringPropertySet(schema));

        var duplicateKey = new VisualObjectRuntimeDefinition(
            secondId,
            "pump",
            new VisualEngineeringPropertySet(schema));

        Assert.Throws<ArgumentException>(() =>
            new VisualRuntimeDefinition(
                Guid.NewGuid(),
                "main",
                VisualRuntimeDefinitionKind.Screen,
                [first, duplicateKey]));

        var parent = new VisualObjectRuntimeDefinition(
            firstId,
            "parent",
            new VisualEngineeringPropertySet(schema),
            secondId);

        var child = new VisualObjectRuntimeDefinition(
            secondId,
            "child",
            new VisualEngineeringPropertySet(schema),
            firstId);

        Assert.Throws<ArgumentException>(() =>
            new VisualRuntimeDefinition(
                Guid.NewGuid(),
                "cyclic",
                VisualRuntimeDefinitionKind.Dynamo,
                [parent, child]));
    }

    [Fact]
    public void RuntimeInstances_ArePerOpenInstanceAndDoNotPersistPresentationOverrides()
    {
        var schema = CreateRectangleSchema();
        var objectId = Guid.NewGuid();
        var definition = new VisualRuntimeDefinition(
            Guid.NewGuid(),
            "overview",
            VisualRuntimeDefinitionKind.Screen,
            [
                new VisualObjectRuntimeDefinition(
                    objectId,
                    "pump",
                    new VisualEngineeringPropertySet(
                        schema,
                        new Dictionary<string, VisualPropertyValue>
                        {
                            [VisualPropertyKeys.X] = new VisualNumberValue(10)
                        }))
            ]);

        using var first = new VisualRuntimeInstance(definition, "client-a");
        first.GetRequiredObject(objectId)
            .WriteScriptProperty(VisualPropertyKeys.X, new VisualNumberValue(50));

        Assert.Equal(
            50d,
            Assert.IsType<VisualNumberValue>(
                first.GetRequiredObject(objectId)
                    .ReadProperty(VisualPropertyKeys.X)
                    .Value)
                .Value);

        var firstRuntimeKey = first.Identity.RuntimeKey;
        first.Dispose();

        using var reopened = new VisualRuntimeInstance(definition, "client-a");

        Assert.NotEqual(firstRuntimeKey, reopened.Identity.RuntimeKey);
        Assert.Equal(
            10d,
            Assert.IsType<VisualNumberValue>(
                reopened.GetRequiredObject(objectId)
                    .ReadProperty(VisualPropertyKeys.X)
                    .Value)
                .Value);
        Assert.Equal(
            VisualPropertyRuntimeSource.EngineeringBase,
            reopened.GetRequiredObject(objectId)
                .ReadProperty(VisualPropertyKeys.X)
                .Source);
    }

    [Fact]
    public async Task ClientVisualObjectApi_OnlyWritesDeclaredRuntimeWritableProperties()
    {
        var schema = new VisualPropertySchemaBuilder("core.readonly")
            .Add(new VisualPropertyDefinition(
                "status",
                new VisualStringValue("idle"),
                engineeringEditable: true,
                runtimeReadable: true,
                runtimeWritable: false,
                supportsBinding: true))
            .Add(new VisualPropertyDefinition(
                VisualPropertyKeys.X,
                new VisualNumberValue(0),
                animatable: true))
            .Add(new VisualPropertyDefinition(
                "lockedPosition",
                new VisualNumberValue(0),
                runtimeWritable: false,
                animatable: true))
            .Build();

        var objectId = Guid.NewGuid();
        var engineering = new VisualEngineeringPropertySet(schema);
        var definition = new VisualRuntimeDefinition(
            Guid.NewGuid(),
            "overview",
            VisualRuntimeDefinitionKind.Screen,
            [
                new VisualObjectRuntimeDefinition(
                    objectId,
                    "readonly",
                    engineering)
            ]);

        using var instance = new VisualRuntimeInstance(definition, "client-a");
        var scheduler = new RecordingTweenScheduler();
        var api = new ClientVisualObjectApi(instance, scheduler);

        Assert.Throws<InvalidOperationException>(() =>
            api.WriteProperty(
                objectId,
                "status",
                new VisualStringValue("forced")));

        api.WriteProperty(
            objectId,
            VisualPropertyKeys.X,
            new VisualNumberValue(25));

        Assert.Equal(
            25d,
            Assert.IsType<VisualNumberValue>(
                api.ReadProperty(objectId, VisualPropertyKeys.X).Value)
                .Value);

        Assert.Equal(
            0d,
            Assert.IsType<VisualNumberValue>(
                engineering.GetBaseValue(VisualPropertyKeys.X))
                .Value);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            api.AnimateAsync(
                    objectId,
                    new VisualTweenRequest(
                        "lockedPosition",
                        new VisualNumberValue(10),
                        TimeSpan.FromMilliseconds(100)))
                .AsTask());
    }

    [Fact]
    public async Task ClientVisualObjectApi_DelegatesTweenToRendererSchedulerForCurrentInstance()
    {
        var schema = CreateRectangleSchema();
        var objectId = Guid.NewGuid();
        var definition = new VisualRuntimeDefinition(
            Guid.NewGuid(),
            "overview",
            VisualRuntimeDefinitionKind.Screen,
            [
                new VisualObjectRuntimeDefinition(
                    objectId,
                    "pump",
                    new VisualEngineeringPropertySet(schema))
            ]);

        using var instance = new VisualRuntimeInstance(definition, "client-a");
        var scheduler = new RecordingTweenScheduler();
        var api = new ClientVisualObjectApi(instance, scheduler);

        var request = new VisualTweenRequest(
            VisualPropertyKeys.X,
            new VisualNumberValue(100),
            TimeSpan.FromMilliseconds(250),
            VisualTweenEasing.EaseInOut);

        var handle = await api.AnimateAsync(objectId, request);

        Assert.Equal(scheduler.NextHandle, handle);
        Assert.Equal(instance.Identity.RuntimeKey, scheduler.LastRuntimeInstanceId);
        Assert.Equal(objectId.ToString("D"), scheduler.LastObjectId);
        Assert.Same(request, scheduler.LastRequest);
    }

    [Fact]
    public void RuntimeDisposal_CancelsLifetimeAndDisposesOwnedSubscriptions()
    {
        var schema = CreateRectangleSchema();
        var definition = new VisualRuntimeDefinition(
            Guid.NewGuid(),
            "popup",
            VisualRuntimeDefinitionKind.Popup,
            [
                new VisualObjectRuntimeDefinition(
                    Guid.NewGuid(),
                    "value",
                    new VisualEngineeringPropertySet(schema))
            ]);

        var script = new PythonScriptDefinition(
            Guid.NewGuid(),
            "screens/popup/script",
            "Popup Script",
            PythonScriptScope.ClientVisual,
            "def on_timer():\n    pass",
            entryPoints:
            [
                new PythonScriptEntryPoint(
                    PythonScriptEventKind.Timer,
                    "on_timer",
                    "refresh")
            ]);

        var instance = new VisualRuntimeInstance(definition, "client-a");
        var lifetime = instance.LifetimeCancellation;

        instance.Subscriptions.Register(
            script,
            Assert.Single(script.EntryPoints),
            ScriptExecutionPolicy.SafeDefault,
            TimeSpan.FromMilliseconds(100));

        Assert.Equal(1, instance.Subscriptions.Count);

        instance.Dispose();

        Assert.True(lifetime.IsCancellationRequested);
        Assert.True(instance.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => instance.ListObjects());
        Assert.Throws<ObjectDisposedException>(() => instance.Subscriptions.Snapshot());
    }

    [Fact]
    public void BoundedEventQueue_CoalescesMatchingKeysWithoutGrowing()
    {
        var policy = new ScriptExecutionPolicy(
            TimeSpan.FromMilliseconds(200),
            maxQueuedEvents: 2,
            minimumTimerInterval: TimeSpan.FromMilliseconds(50),
            maxConsecutiveFailuresBeforeThrottle: 3,
            queueOverflowStrategy: ScriptQueueOverflowStrategy.CoalesceByEventKey);

        var queue = new BoundedScriptEventQueue(
            Guid.NewGuid(),
            "runtime-1",
            policy);

        var first = queue.Enqueue(
            new ScriptEventIdentity(
                PythonScriptEventKind.TagChanged,
                "on_tag",
                "tag:pressure"));

        var replacement = queue.Enqueue(
            new ScriptEventIdentity(
                PythonScriptEventKind.TagChanged,
                "on_tag",
                "tag:pressure"));

        Assert.Equal(ScriptEventEnqueueStatus.Enqueued, first.Status);
        Assert.Equal(ScriptEventEnqueueStatus.Coalesced, replacement.Status);
        Assert.Equal(1, queue.Count);
        Assert.NotNull(replacement.ReplacedOrDroppedEvent);
        Assert.True(
            replacement.EnqueuedEvent!.Sequence >
            replacement.ReplacedOrDroppedEvent!.Sequence);
    }

    [Fact]
    public void BoundedEventQueue_AppliesRejectAndDropOldestPoliciesDeterministically()
    {
        var rejectPolicy = new ScriptExecutionPolicy(
            TimeSpan.FromMilliseconds(200),
            maxQueuedEvents: 1,
            minimumTimerInterval: TimeSpan.FromMilliseconds(50),
            maxConsecutiveFailuresBeforeThrottle: 3,
            queueOverflowStrategy: ScriptQueueOverflowStrategy.RejectNewest);

        var rejectedQueue = new BoundedScriptEventQueue(
            Guid.NewGuid(),
            "runtime-reject",
            rejectPolicy);

        rejectedQueue.Enqueue(
            new ScriptEventIdentity(
                PythonScriptEventKind.ObjectInteraction,
                "on_click",
                "object:a"));

        var rejected = rejectedQueue.Enqueue(
            new ScriptEventIdentity(
                PythonScriptEventKind.ObjectInteraction,
                "on_click",
                "object:b"));

        Assert.Equal(ScriptEventEnqueueStatus.RejectedQueueFull, rejected.Status);
        Assert.Equal(1, rejectedQueue.Count);

        var dropPolicy = new ScriptExecutionPolicy(
            TimeSpan.FromMilliseconds(200),
            maxQueuedEvents: 1,
            minimumTimerInterval: TimeSpan.FromMilliseconds(50),
            maxConsecutiveFailuresBeforeThrottle: 3,
            queueOverflowStrategy: ScriptQueueOverflowStrategy.DropOldest);

        var dropQueue = new BoundedScriptEventQueue(
            Guid.NewGuid(),
            "runtime-drop",
            dropPolicy);

        var first = dropQueue.Enqueue(
            new ScriptEventIdentity(
                PythonScriptEventKind.ObjectInteraction,
                "on_click",
                "object:a"));

        var second = dropQueue.Enqueue(
            new ScriptEventIdentity(
                PythonScriptEventKind.ObjectInteraction,
                "on_click",
                "object:b"));

        Assert.Equal(
            ScriptEventEnqueueStatus.DroppedOldestAndEnqueued,
            second.Status);
        Assert.Equal(first.EnqueuedEvent, second.ReplacedOrDroppedEvent);

        Assert.True(dropQueue.TryDequeue(out var dequeued));
        Assert.Equal("object:b", dequeued!.Identity.TargetReference);
        Assert.False(dropQueue.TryDequeue(out _));
    }

    [Fact]
    public void SubscriptionRegistry_EnforcesScopeTimerBudgetAndNoDuplicates()
    {
        var policy = new ScriptExecutionPolicy(
            TimeSpan.FromMilliseconds(200),
            maxQueuedEvents: 8,
            minimumTimerInterval: TimeSpan.FromMilliseconds(100),
            maxConsecutiveFailuresBeforeThrottle: 3);

        var timerEntry = new PythonScriptEntryPoint(
            PythonScriptEventKind.Timer,
            "tick",
            "timer:refresh");

        var clientScript = new PythonScriptDefinition(
            Guid.NewGuid(),
            "screens/main/timer",
            "Timer",
            PythonScriptScope.ClientVisual,
            "def tick():\n    pass",
            entryPoints: [timerEntry]);

        using var registry = new ScriptEventSubscriptionRegistry("runtime-1");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            registry.Register(
                clientScript,
                timerEntry,
                policy,
                TimeSpan.FromMilliseconds(50)));

        registry.Register(
            clientScript,
            timerEntry,
            policy,
            TimeSpan.FromMilliseconds(100));

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(
                clientScript,
                timerEntry,
                policy,
                TimeSpan.FromMilliseconds(100)));

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(
                clientScript,
                new PythonScriptEntryPoint(
                    PythonScriptEventKind.TagChanged,
                    "undeclared",
                    "tag:pressure"),
                policy));

        var visualEntry = new PythonScriptEntryPoint(
            PythonScriptEventKind.ObjectInteraction,
            "on_click",
            "object:pump");

        var serverScript = new PythonScriptDefinition(
            Guid.NewGuid(),
            "server/calc",
            "Server Calc",
            PythonScriptScope.Server,
            "def on_click():\n    pass",
            entryPoints: [visualEntry]);

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(
                serverScript,
                visualEntry,
                policy));
    }

    [Fact]
    public void DiagnosticsTracker_ThrottlesRepeatedFaultsAndKeepsBoundedSanitizedError()
    {
        var scriptId = Guid.NewGuid();
        const string runtimeId = "runtime-1";
        var policy = new ScriptExecutionPolicy(
            TimeSpan.FromMilliseconds(200),
            maxQueuedEvents: 4,
            minimumTimerInterval: TimeSpan.FromMilliseconds(50),
            maxConsecutiveFailuresBeforeThrottle: 2);

        var diagnostics = new ScriptRuntimeDiagnosticsTracker(
            scriptId,
            runtimeId,
            policy);

        diagnostics.RecordExecution(
            new ScriptExecutionResult(
                scriptId,
                runtimeId,
                "handler",
                ScriptExecutionStatus.Faulted,
                TimeSpan.FromMilliseconds(10),
                DateTimeOffset.UtcNow,
                "first\nfault"));

        diagnostics.RecordExecution(
            new ScriptExecutionResult(
                scriptId,
                runtimeId,
                "handler",
                ScriptExecutionStatus.TimedOut,
                TimeSpan.FromMilliseconds(200),
                DateTimeOffset.UtcNow,
                new string('x', 1200)));

        diagnostics.RecordQueueResult(
            new ScriptEventEnqueueResult(
                ScriptEventEnqueueStatus.RejectedQueueFull,
                null,
                null));

        var snapshot = diagnostics.Snapshot(
            activeSubscriptions: 3,
            queuedEvents: 4);

        Assert.Equal(2L, snapshot.ExecutionCount);
        Assert.Equal(1L, snapshot.FaultedCount);
        Assert.Equal(1L, snapshot.TimeoutCount);
        Assert.Equal(1L, snapshot.QueueRejectedCount);
        Assert.Equal(2, snapshot.ConsecutiveFailures);
        Assert.True(snapshot.IsThrottled);
        Assert.NotNull(snapshot.LastSanitizedError);
        Assert.Equal(1024, snapshot.LastSanitizedError!.Length);
        Assert.DoesNotContain("\n", snapshot.LastSanitizedError, StringComparison.Ordinal);

        diagnostics.ResetThrottle();

        var reset = diagnostics.Snapshot(
            activeSubscriptions: 0,
            queuedEvents: 0);

        Assert.False(reset.IsThrottled);
        Assert.Equal(0, reset.ConsecutiveFailures);
    }

    private static VisualObjectPropertySchema CreateRectangleSchema() =>
        new VisualPropertySchemaBuilder("core.rectangle")
            .Include(CommonVisualPropertyDefinitions.Geometry)
            .Include(CommonVisualPropertyDefinitions.Transform)
            .Include(CommonVisualPropertyDefinitions.Visibility)
            .Build();

    private sealed class RecordingTweenScheduler : IVisualTweenScheduler
    {
        public VisualTweenHandle NextHandle { get; } =
            new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        public string? LastRuntimeInstanceId { get; private set; }

        public string? LastObjectId { get; private set; }

        public VisualTweenRequest? LastRequest { get; private set; }

        public event Action<VisualTweenCompletion>? Completed
        {
            add { }
            remove { }
        }

        public ValueTask<VisualTweenHandle> StartAsync(
            string runtimeInstanceId,
            string objectId,
            VisualObjectPropertySchema schema,
            VisualTweenRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRuntimeInstanceId = runtimeInstanceId;
            LastObjectId = objectId;
            LastRequest = request;
            request.ValidateFor(schema);
            return ValueTask.FromResult(NextHandle);
        }

        public ValueTask<bool> CancelAsync(
            VisualTweenHandle handle,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(handle == NextHandle);
    }
}
