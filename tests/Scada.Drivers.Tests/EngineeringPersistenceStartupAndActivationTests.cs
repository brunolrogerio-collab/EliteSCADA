using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scada.Api.Persistence;
using Scada.Api.Runtime;
using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Engineering.Contracts;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.Reports;

namespace Scada.Drivers.Tests;

public sealed class EngineeringPersistenceStartupAndActivationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartupInitializesPersistenceChecksOutWorkingThenRecoversActiveWithoutLifecycleMutation()
    {
        var events = new List<string>();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            T0,
            new[]
            {
                new TagEngineeringDto(
                    Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                    "Checked out value",
                    "Plant.CheckedOut.Value",
                    TagDataType.Double)
            },
            Array.Empty<AlarmEngineeringDto>(),
            Array.Empty<DataSourceEngineeringDto>());
        var snapshot = Snapshot(2, "eee-demo", "EEE Demo", package);
        var publication = new EngineeringProjectPublication("eee-demo", 2, T0.AddMinutes(1), "publisher");
        var activation = new EngineeringProjectActivation("eee-demo", 2, T0.AddMinutes(2), "operator");
        var store = new StartupStore(snapshot, publication, activation, events);

        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EngineeringWorking:ProjectKey"] = "eee-demo",
            ["EngineeringWorking:Revision"] = "2",
            ["EngineeringRuntime:ProjectKey"] = "eee-demo"
        });

        var workspace = new EngineeringWorkspace(seedDemo: false);
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
        var persistence = new EngineeringProjectPersistenceService(exchange, store, workspace.VisualAssets);
        var checkout = new EngineeringWorkspaceCheckoutService(store, exchange, workspace, gateways, reports);
        var catalog = new StartupCatalog(store, snapshot, events);
        var bootstrap = new EngineeringWorkingBootstrapService(catalog, checkout, workspace);
        var recovery = new RecordingRecovery(store, workspace, events);

        builder.Services.AddSingleton(workspace);
        builder.Services.AddSingleton<IEngineeringProjectPersistenceService>(persistence);
        builder.Services.AddSingleton<IEngineeringWorkingBootstrapService>(bootstrap);
        builder.Services.AddSingleton<IPersistedRuntimeRecoveryService>(recovery);

        await using var app = builder.Build();
        await app.InitializeEngineeringPersistenceAsync();

        Assert.Equal(
            new[] { "initialize", "catalog", "load:eee-demo:2", "assets:eee-demo:2", "recover:eee-demo" },
            events);
        Assert.True(store.Initialized);
        Assert.Equal("eee-demo", workspace.Describe().ProjectKey);
        Assert.Equal("EEE Demo", workspace.Describe().ProjectName);
        Assert.Equal(2, workspace.Describe().BaseRevision);
        Assert.Single(workspace.Tags.Snapshot());
        Assert.Equal("Plant.CheckedOut.Value", workspace.Tags.Snapshot().Single().Path);
        Assert.DoesNotContain(store.LoadRequests, request =>
            request.ProjectKey.Equals("demo", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(publication, store.Publication);
        Assert.Equal(activation, store.Activation);
        Assert.Equal(0, store.PublishCalls);
        Assert.Equal(0, store.ActivateCalls);
        Assert.Equal(1, recovery.Calls);
    }

    [Fact]
    public async Task CrossProjectActivationIsRejectedBeforeServiceAndLeavesActiveAuthorityUnchanged()
    {
        using var workspace = new EngineeringWorkspace(seedDemo: false);
        workspace.SetCheckout("project-b", "Project B", 5, T0);
        var activation = new RecordingActivationService("project-a", 2);

        var result = await EngineeringPersistenceApi.ActivatePublishedAsync(
            "project-b",
            new EngineeringActivateRequest("operator"),
            configuredProjectKey: "project-a",
            activation);

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, status.StatusCode);
        var value = Assert.IsAssignableFrom<IValueHttpResult>(result);
        Assert.Contains("project-a", JsonSerializer.Serialize(value.Value));
        Assert.Equal(0, activation.Calls);
        Assert.Equal("project-a", activation.ActiveProjectKey);
        Assert.Equal(2, activation.ActiveRevision);
        Assert.Equal("project-b", workspace.Describe().ProjectKey);
        Assert.Equal(5, workspace.Describe().BaseRevision);
    }

    private static EngineeringProjectSnapshot Snapshot(
        long revision,
        string projectKey,
        string projectName,
        EngineeringPackage package)
    {
        var json = JsonSerializer.Serialize(package, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        });

        return new EngineeringProjectSnapshot(
            revision,
            projectKey,
            projectName,
            package.Schema,
            package.SchemaVersion,
            T0,
            json,
            "test");
    }

    private sealed class StartupCatalog(
        StartupStore store,
        EngineeringProjectSnapshot snapshot,
        List<string> events) : IEngineeringProjectCatalog
    {
        public Task<IReadOnlyCollection<EngineeringProjectCatalogEntry>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            Assert.True(store.Initialized);
            events.Add("catalog");
            return Task.FromResult<IReadOnlyCollection<EngineeringProjectCatalogEntry>>(
                new[]
                {
                    new EngineeringProjectCatalogEntry(
                        snapshot.ProjectKey,
                        snapshot.ProjectName,
                        snapshot.Revision,
                        snapshot.SavedAtUtc)
                });
        }
    }

    private sealed class RecordingRecovery(
        StartupStore store,
        EngineeringWorkspace workspace,
        List<string> events) : IPersistedRuntimeRecoveryService
    {
        public int Calls { get; private set; }

        public Task<PersistedRuntimeRecoveryResult> RecoverAsync(
            string projectKey,
            CancellationToken cancellationToken = default)
        {
            Assert.True(store.Initialized);
            Assert.Equal("eee-demo", workspace.Describe().ProjectKey);
            Assert.Equal(2, workspace.Describe().BaseRevision);
            Calls++;
            events.Add($"recover:{projectKey}");
            var runtime = new RuntimeActivationResult(
                projectKey,
                2,
                true,
                Array.Empty<EngineeringDriverIssue>(),
                Array.Empty<RuntimeActivationIssue>(),
                T0.AddMinutes(3));
            return Task.FromResult(new PersistedRuntimeRecoveryResult(projectKey, 2, true, runtime));
        }
    }

    private sealed class RecordingActivationService(
        string activeProjectKey,
        long activeRevision) : IPublishedRuntimeActivationService
    {
        public string ActiveProjectKey { get; } = activeProjectKey;
        public long ActiveRevision { get; } = activeRevision;
        public int Calls { get; private set; }

        public Task<PublishedRuntimeActivationOutcome> ActivateAsync(
            string projectKey,
            string? activatedBy = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("Cross-project activation service must not be invoked.");
        }
    }

    private sealed class StartupStore(
        EngineeringProjectSnapshot snapshot,
        EngineeringProjectPublication publication,
        EngineeringProjectActivation activation,
        List<string> events) : IEngineeringProjectStore
    {
        public bool Initialized { get; private set; }
        public EngineeringProjectPublication Publication { get; private set; } = publication;
        public EngineeringProjectActivation Activation { get; private set; } = activation;
        public int PublishCalls { get; private set; }
        public int ActivateCalls { get; private set; }
        public List<(string ProjectKey, long Revision)> LoadRequests { get; } = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            Initialized = true;
            events.Add("initialize");
            return Task.CompletedTask;
        }

        public Task<EngineeringProjectSnapshot> SaveAsync(
            string projectKey,
            string projectName,
            string engineeringSchema,
            int engineeringSchemaVersion,
            string engineeringJson,
            string? savedBy = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Working bootstrap must not save persisted state.");

        public Task<EngineeringProjectSnapshot?> LoadLatestAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectSnapshot?>(Matches(projectKey) ? snapshot : null);

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default)
        {
            Assert.True(Initialized);
            events.Add($"load:{projectKey}:{revision}");
            LoadRequests.Add((projectKey, revision));
            return Task.FromResult<EngineeringProjectSnapshot?>(
                Matches(projectKey) && revision == snapshot.Revision ? snapshot : null);
        }

        public Task<IReadOnlyCollection<EngineeringRevisionAssetPayload>> LoadRevisionAssetsAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default)
        {
            events.Add($"assets:{projectKey}:{revision}");
            return Task.FromResult<IReadOnlyCollection<EngineeringRevisionAssetPayload>>(
                Array.Empty<EngineeringRevisionAssetPayload>());
        }

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
            string projectKey,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectSnapshot>>(
                Matches(projectKey) ? new[] { snapshot } : Array.Empty<EngineeringProjectSnapshot>());

        public Task<EngineeringProjectPublication?> GetPublicationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectPublication?>(Matches(projectKey) ? Publication : null);

        public Task<EngineeringProjectPublication?> PublishRevisionAsync(
            string projectKey,
            long revision,
            string? publishedBy = null,
            CancellationToken cancellationToken = default)
        {
            PublishCalls++;
            throw new InvalidOperationException("Working bootstrap must not publish persisted state.");
        }

        public Task<EngineeringProjectActivation?> GetActivationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectActivation?>(Matches(projectKey) ? Activation : null);

        public Task<EngineeringProjectActivation?> RecordActivationAsync(
            string projectKey,
            long revision,
            string? activatedBy = null,
            CancellationToken cancellationToken = default)
        {
            ActivateCalls++;
            throw new InvalidOperationException("Working bootstrap must not activate persisted state.");
        }

        private bool Matches(string projectKey) =>
            snapshot.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase);
    }
}
