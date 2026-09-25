using Microsoft.Extensions.Configuration;
using Scada.Api.Licensing;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.InternalMemory;
using Scada.Core.Product.Licensing;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.Reports;
using Scada.Engineering.Security;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class InstallationDetachDeepAuditTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 25, 3, 0, 0, TimeSpan.Zero);
    private const string ProjectKey = "plant-a";

    [Fact]
    public async Task InterruptedAttachBeforeFirstSaveRecoversToNeutralBeforeWorkingCheckout()
    {
        var binding = new TestBindingStore(
            new EngineeringInstallationBindingSnapshot(
                EngineeringInstallationBindingState.AttachInProgress,
                ProjectKey,
                1,
                T0));
        var fixture = await CreateFixtureAsync(binding, latest: null);

        await fixture.Service.InitializeAsync();

        Assert.Equal(EngineeringInstallationBindingState.Neutral, binding.Snapshot.State);
        Assert.Null(binding.Snapshot.ProjectKey);
        Assert.True(fixture.Fence.ProcessEffectsFenced);

        var bootstrap = new EngineeringWorkingBootstrapService(
            fixture.Catalog,
            new NeverCheckout(),
            fixture.Workspace);
        var result = await bootstrap.BootstrapAsync(null, null, null);

        Assert.Equal(EngineeringWorkingBootstrapSource.EmptyCatalog, result.Source);
        Assert.False(result.CheckedOut);
        Assert.Null(fixture.Workspace.Describe().ProjectKey);
    }

    [Fact]
    public async Task InterruptedAttachAfterFirstSaveCompletesBindingThenRestoresWorking()
    {
        var binding = new TestBindingStore(
            new EngineeringInstallationBindingSnapshot(
                EngineeringInstallationBindingState.AttachInProgress,
                ProjectKey,
                1,
                T0));
        var fixture = await CreateFixtureAsync(binding, createLatest: true);

        await fixture.Service.InitializeAsync();

        Assert.Equal(EngineeringInstallationBindingState.Attached, binding.Snapshot.State);
        Assert.Equal(ProjectKey, binding.Snapshot.ProjectKey);

        var checkout = new EngineeringWorkspaceCheckoutService(
            fixture.Store,
            fixture.Exchange,
            fixture.Workspace,
            fixture.Gateways,
            fixture.Reports);
        var bootstrap = new EngineeringWorkingBootstrapService(
            fixture.Catalog,
            checkout,
            fixture.Workspace);
        var restored = await bootstrap.BootstrapAsync(
            null,
            null,
            binding.Snapshot.ProjectKey);

        Assert.True(restored.CheckedOut);
        Assert.Equal(ProjectKey, restored.ProjectKey);
        Assert.Equal(fixture.Store.Latest!.Revision, restored.Revision);
        Assert.Equal(ProjectKey, fixture.Workspace.Describe().ProjectKey);
    }

    [Theory]
    [InlineData(AuthorityLifecycleState.AuthorityPresent)]
    [InlineData(AuthorityLifecycleState.DetachInProgress)]
    [InlineData(AuthorityLifecycleState.DeliberatelyDetached)]
    public async Task InterruptedInstallationDetachConvergesApplicationAndAuthorityBeforeJournalCompletes(
        AuthorityLifecycleState authorityState)
    {
        var binding = new TestBindingStore(
            new EngineeringInstallationBindingSnapshot(
                EngineeringInstallationBindingState.DetachInProgress,
                ProjectKey,
                3,
                T0));
        var fixture = await CreateFixtureAsync(
            binding,
            createLatest: true,
            authorityState: authorityState);

        await fixture.Service.InitializeAsync();

        Assert.Equal(EngineeringInstallationBindingState.Neutral, binding.Snapshot.State);
        Assert.Null(binding.Snapshot.ProjectKey);
        Assert.Null(fixture.Store.Latest);
        Assert.Equal(
            AuthorityLifecycleState.DeliberatelyDetached,
            (await fixture.AuthorityLifecycle.GetAsync()).State);
        Assert.Empty(fixture.AuthorityPolicy.Snapshot().Roles);
        Assert.True(fixture.Fence.ProcessEffectsFenced);
    }

    [Theory]
    [InlineData(InstallationDetachLicenseAction.Keep, LicenseState.Valid, LicenseState.Valid)]
    [InlineData(InstallationDetachLicenseAction.Remove, LicenseState.Valid, LicenseState.Demo)]
    [InlineData(InstallationDetachLicenseAction.Replace, LicenseState.Demo, LicenseState.Valid)]
    public async Task SuccessfulLicenseChoicePrecedesDetachJournalAndSurvivesRestartRecovery(
        InstallationDetachLicenseAction action,
        LicenseState initial,
        LicenseState expected)
    {
        var binding = new TestBindingStore(
            new EngineeringInstallationBindingSnapshot(
                EngineeringInstallationBindingState.Attached,
                ProjectKey,
                7,
                T0));
        var fixture = await CreateFixtureAsync(
            binding,
            createLatest: true,
            licenseState: initial);
        fixture.Fence.FailFenceOnce = true;

        var request = Request(
            action,
            action == InstallationDetachLicenseAction.Replace ? "valid-replacement" : null);

        await Assert.ThrowsAsync<IOException>(() => fixture.Service.DetachAsync(request));

        Assert.Equal(expected, fixture.Licensing.CurrentVerification.State);
        Assert.Equal(EngineeringInstallationBindingState.DetachInProgress, binding.Snapshot.State);
        Assert.Equal(AuthorityLifecycleState.AuthorityPresent, (await fixture.AuthorityLifecycle.GetAsync()).State);

        await fixture.Service.InitializeAsync();

        Assert.Equal(expected, fixture.Licensing.CurrentVerification.State);
        Assert.Equal(EngineeringInstallationBindingState.Neutral, binding.Snapshot.State);
        Assert.Equal(
            AuthorityLifecycleState.DeliberatelyDetached,
            (await fixture.AuthorityLifecycle.GetAsync()).State);
    }

    [Fact]
    public async Task LicenseRemoveFailureDoesNotStartApplicationOrAuthorityDetach()
    {
        var binding = new TestBindingStore(
            new EngineeringInstallationBindingSnapshot(
                EngineeringInstallationBindingState.Attached,
                ProjectKey,
                4,
                T0));
        var fixture = await CreateFixtureAsync(
            binding,
            createLatest: true,
            licenseState: LicenseState.Valid);
        fixture.Licensing.FailRemove = true;

        var result = await fixture.Service.DetachAsync(
            Request(InstallationDetachLicenseAction.Remove));

        Assert.False(result.Detached);
        Assert.Equal("license", result.Stage);
        Assert.Equal("file-mutation-failed", result.LicenseOutcome);
        Assert.Equal(EngineeringInstallationBindingState.Attached, binding.Snapshot.State);
        Assert.Equal(AuthorityLifecycleState.AuthorityPresent, (await fixture.AuthorityLifecycle.GetAsync()).State);
        Assert.NotNull(fixture.Store.Latest);
        Assert.False(fixture.Fence.ProcessEffectsFenced);
    }

    [Fact]
    public async Task ReplacementFileFailureDoesNotStartDetachAndPreservesPriorValidLicense()
    {
        var binding = new TestBindingStore(
            new EngineeringInstallationBindingSnapshot(
                EngineeringInstallationBindingState.Attached,
                ProjectKey,
                4,
                T0));
        var fixture = await CreateFixtureAsync(
            binding,
            createLatest: true,
            licenseState: LicenseState.Valid);
        fixture.Licensing.FailInstall = true;

        var result = await fixture.Service.DetachAsync(
            Request(InstallationDetachLicenseAction.Replace, "valid-replacement"));

        Assert.False(result.Detached);
        Assert.Equal("license", result.Stage);
        Assert.Equal("file-mutation-failed", result.LicenseOutcome);
        Assert.Equal(LicenseState.Valid, fixture.Licensing.CurrentVerification.State);
        Assert.Equal(EngineeringInstallationBindingState.Attached, binding.Snapshot.State);
        Assert.Equal(AuthorityLifecycleState.AuthorityPresent, (await fixture.AuthorityLifecycle.GetAsync()).State);
        Assert.NotNull(fixture.Store.Latest);
        Assert.False(fixture.Fence.ProcessEffectsFenced);
    }

    [Fact]
    public async Task InvalidReplacementDoesNotStartDetachAndPreservesPriorValidLicense()
    {
        var binding = new TestBindingStore(
            new EngineeringInstallationBindingSnapshot(
                EngineeringInstallationBindingState.Attached,
                ProjectKey,
                4,
                T0));
        var fixture = await CreateFixtureAsync(
            binding,
            createLatest: true,
            licenseState: LicenseState.Valid);
        fixture.Licensing.CandidateValid = false;

        var result = await fixture.Service.DetachAsync(
            Request(InstallationDetachLicenseAction.Replace, "invalid-replacement"));

        Assert.False(result.Detached);
        Assert.Equal("license", result.Stage);
        Assert.Equal("invalid-candidate", result.LicenseOutcome);
        Assert.Equal(LicenseState.Valid, fixture.Licensing.CurrentVerification.State);
        Assert.Equal(EngineeringInstallationBindingState.Attached, binding.Snapshot.State);
        Assert.Equal(AuthorityLifecycleState.AuthorityPresent, (await fixture.AuthorityLifecycle.GetAsync()).State);
        Assert.NotNull(fixture.Store.Latest);
    }

    private static InstallationDetachRequest Request(
        InstallationDetachLicenseAction action,
        string? replacement = null) =>
        new(
            AcknowledgeUnsavedWorking: true,
            AcknowledgeApplicationRemoval: true,
            AcknowledgeAuthorityRemoval: true,
            AcknowledgeHistorianPreserved: true,
            action,
            replacement);

    private static async Task<Fixture> CreateFixtureAsync(
        TestBindingStore binding,
        EngineeringProjectSnapshot? latest = null,
        bool createLatest = false,
        AuthorityLifecycleState authorityState = AuthorityLifecycleState.AuthorityPresent,
        LicenseState licenseState = LicenseState.Valid)
    {
        var workspace = new EngineeringWorkspace(seedDemo: false);
        var role = new SecurityRoleEngineeringDto(
            Guid.Parse("71000000-0000-0000-0000-000000000001"),
            "system-admin",
            "System Administrator",
            Grants: [new CapabilityGrantEngineeringDto(SecurityCapability.SystemAdmin)]);
        var policy = authorityState == AuthorityLifecycleState.DeliberatelyDetached
            ? new InMemoryAuthorityPolicyStore()
            : new InMemoryAuthorityPolicyStore([role]);
        var exchange = new EngineeringExchangeService(
            workspace.Tags,
            workspace.Alarms,
            workspace.DataSources,
            workspace.Assets,
            workspace.Views,
            new AuthorityPolicyRegistryView(policy),
            workspace.Commands);
        var gateways = new InMemoryGatewayEngineeringRegistry(workspace.MarkDirty);
        var reports = new InMemoryReportEngineeringRegistry(workspace.MarkDirty);

        latest ??= createLatest ? Snapshot(exchange, ProjectKey) : null;
        var store = new RecoveryProjectStore(latest);
        var persistence = new EngineeringProjectPersistenceService(
            exchange,
            store,
            workspace.VisualAssets,
            binding);
        var catalog = new StoreCatalog(store);

        var authorityLifecycle = new InMemoryAuthorityLifecycleStore();
        await authorityLifecycle.MarkAuthorityPresentAsync();
        if (authorityState is AuthorityLifecycleState.DetachInProgress or AuthorityLifecycleState.DeliberatelyDetached)
            await authorityLifecycle.BeginDetachAsync();
        if (authorityState == AuthorityLifecycleState.DeliberatelyDetached)
            await authorityLifecycle.CompleteDetachAsync();

        var identities = new InMemoryLocalIdentityStore();
        var authorityDetach = new AuthorityDetachService(authorityLifecycle, identities, policy);

        var licensing = new TestLicenseService { Installed = licenseState };
        var leaseStore = new InMemoryRuntimeSessionLeaseStore(() => T0);
        var licenseLifecycle = new ProductLicenseLifecycleCoordinator(
            licensing,
            leaseStore,
            new NoopRuntimeReevaluator(),
            new FixedTimeProvider());

        var runtime = new EmptyRuntimeCoordinator();
        var fence = new TestRuntimeFence();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var service = new InstallationDetachService(
            new InitialInstallationGate(),
            binding,
            catalog,
            persistence,
            workspace,
            exchange,
            gateways,
            reports,
            fence,
            runtime,
            eventBus: null,
            configuration,
            authorityDetach,
            authorityLifecycle,
            licensing,
            licenseLifecycle);

        return new Fixture(
            service,
            binding,
            store,
            catalog,
            workspace,
            exchange,
            gateways,
            reports,
            authorityLifecycle,
            policy,
            licensing,
            fence);
    }

    private static EngineeringProjectSnapshot Snapshot(
        IEngineeringExchangeService exchange,
        string projectKey)
    {
        var package = exchange.ExportPackage();
        return new EngineeringProjectSnapshot(
            1,
            projectKey,
            "Plant A",
            package.Schema,
            package.SchemaVersion,
            T0,
            exchange.ExportJson(indented: false),
            "test");
    }

    private sealed record Fixture(
        InstallationDetachService Service,
        TestBindingStore Binding,
        RecoveryProjectStore Store,
        StoreCatalog Catalog,
        EngineeringWorkspace Workspace,
        IEngineeringExchangeService Exchange,
        IGatewayEngineeringRegistry Gateways,
        IReportEngineeringRegistry Reports,
        IAuthorityLifecycleStore AuthorityLifecycle,
        InMemoryAuthorityPolicyStore AuthorityPolicy,
        TestLicenseService Licensing,
        TestRuntimeFence Fence);

    private sealed class TestBindingStore(EngineeringInstallationBindingSnapshot snapshot)
        : IEngineeringInstallationBindingStore
    {
        public EngineeringInstallationBindingSnapshot Snapshot { get; private set; } = snapshot;
        public int InitializeCalls { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeCalls++;
            return Task.CompletedTask;
        }

        public Task<EngineeringInstallationBindingSnapshot> GetAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Snapshot);

        public Task<EngineeringInstallationBindingSnapshot> AdoptLegacyAsync(
            string? projectKey,
            CancellationToken cancellationToken = default) =>
            SetAsync(
                string.IsNullOrWhiteSpace(projectKey)
                    ? EngineeringInstallationBindingState.Neutral
                    : EngineeringInstallationBindingState.Attached,
                projectKey,
                advanceGeneration: false);

        public Task<EngineeringInstallationBindingSnapshot> BeginAttachAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            TransitionAsync(
                EngineeringInstallationBindingState.Neutral,
                EngineeringInstallationBindingState.AttachInProgress,
                null,
                projectKey,
                advanceGeneration: false);

        public Task<EngineeringInstallationBindingSnapshot> CompleteAttachAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            TransitionAsync(
                EngineeringInstallationBindingState.AttachInProgress,
                EngineeringInstallationBindingState.Attached,
                projectKey,
                projectKey,
                advanceGeneration: true);

        public Task<EngineeringInstallationBindingSnapshot> AbortAttachAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            TransitionAsync(
                EngineeringInstallationBindingState.AttachInProgress,
                EngineeringInstallationBindingState.Neutral,
                projectKey,
                null,
                advanceGeneration: true);

        public Task<EngineeringInstallationBindingSnapshot> BeginDetachAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            TransitionAsync(
                EngineeringInstallationBindingState.Attached,
                EngineeringInstallationBindingState.DetachInProgress,
                projectKey,
                projectKey,
                advanceGeneration: false);

        public Task<EngineeringInstallationBindingSnapshot> CompleteDetachAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            TransitionAsync(
                EngineeringInstallationBindingState.DetachInProgress,
                EngineeringInstallationBindingState.Neutral,
                projectKey,
                null,
                advanceGeneration: true);

        private Task<EngineeringInstallationBindingSnapshot> TransitionAsync(
            EngineeringInstallationBindingState expected,
            EngineeringInstallationBindingState next,
            string? expectedProject,
            string? nextProject,
            bool advanceGeneration)
        {
            if (Snapshot.State == next &&
                string.Equals(Snapshot.ProjectKey, nextProject, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Snapshot);
            Assert.Equal(expected, Snapshot.State);
            Assert.True(string.Equals(Snapshot.ProjectKey, expectedProject, StringComparison.OrdinalIgnoreCase));
            return SetAsync(next, nextProject, advanceGeneration);
        }

        private Task<EngineeringInstallationBindingSnapshot> SetAsync(
            EngineeringInstallationBindingState state,
            string? projectKey,
            bool advanceGeneration)
        {
            Snapshot = new EngineeringInstallationBindingSnapshot(
                state,
                projectKey,
                advanceGeneration ? Snapshot.Generation + 1 : Snapshot.Generation,
                T0);
            return Task.FromResult(Snapshot);
        }
    }

    private sealed class RecoveryProjectStore(EngineeringProjectSnapshot? latest) : IEngineeringProjectStore
    {
        public EngineeringProjectSnapshot? Latest { get; private set; } = latest;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<EngineeringProjectSnapshot> SaveAsync(
            string projectKey,
            string projectName,
            string engineeringSchema,
            int engineeringSchemaVersion,
            string engineeringJson,
            string? savedBy = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectSnapshot?> LoadLatestAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectSnapshot?>(
                Matches(projectKey) ? Latest : null);

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectSnapshot?>(
                Matches(projectKey) && Latest!.Revision == revision ? Latest : null);

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
            string projectKey,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectSnapshot>>(
                Matches(projectKey) ? [Latest!] : []);

        public Task<EngineeringProjectPublication?> GetPublicationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectPublication?>(null);

        public Task<EngineeringProjectPublication?> PublishRevisionAsync(
            string projectKey,
            long revision,
            string? publishedBy = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectActivation?> GetActivationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectActivation?>(null);

        public Task<EngineeringProjectActivation?> RecordActivationAsync(
            string projectKey,
            long revision,
            string? activatedBy = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteProjectAsync(
            string projectKey,
            CancellationToken cancellationToken = default)
        {
            if (Matches(projectKey))
                Latest = null;
            return Task.CompletedTask;
        }

        private bool Matches(string projectKey) =>
            Latest is not null &&
            Latest.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StoreCatalog(RecoveryProjectStore store) : IEngineeringProjectCatalog
    {
        public Task<IReadOnlyCollection<EngineeringProjectCatalogEntry>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            if (store.Latest is null)
                return Task.FromResult<IReadOnlyCollection<EngineeringProjectCatalogEntry>>([]);

            var latest = store.Latest;
            return Task.FromResult<IReadOnlyCollection<EngineeringProjectCatalogEntry>>(
                [new EngineeringProjectCatalogEntry(
                    latest.ProjectKey,
                    latest.ProjectName,
                    latest.Revision,
                    latest.SavedAtUtc)]);
        }
    }

    private sealed class NeverCheckout : IEngineeringWorkspaceCheckoutService
    {
        public Task<EngineeringWorkspaceCheckoutOutcome?> CheckoutAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Neutral recovery must not checkout Working.");
    }

    private sealed class TestRuntimeFence : IInstallationRuntimeFence
    {
        public bool ProcessEffectsFenced { get; private set; }
        public bool FailFenceOnce { get; set; }

        public Task FenceProcessEffectsForInstallationDetachAsync(
            CancellationToken cancellationToken = default)
        {
            ProcessEffectsFenced = true;
            if (FailFenceOnce)
            {
                FailFenceOnce = false;
                return Task.FromException(new IOException("fixture crash after detach journal start"));
            }
            return Task.CompletedTask;
        }

        public Task<InstallationRuntimeFenceResult> StopFencedRuntimeForInstallationDetachAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new InstallationRuntimeFenceResult(ProjectKey, 1, RuntimeStopped: true));
    }

    private sealed class TestLicenseService : IProductLicenseService
    {
        public bool CandidateValid { get; set; } = true;
        public bool FailInstall { get; set; }
        public bool FailRemove { get; set; }
        public LicenseState Installed { get; set; } = LicenseState.Valid;
        public string MachineFingerprint => "test-machine";
        public string MachineRequestCode => "test-request";
        public LicenseVerificationResult CurrentVerification => new(Installed);
        public LicenseVerificationResult VerifyCandidate(string licenseCode) =>
            CandidateValid ? new(LicenseState.Valid) : LicenseVerificationResult.Invalid("invalid");

        public RunEntitlementDecision EvaluateRun(int projectTagCount) =>
            throw new NotSupportedException();

        public void InstallLicense(string licenseCode)
        {
            if (FailInstall)
                throw new IOException("fixture license install failure");
            Installed = LicenseState.Valid;
        }

        public void RemoveLicense()
        {
            if (FailRemove)
                throw new IOException("fixture license remove failure");
            Installed = LicenseState.Demo;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => T0;
    }

    private sealed class NoopRuntimeReevaluator : IProductRuntimeAuthorityReevaluator
    {
        public Task<ProductRuntimeAuthorityReevaluationResult> ReevaluateForAuthorityChangeAsync(
            DateTimeOffset authorityChangedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProductRuntimeAuthorityReevaluationResult(
                RuntimeExisted: false,
                RuntimeRetained: false,
                RuntimeStopped: false,
                LicenseState: null,
                Decision: null,
                Diagnostic: ProductLicensedRuntimeCoordinator.NoActiveRuntimeDiagnostic));
    }

    private sealed class EmptyRuntimeCoordinator : IEngineeringRuntimeCoordinator
    {
        public RuntimeDescriptor Describe() => new(null, null, null, [], [], 0, 0);
        public IReadOnlyCollection<TagDefinition> Tags() => [];
        public IReadOnlyCollection<TagValue> CurrentValues() => [];
        public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() => [];
        public IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false) => [];
        public IReadOnlyCollection<CommandDefinition> Commands() => [];
        public IReadOnlyCollection<ClientMemoryRuntimeSource> ClientMemorySources() => [];
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
        public Task<RuntimeActivationResult> ActivateAsync(string projectKey, long revision, EngineeringPackage package, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<RuntimeActivationResult> ActivateAsync(string projectKey, long revision, EngineeringPackage package, Func<RuntimeActivationCommitContext, CancellationToken, Task> commitAsync, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
