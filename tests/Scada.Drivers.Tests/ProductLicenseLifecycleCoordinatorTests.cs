using Scada.Api.Licensing;
using Scada.Core.Events;
using Scada.Core.Product.Licensing;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class ProductLicenseLifecycleCoordinatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-22T12:00:00Z");

    [Fact]
    public async Task InvalidCandidate_DoesNotBeginTransitionOrFenceExistingLease()
    {
        var licensing = new TestLicensing { CandidateValid = false };
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => Now);
        await using var runtime = CreateRuntime(licensing);
        var lease = await AdmitAsync(store);
        var lifecycle = new ProductLicenseLifecycleCoordinator(licensing, store, runtime, new FixedTimeProvider());

        var result = await lifecycle.InstallOrReplaceAsync("invalid");

        Assert.False(result.Succeeded);
        Assert.Equal("invalid-candidate", result.ReasonCode);
        Assert.Equal(0, licensing.InstallCalls);
        Assert.Equal(1, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        Assert.False((await store.GetAuthorityStateAsync()).TransitionPending);
        Assert.True((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client-a")).IsValid);
    }

    [Fact]
    public async Task ValidInstall_CommitsRevisionAndFencesOldLeaseBeforeReturning()
    {
        var licensing = new TestLicensing();
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => Now);
        await using var runtime = CreateRuntime(licensing);
        var lease = await AdmitAsync(store);
        var lifecycle = new ProductLicenseLifecycleCoordinator(licensing, store, runtime, new FixedTimeProvider());

        var result = await lifecycle.InstallOrReplaceAsync("valid");

        Assert.True(result.Succeeded);
        Assert.Equal(LicenseState.Valid, licensing.CurrentVerification.State);
        Assert.Equal(1, result.FencedLeaseCount);
        Assert.Equal("not-active", result.LocalRuntimeOutcome);
        var state = await store.GetAuthorityStateAsync();
        Assert.Equal(2, state.AuthorityRevision);
        Assert.False(state.TransitionPending);
        Assert.False((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client-a")).IsValid);
    }

    [Fact]
    public async Task FailedFileMutation_AbortsWithoutChangingRevisionOrLease()
    {
        var licensing = new TestLicensing { FailInstall = true };
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => Now);
        await using var runtime = CreateRuntime(licensing);
        var lease = await AdmitAsync(store);
        var lifecycle = new ProductLicenseLifecycleCoordinator(licensing, store, runtime, new FixedTimeProvider());

        var result = await lifecycle.InstallOrReplaceAsync("valid");

        Assert.False(result.Succeeded);
        Assert.Equal("file-mutation-failed", result.ReasonCode);
        Assert.Equal(1, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        Assert.False((await store.GetAuthorityStateAsync()).TransitionPending);
        Assert.True((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client-a")).IsValid);
    }

    [Fact]
    public async Task ValidReplacement_ChangesAuthorityAndFencesLease()
    {
        var licensing = new TestLicensing { Installed = LicenseState.Valid };
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => Now);
        var runtime = new TestRuntimeReevaluator();
        var lease = await AdmitAsync(store);
        var lifecycle = new ProductLicenseLifecycleCoordinator(licensing, store, runtime, new FixedTimeProvider());

        var result = await lifecycle.InstallOrReplaceAsync("valid-b");

        Assert.True(result.Succeeded);
        Assert.Equal(LicenseState.Valid, result.PreviousLicenseState);
        Assert.Equal(LicenseState.Valid, result.CurrentLicenseState);
        Assert.Equal(2, result.CurrentAuthorityRevision);
        Assert.Equal(1, runtime.Calls);
        Assert.False((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client-a")).IsValid);
    }

    [Fact]
    public async Task InvalidReplacement_PreservesCurrentAuthorityAndLease()
    {
        var licensing = new TestLicensing { Installed = LicenseState.Valid, CandidateValid = false };
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => Now);
        var runtime = new TestRuntimeReevaluator();
        var lease = await AdmitAsync(store);
        var lifecycle = new ProductLicenseLifecycleCoordinator(licensing, store, runtime, new FixedTimeProvider());

        var result = await lifecycle.InstallOrReplaceAsync("invalid-b");

        Assert.False(result.Succeeded);
        Assert.Equal(LicenseState.Valid, licensing.CurrentVerification.State);
        Assert.Equal(0, licensing.InstallCalls);
        Assert.Equal(0, runtime.Calls);
        Assert.Equal(1, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        Assert.True((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client-a")).IsValid);
    }

    [Fact]
    public async Task Remove_UsesAuthorityChangeTimeAsDemoAnchor()
    {
        var licensing = new TestLicensing { Installed = LicenseState.Valid };
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => Now);
        await using var runtime = CreateRuntime(licensing);
        var lifecycle = new ProductLicenseLifecycleCoordinator(licensing, store, runtime, new FixedTimeProvider());

        var result = await lifecycle.RemoveAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(LicenseState.Demo, licensing.CurrentVerification.State);
        Assert.Equal(Now, result.AuthorityChangedAtUtc);
        var state = await store.GetAuthorityStateAsync();
        Assert.Equal(Now, state.DemoStartedAtUtc);
        Assert.False(state.TransitionPending);
    }

    [Fact]
    public async Task Reconciliation_BeforeFileCommit_ConservativelyBumpsAndFencesOnce()
    {
        var licensing = new TestLicensing();
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => Now);
        await using var runtime = CreateRuntime(licensing);
        var lease = await AdmitAsync(store);
        await store.BeginAuthorityTransitionAsync("remove", Now);
        var lifecycle = new ProductLicenseLifecycleCoordinator(licensing, store, runtime, new FixedTimeProvider());

        await lifecycle.ReconcilePendingAsync();
        await lifecycle.ReconcilePendingAsync();

        var state = await store.GetAuthorityStateAsync();
        Assert.Equal(2, state.AuthorityRevision);
        Assert.Equal(Now, state.AuthorityChangedAtUtc);
        Assert.Equal(Now, state.DemoStartedAtUtc);
        Assert.False(state.TransitionPending);
        Assert.False((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client-a")).IsValid);
    }

    [Fact]
    public async Task Reconciliation_AfterRevisionCommit_CompletesWithoutSecondBump()
    {
        var licensing = new TestLicensing { Installed = LicenseState.Valid };
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => Now);
        await using var runtime = CreateRuntime(licensing);
        var lease = await AdmitAsync(store);
        var transition = await store.BeginAuthorityTransitionAsync("replace", Now);
        await store.CommitAuthorityChangeAsync(transition.TransitionId, transition.BaseAuthorityRevision,
            Now.AddMinutes(1), null);
        var lifecycle = new ProductLicenseLifecycleCoordinator(licensing, store, runtime, new FixedTimeProvider());

        await lifecycle.ReconcilePendingAsync();

        var state = await store.GetAuthorityStateAsync();
        Assert.Equal(2, state.AuthorityRevision);
        Assert.Equal(Now.AddMinutes(1), state.AuthorityChangedAtUtc);
        Assert.False(state.TransitionPending);
        Assert.False((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client-a")).IsValid);
    }

    [Fact]
    public async Task Reconciliation_SecondTransitionBeforeFileCommit_MustNotMistakePriorTimestampForCurrentCommit()
    {
        var licensing = new TestLicensing { Installed = LicenseState.Valid };
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => Now);
        await using var runtime = CreateRuntime(licensing);

        var first = await store.BeginAuthorityTransitionAsync("install", Now);
        await store.CommitAuthorityChangeAsync(first.TransitionId, first.BaseAuthorityRevision, Now, null);
        await store.FenceLeasesBeforeAuthorityRevisionAsync(first.TransitionId, 2);
        await store.CompleteAuthorityTransitionAsync(first.TransitionId, 2);

        var lease = await AdmitAsync(store);
        await store.BeginAuthorityTransitionAsync("replace", Now);
        var lifecycle = new ProductLicenseLifecycleCoordinator(licensing, store, runtime, new FixedTimeProvider());

        await lifecycle.ReconcilePendingAsync();

        var state = await store.GetAuthorityStateAsync();
        Assert.Equal(3, state.AuthorityRevision);
        Assert.False(state.TransitionPending);
        Assert.False((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client-a")).IsValid);
    }

    [Fact]
    public async Task Reconciliation_MissingTransitionBase_FailsClosedWithoutClearingPending()
    {
        var licensing = new TestLicensing();
        await using var inner = new InMemoryRuntimeSessionLeaseStore(() => Now);
        var transition = await inner.BeginAuthorityTransitionAsync("replace", Now);
        await using var store = new FaultingLeaseStore(inner)
        {
            AuthorityStateOverride = (await inner.GetAuthorityStateAsync()) with
            {
                TransitionBaseAuthorityRevision = null
            }
        };
        var runtime = new TestRuntimeReevaluator();
        var lifecycle = new ProductLicenseLifecycleCoordinator(licensing, store, runtime, new FixedTimeProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.ReconcilePendingAsync());

        var state = await inner.GetAuthorityStateAsync();
        Assert.True(state.TransitionPending);
        Assert.Equal(transition.TransitionId, state.TransitionId);
        Assert.Equal(0, runtime.Calls);
        Assert.True(await inner.AbortAuthorityTransitionAsync(
            transition.TransitionId, transition.BaseAuthorityRevision));
    }

    [Fact]
    public async Task Lifecycle_RuntimeReevaluationFailure_LeavesPendingThenReconciliationFinishes()
    {
        var licensing = new TestLicensing();
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => Now);
        var lease = await AdmitAsync(store);
        var failingRuntime = new TestRuntimeReevaluator { Failure = new InvalidOperationException("fixture") };
        var lifecycle = new ProductLicenseLifecycleCoordinator(licensing, store, failingRuntime, new FixedTimeProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.InstallOrReplaceAsync("valid"));

        var pending = await store.GetAuthorityStateAsync();
        Assert.True(pending.TransitionPending);
        Assert.Equal(2, pending.AuthorityRevision);
        Assert.Equal(1, pending.TransitionBaseAuthorityRevision);

        var recoveredRuntime = new TestRuntimeReevaluator();
        await new ProductLicenseLifecycleCoordinator(licensing, store, recoveredRuntime, new FixedTimeProvider())
            .ReconcilePendingAsync();
        Assert.False((await store.GetAuthorityStateAsync()).TransitionPending);
        Assert.False((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client-a")).IsValid);
        Assert.Equal(1, recoveredRuntime.Calls);
    }

    [Fact]
    public async Task Lifecycle_FenceFailure_LeavesPendingAndRetryIsIdempotent()
    {
        var licensing = new TestLicensing();
        await using var inner = new InMemoryRuntimeSessionLeaseStore(() => Now);
        var lease = await AdmitAsync(inner);
        await using var failingStore = new FaultingLeaseStore(inner) { FailFenceOnce = true };
        var runtime = new TestRuntimeReevaluator();
        var lifecycle = new ProductLicenseLifecycleCoordinator(licensing, failingStore, runtime, new FixedTimeProvider());

        await Assert.ThrowsAsync<IOException>(() => lifecycle.InstallOrReplaceAsync("valid"));

        var pending = await inner.GetAuthorityStateAsync();
        Assert.True(pending.TransitionPending);
        Assert.Equal(2, pending.AuthorityRevision);

        await new ProductLicenseLifecycleCoordinator(licensing, inner, runtime, new FixedTimeProvider())
            .ReconcilePendingAsync();
        await new ProductLicenseLifecycleCoordinator(licensing, inner, runtime, new FixedTimeProvider())
            .ReconcilePendingAsync();
        Assert.False((await inner.GetAuthorityStateAsync()).TransitionPending);
        Assert.False((await inner.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client-a")).IsValid);
    }

    [Fact]
    public async Task Reconciliation_AfterFenceBeforeComplete_CompletesWithoutRefencing()
    {
        var licensing = new TestLicensing { Installed = LicenseState.Valid };
        await using var store = new InMemoryRuntimeSessionLeaseStore(() => Now);
        var lease = await AdmitAsync(store);
        var transition = await store.BeginAuthorityTransitionAsync("replace", Now);
        await store.CommitAuthorityChangeAsync(transition.TransitionId, transition.BaseAuthorityRevision, Now, null);
        Assert.Equal(1, await store.FenceLeasesBeforeAuthorityRevisionAsync(transition.TransitionId, 2));

        var runtime = new TestRuntimeReevaluator();
        await new ProductLicenseLifecycleCoordinator(licensing, store, runtime, new FixedTimeProvider())
            .ReconcilePendingAsync();

        Assert.False((await store.GetAuthorityStateAsync()).TransitionPending);
        Assert.False((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client-a")).IsValid);
        Assert.Equal(1, runtime.Calls);
    }

    private static ProductLicensedRuntimeCoordinator CreateRuntime(TestLicensing licensing)
    {
        var bus = new InMemoryScadaEventBus();
        return new ProductLicensedRuntimeCoordinator(
            new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            () => new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            licensing,
            new FixedTimeProvider());
    }

    private static Task<RuntimeSessionLeaseState> AdmitAsync(IRuntimeSessionLeaseStore store) =>
        store.AdmitAsync(new RuntimeSessionLeaseAdmission(
            "operator", "client-a", "interactive",
            new RuntimeSessionRuntimeIdentity("engineering", "plant", 1, Now),
            TimeSpan.FromMinutes(5)));

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class TestLicensing : IProductLicenseService
    {
        public bool CandidateValid { get; set; } = true;
        public bool FailInstall { get; set; }
        public int InstallCalls { get; private set; }
        public LicenseState Installed { get; set; } = LicenseState.Demo;
        public string MachineFingerprint => "test-machine";
        public string MachineRequestCode => "test-request";
        public LicenseVerificationResult CurrentVerification => new(Installed);
        public LicenseVerificationResult VerifyCandidate(string licenseCode) =>
            CandidateValid ? new(LicenseState.Valid) : LicenseVerificationResult.Invalid("invalid");
        public RunEntitlementDecision EvaluateRun(int projectTagCount) =>
            ProductEntitlementEvaluator.Evaluate(CurrentVerification, projectTagCount);
        public void InstallLicense(string licenseCode)
        {
            InstallCalls++;
            if (FailInstall) throw new IOException("fixture file failure");
            Installed = LicenseState.Valid;
        }
        public void RemoveLicense() => Installed = LicenseState.Demo;
    }

    private sealed class TestRuntimeReevaluator : IProductRuntimeAuthorityReevaluator
    {
        public Exception? Failure { get; init; }
        public int Calls { get; private set; }

        public Task<ProductRuntimeAuthorityReevaluationResult> ReevaluateForAuthorityChangeAsync(
            DateTimeOffset authorityChangedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Failure is not null) return Task.FromException<ProductRuntimeAuthorityReevaluationResult>(Failure);
            return Task.FromResult(new ProductRuntimeAuthorityReevaluationResult(
                false, false, false, null, null, ProductLicensedRuntimeCoordinator.NoActiveRuntimeDiagnostic));
        }
    }

    private sealed class FaultingLeaseStore(IRuntimeSessionLeaseStore inner) : IRuntimeSessionLeaseStore
    {
        public RuntimeAuthorityState? AuthorityStateOverride { get; init; }
        public bool FailFenceOnce { get; set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => inner.InitializeAsync(cancellationToken);
        public async Task<RuntimeAuthorityState> GetAuthorityStateAsync(CancellationToken cancellationToken = default) =>
            AuthorityStateOverride ?? await inner.GetAuthorityStateAsync(cancellationToken);
        public Task<RuntimeAuthorityTransition> BeginAuthorityTransitionAsync(string kind, DateTimeOffset startedAtUtc, CancellationToken cancellationToken = default) =>
            inner.BeginAuthorityTransitionAsync(kind, startedAtUtc, cancellationToken);
        public Task<bool> AbortAuthorityTransitionAsync(Guid transitionId, long expectedBaseAuthorityRevision, CancellationToken cancellationToken = default) =>
            inner.AbortAuthorityTransitionAsync(transitionId, expectedBaseAuthorityRevision, cancellationToken);
        public Task<RuntimeAuthorityState> CommitAuthorityChangeAsync(Guid transitionId, long expectedBaseAuthorityRevision, DateTimeOffset authorityChangedAtUtc, DateTimeOffset? demoStartedAtUtc, CancellationToken cancellationToken = default) =>
            inner.CommitAuthorityChangeAsync(transitionId, expectedBaseAuthorityRevision, authorityChangedAtUtc, demoStartedAtUtc, cancellationToken);
        public Task<int> FenceLeasesBeforeAuthorityRevisionAsync(Guid transitionId, long authorityRevision, CancellationToken cancellationToken = default)
        {
            if (FailFenceOnce)
            {
                FailFenceOnce = false;
                return Task.FromException<int>(new IOException("fixture fence failure"));
            }
            return inner.FenceLeasesBeforeAuthorityRevisionAsync(transitionId, authorityRevision, cancellationToken);
        }
        public Task CompleteAuthorityTransitionAsync(Guid transitionId, long authorityRevision, CancellationToken cancellationToken = default) =>
            inner.CompleteAuthorityTransitionAsync(transitionId, authorityRevision, cancellationToken);
        public Task<RuntimeSessionLeaseState> AdmitAsync(RuntimeSessionLeaseAdmission admission, CancellationToken cancellationToken = default) =>
            inner.AdmitAsync(admission, cancellationToken);
        public Task<RuntimeSessionLeaseCapacityAdmissionResult> AdmitWithCapacityAsync(RuntimeSessionLeaseCapacityAdmission admission, CancellationToken cancellationToken = default) =>
            inner.AdmitWithCapacityAsync(admission, cancellationToken);
        public Task<RuntimeSessionLeaseStoreResult> ValidateAsync(Guid sessionId, string subjectId, RuntimeSessionRuntimeIdentity runtime, string? clientInstanceId = null, CancellationToken cancellationToken = default) =>
            inner.ValidateAsync(sessionId, subjectId, runtime, clientInstanceId, cancellationToken);
        public Task<RuntimeSessionLeaseStoreResult> HeartbeatAsync(Guid sessionId, string subjectId, string clientInstanceId, RuntimeSessionRuntimeIdentity runtime, CancellationToken cancellationToken = default, long? expectedGeneration = null) =>
            inner.HeartbeatAsync(sessionId, subjectId, clientInstanceId, runtime, cancellationToken, expectedGeneration);
        public Task<RuntimeSessionLeaseStoreResult> TerminateAsync(Guid sessionId, string subjectId, string clientInstanceId, RuntimeSessionRuntimeIdentity runtime, CancellationToken cancellationToken = default, long? expectedGeneration = null) =>
            inner.TerminateAsync(sessionId, subjectId, clientInstanceId, runtime, cancellationToken, expectedGeneration);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
