using Scada.Api.Licensing;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed partial class OnlineProductLicenseLifecycleTests
{
    [Fact]
    public async Task OnlineExpiry_QueuedRichCommandDoesNotDispatchAfterDeadline()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var bus = new InMemoryScadaEventBus();
        await using var runtime = new ProductLicensedRuntimeCoordinator(
            new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            () => new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)), source, f.Clock, store);
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock);
        Assert.True((await lifecycle.InstallOnlineAsync(Candidate())).Succeeded);
        Assert.True((await runtime.ActivateAsync("plant", 1, Package(1, true))).Activated);
        var id = Guid.NewGuid();
        var dataSource = Guid.NewGuid();
        var executor = new DeadlineBlockingExecutor();
        var queued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var checks = 0;
        using var commands = RichCommandRuntimeService.CreateWithAsyncAuthority(
            new InMemoryRichCommandDefinitionResolver([new RichCommandDefinition(id, "test.run", [])]),
            new InMemoryRichCommandBindingResolver([new DriverCommandBinding(id, dataSource, "device", "test.run")]),
            new InMemoryRichCommandDriverExecutorResolver([new(dataSource, executor)]),
            async ct =>
            {
                var allowed = await runtime.CanStartLicensedEffectAsync(ct);
                if (Interlocked.Increment(ref checks) == 4) queued.TrySetResult();
                return allowed;
            }, new RichCommandRuntimePolicy(3, 1, TimeSpan.FromSeconds(10)));
        RichCommandInvocation Invocation() => new(Guid.NewGuid(), id, [], f.Clock.GetUtcNow());
        var first = commands.InvokeAsync(Invocation()).AsTask();
        try
        {
            await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var second = commands.InvokeAsync(Invocation()).AsTask();
            await queued.Task.WaitAsync(TimeSpan.FromSeconds(5));
            f.Clock.Advance(TimeSpan.FromHours(72));
            executor.Release.TrySetResult();
            await first;
            Assert.Equal(RichCommandOutcome.Rejected, (await second).Outcome);
            Assert.Equal(1, executor.Count);
            await Guard(source, lifecycle, runtime, f.Clock).RunOnceAsync();
            Assert.Equal(RichCommandOutcome.Completed, (await commands.InvokeAsync(Invocation())).Outcome);
            Assert.Equal(2, executor.Count);
        }
        finally { executor.Release.TrySetResult(); await first; }
    }

    private sealed class DeadlineBlockingExecutor : IRichCommandDriverExecutor
    {
        internal int Count;
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<RichCommandResult> ExecuteAsync(RichCommandInvocation invocation, DriverCommandBinding binding,
            RichCommandExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref Count) == 1)
            {
                Started.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return new(invocation.InvocationId, invocation.CommandId, RichCommandOutcome.Completed, DateTimeOffset.UtcNow);
        }
    }
}
