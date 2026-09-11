using Scada.Api.Persistence;
using Scada.Api.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.Reports;

namespace Scada.Drivers.Tests;

public sealed class EngineeringFirstProjectBootstrapTests
{
    [Fact]
    public void BuiltInLibrary_HasNoExternalEquipmentTemplateDependencies()
    {
        var definitions = BuiltinDynamoLibrary.Create();

        Assert.Equal(8, definitions.Count);
        Assert.All(definitions, definition => Assert.Null(definition.TemplateKey));
    }

    [Fact]
    public async Task FirstProject_CanSaveAndPublishWithOnlyBuiltInBootstrapContent()
    {
        using var workspace = new EngineeringWorkspace();
        var gateways = new InMemoryGatewayEngineeringRegistry(workspace.MarkDirty);
        var reports = new InMemoryReportEngineeringRegistry(workspace.MarkDirty);
        var exchange = new EngineeringExchangeService(
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
        var store = new InMemoryProjectStore();
        var persistence = new EngineeringProjectPersistenceService(
            exchange,
            store,
            workspace.VisualAssets);

        var snapshot = await EngineeringPersistenceApi.SaveFirstProjectAsync(
            "fresh-project",
            new EngineeringSaveRequest("Fresh Project", "test"),
            persistence,
            workspace,
            gateways,
            reports);

        var savedPackage = exchange.ParseJson(snapshot.EngineeringJson);
        Assert.Empty(savedPackage.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>());
        var savedDynamos = savedPackage.Dynamos ?? Array.Empty<DynamoEngineeringDto>();
        Assert.Equal(8, savedDynamos.Count);
        Assert.All(savedDynamos, definition => Assert.Null(definition.TemplateKey));

        var result = await persistence.PublishRevisionAsync(
            snapshot.ProjectKey,
            snapshot.Revision,
            "test");

        Assert.NotNull(result);
        Assert.True(result!.Preview.CanApply);
        Assert.True(result.Published);
        Assert.NotNull(result.Publication);
        Assert.Equal(snapshot.Revision, result.Publication!.PublishedRevision);
    }

    private sealed class InMemoryProjectStore : IEngineeringProjectStore
    {
        private EngineeringProjectSnapshot? _snapshot;
        private EngineeringProjectPublication? _publication;
        private EngineeringProjectActivation? _activation;

        public Task InitializeAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<EngineeringProjectSnapshot> SaveAsync(
            string projectKey,
            string projectName,
            string engineeringSchema,
            int engineeringSchemaVersion,
            string engineeringJson,
            string? savedBy = null,
            CancellationToken cancellationToken = default)
        {
            var revision = (_snapshot?.Revision ?? 0) + 1;
            _snapshot = new EngineeringProjectSnapshot(
                revision,
                projectKey,
                projectName,
                engineeringSchema,
                engineeringSchemaVersion,
                DateTimeOffset.UtcNow,
                engineeringJson,
                savedBy);
            return Task.FromResult(_snapshot);
        }

        public Task<EngineeringProjectSnapshot?> LoadLatestAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectSnapshot?>(Matches(projectKey) ? _snapshot : null);

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectSnapshot?>(
                Matches(projectKey) && _snapshot!.Revision == revision ? _snapshot : null);

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
            string projectKey,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectSnapshot>>(
                Matches(projectKey) ? new[] { _snapshot! } : Array.Empty<EngineeringProjectSnapshot>());

        public Task<EngineeringProjectPublication?> GetPublicationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectPublication?>(
                _publication is not null && Matches(projectKey) ? _publication : null);

        public Task<EngineeringProjectPublication?> PublishRevisionAsync(
            string projectKey,
            long revision,
            string? publishedBy = null,
            CancellationToken cancellationToken = default)
        {
            if (!Matches(projectKey) || _snapshot!.Revision != revision)
                return Task.FromResult<EngineeringProjectPublication?>(null);

            _publication = new EngineeringProjectPublication(
                projectKey,
                revision,
                DateTimeOffset.UtcNow,
                publishedBy);
            return Task.FromResult<EngineeringProjectPublication?>(_publication);
        }

        public Task<EngineeringProjectActivation?> GetActivationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectActivation?>(
                _activation is not null && Matches(projectKey) ? _activation : null);

        public Task<EngineeringProjectActivation?> RecordActivationAsync(
            string projectKey,
            long revision,
            string? activatedBy = null,
            CancellationToken cancellationToken = default)
        {
            if (!Matches(projectKey) || _snapshot!.Revision != revision)
                return Task.FromResult<EngineeringProjectActivation?>(null);

            _activation = new EngineeringProjectActivation(
                projectKey,
                revision,
                DateTimeOffset.UtcNow,
                activatedBy);
            return Task.FromResult<EngineeringProjectActivation?>(_activation);
        }

        private bool Matches(string projectKey) =>
            _snapshot is not null &&
            _snapshot.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase);
    }
}
