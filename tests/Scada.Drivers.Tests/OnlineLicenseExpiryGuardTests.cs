using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Scada.Api.Licensing;
using Scada.Api.Security;
using Scada.Core.Events;
using Scada.Core.Product.Licensing;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed partial class OnlineProductLicenseLifecycleTests
{
    private static OnlineLicenseExpiryHostedService Guard(IProductLicenseService source,
        ProductLicenseLifecycleCoordinator lifecycle, IProductRuntimeAuthorityReevaluator runtime, TimeProvider clock) =>
        new(source, lifecycle, runtime, clock, NullLogger<OnlineLicenseExpiryHostedService>.Instance);

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public async Task AutomaticGuard_ExpiresAtSignedDeadline_ReevaluatesRealRunAndAuditsEpoch(int definitions, bool retained)
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var bus = new InMemoryScadaEventBus();
        await using var runtime = new ProductLicensedRuntimeCoordinator(
            new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            () => new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)), source, f.Clock);
        var audit = new InMemoryAuditSink();
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock, audit);
        await lifecycle.InstallOnlineAsync(Candidate());
        Assert.True((await runtime.ActivateAsync("plant", 1, Package(definitions, true))).Activated);
        var guard = Guard(source, lifecycle, runtime, f.Clock);
        Assert.Equal("valid", (await guard.RunOnceAsync()).Code);
        f.Clock.Advance(TimeSpan.FromHours(72) - TimeSpan.FromSeconds(1));
        var oldLease = await Admit(store);
        var beforeDeadline = await guard.RunOnceAsync();
        Assert.Equal(TimeSpan.FromSeconds(1), beforeDeadline.NextCheck);
        Assert.Equal(LicenseState.Valid, source.CurrentVerification.State);
        f.Clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal("expired-to-demo", (await guard.RunOnceAsync()).Code);

        Assert.Equal(LicenseState.Demo, source.CurrentVerification.State);
        Assert.Equal(retained ? definitions : 0, runtime.Describe().TagCount);
        Assert.False((await store.ValidateAsync(oldLease.SessionId, "operator", oldLease.Runtime, "client")).IsValid);
        Assert.Equal(3, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        var events = await audit.QueryAsync(action: "product-license.online-transition");
        var expiry = Assert.Single(events, x => x.Details!["operation"] == "online-expire-to-demo");
        Assert.Equal("2", expiry.Details!["previousEpoch"]);
        Assert.Equal("3", expiry.Details["currentEpoch"]);
        Assert.Equal(Binding.ActivationId.ToString("D"), expiry.Details["activationId"]);
        Assert.Equal("Demo", expiry.Details["licenseState"]);
        Assert.Equal(expiry.TargetId, expiry.CorrelationId);
        Assert.DoesNotContain("ESLEASE1.", JsonSerializer.Serialize(events));
        Assert.Equal("online-demo", (await guard.RunOnceAsync()).Code);
        Assert.Equal(3, (await store.GetAuthorityStateAsync()).AuthorityRevision);
    }

    [Fact]
    public async Task Guard_InactiveAndOfflineSelections_DoNotCreateClockFilesOrChangeAuthority()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var guard = Guard(source, CreateLifecycle(source, store, runtime, f.Clock), runtime, f.Clock);
        Assert.Equal("not-online", (await guard.RunOnceAsync()).Code);
        Assert.False(Directory.Exists(f.Root));
        source.InstallLicense(OfflineCode());
        var contents = File.ReadAllText(f.Path);
        f.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal("not-online", (await guard.RunOnceAsync()).Code);
        Assert.Equal(contents, File.ReadAllText(f.Path));
        Assert.Equal(1, (await store.GetAuthorityStateAsync()).AuthorityRevision);
    }

    [Fact]
    public async Task Guard_CheckpointsAtOneMinuteWithoutEpochChange_AndRestartRollbackFailsClosedOnce()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock);
        await lifecycle.InstallOnlineAsync(Candidate());
        var revision = source.ReadOnlineInstallation()!.Revision;
        var guard = Guard(source, lifecycle, runtime, f.Clock);
        await guard.RunOnceAsync();
        var initial = File.ReadAllText(f.Path);
        f.Clock.Advance(TimeSpan.FromSeconds(59));
        await guard.RunOnceAsync();
        Assert.Equal(initial, File.ReadAllText(f.Path));
        f.Clock.Advance(TimeSpan.FromSeconds(1));
        await guard.RunOnceAsync();
        Assert.NotEqual(initial, File.ReadAllText(f.Path));
        Assert.Equal(revision, source.ReadOnlineInstallation()!.Revision);
        Assert.Equal(2, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        f.Clock.SetWall(Issued);
        using var reopened = f.Source();
        var restarted = Guard(reopened, CreateLifecycle(reopened, store, runtime, f.Clock), runtime, f.Clock);
        Assert.Equal("online-invalid", (await restarted.RunOnceAsync()).Code);
        Assert.Equal(LicenseState.Invalid, reopened.CurrentVerification.State);
        Assert.Equal(3, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        Assert.Equal("online-invalid", (await restarted.RunOnceAsync()).Code);
        Assert.Equal(3, (await store.GetAuthorityStateAsync()).AuthorityRevision);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("descriptor")]
    [InlineData("early-demo")]
    [InlineData("header")]
    public async Task Guard_InvalidOnlineEvidence_FencesOldLeasesWithoutManufacturingDemo(string damage)
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock);
        var candidate = Candidate();
        await lifecycle.InstallOnlineAsync(candidate);
        var guard = Guard(source, lifecycle, runtime, f.Clock);
        Assert.Equal("valid", (await guard.RunOnceAsync()).Code);
        var lease = await Admit(store);
        var text = File.ReadAllText(f.Path);
        File.WriteAllText(f.Path, damage switch
        {
            "signature" => text.Replace(candidate.Envelope, candidate.Envelope[..^8] + "XXXXXXXX"),
            "early-demo" => text.Replace("\"expiryApplied\":false", "\"expiryApplied\":true"),
            "header" => text.Replace("SCADIVO-ONLINE-INSTALLATION1", "SCADIVO-UNRECOGNIZED-INSTALLATION1"),
            _ => "SCADIVO-ONLINE-INSTALLATION1\n{broken"
        });

        Assert.Equal("online-invalid", (await guard.RunOnceAsync()).Code);

        Assert.Equal(LicenseState.Invalid, source.CurrentVerification.State);
        Assert.False(source.EvaluateRun(1).Allowed);
        Assert.False((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client")).IsValid);
        Assert.Equal(3, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        Assert.Equal("online-invalid", (await guard.RunOnceAsync()).Code);
        Assert.Equal(3, (await store.GetAuthorityStateAsync()).AuthorityRevision);
    }

    [Fact]
    public async Task Guard_UsesTheSameWhitespaceNormalizationAsCanonicalVerification()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock);
        await lifecycle.InstallOnlineAsync(Candidate());
        File.WriteAllText(f.Path, " \n" + File.ReadAllText(f.Path) + "\n ");
        Assert.Equal(LicenseState.Valid, source.CurrentVerification.State);
        f.Clock.Advance(TimeSpan.FromHours(72));
        Assert.Equal("expired-to-demo", (await Guard(source, lifecycle, runtime, f.Clock).RunOnceAsync()).Code);
        Assert.Equal(LicenseState.Demo, source.CurrentVerification.State);
    }

    [Fact]
    public async Task Guard_AuditFailureKeepsPending_AutomaticallyRecoversOwnedTransitionWithStableCorrelation()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var audit = new GuardAuditStore();
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock, audit);
        await lifecycle.InstallOnlineAsync(Candidate());
        f.Clock.Advance(TimeSpan.FromHours(72));
        var lease = await Admit(store);
        audit.FailWrites = true;
        var guard = Guard(source, lifecycle, runtime, f.Clock);
        Assert.Equal("maintenance-unavailable", (await guard.RunOnceAsync()).Code);
        var pending = await store.GetAuthorityStateAsync();
        Assert.True(pending.TransitionPending);
        Assert.Equal(LicenseState.Demo, source.CurrentVerification.State);
        Assert.False((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client")).IsValid);
        audit.FailWrites = false;

        Assert.Equal("online-demo", (await guard.RunOnceAsync()).Code);

        var completed = await store.GetAuthorityStateAsync();
        Assert.False(completed.TransitionPending);
        Assert.Equal(pending.AuthorityRevision, completed.AuthorityRevision);
        var recovery = Assert.Single(await audit.Inner.QueryAsync(), x => x.Details!["recovered"] == "true");
        Assert.Equal(pending.TransitionId!.Value.ToString("D"), recovery.CorrelationId);
        Assert.True(audit.Initializations >= 3);
    }

    [Fact]
    public async Task StartupRecovery_AfterAuditResponseLoss_ReplaysWithSameOperationCorrelationAndNoEpochIncrease()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var audit = new GuardAuditStore();
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock, audit);
        await lifecycle.InstallOnlineAsync(Candidate());
        f.Clock.Advance(TimeSpan.FromHours(72));
        audit.FailAfterWrite = true;
        Assert.Equal("maintenance-unavailable", (await Guard(source, lifecycle, runtime, f.Clock).RunOnceAsync()).Code);
        var pending = await store.GetAuthorityStateAsync();
        Assert.True(pending.TransitionPending);
        using var reopened = f.Source();
        audit.FailAfterWrite = false;
        await CreateLifecycle(reopened, store, runtime, f.Clock, audit).ReconcilePendingAsync();
        Assert.False((await store.GetAuthorityStateAsync()).TransitionPending);
        Assert.Equal(pending.AuthorityRevision, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        var correlated = (await audit.Inner.QueryAsync()).Where(x => x.CorrelationId == pending.TransitionId!.Value.ToString("D")).ToArray();
        Assert.Equal(2, correlated.Length);
        Assert.Equal(2, correlated.Select(x => x.Id).Distinct().Count());
        Assert.Single(correlated, x => x.Details!["recovered"] == "true");
    }

    [Fact]
    public async Task Guard_DoesNotRecoverAnOnlineTransitionStillOwnedByAnotherCaller()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock);
        await lifecycle.InstallOnlineAsync(Candidate());
        runtime.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var renewal = lifecycle.RenewOnlineAsync(Candidate(), source.ReadOnlineInstallation()!.Revision);
        await runtime.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var pending = await store.GetAuthorityStateAsync();
            Assert.Equal("transition-in-progress", (await Guard(source, lifecycle, runtime, f.Clock).RunOnceAsync()).Code);
            Assert.Equal(pending, await store.GetAuthorityStateAsync());
            Assert.False(renewal.IsCompleted);
        }
        finally { runtime.Release.TrySetResult(); }
        Assert.True((await renewal).Succeeded);
    }

    [Fact]
    public async Task Guard_InvalidationAuditRecovery_DoesNotFenceTheSameEvidenceTwice()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var audit = new GuardAuditStore();
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock, audit);
        await lifecycle.InstallOnlineAsync(Candidate());
        File.WriteAllText(f.Path, "SCADIVO-ONLINE-INSTALLATION1\n{broken");
        var guard = Guard(source, lifecycle, runtime, f.Clock);
        audit.FailWrites = true;
        Assert.Equal("maintenance-unavailable", (await guard.RunOnceAsync()).Code);
        Assert.Equal(3, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        audit.FailWrites = false;
        Assert.Equal("online-invalid", (await guard.RunOnceAsync()).Code);
        Assert.Equal(3, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        Assert.False((await store.GetAuthorityStateAsync()).TransitionPending);
    }

    [Fact]
    public async Task Guard_UsesNewSelectionAfterRenewal_AndIgnoresAnExplicitOfflineSwitch()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock);
        await lifecycle.InstallOnlineAsync(Candidate());
        var guard = Guard(source, lifecycle, runtime, f.Clock);
        await guard.RunOnceAsync();
        f.Clock.Advance(TimeSpan.FromHours(48));
        await lifecycle.RenewOnlineAsync(Candidate(issued: f.Clock.GetUtcNow()), source.ReadOnlineInstallation()!.Revision);
        f.Clock.Advance(TimeSpan.FromHours(24));
        Assert.Equal("valid", (await guard.RunOnceAsync()).Code);
        Assert.Equal(LicenseState.Valid, source.CurrentVerification.State);
        var offline = EliteScadaLicenseCodec.CreateSignedLicenseV3(new(3, "guard-offline", Binding.MachineFingerprint,
            LicenseTier.Tags500, f.Clock.GetUtcNow(), f.Clock.GetUtcNow().AddDays(1), "ephemeral-offline", 2, 2, false, false), keys.Offline);
        Assert.True((await lifecycle.InstallOrReplaceAsync(offline)).Succeeded);
        Assert.Equal("not-online", (await guard.RunOnceAsync()).Code);
        Assert.Null(source.ReadOnlineInstallation());
        Assert.Equal("guard-offline", source.CurrentVerification.License!.LicenseId);
    }

    [Fact]
    public async Task Guard_AutomaticHostedLoopExpiresOnVirtualTimeAndStopsCleanly()
    {
        using var f = new Fixture(keys);
        var clock = new GuardClock();
        using var source = f.Source(clock: clock);
        await using var store = new InMemoryRuntimeSessionLeaseStore(clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var lifecycle = CreateLifecycle(source, store, runtime, clock);
        await lifecycle.InstallOnlineAsync(Candidate());
        using var guard = Guard(source, lifecycle, runtime, clock);
        await guard.StartAsync(CancellationToken.None);
        await WaitForGuardAsync(() => clock.TimerCount > 0);
        clock.Advance(TimeSpan.FromHours(72));
        await WaitForGuardAsync(() => source.CurrentVerification.State == LicenseState.Demo);
        await guard.StopAsync(CancellationToken.None);
        Assert.False((await store.GetAuthorityStateAsync()).TransitionPending);
        Assert.Equal(3, (await store.GetAuthorityStateAsync()).AuthorityRevision);
    }

    [Fact]
    public async Task OnlineMutationWithoutCanonicalAudit_IsRejectedBeforeChangingEpochOrFile()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var lifecycle = new ProductLicenseLifecycleCoordinator(source, store, new RecordingRuntime(), f.Clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.InstallOnlineAsync(Candidate()));
        Assert.Equal(1, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        Assert.False(File.Exists(f.Path));
    }

    [Fact]
    public async Task Guard_ExpiryCancelsIntelligenceWork_ButUnchangedLicensedRenewalPreservesPermit()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var gate = new AssetProcessIntelligenceExecutionGate();
        var lifecycle = new ProductLicenseLifecycleCoordinator(source, store, runtime, f.Clock, new InMemoryAuditSink(), gate);
        await lifecycle.InstallOnlineAsync(Candidate());
        var equipment = Guid.NewGuid();
        gate.Activate("plant", 1, AssetProcessIntelligenceScopeKind.Equipment, equipment);
        Assert.True(gate.TryGetToken("plant", 1, AssetProcessIntelligenceScopeKind.Equipment, equipment, out var permit));
        f.Clock.Advance(TimeSpan.FromHours(48));
        Assert.True((await lifecycle.RenewOnlineAsync(Candidate(issued: f.Clock.GetUtcNow()), source.ReadOnlineInstallation()!.Revision)).Succeeded);
        Assert.False(permit.IsCancellationRequested);
        Assert.Equal(1, gate.ActivePermitCount);
        f.Clock.Advance(TimeSpan.FromHours(72));
        Assert.Equal("expired-to-demo", (await Guard(source, lifecycle, runtime, f.Clock).RunOnceAsync()).Code);
        Assert.True(permit.IsCancellationRequested);
        Assert.Equal(0, gate.ActivePermitCount);
    }

    [Fact]
    public async Task Guard_CannotBeDelayedByAnOutstandingDeviceHttpOperation()
    {
        using var f = new Fixture(keys);
        var protectionKey = RandomNumberGenerator.GetBytes(32);
        using var protection = new FileHostProtectedMaterialAuthority(new(Path.Combine(f.Root, "material"),
            "FICTITIOUS_TEST_KEY", null), protectionKey, new InMemoryAuditSink(), f.Clock);
        CryptographicOperations.ZeroMemory(protectionKey);
        var journal = new FileOnlineLicenseStateStore(Path.Combine(f.Root, "journal"), Binding.MachineFingerprint,
            new Uri("https://portal.example.test/"), protection, f.Clock,
            (code, binding, now) => OnlineLeaseVerifier.VerifyWithKey(code, binding, now, keys.Online));
        var admin = new ProtectedMaterialMutationContext(new SecurityPrincipal("fictitious-admin", "Test", []),
            new AuthorizationDecision(true, SecurityCapability.EngineeringModify, "test authority", []));
        var prepared = await journal.PrepareActivationAsync(new(Binding.LicenseId, Binding.OrganizationId,
            Binding.ApplicationId, "SCV-FICTITIOUS"), "fictitious-account", admin);
        await journal.CompleteAsync(prepared.PendingRequestId!.Value,
            Candidate(binding: Binding with { ActivationId = prepared.ActivationId }).Envelope, admin, default);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock);
        await lifecycle.InstallOnlineAsync(await journal.ReadEntitlementCandidateAsync());
        f.Clock.Advance(TimeSpan.FromHours(48));
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new OnlineLicenseDeviceClient(journal, new HangingDeviceHandler(entered));
        var sending = client.RenewAsync(admin, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            f.Clock.Advance(TimeSpan.FromHours(24));
            Assert.Equal("expired-to-demo", (await Guard(source, lifecycle, runtime, f.Clock).RunOnceAsync()).Code);
            Assert.False(sending.IsCompleted);
            Assert.Equal(LicenseState.Demo, source.CurrentVerification.State);
        }
        finally { cancellation.Cancel(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);
    }

    private sealed class HangingDeviceHandler(TaskCompletionSource entered) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Guard_SharedAuthorityUnavailable_StopsInvalidRunLocally_ThenRecoversWithoutAutomaticRun()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var inner = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        await using var store = new GuardLeaseStore(inner);
        var bus = new InMemoryScadaEventBus();
        await using var runtime = new ProductLicensedRuntimeCoordinator(
            new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            () => new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)), source, f.Clock);
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock);
        await lifecycle.InstallOnlineAsync(Candidate());
        Assert.True((await runtime.ActivateAsync("plant", 1, Package(200, true))).Activated);
        Assert.Equal(200, runtime.Describe().TagCount);
        var guard = Guard(source, lifecycle, runtime, f.Clock);
        f.Clock.Advance(TimeSpan.FromHours(72));
        store.FailReads = true;

        Assert.Equal("maintenance-unavailable", (await guard.RunOnceAsync()).Code);

        Assert.Equal(0, runtime.Describe().TagCount);
        Assert.Equal(LicenseState.Invalid, source.CurrentVerification.State);
        Assert.Equal(2, (await inner.GetAuthorityStateAsync()).AuthorityRevision);
        store.FailReads = false;
        Assert.Equal("expired-to-demo", (await guard.RunOnceAsync()).Code);
        Assert.Equal(LicenseState.Demo, source.CurrentVerification.State);
        Assert.Equal(0, runtime.Describe().TagCount);
        Assert.Equal(3, (await inner.GetAuthorityStateAsync()).AuthorityRevision);
    }

    private sealed class GuardLeaseStore(IRuntimeSessionLeaseStore inner) : IRuntimeSessionLeaseStore
    {
        internal bool FailReads { get; set; }
        internal bool FailAbort { get; set; }
        public Task InitializeAsync(CancellationToken ct = default) => inner.InitializeAsync(ct);
        public Task<RuntimeAuthorityState> GetAuthorityStateAsync(CancellationToken ct = default) => FailReads
            ? Task.FromException<RuntimeAuthorityState>(new IOException("fictitious shared authority unavailable")) : inner.GetAuthorityStateAsync(ct);
        public Task<RuntimeDemoSessionAnchorResult> EstablishDemoSessionAnchorAsync(DateTimeOffset requested, DateTimeOffset? expected, CancellationToken ct = default) =>
            inner.EstablishDemoSessionAnchorAsync(requested, expected, ct);
        public Task<RuntimeAuthorityTransition> BeginAuthorityTransitionAsync(string kind, DateTimeOffset started, CancellationToken ct = default) =>
            inner.BeginAuthorityTransitionAsync(kind, started, ct);
        public Task<bool> AbortAuthorityTransitionAsync(Guid id, long revision, CancellationToken ct = default) =>
            FailAbort ? Task.FromException<bool>(new IOException("fictitious abort unavailable")) : inner.AbortAuthorityTransitionAsync(id, revision, ct);
        public Task<RuntimeAuthorityState> CommitAuthorityChangeAsync(Guid id, long revision, DateTimeOffset changed, DateTimeOffset? demo, CancellationToken ct = default) =>
            inner.CommitAuthorityChangeAsync(id, revision, changed, demo, ct);
        public Task<int> FenceLeasesBeforeAuthorityRevisionAsync(Guid id, long revision, CancellationToken ct = default) =>
            inner.FenceLeasesBeforeAuthorityRevisionAsync(id, revision, ct);
        public Task CompleteAuthorityTransitionAsync(Guid id, long revision, CancellationToken ct = default) =>
            inner.CompleteAuthorityTransitionAsync(id, revision, ct);
        public Task<RuntimeSessionLeaseState> AdmitAsync(RuntimeSessionLeaseAdmission admission, CancellationToken ct = default) => inner.AdmitAsync(admission, ct);
        public Task<RuntimeSessionLeaseCapacityAdmissionResult> AdmitWithCapacityAsync(RuntimeSessionLeaseCapacityAdmission admission, CancellationToken ct = default) =>
            inner.AdmitWithCapacityAsync(admission, ct);
        public Task<RuntimeSessionLeaseStoreResult> ValidateAsync(Guid id, string subject, RuntimeSessionRuntimeIdentity runtime, string? client = null, CancellationToken ct = default) =>
            inner.ValidateAsync(id, subject, runtime, client, ct);
        public Task<RuntimeSessionLeaseStoreResult> HeartbeatAsync(Guid id, string subject, string client, RuntimeSessionRuntimeIdentity runtime, CancellationToken ct = default, long? expectedGeneration = null) =>
            inner.HeartbeatAsync(id, subject, client, runtime, ct, expectedGeneration);
        public Task<RuntimeSessionLeaseStoreResult> TerminateAsync(Guid id, string subject, string client, RuntimeSessionRuntimeIdentity runtime, CancellationToken ct = default, long? expectedGeneration = null) =>
            inner.TerminateAsync(id, subject, client, runtime, ct, expectedGeneration);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Guard_FailedFileMutationAndUnavailableAbort_RecoversItsPendingOperationAfterStorageReturns()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var inner = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        await using var store = new GuardLeaseStore(inner);
        var runtime = new RecordingRuntime();
        var lifecycle = CreateLifecycle(source, store, runtime, f.Clock);
        await lifecycle.InstallOnlineAsync(Candidate());
        f.Clock.Advance(TimeSpan.FromHours(72));
        var guard = Guard(source, lifecycle, runtime, f.Clock);
        store.FailAbort = true;
        using (var busy = new FileStream(f.Path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal("maintenance-unavailable", (await guard.RunOnceAsync()).Code);
            Assert.True((await inner.GetAuthorityStateAsync()).TransitionPending);
            Assert.Equal(LicenseState.Invalid, source.CurrentVerification.State);
        }
        store.FailAbort = false;
        Assert.Equal("expired-to-demo", (await guard.RunOnceAsync()).Code);
        Assert.False((await inner.GetAuthorityStateAsync()).TransitionPending);
        Assert.Equal(LicenseState.Demo, source.CurrentVerification.State);
        // Recovery conservatively fences the uncertain operation, then authentic expiry
        // applies the canonical Demo marker in its own transition.
        Assert.Equal(4, (await inner.GetAuthorityStateAsync()).AuthorityRevision);
    }

    private static async Task WaitForGuardAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(5, timeout.Token);
    }

    private sealed class GuardClock : TimeProvider
    {
        private readonly object gate = new();
        private readonly List<GuardTimer> timers = [];
        private long ticks;
        public override DateTimeOffset GetUtcNow() { lock (gate) return Issued.AddTicks(ticks); }
        public override long GetTimestamp() { lock (gate) return ticks; }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        internal int TimerCount { get { lock (gate) return timers.Count(x => !x.Disposed); } }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new GuardTimer(this, callback, state);
            lock (gate) timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }
        internal void Advance(TimeSpan amount)
        {
            GuardTimer[] ready;
            lock (gate)
            {
                ticks += amount.Ticks;
                ready = timers.Where(x => !x.Disposed && x.Due <= ticks).ToArray();
                foreach (var timer in ready) timer.Due = long.MaxValue;
            }
            foreach (var timer in ready) timer.Callback(timer.State);
        }
        private sealed class GuardTimer(GuardClock owner, TimerCallback callback, object? state) : ITimer
        {
            internal TimerCallback Callback => callback;
            internal object? State => state;
            internal long Due { get; set; } = long.MaxValue;
            internal bool Disposed { get; private set; }
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (owner.gate)
                {
                    if (Disposed) return false;
                    Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : checked(owner.ticks + dueTime.Ticks);
                    return true;
                }
            }
            public void Dispose() { lock (owner.gate) Disposed = true; }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }

    private sealed class GuardAuditStore : IAuditStore
    {
        internal InMemoryAuditSink Inner { get; } = new();
        internal bool FailWrites { get; set; }
        internal bool FailAfterWrite { get; set; }
        internal int Initializations { get; private set; }
        public Task InitializeAsync(CancellationToken ct = default) { Initializations++; return Inner.InitializeAsync(ct); }
        public async ValueTask WriteAsync(AuditEvent evt, CancellationToken ct = default)
        {
            if (FailWrites) throw new IOException("fictitious audit unavailable");
            await Inner.WriteAsync(evt, ct);
            if (FailAfterWrite) throw new IOException("fictitious persisted audit response loss");
        }
        public Task<AuditPage> QueryPageAsync(AuditQuery query, CancellationToken ct = default) => Inner.QueryPageAsync(query, ct);
        public Task<IReadOnlyCollection<AuditEvent>> QueryAsync(int limit = 100, string? subjectId = null, string? action = null,
            AuditOutcome? outcome = null, DateTimeOffset? fromUtc = null, DateTimeOffset? toUtc = null, CancellationToken cancellationToken = default) =>
            Inner.QueryAsync(limit, subjectId, action, outcome, fromUtc, toUtc, cancellationToken);
        public Task<int> ApplyRetentionBatchAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct = default) =>
            Inner.ApplyRetentionBatchAsync(cutoff, batchSize, ct);
        public AuditStoreHealthSnapshot GetHealthSnapshot() => Inner.GetHealthSnapshot();
    }
}
