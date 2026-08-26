using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Api.Persistence;
using Scada.Api.Runtime;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;

namespace Scada.Drivers.Tests;

public sealed class EngineeringWorkspaceCheckoutServiceTests
{
    [Fact]
    public async Task CheckoutAsync_ReplacesEntireWorkspaceAndRecordsBaseRevision()
    {
        using var workspace = new EngineeringWorkspace();
        var exchange = CreateExchange(workspace);
        Assert.Equal(7, exchange.ExportPackage().Tags.Count);
        Assert.Single(exchange.ExportPackage().Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>());
        Assert.Single(exchange.ExportPackage().Screens ?? Array.Empty<ScreenEngineeringDto>());

        var targetTagId = Guid.Parse("50000000-0000-0000-0000-000000000001");
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(
                    targetTagId,
                    "Pressure",
                    "Plant.Pressure",
                    TagDataType.Double,
                    Source: "plc-a",
                    Address: "holding:1",
                    EngineeringUnit: "bar")
            },
            Array.Empty<AlarmEngineeringDto>(),
            new[]
            {
                new DataSourceEngineeringDto(
                    Guid.Parse("51000000-0000-0000-0000-000000000001"),
                    "plc-a",
                    "PLC A",
                    "modbus.tcp",
                    Settings: new Dictionary<string, string>
                    {
                        ["host"] = "127.0.0.1",
                        ["port"] = "502",
                        ["unitId"] = "1"
                    })
            });

        var snapshot = Snapshot(12, "plant-a", "Plant A", package);
        var service = new EngineeringWorkspaceCheckoutService(
            new SingleSnapshotStore(snapshot),
            exchange,
            workspace);

        var outcome = await service.CheckoutAsync("plant-a", 12);

        Assert.NotNull(outcome);
        Assert.True(outcome!.CheckedOut);
        Assert.Equal("plant-a", outcome.Workspace.ProjectKey);
        Assert.Equal("Plant A", outcome.Workspace.ProjectName);
        Assert.Equal(12, outcome.Workspace.BaseRevision);
        Assert.Equal(1, outcome.Workspace.TagCount);
        Assert.Equal(0, outcome.Workspace.AlarmCount);
        Assert.Equal(1, outcome.Workspace.DataSourceCount);
        Assert.Equal(0, outcome.Workspace.TemplateCount);
        Assert.Equal(0, outcome.Workspace.EquipmentCount);
        Assert.Equal(0, outcome.Workspace.DynamoCount);
        Assert.Equal(0, outcome.Workspace.ScreenCount);
        Assert.Equal(0, outcome.Workspace.PopupCount);

        var exported = exchange.ExportPackage();
        Assert.Single(exported.Tags);
        Assert.Equal(targetTagId, exported.Tags.Single().Id);
        Assert.DoesNotContain(exported.Tags, x => x.Path == "Demo.Tank01.Level");
        Assert.Single(exported.DataSources ?? Array.Empty<DataSourceEngineeringDto>());
    }

    [Fact]
    public async Task CheckoutAsync_InvalidRevisionLeavesExistingWorkspaceUntouched()
    {
        using var workspace = new EngineeringWorkspace();
        var exchange = CreateExchange(workspace);
        var beforeJson = exchange.ExportJson(indented: false);
        var before = workspace.Describe();

        var invalid = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(
                    Guid.Parse("52000000-0000-0000-0000-000000000001"),
                    "Orphan",
                    "Plant.Orphan",
                    TagDataType.Double,
                    Source: "missing.datasource")
            },
            Array.Empty<AlarmEngineeringDto>(),
            Array.Empty<DataSourceEngineeringDto>());

        var snapshot = Snapshot(4, "broken", "Broken Project", invalid);
        var service = new EngineeringWorkspaceCheckoutService(
            new SingleSnapshotStore(snapshot),
            exchange,
            workspace);

        var outcome = await service.CheckoutAsync("broken", 4);

        Assert.NotNull(outcome);
        Assert.False(outcome!.CheckedOut);
        Assert.False(outcome.Preview.CanApply);
        Assert.Null(outcome.ApplyResult);
        Assert.Equal(beforeJson, exchange.ExportJson(indented: false));

        var after = workspace.Describe();
        Assert.Equal(before.ProjectKey, after.ProjectKey);
        Assert.Equal(before.ProjectName, after.ProjectName);
        Assert.Equal(before.BaseRevision, after.BaseRevision);
        Assert.Equal(7, after.TagCount);
        Assert.Equal(2, after.AlarmCount);
        Assert.Equal(1, after.DataSourceCount);
        Assert.Equal(1, after.TemplateCount);
        Assert.Equal(1, after.ScreenCount);
    }

    private static EngineeringExchangeService CreateExchange(EngineeringWorkspace workspace) =>
        new(
            workspace.Tags,
            workspace.Alarms,
            workspace.DataSources,
            workspace.Assets,
            workspace.Views);

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
            DateTimeOffset.UtcNow,
            json,
            "test");
    }

    private sealed class SingleSnapshotStore(EngineeringProjectSnapshot snapshot) : IEngineeringProjectStore
    {
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
                projectKey.Equals(snapshot.ProjectKey, StringComparison.OrdinalIgnoreCase) ? snapshot : null);

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectSnapshot?>(
                projectKey.Equals(snapshot.ProjectKey, StringComparison.OrdinalIgnoreCase) && revision == snapshot.Revision
                    ? snapshot
                    : null);

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
            string projectKey,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectSnapshot>>(
                projectKey.Equals(snapshot.ProjectKey, StringComparison.OrdinalIgnoreCase)
                    ? new[] { snapshot }
                    : Array.Empty<EngineeringProjectSnapshot>());

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
    }
}
