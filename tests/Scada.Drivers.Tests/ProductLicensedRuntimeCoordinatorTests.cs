using System.Security.Cryptography;
using Scada.Api.Licensing;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Product.Licensing;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Engineering.Contracts;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class ProductLicensedRuntimeCoordinatorTests
{
    [Fact]
    public async Task DemoGate_DeniedActivationPreservesPreviouslyActiveRuntime()
    {
        var initial = new FakeRuntimeCoordinator();
        var entitlement = new DelegateEntitlementProvider(tagCount =>
            ProductEntitlementEvaluator.Evaluate(LicenseVerificationResult.Demo(), tagCount));
        await using var coordinator = new ProductLicensedRuntimeCoordinator(
            initial,
            () => new FakeRuntimeCoordinator(),
            entitlement);

        var first = await coordinator.ActivateAsync("plant", 1, PackageWithTags(100));
        var blocked = await coordinator.ActivateAsync("plant", 2, PackageWithTags(201));

        Assert.True(first.Activated);
        Assert.False(blocked.Activated);
        Assert.Contains(
            blocked.RuntimeIssues,
            issue => issue.Code == ProductLicensedRuntimeCoordinator.EntitlementDeniedIssueCode);
        Assert.Equal(1, initial.ActivationCount);
        Assert.False(initial.Disposed);
        Assert.Equal(1, coordinator.Describe().Revision);
    }

    [Fact]
    public async Task DemoExpiry_StopsRuntimeAndLaterExplicitRunUsesFreshCoordinator()
    {
        var created = new List<FakeRuntimeCoordinator>();
        var initial = new FakeRuntimeCoordinator();
        created.Add(initial);
        var entitlement = new DelegateEntitlementProvider(_ =>
            new RunEntitlementDecision(
                true,
                LicenseState.Demo,
                200,
                TimeSpan.FromMilliseconds(40),
                null));
        await using var coordinator = new ProductLicensedRuntimeCoordinator(
            initial,
            () =>
            {
                var fresh = new FakeRuntimeCoordinator();
                created.Add(fresh);
                return fresh;
            },
            entitlement);

        var first = await coordinator.ActivateAsync("plant", 1, PackageWithTags(10));
        Assert.True(first.Activated);

        await WaitUntilAsync(
            () => coordinator.GetProductRuntimeStatus().State == ProductRuntimeLifecycleState.DemoExpired,
            TimeSpan.FromSeconds(2));

        Assert.True(initial.Disposed);
        Assert.Null(coordinator.Describe().Revision);
        Assert.Equal(ProductRuntimeLifecycleState.DemoExpired, coordinator.GetProductRuntimeStatus().State);
        Assert.Contains(
            "expired",
            coordinator.GetProductRuntimeStatus().LastDiagnostic,
            StringComparison.OrdinalIgnoreCase);

        var restarted = await coordinator.ActivateAsync("plant", 2, PackageWithTags(10));
        Assert.True(restarted.Activated);
        Assert.True(created.Count >= 2);
        Assert.Equal(1, created[1].ActivationCount);
        Assert.Equal(2, coordinator.Describe().Revision);
        Assert.Equal(ProductRuntimeLifecycleState.Running, coordinator.GetProductRuntimeStatus().State);
    }

    [Fact]
    public async Task AuthorityReevaluation_ValidToValid_RetainsExactRuntimeIdentity()
    {
        var initial = new FakeRuntimeCoordinator();
        var factoryCalls = 0;
        var verification = ValidVerification(LicenseTier.Unlimited);
        var entitlement = new DelegateEntitlementProvider(tagCount =>
            ProductEntitlementEvaluator.Evaluate(verification, tagCount));
        await using var coordinator = new ProductLicensedRuntimeCoordinator(
            initial,
            () =>
            {
                factoryCalls++;
                return new FakeRuntimeCoordinator();
            },
            entitlement);

        Assert.True((await coordinator.ActivateAsync("plant", 7, PackageWithTags(300))).Activated);
        var before = coordinator.Describe();

        verification = ValidVerification(LicenseTier.Tags500);
        var result = await coordinator.ReevaluateForAuthorityChangeAsync(DateTimeOffset.UtcNow);

        Assert.True(result.RuntimeExisted);
        Assert.True(result.RuntimeRetained);
        Assert.False(result.RuntimeStopped);
        Assert.Equal(LicenseState.Valid, result.LicenseState);
        Assert.False(initial.Disposed);
        Assert.Equal(0, factoryCalls);
        Assert.Equal(before.ProjectKey, coordinator.Describe().ProjectKey);
        Assert.Equal(before.Revision, coordinator.Describe().Revision);
        Assert.Equal(before.ActivatedAtUtc, coordinator.Describe().ActivatedAtUtc);
        Assert.Equal(LicenseTier.Tags500, coordinator.GetProductRuntimeStatus().ActiveTier);
    }

    [Fact]
    public async Task AuthorityReevaluation_DowngradeDenyingTagCount_StopsAndDisposesRuntime()
    {
        var initial = new FakeRuntimeCoordinator();
        var fresh = new FakeRuntimeCoordinator();
        var verification = ValidVerification(LicenseTier.Unlimited);
        var entitlement = new DelegateEntitlementProvider(tagCount =>
            ProductEntitlementEvaluator.Evaluate(verification, tagCount));
        await using var coordinator = new ProductLicensedRuntimeCoordinator(
            initial,
            () => fresh,
            entitlement);

        Assert.True((await coordinator.ActivateAsync("plant", 3, PackageWithTags(300))).Activated);
        verification = LicenseVerificationResult.Demo();

        var result = await coordinator.ReevaluateForAuthorityChangeAsync(DateTimeOffset.UtcNow);

        Assert.True(result.RuntimeExisted);
        Assert.False(result.RuntimeRetained);
        Assert.True(result.RuntimeStopped);
        Assert.Equal(LicenseState.Demo, result.LicenseState);
        Assert.True(initial.Disposed);
        Assert.Null(coordinator.Describe().Revision);
        Assert.Contains("200", result.Diagnostic);
        Assert.Equal(ProductRuntimeLifecycleState.Idle, coordinator.GetProductRuntimeStatus().State);
    }

    [Fact]
    public async Task AuthorityReevaluation_ValidToDemo_UsesAuthorityChangeAnchorAndSchedulesRemaining()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var time = new RecordingTimeProvider(now);
        var initial = new FakeRuntimeCoordinator();
        var verification = ValidVerification(LicenseTier.Unlimited);
        var entitlement = new DelegateEntitlementProvider(tagCount =>
            ProductEntitlementEvaluator.Evaluate(verification, tagCount));
        await using var coordinator = new ProductLicensedRuntimeCoordinator(
            initial,
            () => new FakeRuntimeCoordinator(),
            entitlement,
            time);

        Assert.True((await coordinator.ActivateAsync("plant", 11, PackageWithTags(100))).Activated);
        verification = LicenseVerificationResult.Demo();
        var authorityChangedAtUtc = now.AddHours(-2);

        var result = await coordinator.ReevaluateForAuthorityChangeAsync(authorityChangedAtUtc);
        var status = coordinator.GetProductRuntimeStatus();

        Assert.True(result.RuntimeRetained);
        Assert.False(initial.Disposed);
        Assert.Equal(authorityChangedAtUtc, status.DemoStartedAtUtc);
        Assert.Equal(authorityChangedAtUtc + LicensingPolicy.DemoMaxContinuousRun, status.DemoExpiresAtUtc);
        Assert.Equal(TimeSpan.FromHours(3), status.DemoRemaining);
        Assert.Equal(TimeSpan.FromHours(3), time.LastTimerDueTime);
    }

    [Fact]
    public async Task AuthorityReevaluation_ExpiredDemoAnchor_StopsImmediately()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var time = new RecordingTimeProvider(now);
        var initial = new FakeRuntimeCoordinator();
        var verification = ValidVerification(LicenseTier.Unlimited);
        var entitlement = new DelegateEntitlementProvider(tagCount =>
            ProductEntitlementEvaluator.Evaluate(verification, tagCount));
        await using var coordinator = new ProductLicensedRuntimeCoordinator(
            initial,
            () => new FakeRuntimeCoordinator(),
            entitlement,
            time);

        Assert.True((await coordinator.ActivateAsync("plant", 5, PackageWithTags(100))).Activated);
        verification = LicenseVerificationResult.Demo();
        var anchor = now - LicensingPolicy.DemoMaxContinuousRun - TimeSpan.FromMinutes(1);

        var result = await coordinator.ReevaluateForAuthorityChangeAsync(anchor);
        var status = coordinator.GetProductRuntimeStatus();

        Assert.True(result.RuntimeStopped);
        Assert.True(initial.Disposed);
        Assert.Null(coordinator.Describe().Revision);
        Assert.Equal(ProductRuntimeLifecycleState.DemoExpired, status.State);
        Assert.Equal(anchor, status.DemoStartedAtUtc);
        Assert.Equal(TimeSpan.Zero, status.DemoRemaining);
        Assert.Equal(anchor + LicensingPolicy.DemoMaxContinuousRun, status.DemoExpiresAtUtc);
    }

    [Fact]
    public async Task DemoStatus_CombinesDurableElapsedAndCurrentProcessMonotonicElapsed()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var time = new RecordingTimeProvider(now);
        var initial = new FakeRuntimeCoordinator();
        var verification = ValidVerification(LicenseTier.Unlimited);
        var entitlement = new DelegateEntitlementProvider(tagCount =>
            ProductEntitlementEvaluator.Evaluate(verification, tagCount));
        await using var coordinator = new ProductLicensedRuntimeCoordinator(
            initial,
            () => new FakeRuntimeCoordinator(),
            entitlement,
            time);

        Assert.True((await coordinator.ActivateAsync("plant", 9, PackageWithTags(100))).Activated);
        verification = LicenseVerificationResult.Demo();
        var anchor = now.AddHours(-1);
        Assert.True((await coordinator.ReevaluateForAuthorityChangeAsync(anchor)).RuntimeRetained);

        time.Advance(TimeSpan.FromMinutes(30));
        var status = coordinator.GetProductRuntimeStatus();

        Assert.Equal(anchor, status.DemoStartedAtUtc);
        Assert.Equal(anchor + LicensingPolicy.DemoMaxContinuousRun, status.DemoExpiresAtUtc);
        Assert.Equal(TimeSpan.FromHours(3.5), status.DemoRemaining);
    }

    [Fact]
    public async Task DemoActivation_WithoutDurableAnchor_StartsAtActivationTime()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var time = new RecordingTimeProvider(now);
        await using var authorityStore = new InMemoryRuntimeSessionLeaseStore();
        var initial = new FakeRuntimeCoordinator();
        var entitlement = new DelegateEntitlementProvider(tagCount =>
            ProductEntitlementEvaluator.Evaluate(LicenseVerificationResult.Demo(), tagCount));
        await using var coordinator = new ProductLicensedRuntimeCoordinator(
            initial,
            () => new FakeRuntimeCoordinator(),
            entitlement,
            time,
            authorityStore);

        Assert.True((await coordinator.ActivateAsync("plant", 1, PackageWithTags(100))).Activated);
        var status = coordinator.GetProductRuntimeStatus();

        Assert.Equal(now, status.DemoStartedAtUtc);
        Assert.Equal(now + LicensingPolicy.DemoMaxContinuousRun, status.DemoExpiresAtUtc);
        Assert.Equal(LicensingPolicy.DemoMaxContinuousRun, status.DemoRemaining);
    }

    [Fact]
    public async Task DemoActivation_WithAuthorityAnchor_DoesNotMintFreshWindow()
    {
        var now = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var anchor = now.AddHours(-1);
        var time = new RecordingTimeProvider(now);
        await using var authorityStore = new InMemoryRuntimeSessionLeaseStore();
        await SeedDemoAuthorityAsync(authorityStore, anchor);
        var initial = new FakeRuntimeCoordinator();
        var entitlement = new DelegateEntitlementProvider(tagCount =>
            ProductEntitlementEvaluator.Evaluate(LicenseVerificationResult.Demo(), tagCount));
        await using var coordinator = new ProductLicensedRuntimeCoordinator(
            initial,
            () => new FakeRuntimeCoordinator(),
            entitlement,
            time,
            authorityStore);

        Assert.True((await coordinator.ActivateAsync("plant", 1, PackageWithTags(100))).Activated);
        var status = coordinator.GetProductRuntimeStatus();

        Assert.Equal(anchor, status.DemoStartedAtUtc);
        Assert.Equal(TimeSpan.FromHours(4), status.DemoRemaining);
        Assert.Equal(TimeSpan.FromHours(4), time.LastTimerDueTime);
    }

    [Fact]
    public async Task AuthorityReevaluation_NoActiveRuntime_ReturnsDeterministicNoOp()
    {
        var initial = new FakeRuntimeCoordinator();
        var entitlement = new DelegateEntitlementProvider(_ =>
            ProductEntitlementEvaluator.Evaluate(LicenseVerificationResult.Demo(), 0));
        await using var coordinator = new ProductLicensedRuntimeCoordinator(
            initial,
            () => new FakeRuntimeCoordinator(),
            entitlement);

        var result = await coordinator.ReevaluateForAuthorityChangeAsync(DateTimeOffset.UtcNow);

        Assert.False(result.RuntimeExisted);
        Assert.False(result.RuntimeRetained);
        Assert.False(result.RuntimeStopped);
        Assert.Equal(ProductLicensedRuntimeCoordinator.NoActiveRuntimeDiagnostic, result.Diagnostic);
        Assert.False(initial.Disposed);
    }

    [Fact]
    public async Task AuthorityReevaluation_SerializesBehindActivation_AndStopsNewlyDeniedRuntime()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var initial = new FakeRuntimeCoordinator
        {
            ActivationEntered = entered,
            ActivationRelease = release
        };
        var verification = ValidVerification(LicenseTier.Unlimited);
        var entitlement = new DelegateEntitlementProvider(tagCount =>
            ProductEntitlementEvaluator.Evaluate(verification, tagCount));
        await using var coordinator = new ProductLicensedRuntimeCoordinator(
            initial,
            () => new FakeRuntimeCoordinator(),
            entitlement);

        var activation = coordinator.ActivateAsync("plant", 1, PackageWithTags(300));
        await entered.Task;
        verification = LicenseVerificationResult.Demo();

        var reevaluation = coordinator.ReevaluateForAuthorityChangeAsync(DateTimeOffset.UtcNow);
        Assert.False(reevaluation.IsCompleted);

        release.TrySetResult(true);
        Assert.True((await activation).Activated);

        var result = await reevaluation;
        Assert.True(result.RuntimeStopped);
        Assert.True(initial.Disposed);
        Assert.Null(coordinator.Describe().Revision);
    }

    [Fact]
    public void FileLicenseService_RejectsInvalidInstallAndAcceptsMatchingSignedLicense()
    {
        var directory = Path.Combine(Path.GetTempPath(), "elitescada-license-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "EliteSCADA.license");
        try
        {
            using var privateKey = RSA.Create(2048);
            using var publicKey = RSA.Create();
            publicKey.ImportSubjectPublicKeyInfo(privateKey.ExportSubjectPublicKeyInfo(), out _);
            var machine = MachineFingerprint.HashIdentity("runtime-license-test-machine");
            using var service = new FileProductLicenseService(
                new FixedMachineIdentityProvider(machine),
                path,
                new Dictionary<string, RSA> { ["test-key"] = publicKey });

            Assert.Throws<InvalidDataException>(() => service.InstallLicense("not-a-license"));
            Assert.False(File.Exists(path));

            var payload = new EliteScadaLicensePayload(
                EliteScadaLicenseCodec.CurrentSchemaVersion,
                Guid.NewGuid().ToString("D"),
                machine,
                LicenseTier.Tags1000,
                DateTimeOffset.UtcNow,
                null,
                "test-key");
            var code = EliteScadaLicenseCodec.CreateSignedLicense(payload, privateKey);

            service.InstallLicense(code);

            Assert.True(File.Exists(path));
            Assert.Equal(LicenseState.Valid, service.CurrentVerification.State);
            Assert.Equal(LicenseTier.Tags1000, service.CurrentVerification.License?.Tier);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FileLicenseService_ValidEslic2RetainsExistingTagEntitlementGate()
    {
        var directory = Path.Combine(Path.GetTempPath(), "elitescada-license-v2-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "EliteSCADA.license");
        try
        {
            using var privateKey = RSA.Create(2048);
            using var publicKey = RSA.Create();
            publicKey.ImportSubjectPublicKeyInfo(privateKey.ExportSubjectPublicKeyInfo(), out _);
            var machine = MachineFingerprint.HashIdentity("runtime-license-v2-test-machine");
            using var service = new FileProductLicenseService(
                new FixedMachineIdentityProvider(machine),
                path,
                new Dictionary<string, RSA> { ["test-key"] = publicKey });
            var payload = new EliteScadaLicenseV2Payload(
                EliteScadaLicenseCodec.LicenseV2SchemaVersion,
                Guid.NewGuid().ToString("D"),
                machine,
                LicenseTier.Tags1000,
                DateTimeOffset.UtcNow,
                null,
                "test-key",
                ViewOnlySeats: 0,
                InteractiveSeats: 0,
                HaRuntime: false);

            service.InstallLicense(EliteScadaLicenseCodec.CreateSignedLicenseV2(payload, privateKey));

            Assert.Equal(LicenseState.Valid, service.CurrentVerification.State);
            Assert.Equal(new MachineLicenseV2Entitlements(0, 0, false), service.CurrentVerification.SessionEntitlements);
            Assert.True(service.EvaluateRun(1000).Allowed);
            Assert.False(service.EvaluateRun(1001).Allowed);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static LicenseVerificationResult ValidVerification(
        LicenseTier tier = LicenseTier.Unlimited) =>
        LicenseVerificationResult.Valid(
            new EliteScadaLicensePayload(
                EliteScadaLicenseCodec.CurrentSchemaVersion,
                Guid.NewGuid().ToString("D"),
                new string('a', 64),
                tier,
                DateTimeOffset.UnixEpoch,
                null,
                "test-key"));

    private static async Task SeedDemoAuthorityAsync(
        InMemoryRuntimeSessionLeaseStore store,
        DateTimeOffset anchor)
    {
        var transition = await store.BeginAuthorityTransitionAsync("demo-test", anchor);
        var state = await store.CommitAuthorityChangeAsync(
            transition.TransitionId,
            transition.BaseAuthorityRevision,
            anchor,
            anchor);
        await store.CompleteAuthorityTransitionAsync(
            transition.TransitionId,
            state.AuthorityRevision);
    }

    private static EngineeringPackage PackageWithTags(int count) =>
        new(
            "elitescada.engineering",
            1,
            DateTimeOffset.UtcNow,
            Enumerable.Range(0, count)
                .Select(index => new TagEngineeringDto(
                    Guid.NewGuid(),
                    $"Tag {index}",
                    $"Plant.Tag{index:D5}",
                    TagDataType.Double))
                .ToArray(),
            Array.Empty<AlarmEngineeringDto>(),
            Array.Empty<DataSourceEngineeringDto>());

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(10);
        }

        Assert.True(condition(), "Condition was not reached before timeout.");
    }

    private sealed class DelegateEntitlementProvider(Func<int, RunEntitlementDecision> evaluate)
        : IProductRunEntitlementProvider
    {
        public RunEntitlementDecision EvaluateRun(int projectTagCount) => evaluate(projectTagCount);
    }

    private sealed class RecordingTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        private long _timestamp;

        public TimeSpan? LastTimerDueTime { get; private set; }

        public override DateTimeOffset GetUtcNow() => _utcNow;
        public override long GetTimestamp() => _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public void Advance(TimeSpan amount)
        {
            _utcNow += amount;
            _timestamp = checked(_timestamp + amount.Ticks);
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            LastTimerDueTime = dueTime;
            return new PassiveTimer();
        }

        private sealed class PassiveTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class FixedMachineIdentityProvider(string fingerprint) : IMachineIdentityProvider
    {
        public string GetMachineFingerprint() => fingerprint;
    }

    private sealed class FakeRuntimeCoordinator : IEngineeringRuntimeCoordinator
    {
        private RuntimeDescriptor _descriptor = EmptyDescriptor();

        public int ActivationCount { get; private set; }
        public bool Disposed { get; private set; }
        public TaskCompletionSource<bool>? ActivationEntered { get; init; }
        public TaskCompletionSource<bool>? ActivationRelease { get; init; }

        public RuntimeDescriptor Describe() => _descriptor;
        public IReadOnlyCollection<TagDefinition> Tags() => Array.Empty<TagDefinition>();
        public IReadOnlyCollection<TagValue> CurrentValues() => Array.Empty<TagValue>();
        public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() => Array.Empty<AlarmDefinition>();
        public IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false) => Array.Empty<AlarmInstance>();
        public IReadOnlyCollection<CommandDefinition> Commands() => Array.Empty<CommandDefinition>();
        public IReadOnlyCollection<ClientMemoryRuntimeSource> ClientMemorySources() => Array.Empty<ClientMemoryRuntimeSource>();
        public bool TryGetTag(Guid tagId, out TagDefinition? tag) { tag = null; return false; }
        public bool TryGetTagByPath(string path, out TagDefinition? tag) { tag = null; return false; }
        public bool TryGetCurrent(Guid tagId, out TagValue? value) { value = null; return false; }
        public bool TryGetCommand(Guid commandId, out CommandDefinition? command) { command = null; return false; }
        public bool IsServerMemoryTag(Guid tagId) => false;
        public ValueTask<bool> AcknowledgeAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> ShelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask<bool> UnshelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
        public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ResetServerMemoryRetainedValueAsync(Guid tagId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask ExecuteCommandAsync(Guid commandId, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public Task<RuntimeActivationResult> ActivateAsync(
            string projectKey,
            long revision,
            EngineeringPackage package,
            CancellationToken cancellationToken = default) =>
            ActivateCoreAsync(projectKey, revision, package, null, cancellationToken);

        public Task<RuntimeActivationResult> ActivateAsync(
            string projectKey,
            long revision,
            EngineeringPackage package,
            Func<RuntimeActivationCommitContext, CancellationToken, Task> commitAsync,
            CancellationToken cancellationToken = default) =>
            ActivateCoreAsync(projectKey, revision, package, commitAsync, cancellationToken);

        private async Task<RuntimeActivationResult> ActivateCoreAsync(
            string projectKey,
            long revision,
            EngineeringPackage package,
            Func<RuntimeActivationCommitContext, CancellationToken, Task>? commitAsync,
            CancellationToken cancellationToken)
        {
            ActivationCount++;
            ActivationEntered?.TrySetResult(true);
            if (ActivationRelease is not null)
                await ActivationRelease.Task.WaitAsync(cancellationToken);
            var activatedAt = DateTimeOffset.UtcNow;
            if (commitAsync is not null)
                await commitAsync(new RuntimeActivationCommitContext(projectKey, revision, activatedAt), cancellationToken);

            _descriptor = new RuntimeDescriptor(
                projectKey,
                revision,
                activatedAt,
                Array.Empty<DriverStatus>(),
                Array.Empty<CommunicationDriverDiagnosticSnapshot>(),
                package.Tags.Count,
                0);

            return new RuntimeActivationResult(
                projectKey,
                revision,
                true,
                Array.Empty<EngineeringDriverIssue>(),
                Array.Empty<RuntimeActivationIssue>(),
                activatedAt);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            _descriptor = EmptyDescriptor();
            return ValueTask.CompletedTask;
        }

        private static RuntimeDescriptor EmptyDescriptor() =>
            new(
                null,
                null,
                null,
                Array.Empty<DriverStatus>(),
                Array.Empty<CommunicationDriverDiagnosticSnapshot>(),
                0,
                0);
    }
}
