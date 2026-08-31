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

    private sealed class FixedMachineIdentityProvider(string fingerprint) : IMachineIdentityProvider
    {
        public string GetMachineFingerprint() => fingerprint;
    }

    private sealed class FakeRuntimeCoordinator : IEngineeringRuntimeCoordinator
    {
        private RuntimeDescriptor _descriptor = EmptyDescriptor();

        public int ActivationCount { get; private set; }
        public bool Disposed { get; private set; }

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
