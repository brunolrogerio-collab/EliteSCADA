using Microsoft.Extensions.Configuration;
using Scada.Api.Persistence;
using Scada.Api.ProjectPackages;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Reports;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class SystemRecoveryApplicationServiceTests
{
    [Fact]
    public async Task PreviewAsync_RequiresBootstrapAuthorityAnchorAndProspectiveCapabilities()
    {
        using var workspace = new EngineeringWorkspace();
        var gateways = new InMemoryGatewayEngineeringRegistry(workspace.MarkDirty);
        var reports = new InMemoryReportEngineeringRegistry(workspace.MarkDirty);
        var exchange = CreateExchange(workspace, gateways, reports);
        var packages = new ProjectPackageService(exchange, workspace.VisualAssets);
        var store = new InMemoryProjectStore();
        var persistence = new EngineeringProjectPersistenceService(exchange, store, workspace.VisualAssets);
        var identities = new InMemoryLocalIdentityStore();
        var actor = Account("operator");
        await identities.CreateAsync(actor);

        var packageBytes = BuildPackage(
            "plant-a",
            "Plant A",
            "operator",
            SecurityCapability.EngineeringModify,
            SecurityCapability.SystemAdmin);
        var service = CreateService(
            packages,
            persistence,
            new EmptyCatalog(),
            new SuccessfulActivationService(persistence),
            identities,
            workspace,
            exchange,
            gateways,
            reports,
            "plant-a");

        var preview = await service.PreviewAsync(
            packageBytes,
            actor,
            requireCurrentUserAdmission: true);

        Assert.False(preview.CanApply);
        Assert.NotNull(preview.CurrentUserAdmission);
        Assert.False(preview.CurrentUserAdmission!.Allowed);
        Assert.False(preview.CurrentUserAdmission.BootstrapAuthority);
        Assert.Contains("bootstrap", preview.CurrentUserAdmission.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyAsync_ReplacesWorkingSavesRootPublishesAndActivates()
    {
        using var workspace = new EngineeringWorkspace();
        var gateways = new InMemoryGatewayEngineeringRegistry(workspace.MarkDirty);
        var reports = new InMemoryReportEngineeringRegistry(workspace.MarkDirty);
        var exchange = CreateExchange(workspace, gateways, reports);
        var packages = new ProjectPackageService(exchange, workspace.VisualAssets);
        var store = new InMemoryProjectStore();
        var persistence = new EngineeringProjectPersistenceService(exchange, store, workspace.VisualAssets);
        var identities = new InMemoryLocalIdentityStore();
        var actor = Account(LocalIdentityBootstrapService.InitialAdministratorRole);
        await identities.CreateAsync(actor);

        var packageBytes = BuildPackage(
            "plant-a",
            "Plant A",
            LocalIdentityBootstrapService.InitialAdministratorRole,
            SecurityCapability.EngineeringModify,
            SecurityCapability.UserRoleAdmin);
        var service = CreateService(
            packages,
            persistence,
            new EmptyCatalog(store),
            new SuccessfulActivationService(persistence),
            identities,
            workspace,
            exchange,
            gateways,
            reports,
            "plant-a");

        var result = await service.ApplyAsync(packageBytes, actor, "recovery-test");

        Assert.True(result.Recovered);
        Assert.NotNull(result.Revision);
        Assert.Null(result.Revision!.BasedOnRevision);
        Assert.Equal("plant-a", result.Revision.ProjectKey);
        Assert.True(result.Published);
        Assert.True(result.Activated);
        Assert.Equal(result.Revision.Revision, result.Lifecycle!.ActiveRevision);
        Assert.Equal(result.Revision.Revision, result.Activation!.ActiveRevision);

        var recovered = exchange.ExportPackage();
        Assert.Empty(recovered.Tags);
        Assert.Single(recovered.SecurityRoles ?? Array.Empty<SecurityRoleEngineeringDto>());
        Assert.DoesNotContain(recovered.Tags, tag => tag.Path.StartsWith("Demo.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ApplyAsync_BlockingReplacementPreviewRestoresPreviousWorkspaceAndDoesNotPersist()
    {
        using var workspace = new EngineeringWorkspace();
        var gateways = new InMemoryGatewayEngineeringRegistry(workspace.MarkDirty);
        var reports = new InMemoryReportEngineeringRegistry(workspace.MarkDirty);
        var exchange = CreateExchange(workspace, gateways, reports);
        var before = exchange.ExportPackage();
        var beforeDescriptor = workspace.Describe();
        var packages = new ProjectPackageService(exchange, workspace.VisualAssets);
        var store = new InMemoryProjectStore();
        var persistence = new EngineeringProjectPersistenceService(exchange, store, workspace.VisualAssets);
        var identities = new InMemoryLocalIdentityStore();
        var actor = Account(LocalIdentityBootstrapService.InitialAdministratorRole);
        await identities.CreateAsync(actor);

        var brokenPackage = BuildPackageWithOrphanTag(
            "plant-a",
            "Plant A",
            LocalIdentityBootstrapService.InitialAdministratorRole);
        var service = CreateService(
            packages,
            persistence,
            new EmptyCatalog(store),
            new SuccessfulActivationService(persistence),
            identities,
            workspace,
            exchange,
            gateways,
            reports,
            "plant-a");

        var result = await service.ApplyAsync(brokenPackage, actor, "recovery-test");

        Assert.False(result.Recovered);
        Assert.False(result.DurableRevisionSaved);
        Assert.Null(store.Snapshot);
        var after = exchange.ExportPackage();
        Assert.Equal(
            before.Tags.Select(x => (x.Id, x.Path)).OrderBy(x => x.Path),
            after.Tags.Select(x => (x.Id, x.Path)).OrderBy(x => x.Path));
        var afterDescriptor = workspace.Describe();
        Assert.Equal(beforeDescriptor.ProjectKey, afterDescriptor.ProjectKey);
        Assert.Equal(beforeDescriptor.BaseRevision, afterDescriptor.BaseRevision);
        Assert.Equal(beforeDescriptor.ChangeVersion, afterDescriptor.ChangeVersion);
    }

    private static SystemRecoveryApplicationService CreateService(
        IProjectPackageService packages,
        IEngineeringProjectPersistenceService persistence,
        IEngineeringProjectCatalog catalog,
        IPublishedRuntimeActivationService activation,
        ILocalIdentityStore identities,
        EngineeringWorkspace workspace,
        IEngineeringExchangeService exchange,
        IGatewayEngineeringRegistry gateways,
        IReportEngineeringRegistry reports,
        string configuredProjectKey) =>
        new(
            packages,
            persistence,
            catalog,
            activation,
            identities,
            workspace,
            exchange,
            gateways,
            reports,
            new InitialInstallationGate(),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["EngineeringRuntime:ProjectKey"] = configuredProjectKey
                })
                .Build());

    private static EngineeringExchangeService CreateExchange(
        EngineeringWorkspace workspace,
        IGatewayEngineeringRegistry gateways,
        IReportEngineeringRegistry reports) =>
        new(
            workspace.Tags,
            workspace.Alarms,
            workspace.DataSources,
            workspace.Assets,
            workspace.Views,
            workspace.SecurityPolicies,
            workspace.Commands,
            gateways,
            workspace.Scripts,
            workspace.VisualAssets,
            reports);

    private static byte[] BuildPackage(
        string projectKey,
        string projectName,
        string roleKey,
        params SecurityCapability[] capabilities)
    {
        using var source = new EngineeringWorkspace();
        source.Clear();
        var gateways = new InMemoryGatewayEngineeringRegistry(source.MarkDirty);
        var reports = new InMemoryReportEngineeringRegistry(source.MarkDirty);
        source.SecurityPolicies.UpsertRole(new SecurityRoleEngineeringDto(
            Guid.Parse("94000000-0000-0000-0000-000000000001"),
            roleKey,
            "Recovery Administrator",
            Grants: capabilities.Select(x => new CapabilityGrantEngineeringDto(x)).ToArray()));
        var exchange = CreateExchange(source, gateways, reports);
        return new ProjectPackageService(exchange, source.VisualAssets).Export(projectKey, projectName);
    }

    private static byte[] BuildPackageWithOrphanTag(
        string projectKey,
        string projectName,
        string roleKey)
    {
        using var source = new EngineeringWorkspace();
        source.Clear();
        var gateways = new InMemoryGatewayEngineeringRegistry(source.MarkDirty);
        var reports = new InMemoryReportEngineeringRegistry(source.MarkDirty);
        source.SecurityPolicies.UpsertRole(new SecurityRoleEngineeringDto(
            Guid.Parse("94000000-0000-0000-0000-000000000002"),
            roleKey,
            "Recovery Administrator",
            Grants:
            [
                new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringModify),
                new CapabilityGrantEngineeringDto(SecurityCapability.UserRoleAdmin)
            ]));
        source.Tags.Register(Scada.Core.Tags.TagDefinition.Create(
            "Orphan",
            "Plant.Orphan",
            Scada.Core.Tags.TagDataType.Double,
            "missing.datasource"));
        var exchange = CreateExchange(source, gateways, reports);
        return new ProjectPackageService(exchange, source.VisualAssets).Export(projectKey, projectName);
    }

    private static LocalUserAccount Account(params string[] roles)
    {
        var now = new DateTimeOffset(2026, 9, 6, 4, 0, 0, TimeSpan.Zero);
        return new LocalUserAccount(
            Guid.Parse("94000000-0000-0000-0000-000000000010"),
            "administrator",
            LocalIdentityNormalization.NormalizeUsername("administrator"),
            "Administrator",
            true,
            roles,
            new PasswordCredential(new byte[32], new byte[32], 100_000),
            now,
            now);
    }

    private sealed class EmptyCatalog(InMemoryProjectStore? store = null) : IEngineeringProjectCatalog
    {
        public Task<IReadOnlyCollection<EngineeringProjectCatalogEntry>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectCatalogEntry>>(
                store?.Snapshot is null
                    ? Array.Empty<EngineeringProjectCatalogEntry>()
                    : [new EngineeringProjectCatalogEntry(
                        store.Snapshot.ProjectKey,
                        store.Snapshot.ProjectName,
                        store.Snapshot.Revision,
                        store.Snapshot.SavedAtUtc)]);

        public Task<bool> HasAnyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(store?.Snapshot is not null);
    }

    private sealed class SuccessfulActivationService(IEngineeringProjectPersistenceService persistence) : IPublishedRuntimeActivationService
    {
        public async Task<PublishedRuntimeActivationOutcome> ActivateAsync(
            string projectKey,
            string? activatedBy = null,
            CancellationToken cancellationToken = default)
        {
            var snapshot = await persistence.LoadPublishedAsync(projectKey, cancellationToken);
            if (snapshot is null)
                return new PublishedRuntimeActivationOutcome(null, null, null, null);

            var activation = await persistence.RecordActivationAsync(
                projectKey,
                snapshot.Revision,
                activatedBy,
                cancellationToken);
            var lifecycle = await persistence.GetLifecycleAsync(projectKey, cancellationToken);
            var runtime = new Scada.DriverHost.Runtime.RuntimeActivationResult(
                snapshot.ProjectKey,
                snapshot.Revision,
                true,
                Array.Empty<Scada.DriverHost.Engineering.EngineeringDriverIssue>(),
                Array.Empty<Scada.DriverHost.Runtime.RuntimeActivationIssue>(),
                DateTimeOffset.UtcNow);
            return new PublishedRuntimeActivationOutcome(snapshot, runtime, activation, lifecycle);
        }
    }

    private sealed class InMemoryProjectStore : IEngineeringProjectStore
    {
        private EngineeringProjectPublication? _publication;
        private EngineeringProjectActivation? _activation;
        public EngineeringProjectSnapshot? Snapshot { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<EngineeringProjectSnapshot> SaveAsync(
            string projectKey,
            string projectName,
            string engineeringSchema,
            int engineeringSchemaVersion,
            string engineeringJson,
            string? savedBy = null,
            CancellationToken cancellationToken = default) =>
            SaveDerivedWithAssetsAsync(
                projectKey,
                projectName,
                engineeringSchema,
                engineeringSchemaVersion,
                engineeringJson,
                null,
                Array.Empty<EngineeringRevisionAssetPayload>(),
                savedBy,
                cancellationToken);

        public Task<EngineeringProjectSnapshot> SaveDerivedWithAssetsAsync(
            string projectKey,
            string projectName,
            string engineeringSchema,
            int engineeringSchemaVersion,
            string engineeringJson,
            long? basedOnRevision,
            IReadOnlyCollection<EngineeringRevisionAssetPayload> assets,
            string? savedBy = null,
            CancellationToken cancellationToken = default)
        {
            Snapshot = new EngineeringProjectSnapshot(
                (Snapshot?.Revision ?? 0) + 1,
                projectKey,
                projectName,
                engineeringSchema,
                engineeringSchemaVersion,
                DateTimeOffset.UtcNow,
                engineeringJson,
                savedBy,
                basedOnRevision);
            return Task.FromResult(Snapshot);
        }

        public Task<EngineeringProjectSnapshot?> LoadLatestAsync(string projectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectSnapshot?>(Matches(projectKey) ? Snapshot : null);

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(string projectKey, long revision, CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectSnapshot?>(Matches(projectKey) && Snapshot!.Revision == revision ? Snapshot : null);

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(string projectKey, int limit = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectSnapshot>>(Matches(projectKey) ? [Snapshot!] : Array.Empty<EngineeringProjectSnapshot>());

        public Task<EngineeringProjectPublication?> GetPublicationAsync(string projectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectPublication?>(Matches(projectKey) ? _publication : null);

        public Task<EngineeringProjectPublication?> PublishRevisionAsync(string projectKey, long revision, string? publishedBy = null, CancellationToken cancellationToken = default)
        {
            if (!Matches(projectKey) || Snapshot!.Revision != revision)
                return Task.FromResult<EngineeringProjectPublication?>(null);
            _publication = new EngineeringProjectPublication(projectKey, revision, DateTimeOffset.UtcNow, publishedBy);
            return Task.FromResult<EngineeringProjectPublication?>(_publication);
        }

        public Task<EngineeringProjectActivation?> GetActivationAsync(string projectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectActivation?>(Matches(projectKey) ? _activation : null);

        public Task<EngineeringProjectActivation?> RecordActivationAsync(string projectKey, long revision, string? activatedBy = null, CancellationToken cancellationToken = default)
        {
            if (_publication is null || !Matches(projectKey) || _publication.PublishedRevision != revision)
                return Task.FromResult<EngineeringProjectActivation?>(null);
            _activation = new EngineeringProjectActivation(projectKey, revision, DateTimeOffset.UtcNow, activatedBy);
            return Task.FromResult<EngineeringProjectActivation?>(_activation);
        }

        private bool Matches(string projectKey) =>
            Snapshot is not null && Snapshot.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase);
    }
}
