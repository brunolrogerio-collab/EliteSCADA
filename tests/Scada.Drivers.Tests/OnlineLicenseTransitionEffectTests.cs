using Scada.Api.Licensing;
using Scada.Core.Events;
using Scada.Core.Product.Licensing;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Security.Authorization;
using System.Reflection;

namespace Scada.Drivers.Tests;

public sealed partial class OnlineProductLicenseLifecycleTests
{
    [Fact]
    public async Task OfflineLicense_WriteDoesNotAcquireOnlineAuthorityDependency()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        source.InstallLicense(OfflineCode());
        await using var inner = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var store = NewInterceptedStore(inner, out var proxy);
        var bus = new InMemoryScadaEventBus();
        await using var runtime = new ProductLicensedRuntimeCoordinator(
            new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            () => new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)), source, f.Clock, store);
        var original = Package(1, true);
        Assert.True((await runtime.ActivateAsync("plant", 1,
            original with { Tags = original.Tags.Select(t => t with { ReadOnly = false }).ToArray() })).Activated);
        proxy.ReadOverride = _ => Task.FromException<RuntimeAuthorityState>(new IOException("online authority unavailable"));
        var tag = Assert.Single(runtime.Tags());
        await runtime.WriteAsync(tag.Id, 7d);
        Assert.True(runtime.TryGetCurrent(tag.Id, out var value));
        Assert.Equal(7d, value!.Value);
    }

    [Fact]
    public async Task Reevaluate_AuthorityReadFailure_LeavesTransitionRecoverable()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var inner = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var store = NewInterceptedStore(inner, out var proxy);
        var bus = new InMemoryScadaEventBus();
        await using var runtime = new ProductLicensedRuntimeCoordinator(
            new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            () => new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)), source, f.Clock, store);
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock);
        Assert.True((await lifecycle.InstallOnlineAsync(Candidate())).Succeeded);
        var original = Package(1, true);
        Assert.True((await runtime.ActivateAsync("plant", 1,
            original with { Tags = original.Tags.Select(t => t with { ReadOnly = false }).ToArray() })).Activated);
        f.Clock.Advance(TimeSpan.FromHours(72));
        proxy.ReadOverride = async ct =>
        {
            var state = await inner.GetAuthorityStateAsync(ct);
            if (state.TransitionPending && state.AuthorityRevision >= 3) throw new IOException("injected read failure");
            return state;
        };
        await Assert.ThrowsAnyAsync<Exception>(() => lifecycle.ExpireOnlineAsync(source.ReadOnlineInstallation()!.Revision));
        Assert.True((await inner.GetAuthorityStateAsync()).TransitionPending);
        proxy.ReadOverride = null;
        await lifecycle.ReconcileFailedOnlineAsync();
        Assert.False((await inner.GetAuthorityStateAsync()).TransitionPending);
        await runtime.WriteAsync(Assert.Single(runtime.Tags()).Id, 9d);
    }

    [Fact]
    public async Task Effect_AuthorityUnavailable_FailsClosedWithoutChangingTag()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var innerStore = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var store = NewInterceptedStore(innerStore, out var proxy);
        var bus = new InMemoryScadaEventBus();
        await using var runtime = new ProductLicensedRuntimeCoordinator(
            new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            () => new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)), source, f.Clock, store);
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock);
        Assert.True((await lifecycle.InstallOnlineAsync(Candidate())).Succeeded);
        var original = Package(1, true);
        Assert.True((await runtime.ActivateAsync("plant", 1, original with { Tags = original.Tags.Select(t => t with { ReadOnly = false }).ToArray() })).Activated);
        var tag = Assert.Single(runtime.Tags());
        await runtime.WriteAsync(tag.Id, 7d);
        proxy.ReadOverride = _ => Task.FromException<RuntimeAuthorityState>(new IOException("Authority unavailable"));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await runtime.WriteAsync(tag.Id, 8d));
        Assert.True(runtime.TryGetCurrent(tag.Id, out var value));
        Assert.Equal(7d, value!.Value);
        proxy.ReadOverride = null;
    }

    [Fact]
    public async Task Effect_LicenseChangesDuringAuthorityRead_DoesNotWrite()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var innerStore = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var store = NewInterceptedStore(innerStore, out var proxy);
        var bus = new InMemoryScadaEventBus();
        await using var runtime = new ProductLicensedRuntimeCoordinator(
            new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            () => new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)), source, f.Clock, store);
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock);
        Assert.True((await lifecycle.InstallOnlineAsync(Candidate())).Succeeded);
        var original = Package(1, true);
        Assert.True((await runtime.ActivateAsync("plant", 1, original with { Tags = original.Tags.Select(t => t with { ReadOnly = false }).ToArray() })).Activated);
        var tag = Assert.Single(runtime.Tags());
        await runtime.WriteAsync(tag.Id, 7d);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        proxy.ReadOverride = async ct => { entered.TrySetResult(); await release.Task.WaitAsync(ct); return await innerStore.GetAuthorityStateAsync(ct); };
        var writing = runtime.WriteAsync(tag.Id, 8d).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // Simulate the actual selected installation changing while the Authority read is blocked.
            // The normal lifecycle also records an Authority transition; this test isolates the double-read race.
            source.RemoveLicense();
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => writing);
        Assert.True(runtime.TryGetCurrent(tag.Id, out var value));
        Assert.Equal(7d, value!.Value);
        proxy.ReadOverride = null;
    }

    [Fact]
    public async Task Effect_SecondNodeStaleAuthority_DeniesUntilItsOwnRuntimeIsReevaluated()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var bus = new InMemoryScadaEventBus();
        ProductLicensedRuntimeCoordinator CreateNode() => new(
            new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            () => new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)), source, f.Clock, store);
        await using var first = CreateNode();
        await using var second = CreateNode();
        var lifecycle = CreateLifecycle(source, store, first, f.Clock);
        Assert.True((await lifecycle.InstallOnlineAsync(Candidate())).Succeeded);
        var original = Package(1, true);
        var package = original with { Tags = original.Tags.Select(t => t with { ReadOnly = false }).ToArray() };
        Assert.True((await first.ActivateAsync("plant", 1, package)).Activated);
        Assert.True((await second.ActivateAsync("plant", 1, package)).Activated);
        var tag = Assert.Single(second.Tags());
        await second.WriteAsync(tag.Id, 7d);
        f.Clock.Advance(TimeSpan.FromHours(72));
        Assert.Equal("expired-to-demo", (await Guard(source, lifecycle, first, f.Clock).RunOnceAsync()).Code);
        Assert.Equal(LicenseState.Demo, source.CurrentVerification.State);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await second.WriteAsync(tag.Id, 8d));
        Assert.True(second.TryGetCurrent(tag.Id, out var old));
        Assert.Equal(7d, old!.Value);
        Assert.True((await second.ReevaluateForAuthorityChangeAsync(f.Clock.GetUtcNow())).RuntimeRetained);
        await second.WriteAsync(tag.Id, 9d);
        Assert.True(second.TryGetCurrent(tag.Id, out var current));
        Assert.Equal(9d, current!.Value);
    }

    private static IRuntimeSessionLeaseStore NewInterceptedStore(IRuntimeSessionLeaseStore inner, out AuthorityStoreProxy proxy)
    {
        var result = DispatchProxy.Create<IRuntimeSessionLeaseStore, AuthorityStoreProxy>();
        proxy = (AuthorityStoreProxy)result;
        proxy.Inner = inner;
        return result;
    }

    public class AuthorityStoreProxy : DispatchProxy
    {
        internal IRuntimeSessionLeaseStore Inner { get; set; } = null!;
        internal Func<CancellationToken, Task<RuntimeAuthorityState>>? ReadOverride { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name is nameof(IRuntimeSessionLeaseStore.GetAuthorityStateAsync) or nameof(IRuntimeSessionLeaseStore.ReadAuthoritySnapshotAsync) && ReadOverride is { } read)
                return read((CancellationToken)args![0]!);
            return method!.Invoke(Inner, args);
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(201, false)]
    public async Task OnlineExpiry_PendingAuthorityCannotWriteUntilDemoRunIsReevaluated(int count, bool demoEligible)
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var bus = new InMemoryScadaEventBus();
        await using var runtime = new ProductLicensedRuntimeCoordinator(
            new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            () => new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            source, f.Clock, store);
        var pausedRuntime = new PausedTransitionReevaluator(runtime);
        var lifecycle = CreateLifecycle(source, store, pausedRuntime, f.Clock);
        Assert.True((await lifecycle.InstallOnlineAsync(Candidate())).Succeeded);
        var original = Package(count, true);
        var package = original with { Tags = original.Tags.Select(t => t with { ReadOnly = false }).ToArray() };
        Assert.True((await runtime.ActivateAsync("plant", 1, package)).Activated);
        var tag = runtime.Tags().First();
        await runtime.WriteAsync(tag.Id, 7d);

        f.Clock.Advance(TimeSpan.FromHours(72));
        var revision = source.ReadOnlineInstallation()!.Revision;
        pausedRuntime.Pause = true;
        var expiration = lifecycle.ExpireOnlineAsync(revision);
        try
        {
            await pausedRuntime.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True((await store.GetAuthorityStateAsync()).TransitionPending);
            Assert.True(source.ReadOnlineInstallation()!.ExpiryApplied);
            Assert.Equal(LicenseState.Demo, source.CurrentVerification.State);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await runtime.WriteAsync(tag.Id, 8d));
            Assert.True(runtime.TryGetCurrent(tag.Id, out var preserved));
            Assert.Equal(7d, preserved!.Value);
        }
        finally
        {
            pausedRuntime.Release.TrySetResult();
            await expiration;
        }

        Assert.True((await expiration).Succeeded);
        Assert.False((await store.GetAuthorityStateAsync()).TransitionPending);
        Assert.Equal(LicenseState.Demo, source.CurrentVerification.State);
        if (demoEligible)
        {
            await runtime.WriteAsync(tag.Id, 9d);
            Assert.True(runtime.TryGetCurrent(tag.Id, out var current));
            Assert.Equal(9d, current!.Value);
        }
        else
        {
            Assert.Null(runtime.Describe().Revision);
            Assert.False(runtime.TryGetTag(tag.Id, out _));
        }
    }

    private sealed class PausedTransitionReevaluator(IProductRuntimeAuthorityReevaluator inner)
        : IProductRuntimeAuthorityReevaluator
    {
        internal bool Pause { get; set; }
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<ProductRuntimeAuthorityReevaluationResult> ReevaluateForAuthorityChangeAsync(
            DateTimeOffset changedAtUtc, CancellationToken cancellationToken = default)
        {
            if (Pause)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return await inner.ReevaluateForAuthorityChangeAsync(changedAtUtc, cancellationToken);
        }
    }
}
