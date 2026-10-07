using Scada.Core.Commands;
using Scada.Core.Interactions;
using Scada.Core.Tags;
using Scada.DriverHost.Runtime;

namespace Scada.Drivers.Tests;

public sealed class RichCommandRuntimeServiceTests
{
    [Fact]
    public async Task ValidInvocation_DefinitionAndBinding_DispatchesExactlyOnce()
    {
        var fixture = CreateFixture();
        using var runtime = fixture.Runtime;
        var invocation = fixture.Invocation();

        var result = await runtime.InvokeAsync(invocation);

        Assert.Equal(RichCommandOutcome.Completed, result.Outcome);
        Assert.Equal(1, fixture.Executor.DispatchCount);
        Assert.Equal(fixture.Binding, fixture.Executor.LastBinding);
        Assert.Equal(invocation.CommandId, fixture.Executor.LastInvocation!.CommandId);
    }

    [Fact]
    public async Task ParameterValidation_HappensBeforePhysicalDispatch()
    {
        var fixture = CreateFixture();
        using var runtime = fixture.Runtime;
        var invocation = fixture.Invocation(parameters:
        [
            new RichCommandParameterValue("position", InteractionScalarValue.String("invalid"))
        ]);

        var result = await runtime.InvokeAsync(invocation);

        Assert.Equal(RichCommandOutcome.Rejected, result.Outcome);
        Assert.Equal("invocation.invalid", result.Code);
        Assert.Equal(0, fixture.Executor.DispatchCount);
    }

    [Fact]
    public async Task UnknownDefinition_IsRejectedWithoutPhysicalDispatch()
    {
        var fixture = CreateFixture();
        using var runtime = fixture.Runtime;
        var invocation = fixture.Invocation() with { CommandId = Guid.NewGuid() };

        var result = await runtime.InvokeAsync(invocation);

        Assert.Equal(RichCommandOutcome.Rejected, result.Outcome);
        Assert.Equal("definition.not_found", result.Code);
        Assert.Equal(0, fixture.Executor.DispatchCount);
    }

    [Fact]
    public async Task MissingBinding_IsRejectedWithoutPhysicalDispatch()
    {
        var fixture = CreateFixture(includeBinding: false);
        using var runtime = fixture.Runtime;

        var result = await runtime.InvokeAsync(fixture.Invocation());

        Assert.Equal(RichCommandOutcome.Rejected, result.Outcome);
        Assert.Equal("binding.not_found", result.Code);
        Assert.Equal(0, fixture.Executor.DispatchCount);
    }

    [Fact]
    public async Task MismatchedBinding_IsRejectedWithoutPhysicalDispatch()
    {
        var fixture = CreateFixture(bindingResolverOverride: new MismatchedBindingResolver());
        using var runtime = fixture.Runtime;

        var result = await runtime.InvokeAsync(fixture.Invocation());

        Assert.Equal(RichCommandOutcome.Rejected, result.Outcome);
        Assert.Equal("binding.invalid", result.Code);
        Assert.Equal(0, fixture.Executor.DispatchCount);
    }

    [Fact]
    public async Task UnsupportedExecutor_IsRejectedWithoutPhysicalDispatch()
    {
        var fixture = CreateFixture(includeExecutor: false);
        using var runtime = fixture.Runtime;

        var result = await runtime.InvokeAsync(fixture.Invocation());

        Assert.Equal(RichCommandOutcome.Rejected, result.Outcome);
        Assert.Equal("executor.unsupported", result.Code);
        Assert.Equal(0, fixture.Executor.DispatchCount);
    }

    [Fact]
    public async Task Saturation_IsRejectedBeforePhysicalDispatch()
    {
        var gate = new BlockingExecutor();
        var fixture = CreateFixture(
            executor: gate,
            policy: new RichCommandRuntimePolicy(1, 1, TimeSpan.FromSeconds(5)));
        using var runtime = fixture.Runtime;

        var first = runtime.InvokeAsync(fixture.Invocation()).AsTask();
        await gate.FirstDispatchStarted.WaitAsync(TimeSpan.FromSeconds(2));

        var second = await runtime.InvokeAsync(fixture.Invocation());

        Assert.Equal(RichCommandOutcome.Rejected, second.Outcome);
        Assert.Equal("runtime.saturated", second.Code);
        Assert.Equal(1, gate.DispatchCount);

        gate.Release();
        Assert.Equal(RichCommandOutcome.Completed, (await first).Outcome);
    }

    [Fact]
    public async Task SameTarget_IsDispatchedInDeterministicAdmissionOrder()
    {
        var executor = new OrderedBlockingExecutor();
        var fixture = CreateFixture(
            executor: executor,
            policy: new RichCommandRuntimePolicy(4, 2, TimeSpan.FromSeconds(5)));
        using var runtime = fixture.Runtime;

        var firstInvocation = fixture.Invocation(Guid.Parse("55600000-0000-0000-0000-000000000001"));
        var secondInvocation = fixture.Invocation(Guid.Parse("55600000-0000-0000-0000-000000000002"));

        var first = runtime.InvokeAsync(firstInvocation).AsTask();
        await executor.FirstDispatchStarted.WaitAsync(TimeSpan.FromSeconds(2));
        var second = runtime.InvokeAsync(secondInvocation).AsTask();

        await Task.Delay(100);
        Assert.Equal(new[] { firstInvocation.InvocationId }, executor.DispatchOrder);

        executor.ReleaseFirst();
        await Task.WhenAll(first, second);

        Assert.Equal(
            new[] { firstInvocation.InvocationId, secondInvocation.InvocationId },
            executor.DispatchOrder);
    }

    [Fact]
    public async Task IndependentTargets_MayDispatchConcurrentlyWithoutGlobalOrdering()
    {
        var executor = new ConcurrentProbeExecutor(expectedConcurrent: 2);
        var secondDataSourceId = Guid.Parse("55600000-0000-0000-0000-000000000099");
        var fixture = CreateFixture(
            executor: executor,
            additionalBinding: CreateBinding(
                Command2Id,
                secondDataSourceId,
                "independent-device"),
            additionalDefinition: CreateDefinition(Command2Id),
            additionalExecutorDataSourceId: secondDataSourceId,
            policy: new RichCommandRuntimePolicy(4, 2, TimeSpan.FromSeconds(5)));
        using var runtime = fixture.Runtime;

        var first = runtime.InvokeAsync(fixture.Invocation()).AsTask();
        var second = runtime.InvokeAsync(
            fixture.Invocation(commandId: Command2Id)).AsTask();

        await executor.ConcurrencyReached.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, executor.PeakConcurrency);

        executor.Release();
        await Task.WhenAll(first, second);
    }

    [Theory]
    [InlineData(RichCommandOutcome.Accepted)]
    [InlineData(RichCommandOutcome.Completed)]
    [InlineData(RichCommandOutcome.Failed)]
    [InlineData(RichCommandOutcome.TimedOut)]
    [InlineData(RichCommandOutcome.Unknown)]
    public async Task CanonicalExecutorOutcome_IsPreserved(RichCommandOutcome outcome)
    {
        var executor = new RecordingExecutor(outcome);
        var fixture = CreateFixture(executor: executor);
        using var runtime = fixture.Runtime;

        var result = await runtime.InvokeAsync(fixture.Invocation());

        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(1, executor.DispatchCount);
    }

    [Fact]
    public async Task CancellationBeforeAdmissionDispatch_IsRejectedWithZeroPhysicalDispatch()
    {
        var fixture = CreateFixture();
        using var runtime = fixture.Runtime;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var result = await runtime.InvokeAsync(fixture.Invocation(), cancelled.Token);

        Assert.Equal(RichCommandOutcome.Rejected, result.Outcome);
        Assert.Equal("runtime.cancelled_before_dispatch", result.Code);
        Assert.Equal(0, fixture.Executor.DispatchCount);
    }

    [Fact]
    public async Task CancellationWhileWaitingForSameTarget_PreventsPhysicalDispatch()
    {
        var executor = new OrderedBlockingExecutor();
        var fixture = CreateFixture(
            executor: executor,
            policy: new RichCommandRuntimePolicy(3, 2, TimeSpan.FromSeconds(5)));
        using var runtime = fixture.Runtime;

        var first = runtime.InvokeAsync(fixture.Invocation()).AsTask();
        await executor.FirstDispatchStarted.WaitAsync(TimeSpan.FromSeconds(2));

        using var cancellation = new CancellationTokenSource();
        var second = runtime.InvokeAsync(fixture.Invocation(), cancellation.Token).AsTask();
        cancellation.Cancel();

        executor.ReleaseFirst();
        var secondResult = await second;
        await first;

        Assert.Equal(RichCommandOutcome.Rejected, secondResult.Outcome);
        Assert.Equal("runtime.cancelled_before_dispatch", secondResult.Code);
        Assert.Single(executor.DispatchOrder);
    }

    [Fact]
    public async Task CancellationAfterPhysicalDispatch_IsUnknownAndNeverRetried()
    {
        var executor = new CancellationAwareBlockingExecutor();
        var fixture = CreateFixture(
            executor: executor,
            policy: new RichCommandRuntimePolicy(2, 1, TimeSpan.FromSeconds(5)));
        using var runtime = fixture.Runtime;
        using var cancellation = new CancellationTokenSource();

        var pending = runtime.InvokeAsync(fixture.Invocation(), cancellation.Token).AsTask();
        await executor.DispatchStarted.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        var result = await pending;

        Assert.Equal(RichCommandOutcome.Unknown, result.Outcome);
        Assert.Equal("execution.cancelled_ambiguous", result.Code);
        Assert.Equal(1, executor.DispatchCount);
    }

    [Fact]
    public async Task TimeoutAfterPhysicalDispatch_IsUnknownAndNeverRetried()
    {
        var executor = new CancellationAwareBlockingExecutor();
        var fixture = CreateFixture(
            executor: executor,
            policy: new RichCommandRuntimePolicy(2, 1, TimeSpan.FromMilliseconds(100)));
        using var runtime = fixture.Runtime;

        var result = await runtime.InvokeAsync(fixture.Invocation());

        Assert.Equal(RichCommandOutcome.Unknown, result.Outcome);
        Assert.Equal("execution.timeout_ambiguous", result.Code);
        Assert.Equal(1, executor.DispatchCount);
    }

    [Fact]
    public async Task NonCooperativeExecutor_TimeoutReturnsUnknownButRetainsPhysicalOwnership()
    {
        var executor = new NonCooperativeBlockingExecutor();
        var independent = new RecordingExecutor(RichCommandOutcome.Completed);
        var independentDataSourceId = Guid.Parse("55600000-0000-0000-0000-000000000099");
        var fixture = CreateFixture(
            executor: executor,
            additionalDefinition: CreateDefinition(Command2Id),
            additionalBinding: CreateBinding(
                Command2Id,
                independentDataSourceId,
                "independent-device"),
            additionalExecutorDataSourceId: independentDataSourceId,
            additionalExecutor: independent,
            policy: new RichCommandRuntimePolicy(4, 2, TimeSpan.FromMilliseconds(100)));
        using var runtime = fixture.Runtime;

        var first = runtime.InvokeAsync(fixture.Invocation()).AsTask();
        await executor.FirstDispatchStarted.WaitAsync(TimeSpan.FromSeconds(2));

        var firstResult = await first.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(RichCommandOutcome.Unknown, firstResult.Outcome);
        Assert.Equal("execution.timeout_ambiguous", firstResult.Code);
        Assert.Equal(1, executor.DispatchCount);
        Assert.Equal(1, executor.ActiveCount);
        Assert.Equal(1, executor.PeakConcurrency);

        // Caller timeout must not release the same-target physical ownership while
        // the non-cooperative executor is still running.
        var sameTarget = runtime.InvokeAsync(fixture.Invocation()).AsTask();
        await Task.Delay(150);
        Assert.False(sameTarget.IsCompleted);
        Assert.Equal(1, executor.DispatchCount);
        Assert.Equal(1, executor.PeakConcurrency);

        // Independent targets remain allowed to use another bounded physical slot.
        var independentResult = await runtime.InvokeAsync(
            fixture.Invocation(commandId: Command2Id)).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(RichCommandOutcome.Completed, independentResult.Outcome);
        Assert.Equal(1, independent.DispatchCount);

        // Once the first physical operation actually terminates, the queued same-target
        // invocation may proceed. The first caller result remains Unknown and no retry occurred.
        executor.Release();
        var sameTargetResult = await sameTarget.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(RichCommandOutcome.Completed, sameTargetResult.Outcome);
        Assert.Equal(2, executor.DispatchCount);
        Assert.Equal(1, executor.PeakConcurrency);
        Assert.Equal(0, executor.ActiveCount);
    }

    [Fact]
    public async Task ExceptionAfterPhysicalDispatch_IsUnknownAndNeverRetried()
    {
        var executor = new ThrowingExecutor();
        var fixture = CreateFixture(executor: executor);
        using var runtime = fixture.Runtime;

        var result = await runtime.InvokeAsync(fixture.Invocation());

        Assert.Equal(RichCommandOutcome.Unknown, result.Outcome);
        Assert.Equal("execution.exception_ambiguous", result.Code);
        Assert.Equal(1, executor.DispatchCount);
    }

    [Fact]
    public async Task InvalidExecutorResult_AfterDispatch_IsUnknownAndNeverRetried()
    {
        var executor = new InvalidResultExecutor();
        var fixture = CreateFixture(executor: executor);
        using var runtime = fixture.Runtime;

        var result = await runtime.InvokeAsync(fixture.Invocation());

        Assert.Equal(RichCommandOutcome.Unknown, result.Outcome);
        Assert.Equal("executor.invalid_result", result.Code);
        Assert.Equal(1, executor.DispatchCount);
    }

    [Fact]
    public async Task NonAuthoritativeRuntime_RejectsWithZeroPhysicalDispatch()
    {
        var fixture = CreateFixture(authoritative: () => false);
        using var runtime = fixture.Runtime;

        var result = await runtime.InvokeAsync(fixture.Invocation());

        Assert.Equal(RichCommandOutcome.Rejected, result.Outcome);
        Assert.Equal("runtime.non_authoritative", result.Code);
        Assert.Equal(0, fixture.Executor.DispatchCount);
    }

    [Fact]
    public async Task AuthorityLostWhileWaitingForPhysicalSlot_RejectsBeforeDispatch()
    {
        var authority = 1;
        var blocker = new MultiTargetBlockingExecutor();
        var secondDataSourceId = Guid.Parse("55600000-0000-0000-0000-000000000099");
        var fixture = CreateFixture(
            executor: blocker,
            authoritative: () => Volatile.Read(ref authority) == 1,
            additionalBinding: CreateBinding(Command2Id, secondDataSourceId, "independent-device"),
            additionalDefinition: CreateDefinition(Command2Id),
            additionalExecutorDataSourceId: secondDataSourceId,
            policy: new RichCommandRuntimePolicy(3, 1, TimeSpan.FromSeconds(5)));
        using var runtime = fixture.Runtime;

        var first = runtime.InvokeAsync(fixture.Invocation()).AsTask();
        await blocker.FirstDispatchStarted.WaitAsync(TimeSpan.FromSeconds(2));

        var second = runtime.InvokeAsync(fixture.Invocation(commandId: Command2Id)).AsTask();
        Volatile.Write(ref authority, 0);
        blocker.Release();

        var secondResult = await second;
        await first;

        Assert.Equal(RichCommandOutcome.Rejected, secondResult.Outcome);
        Assert.Equal("runtime.non_authoritative", secondResult.Code);
        Assert.Equal(1, blocker.DispatchCount);
    }

    [Fact]
    public async Task RuntimeCore_HasNoTagStateDependencyAndSynthesizesNoTag()
    {
        var fixture = CreateFixture();
        using var runtime = fixture.Runtime;

        var result = await runtime.InvokeAsync(fixture.Invocation());

        Assert.Equal(RichCommandOutcome.Completed, result.Outcome);
        Assert.DoesNotContain(
            typeof(RichCommandRuntimeService).GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public),
            field => typeof(ICurrentTagCache).IsAssignableFrom(field.FieldType) ||
                     typeof(ITagRegistry).IsAssignableFrom(field.FieldType));
        Assert.DoesNotContain(
            typeof(RichCommandRuntimeService).GetMethods(),
            method => method.Name.Contains("Write", StringComparison.OrdinalIgnoreCase) ||
                      method.Name.Contains("Tag", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LegacyWriteTagValueContract_RemainsUnchangedAndIndependent()
    {
        var legacy = new CommandDefinition(
            Guid.NewGuid(),
            "plant.p01.start",
            "Start P01",
            CommandKind.WriteTagValue,
            Guid.NewGuid(),
            "Plant.P01.Run",
            true);

        Assert.Equal(CommandKind.WriteTagValue, legacy.Kind);
        Assert.False(typeof(CommandDefinition).IsAssignableFrom(typeof(RichCommandDefinition)));
        Assert.False(typeof(RichCommandDefinition).IsAssignableFrom(typeof(CommandDefinition)));
        Assert.Single(typeof(IEngineeringRuntimeCoordinator).GetMethods()
            .Where(method => method.Name == nameof(IEngineeringRuntimeCoordinator.ExecuteCommandAsync)));
        var parameters = typeof(IEngineeringRuntimeCoordinator)
            .GetMethod(nameof(IEngineeringRuntimeCoordinator.ExecuteCommandAsync))!
            .GetParameters();
        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
    }

    private static readonly Guid CommandId =
        Guid.Parse("55600000-0000-0000-0000-000000000010");
    private static readonly Guid Command2Id =
        Guid.Parse("55600000-0000-0000-0000-000000000011");
    private static readonly Guid DataSourceId =
        Guid.Parse("55600000-0000-0000-0000-000000000020");

    private static RuntimeFixture CreateFixture(
        IRichCommandDriverExecutor? executor = null,
        Func<bool>? authoritative = null,
        RichCommandRuntimePolicy? policy = null,
        bool includeBinding = true,
        bool includeExecutor = true,
        IRichCommandBindingResolver? bindingResolverOverride = null,
        RichCommandDefinition? additionalDefinition = null,
        DriverCommandBinding? additionalBinding = null,
        Guid? additionalExecutorDataSourceId = null,
        IRichCommandDriverExecutor? additionalExecutor = null)
    {
        var definition = CreateDefinition(CommandId);
        var definitions = additionalDefinition is null
            ? new[] { definition }
            : new[] { definition, additionalDefinition };

        var binding = CreateBinding(CommandId, DataSourceId, "cover-device-01");
        var bindings = new List<DriverCommandBinding>();
        if (includeBinding)
            bindings.Add(binding);
        if (additionalBinding is not null)
            bindings.Add(additionalBinding);

        var recording = executor as RecordingExecutor ?? new RecordingExecutor(RichCommandOutcome.Completed);
        var selectedExecutor = executor ?? recording;
        var executors = new List<KeyValuePair<Guid, IRichCommandDriverExecutor>>();
        if (includeExecutor)
            executors.Add(new(DataSourceId, selectedExecutor));
        if (includeExecutor && additionalExecutorDataSourceId.HasValue)
            executors.Add(new(
                additionalExecutorDataSourceId.Value,
                additionalExecutor ?? selectedExecutor));

        var runtime = new RichCommandRuntimeService(
            new InMemoryRichCommandDefinitionResolver(definitions),
            bindingResolverOverride ?? new InMemoryRichCommandBindingResolver(bindings),
            new InMemoryRichCommandDriverExecutorResolver(executors),
            authoritative ?? (() => true),
            policy);

        return new RuntimeFixture(
            runtime,
            definition,
            binding,
            selectedExecutor as RecordingExecutor ?? new RecordingExecutorProxy(selectedExecutor));
    }

    private static RichCommandDefinition CreateDefinition(Guid commandId) =>
        new(
            commandId,
            "cover.move",
            [
                new RichCommandParameterDefinition(
                    "position",
                    new InteractionScalarSchema(
                        InteractionScalarKind.Percentage,
                        Minimum: 0,
                        Maximum: 100),
                    Required: true)
            ]);

    private static DriverCommandBinding CreateBinding(
        Guid commandId,
        Guid dataSourceId,
        string stableDeviceIdentity) =>
        new(
            commandId,
            dataSourceId,
            stableDeviceIdentity,
            "cover.move",
            Settings:
            [
                new DriverCommandBindingSetting("transition.profile", "normal")
            ]);

    private sealed record RuntimeFixture(
        RichCommandRuntimeService Runtime,
        RichCommandDefinition Definition,
        DriverCommandBinding Binding,
        RecordingExecutor Executor)
    {
        public RichCommandInvocation Invocation(
            Guid? invocationId = null,
            Guid? commandId = null,
            IReadOnlyList<RichCommandParameterValue>? parameters = null) =>
            new(
                invocationId ?? Guid.NewGuid(),
                commandId ?? Definition.CommandId,
                parameters ??
                [
                    new RichCommandParameterValue(
                        "position",
                        InteractionScalarValue.Percentage(50m))
                ],
                DateTimeOffset.UtcNow);
    }

    private class RecordingExecutor : IRichCommandDriverExecutor
    {
        private readonly RichCommandOutcome _outcome;
        private int _dispatchCount;

        public RecordingExecutor(RichCommandOutcome outcome) => _outcome = outcome;

        public int DispatchCount => Volatile.Read(ref _dispatchCount);
        public RichCommandInvocation? LastInvocation { get; private set; }
        public DriverCommandBinding? LastBinding { get; private set; }

        public virtual ValueTask<RichCommandResult> ExecuteAsync(
            RichCommandInvocation invocation,
            DriverCommandBinding binding,
            RichCommandExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _dispatchCount);
            LastInvocation = invocation;
            LastBinding = binding;
            return ValueTask.FromResult(new RichCommandResult(
                invocation.InvocationId,
                invocation.CommandId,
                _outcome,
                DateTimeOffset.UtcNow));
        }

        protected void RecordDispatch() => Interlocked.Increment(ref _dispatchCount);
    }

    private sealed class RecordingExecutorProxy : RecordingExecutor
    {
        private readonly IRichCommandDriverExecutor _inner;

        public RecordingExecutorProxy(IRichCommandDriverExecutor inner)
            : base(RichCommandOutcome.Completed)
        {
            _inner = inner;
        }

        public override async ValueTask<RichCommandResult> ExecuteAsync(
            RichCommandInvocation invocation,
            DriverCommandBinding binding,
            RichCommandExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            RecordDispatch();
            return await _inner.ExecuteAsync(invocation, binding, context, cancellationToken);
        }
    }

    private sealed class BlockingExecutor : IRichCommandDriverExecutor
    {
        private readonly TaskCompletionSource<bool> _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _dispatchCount;

        public Task FirstDispatchStarted => _started.Task;
        public int DispatchCount => Volatile.Read(ref _dispatchCount);
        public void Release() => _release.TrySetResult(true);

        public async ValueTask<RichCommandResult> ExecuteAsync(
            RichCommandInvocation invocation,
            DriverCommandBinding binding,
            RichCommandExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _dispatchCount);
            _started.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken);
            return new RichCommandResult(
                invocation.InvocationId,
                invocation.CommandId,
                RichCommandOutcome.Completed,
                DateTimeOffset.UtcNow);
        }
    }

    private sealed class OrderedBlockingExecutor : IRichCommandDriverExecutor
    {
        private readonly object _gate = new();
        private readonly List<Guid> _order = new();
        private readonly TaskCompletionSource<bool> _firstStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseFirst =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _dispatchCount;

        public Task FirstDispatchStarted => _firstStarted.Task;

        public IReadOnlyList<Guid> DispatchOrder
        {
            get
            {
                lock (_gate)
                    return _order.ToArray();
            }
        }

        public void ReleaseFirst() => _releaseFirst.TrySetResult(true);

        public async ValueTask<RichCommandResult> ExecuteAsync(
            RichCommandInvocation invocation,
            DriverCommandBinding binding,
            RichCommandExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var position = Interlocked.Increment(ref _dispatchCount);
            lock (_gate)
                _order.Add(invocation.InvocationId);

            if (position == 1)
            {
                _firstStarted.TrySetResult(true);
                await _releaseFirst.Task.WaitAsync(cancellationToken);
            }

            return new RichCommandResult(
                invocation.InvocationId,
                invocation.CommandId,
                RichCommandOutcome.Completed,
                DateTimeOffset.UtcNow);
        }
    }

    private sealed class ConcurrentProbeExecutor(int expectedConcurrent) : IRichCommandDriverExecutor
    {
        private readonly TaskCompletionSource<bool> _reached =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _active;
        private int _peak;

        public Task ConcurrencyReached => _reached.Task;
        public int PeakConcurrency => Volatile.Read(ref _peak);
        public void Release() => _release.TrySetResult(true);

        public async ValueTask<RichCommandResult> ExecuteAsync(
            RichCommandInvocation invocation,
            DriverCommandBinding binding,
            RichCommandExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var active = Interlocked.Increment(ref _active);
            UpdatePeak(active);
            if (active >= expectedConcurrent)
                _reached.TrySetResult(true);

            try
            {
                await _release.Task.WaitAsync(cancellationToken);
                return new RichCommandResult(
                    invocation.InvocationId,
                    invocation.CommandId,
                    RichCommandOutcome.Completed,
                    DateTimeOffset.UtcNow);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        private void UpdatePeak(int candidate)
        {
            while (true)
            {
                var current = Volatile.Read(ref _peak);
                if (candidate <= current || Interlocked.CompareExchange(ref _peak, candidate, current) == current)
                    return;
            }
        }
    }

    private sealed class CancellationAwareBlockingExecutor : IRichCommandDriverExecutor
    {
        private readonly TaskCompletionSource<bool> _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _dispatchCount;

        public Task DispatchStarted => _started.Task;
        public int DispatchCount => Volatile.Read(ref _dispatchCount);

        public async ValueTask<RichCommandResult> ExecuteAsync(
            RichCommandInvocation invocation,
            DriverCommandBinding binding,
            RichCommandExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _dispatchCount);
            _started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }

    private sealed class NonCooperativeBlockingExecutor : IRichCommandDriverExecutor
    {
        private readonly TaskCompletionSource<bool> _firstStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _dispatchCount;
        private int _activeCount;
        private int _peakConcurrency;

        public Task FirstDispatchStarted => _firstStarted.Task;
        public int DispatchCount => Volatile.Read(ref _dispatchCount);
        public int ActiveCount => Volatile.Read(ref _activeCount);
        public int PeakConcurrency => Volatile.Read(ref _peakConcurrency);

        public void Release() => _release.TrySetResult(true);

        public async ValueTask<RichCommandResult> ExecuteAsync(
            RichCommandInvocation invocation,
            DriverCommandBinding binding,
            RichCommandExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var dispatch = Interlocked.Increment(ref _dispatchCount);
            var active = Interlocked.Increment(ref _activeCount);
            UpdatePeak(active);
            if (dispatch == 1)
                _firstStarted.TrySetResult(true);

            try
            {
                // Deliberately ignore cancellation to prove the Runtime timeout is caller-bounded
                // without pretending the physical command stopped.
                await _release.Task.ConfigureAwait(false);
                return new RichCommandResult(
                    invocation.InvocationId,
                    invocation.CommandId,
                    RichCommandOutcome.Completed,
                    DateTimeOffset.UtcNow);
            }
            finally
            {
                Interlocked.Decrement(ref _activeCount);
            }
        }

        private void UpdatePeak(int candidate)
        {
            while (true)
            {
                var current = Volatile.Read(ref _peakConcurrency);
                if (candidate <= current ||
                    Interlocked.CompareExchange(ref _peakConcurrency, candidate, current) == current)
                {
                    return;
                }
            }
        }
    }

    private sealed class ThrowingExecutor : IRichCommandDriverExecutor
    {
        public int DispatchCount { get; private set; }

        public ValueTask<RichCommandResult> ExecuteAsync(
            RichCommandInvocation invocation,
            DriverCommandBinding binding,
            RichCommandExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            DispatchCount++;
            throw new IOException("synthetic ambiguous transport failure");
        }
    }

    private sealed class InvalidResultExecutor : IRichCommandDriverExecutor
    {
        public int DispatchCount { get; private set; }

        public ValueTask<RichCommandResult> ExecuteAsync(
            RichCommandInvocation invocation,
            DriverCommandBinding binding,
            RichCommandExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            DispatchCount++;
            return ValueTask.FromResult(new RichCommandResult(
                Guid.NewGuid(),
                invocation.CommandId,
                RichCommandOutcome.Completed,
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class MultiTargetBlockingExecutor : IRichCommandDriverExecutor
    {
        private readonly TaskCompletionSource<bool> _firstStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _dispatchCount;

        public Task FirstDispatchStarted => _firstStarted.Task;
        public int DispatchCount => Volatile.Read(ref _dispatchCount);
        public void Release() => _release.TrySetResult(true);

        public async ValueTask<RichCommandResult> ExecuteAsync(
            RichCommandInvocation invocation,
            DriverCommandBinding binding,
            RichCommandExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var count = Interlocked.Increment(ref _dispatchCount);
            if (count == 1)
            {
                _firstStarted.TrySetResult(true);
                await _release.Task.WaitAsync(cancellationToken);
            }

            return new RichCommandResult(
                invocation.InvocationId,
                invocation.CommandId,
                RichCommandOutcome.Completed,
                DateTimeOffset.UtcNow);
        }
    }

    private sealed class MismatchedBindingResolver : IRichCommandBindingResolver
    {
        public bool TryResolve(Guid commandId, out DriverCommandBinding? binding)
        {
            binding = CreateBinding(Guid.NewGuid(), DataSourceId, "cover-device-01");
            return true;
        }
    }
}
