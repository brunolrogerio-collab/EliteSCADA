using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scada.Api.Licensing;
using Scada.Api.Security;
using Scada.Core.Events;
using Scada.Core.Product.Licensing;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Security.Authorization;
using Scada.Security.Audit;

namespace Scada.Drivers.Tests;

public sealed class OnlineProductLicenseLifecycleTests(OnlineProductLicenseTestKeys keys)
    : IClassFixture<OnlineProductLicenseTestKeys>
{
    private static readonly DateTimeOffset Issued = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly OnlineLeaseBinding Binding = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), new string('a', 64));

    [Fact]
    public async Task EncryptedJournalCompletion_DoesNotApplyEntitlementUntilCanonicalLifecycleRuns()
    {
        using var f = new Fixture(keys);
        var protectionKey = RandomNumberGenerator.GetBytes(32);
        using var protection = new FileHostProtectedMaterialAuthority(new(
            System.IO.Path.Combine(f.Root, "material"), "FICTITIOUS_TEST_KEY", null), protectionKey, new InMemoryAuditSink(), f.Clock);
        CryptographicOperations.ZeroMemory(protectionKey);
        var journal = new FileOnlineLicenseStateStore(System.IO.Path.Combine(f.Root, "journal"), Binding.MachineFingerprint,
            new Uri("https://portal.example.test/"), protection, f.Clock,
            (code, binding, now) => OnlineLeaseVerifier.VerifyWithKey(code, binding, now, keys.Online));
        var admin = new ProtectedMaterialMutationContext(new SecurityPrincipal("fictitious-admin", "Test Admin", []),
            new AuthorizationDecision(true, SecurityCapability.EngineeringModify, "test authority decision", []));
        var prepared = await journal.PrepareActivationAsync(new(Binding.LicenseId, Binding.OrganizationId,
            Binding.ApplicationId, "SCV-FICTITIOUS"), "fictitious-account", admin);
        var activationBinding = Binding with { ActivationId = prepared.ActivationId };
        await journal.CompleteAsync(prepared.PendingRequestId!.Value, Candidate(binding: activationBinding).Envelope, admin, default);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        Assert.Equal(LicenseState.Demo, source.CurrentVerification.State);
        Assert.Equal(1, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        var candidate = await journal.ReadEntitlementCandidateAsync();
        Assert.Equal(activationBinding, candidate.Binding);
        Assert.True((await new ProductLicenseLifecycleCoordinator(source, store, new RecordingRuntime(), f.Clock)
            .InstallOnlineAsync(candidate)).Succeeded);
        Assert.Equal(LicenseState.Valid, source.CurrentVerification.State);
        Assert.Equal(2, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        f.Clock.Advance(TimeSpan.FromHours(72));
        await Assert.ThrowsAsync<OnlineLicenseProtocolException>(() => journal.ReadEntitlementCandidateAsync());
    }

    [Fact]
    public async Task SelectedOnlineLease_ProjectsOnlySignedEntitlementsAndFencesPreviousSessions()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var lease = await Admit(store);
        var lifecycle = new ProductLicenseLifecycleCoordinator(source, store, runtime, f.Clock);

        var result = await lifecycle.InstallOnlineAsync(Candidate());

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.CurrentAuthorityRevision);
        Assert.Equal(1, result.FencedLeaseCount);
        Assert.False((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client")).IsValid);
        var verified = source.CurrentVerification;
        Assert.Equal(LicenseState.Valid, verified.State);
        Assert.Equal(LicenseTier.Tags1000, verified.License!.Tier);
        Assert.Equal(ProductLicenseTrustAnchors.OnlineLeaseKeyId, verified.License.KeyId);
        Assert.Equal(new MachineLicenseV2Entitlements(7, 3, true), verified.SessionEntitlements);
        Assert.True(verified.HasAssetProcessIntelligence);
        Assert.True(source.EvaluateRun(1000).Allowed);
        Assert.False(source.EvaluateRun(1001).Allowed);
        using var reopened = f.Source();
        Assert.Equal(verified, reopened.CurrentVerification);
    }

    [Fact]
    public void DefaultSource_DoesNotTrustEphemeralSigningKeyOrOfflineImportOfOnlineMaterial()
    {
        using var f = new Fixture(keys);
        var candidate = Candidate();
        using var pinned = f.Source(useEphemeralVerifier: false);
        Assert.Equal(LicenseState.Invalid, pinned.VerifyOnlineCandidate(candidate).State);
        Assert.Equal(LicenseState.Invalid, pinned.VerifyCandidate(candidate.Envelope).State);
        Assert.Throws<InvalidDataException>(() => pinned.InstallLicense(candidate.Envelope));
        Assert.Equal(LicenseState.Demo, pinned.CurrentVerification.State);
        using var source = f.Source();
        source.SelectOnline(candidate);
        var internalArtifact = File.ReadAllText(f.Path);
        Assert.Equal(LicenseState.Invalid, source.VerifyCandidate(internalArtifact).State);
        Assert.Throws<InvalidDataException>(() => source.InstallLicense(internalArtifact));
        Assert.Equal(LicenseState.Valid, source.CurrentVerification.State);
    }

    [Theory]
    [InlineData(200, "retained", true)]
    [InlineData(201, "stopped", true)]
    [InlineData(200, "retained", false)]
    [InlineData(201, "stopped", false)]
    public async Task Expiry_RequiresCoordinatedTransition_ReevaluatesWholeRunAndFencesOldLease(int tags, string outcome, bool materializedTags)
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var bus = new InMemoryScadaEventBus();
        await using var runtime = new ProductLicensedRuntimeCoordinator(
            new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)),
            () => new EngineeringRuntimeCoordinator(bus, new EngineeringDriverCompiler(), TimeSpan.FromSeconds(2)), source, f.Clock);
        var lifecycle = new ProductLicenseLifecycleCoordinator(source, store, runtime, f.Clock);
        Assert.True((await lifecycle.InstallOnlineAsync(Candidate())).Succeeded);
        Assert.True((await runtime.ActivateAsync("plant", 1, Package(tags, materializedTags))).Activated);
        Assert.Equal(materializedTags ? tags : 0, runtime.Describe().TagCount);
        f.Clock.Advance(TimeSpan.FromHours(72) - TimeSpan.FromMinutes(1));
        var lease = await Admit(store);
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client")).IsValid);
        var selected = source.ReadOnlineInstallation()!;
        Assert.Equal(OnlineLeaseVerificationStatus.Expired, selected.Status);
        Assert.Equal(LicenseState.Invalid, source.CurrentVerification.State);
        Assert.Equal("ONLINE_LICENSE_EXPIRY_TRANSITION_REQUIRED", source.CurrentVerification.Diagnostic);

        var result = await lifecycle.ExpireOnlineAsync(selected.Revision);

        Assert.True(result.Succeeded);
        Assert.Equal(outcome, result.LocalRuntimeOutcome);
        Assert.Equal(3, result.CurrentAuthorityRevision);
        Assert.Equal(1, result.FencedLeaseCount);
        Assert.Equal(tags == 200 && materializedTags ? tags : 0, runtime.Describe().TagCount);
        Assert.False((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client")).IsValid);
        Assert.False((await store.GetAuthorityStateAsync()).TransitionPending);
        Assert.Equal(LicenseState.Demo, source.CurrentVerification.State);
        Assert.Null(source.CurrentVerification.SessionEntitlements);
        Assert.False(source.CurrentVerification.HasAssetProcessIntelligence);
        Assert.True(source.EvaluateRun(200).Allowed);
        Assert.False(source.EvaluateRun(201).Allowed);
        Assert.Null(source.EvaluateRun(200).MaximumContinuousRun);
        using var reopened = f.Source();
        Assert.Equal(LicenseState.Demo, reopened.CurrentVerification.State);
        var repeat = await lifecycle.ExpireOnlineAsync(reopened.ReadOnlineInstallation()!.Revision);
        Assert.Equal("already-online-demo", repeat.ReasonCode);
        Assert.Equal(3, (await store.GetAuthorityStateAsync()).AuthorityRevision);
    }

    [Fact]
    public async Task ExpiryBeforeDeadline_AndStaleExpiryAfterRenewal_DoNotMutateAuthority()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var lifecycle = new ProductLicenseLifecycleCoordinator(source, store, new RecordingRuntime(), f.Clock);
        await lifecycle.InstallOnlineAsync(Candidate());
        var old = source.ReadOnlineInstallation()!;
        Assert.False((await lifecycle.ExpireOnlineAsync(old.Revision)).Succeeded);
        f.Clock.Advance(TimeSpan.FromHours(48));
        Assert.True((await lifecycle.RenewOnlineAsync(Candidate(issued: f.Clock.GetUtcNow()), old.Revision)).Succeeded);
        var current = source.ReadOnlineInstallation()!;
        f.Clock.Advance(TimeSpan.FromHours(24));
        var before = await store.GetAuthorityStateAsync();
        Assert.False((await lifecycle.ExpireOnlineAsync(old.Revision)).Succeeded);
        Assert.Equal(before, await store.GetAuthorityStateAsync());
        Assert.Equal(current.Revision, source.ReadOnlineInstallation()!.Revision);
        Assert.Equal(LicenseState.Valid, source.CurrentVerification.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateRenewalCannotRestoreOnlineAfterOfflineSelectionOrRemoval(bool remove)
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var lifecycle = new ProductLicenseLifecycleCoordinator(source, store, new RecordingRuntime(), f.Clock);
        await lifecycle.InstallOnlineAsync(Candidate());
        var selected = source.ReadOnlineInstallation()!;
        if (remove) await lifecycle.RemoveAsync();
        else Assert.True((await lifecycle.InstallOrReplaceAsync(OfflineCode())).Succeeded);
        var previous = source.CurrentVerification;
        var authority = await store.GetAuthorityStateAsync();
        var lease = await Admit(store);

        var late = await lifecycle.RenewOnlineAsync(Candidate(), selected.Revision);

        Assert.False(late.Succeeded);
        Assert.Equal(authority, await store.GetAuthorityStateAsync());
        Assert.Equal(previous, source.CurrentVerification);
        Assert.True((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client")).IsValid);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("binding")]
    [InlineData("early-demo")]
    [InlineData("schema")]
    public async Task InvalidOnlineArtifact_NeverFallsBackToDemoOrCommitsExpiry(string damage)
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var lifecycle = new ProductLicenseLifecycleCoordinator(source, store, new RecordingRuntime(), f.Clock);
        var candidate = Candidate();
        await lifecycle.InstallOnlineAsync(candidate);
        var revision = source.ReadOnlineInstallation()!.Revision;
        var text = File.ReadAllText(f.Path);
        text = damage switch
        {
            "signature" => text.Replace(candidate.Envelope, candidate.Envelope[..^8] + "XXXXXXXX"),
            "binding" => text.Replace(Binding.ActivationId.ToString("D"), Guid.NewGuid().ToString("D")),
            "early-demo" => text.Replace("\"expiryApplied\":false", "\"expiryApplied\":true"),
            _ => text.Replace("\"schemaVersion\":1", "\"schemaVersion\":999")
        };
        Assert.NotEqual(File.ReadAllText(f.Path), text);
        File.WriteAllText(f.Path, text);
        if (damage != "early-demo") f.Clock.Advance(TimeSpan.FromHours(73));
        Assert.Equal(LicenseState.Invalid, source.CurrentVerification.State);
        var authority = await store.GetAuthorityStateAsync();
        if (damage == "schema")
            await Assert.ThrowsAsync<InvalidDataException>(() => lifecycle.ExpireOnlineAsync(revision));
        else Assert.False((await lifecycle.ExpireOnlineAsync(revision)).Succeeded);
        Assert.Equal(authority, await store.GetAuthorityStateAsync());
        Assert.False(source.EvaluateRun(1).Allowed);
    }

    [Fact]
    public async Task ExpiredOfflineLicense_RemainsInvalidAndCannotUseOnlineFallback()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        source.InstallLicense(OfflineCode());
        f.Clock.Advance(TimeSpan.FromDays(2));
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var lifecycle = new ProductLicenseLifecycleCoordinator(source, store, new RecordingRuntime(), f.Clock);
        Assert.Equal(LicenseState.Invalid, source.CurrentVerification.State);
        Assert.Null(source.ReadOnlineInstallation());
        Assert.False((await lifecycle.ExpireOnlineAsync(Guid.NewGuid())).Succeeded);
        Assert.Equal(LicenseState.Invalid, source.CurrentVerification.State);
        Assert.False(source.EvaluateRun(1).Allowed);
    }

    [Fact]
    public void MonotonicTimeAndDurableWatermark_DoNotExtendLeaseWhenUtcStopsOrGoesBack()
    {
        using var f = new Fixture(keys);
        using (var source = f.Source())
        {
            source.SelectOnline(Candidate());
            var selected = source.ReadOnlineInstallation()!;
            f.Clock.Advance(TimeSpan.FromHours(71));
            source.CheckpointOnlineClock(selected.Revision);
            Assert.Equal(selected.Revision, source.ReadOnlineInstallation()!.Revision);
            f.Clock.SetWall(Issued);
            Assert.Equal(LicenseState.Valid, source.CurrentVerification.State);
            f.Clock.AdvanceMonotonic(TimeSpan.FromHours(1));
            Assert.Equal(OnlineLeaseVerificationStatus.Expired, source.ReadOnlineInstallation()!.Status);
            Assert.Equal("ONLINE_LICENSE_EXPIRY_TRANSITION_REQUIRED", source.CurrentVerification.Diagnostic);
        }
        using var reopened = f.Source();
        // Preparing a candidate cannot prime away the installation's restart watermark.
        _ = reopened.VerifyOnlineCandidate(Candidate());
        Assert.Equal(LicenseState.Invalid, reopened.CurrentVerification.State);
        Assert.Equal("ONLINE_LICENSE_CLOCK_ROLLBACK", reopened.CurrentVerification.Diagnostic);
        f.Clock.SetWall(Issued.AddHours(72));
        Assert.Equal(OnlineLeaseVerificationStatus.Expired, reopened.ReadOnlineInstallation()!.Status);
        Assert.Equal(LicenseState.Invalid, reopened.CurrentVerification.State);
        Assert.Equal("ONLINE_LICENSE_EXPIRY_TRANSITION_REQUIRED", reopened.CurrentVerification.Diagnostic);
    }

    [Fact]
    public async Task CrashAfterExpiryFileBeforeEpochCommit_ReopenReconcilesFromCanonicalDemoAndFencesOnce()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var lifecycle = new ProductLicenseLifecycleCoordinator(source, store, runtime, f.Clock);
        await lifecycle.InstallOnlineAsync(Candidate());
        f.Clock.Advance(TimeSpan.FromHours(72));
        var lease = await Admit(store);
        await store.BeginAuthorityTransitionAsync("online-expire-to-demo", f.Clock.GetUtcNow());
        source.ApplyOnlineExpiry(source.ReadOnlineInstallation()!.Revision);
        Assert.False((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client")).IsValid);
        using var reopened = f.Source();

        var recovered = new ProductLicenseLifecycleCoordinator(reopened, store, runtime, f.Clock);
        await recovered.ReconcilePendingAsync();
        await recovered.ReconcilePendingAsync();

        Assert.Equal(LicenseState.Demo, reopened.CurrentVerification.State);
        Assert.Equal(3, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        Assert.False((await store.GetAuthorityStateAsync()).TransitionPending);
        Assert.False((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client")).IsValid);
    }

    [Fact]
    public async Task FailedLocalReevaluation_LeavesPendingAndDeniesLeasesUntilReconciliation()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var lifecycle = new ProductLicenseLifecycleCoordinator(source, store, runtime, f.Clock);
        await lifecycle.InstallOnlineAsync(Candidate());
        f.Clock.Advance(TimeSpan.FromHours(72));
        var lease = await Admit(store);
        runtime.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => lifecycle.ExpireOnlineAsync(source.ReadOnlineInstallation()!.Revision));
        Assert.Equal(LicenseState.Demo, source.CurrentVerification.State);
        Assert.True((await store.GetAuthorityStateAsync()).TransitionPending);
        Assert.False((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client")).IsValid);
        runtime.Fail = false;
        await lifecycle.ReconcilePendingAsync();
        Assert.Equal(3, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        Assert.False((await store.GetAuthorityStateAsync()).TransitionPending);
    }

    [Fact]
    public async Task ConcurrentRenewalCannotEnterWhileExpiryIsReevaluating_AndPendingDeniesOldSessions()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var runtime = new RecordingRuntime();
        var lifecycle = new ProductLicenseLifecycleCoordinator(source, store, runtime, f.Clock);
        await lifecycle.InstallOnlineAsync(Candidate());
        var revision = source.ReadOnlineInstallation()!.Revision;
        f.Clock.Advance(TimeSpan.FromHours(72));
        var lease = await Admit(store);
        runtime.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var expiring = lifecycle.ExpireOnlineAsync(revision);
        await runtime.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            using var secondSource = f.Source();
            var competing = new ProductLicenseLifecycleCoordinator(secondSource, store, new RecordingRuntime(), f.Clock);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                competing.RenewOnlineAsync(Candidate(issued: f.Clock.GetUtcNow()), revision));
            Assert.True((await store.GetAuthorityStateAsync()).TransitionPending);
            Assert.False((await store.ValidateAsync(lease.SessionId, "operator", lease.Runtime, "client")).IsValid);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Admit(store));
        }
        finally { runtime.Release.SetResult(); }
        Assert.True((await expiring).Succeeded);
        Assert.Equal(3, (await store.GetAuthorityStateAsync()).AuthorityRevision);
        Assert.False((await store.GetAuthorityStateAsync()).TransitionPending);
    }

    [Fact]
    public async Task ExpiredOnlineDemo_RemovalClearsSelectedMode_AndCompetingFileWriterFailsWithoutEpochAdvance()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var lifecycle = new ProductLicenseLifecycleCoordinator(source, store, new RecordingRuntime(), f.Clock);
        await lifecycle.InstallOnlineAsync(Candidate());
        f.Clock.Advance(TimeSpan.FromHours(72));
        await lifecycle.ExpireOnlineAsync(source.ReadOnlineInstallation()!.Revision);
        var before = await store.GetAuthorityStateAsync();
        using (var otherProcess = new FileStream(f.Path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var denied = await lifecycle.RemoveAsync();
            Assert.False(denied.Succeeded);
            Assert.Equal(before, await store.GetAuthorityStateAsync());
            Assert.NotNull(source.ReadOnlineInstallation());
        }
        Assert.True((await lifecycle.RemoveAsync()).Succeeded);
        Assert.Null(source.ReadOnlineInstallation());
        Assert.False(File.Exists(f.Path));
        Assert.Equal(LicenseState.Demo, source.CurrentVerification.State);
        Assert.Equal(before.AuthorityRevision + 1, (await store.GetAuthorityStateAsync()).AuthorityRevision);
    }

    [Fact]
    public async Task RenewalRequiresSameActivationAndNondecreasingSignedIssueTime()
    {
        using var f = new Fixture(keys);
        using var source = f.Source();
        await using var store = new InMemoryRuntimeSessionLeaseStore(f.Clock.GetUtcNow);
        var lifecycle = new ProductLicenseLifecycleCoordinator(source, store, new RecordingRuntime(), f.Clock);
        f.Clock.Advance(TimeSpan.FromHours(2));
        await lifecycle.InstallOnlineAsync(Candidate(issued: Issued.AddHours(1)));
        var revision = source.ReadOnlineInstallation()!.Revision;
        Assert.False((await lifecycle.RenewOnlineAsync(Candidate(), revision)).Succeeded);
        var other = Binding with { ActivationId = Guid.NewGuid() };
        Assert.False((await lifecycle.RenewOnlineAsync(Candidate(binding: other), revision)).Succeeded);
        Assert.Equal(revision, source.ReadOnlineInstallation()!.Revision);
        Assert.Equal(2, (await store.GetAuthorityStateAsync()).AuthorityRevision);
    }

    private OnlineProductLicenseCandidate Candidate(DateTimeOffset? issued = null, OnlineLeaseBinding? binding = null)
    {
        binding ??= Binding;
        var json = JsonSerializer.SerializeToUtf8Bytes(new OnlineLeaseClaims(1, binding.LicenseId, binding.OrganizationId,
            binding.ApplicationId, binding.ActivationId, binding.MachineFingerprint, LicenseTier.Tags1000,
            7, 3, true, true, issued ?? Issued, (issued ?? Issued).AddHours(72), ProductLicenseTrustAnchors.OnlineLeaseKeyId),
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
        var signature = keys.Online.SignData(json, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new(binding, $"ESLEASE1.{Encode(json)}.{Encode(signature)}");
    }

    private string OfflineCode() => EliteScadaLicenseCodec.CreateSignedLicenseV3(new(3, "fictitious-offline",
        Binding.MachineFingerprint, LicenseTier.Tags500, Issued, Issued.AddDays(1), "ephemeral-offline", 2, 2, false, false), keys.Offline);

    private static Task<RuntimeSessionLeaseState> Admit(IRuntimeSessionLeaseStore store) => store.AdmitAsync(new(
        "operator", "client", "interactive", new("engineering", "plant", 1, Issued), TimeSpan.FromMinutes(5)));

    private static EngineeringPackage Package(int count, bool materializedTags) => new("elitescada.engineering", 1, Issued,
        Enumerable.Range(0, count).Select(i => new TagEngineeringDto(Guid.NewGuid(), $"Tag{i}", $"Plant.Tag{i}",
            TagDataType.Double, Source: materializedTags ? "memory.server" : null)).ToArray(),
        [], materializedTags ? [new DataSourceEngineeringDto(null, "memory.server", "Memory", InternalMemoryRuntimePlanner.ServerMemoryDriverKey)] : []);

    private sealed class Fixture(OnlineProductLicenseTestKeys keys) : IDisposable
    {
        private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "scadivo-online-lifecycle-" + Guid.NewGuid().ToString("N"));
        internal string Root => root;
        internal string Path => System.IO.Path.Combine(root, "product.license");
        internal TestClock Clock { get; } = new();
        internal FileProductLicenseService Source(bool useEphemeralVerifier = true)
        {
            var publicKey = RSA.Create();
            publicKey.ImportParameters(keys.Offline.ExportParameters(false));
            return new(new Machine(), Path, new Dictionary<string, RSA> { ["ephemeral-offline"] = publicKey }, Clock)
            {
                OnlineVerifier = useEphemeralVerifier
                    ? (code, binding, now) => OnlineLeaseVerifier.VerifyWithKey(code, binding, now, keys.Online)
                    : OnlineLeaseVerifier.Verify
            };
        }
        public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    private sealed class Machine : IMachineIdentityProvider
    {
        public string GetMachineFingerprint() => Binding.MachineFingerprint;
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset wall = Issued;
        private long monotonic;
        public override DateTimeOffset GetUtcNow() => wall;
        public override long GetTimestamp() => monotonic;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        internal void Advance(TimeSpan elapsed) { wall += elapsed; monotonic += elapsed.Ticks; }
        internal void AdvanceMonotonic(TimeSpan elapsed) => monotonic += elapsed.Ticks;
        internal void SetWall(DateTimeOffset value) => wall = value;
    }

    private sealed class RecordingRuntime : IProductRuntimeAuthorityReevaluator
    {
        internal bool Fail { get; set; }
        internal TaskCompletionSource? Entered { get; set; }
        internal TaskCompletionSource? Release { get; set; }
        public async Task<ProductRuntimeAuthorityReevaluationResult> ReevaluateForAuthorityChangeAsync(DateTimeOffset when, CancellationToken ct = default)
        {
            if (Fail) throw new IOException("fictitious reevaluation interruption");
            Entered?.TrySetResult();
            if (Release is { } release) await release.Task.WaitAsync(ct);
            return new(false, false, false, null, null, "not-active");
        }
    }
}

public sealed class OnlineProductLicenseTestKeys : IDisposable
{
    internal RSA Online { get; } = RSA.Create(3072);
    internal RSA Offline { get; } = RSA.Create(2048);
    public void Dispose() { Online.Dispose(); Offline.Dispose(); }
}
