using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Api.HostedServices;
using Scada.Api.Persistence;
using Scada.Api.Runtime;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Simulation;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;

namespace Scada.Drivers.Tests;

public sealed class PersistedRuntimeRecoveryServiceTests
{
    [Fact]
    public async Task Recovery_UsesPersistedActiveRevisionEvenWhenNewerRevisionIsPublished()
    {
        await using var activeServer = new TestModbusTcpServer();
        activeServer.HoldingRegisters[10] = 111;
        activeServer.Start();

        await using var publishedServer = new TestModbusTcpServer();
        publishedServer.HoldingRegisters[20] = 222;
        publishedServer.Start();

        var activeTagId = Guid.NewGuid();
        var publishedTagId = Guid.NewGuid();
        var activePackage = CreatePackage(activeServer.Port, activeTagId, "Plant.Active.Value", "holding:10");
        var publishedPackage = CreatePackage(publishedServer.Port, publishedTagId, "Plant.Published.Value", "holding:20");
        var activeSnapshot = CreateSnapshot(1, activePackage);
        var publishedSnapshot = CreateSnapshot(2, publishedPackage);
        var store = new RecoveryStore(activeSnapshot, publishedSnapshot);

        var exchangeBus = new InMemoryScadaEventBus();
        using var exchangeAlarms = new InMemoryAlarmEngine(exchangeBus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), exchangeAlarms);
        var persistence = new EngineeringProjectPersistenceService(exchange, store);

        var runtimeBus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            runtimeBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));
        var recovery = new PersistedRuntimeRecoveryService(persistence, exchange, runtime);

        var result = await recovery.RecoverAsync("plant-a");

        Assert.True(result.Found);
        Assert.True(result.Recovered);
        Assert.Equal(1, result.PersistedActiveRevision);
        Assert.Equal(1, runtime.Describe().Revision);
        Assert.True(runtime.TryGetTag(activeTagId, out var activeTag));
        Assert.Equal("Plant.Active.Value", activeTag!.Path);
        Assert.False(runtime.TryGetTag(publishedTagId, out _));
        Assert.True(runtime.TryGetCurrent(activeTagId, out var current));
        Assert.Equal(111d, Convert.ToDouble(current!.Value));

        var loadedActive = await persistence.LoadActiveAsync("plant-a");
        var loadedPublished = await persistence.LoadPublishedAsync("plant-a");
        Assert.Equal(1, loadedActive!.Revision);
        Assert.Equal(2, loadedPublished!.Revision);
    }

    [Fact]
    public async Task DemoHostedService_DoesNotStartSimulationAfterEngineeringRecovery()
    {
        await using var activeServer = new TestModbusTcpServer();
        activeServer.HoldingRegisters[10] = 55;
        activeServer.Start();

        var activePackage = CreatePackage(
            activeServer.Port,
            Guid.NewGuid(),
            "Plant.Active.Value",
            "holding:10");
        var activeSnapshot = CreateSnapshot(1, activePackage);
        var store = new RecoveryStore(activeSnapshot, activeSnapshot);

        var exchangeBus = new InMemoryScadaEventBus();
        using var exchangeAlarms = new InMemoryAlarmEngine(exchangeBus);
        var exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), exchangeAlarms);
        var persistence = new EngineeringProjectPersistenceService(exchange, store);

        var runtimeBus = new InMemoryScadaEventBus();
        await using var runtime = new EngineeringRuntimeCoordinator(
            runtimeBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));
        var recovery = new PersistedRuntimeRecoveryService(persistence, exchange, runtime);
        Assert.True((await recovery.RecoverAsync("plant-a")).Recovered);

        using var fallback = new DemoRuntimeServices(runtimeBus);
        var fallbackTag = TagDefinition.Create(
            "Demo fallback",
            "Demo.Fallback",
            TagDataType.Double,
            "builtin.simulation");
        await using var simulation = new SimulationDriver(
            fallback.Cache,
            fallback.Registry,
            new[] { new SimulationPoint(fallbackTag, SimulationSignalType.Constant, ConstantValue: 12) },
            TimeSpan.FromMilliseconds(20));

        var hosted = new SimulationDriverHostedService(
            simulation,
            fallback,
            runtime);

        await hosted.StartAsync(CancellationToken.None);

        Assert.Equal(DriverState.Stopped, simulation.Status.State);
        Assert.Empty(fallback.Registry.Snapshot());
    }

    private static EngineeringPackage CreatePackage(
        int port,
        Guid tagId,
        string path,
        string address)
    {
        var tag = new TagEngineeringDto(
            tagId,
            path.Split('.').Last(),
            path,
            TagDataType.Int16,
            Source: "plc-a",
            Address: address);

        var dataSource = new DataSourceEngineeringDto(
            null,
            "plc-a",
            "PLC A",
            EngineeringDriverCompiler.ModbusTcpDriverKey,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1",
                ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["scanIntervalMilliseconds"] = "20",
                ["requestTimeoutMilliseconds"] = "100",
                ["unitId"] = "1"
            });

        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[] { tag },
            Array.Empty<AlarmEngineeringDto>(),
            new[] { dataSource });
    }

    private static EngineeringProjectSnapshot CreateSnapshot(long revision, EngineeringPackage package)
    {
        var json = JsonSerializer.Serialize(package, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        });

        return new EngineeringProjectSnapshot(
            revision,
            "plant-a",
            "Plant A",
            package.Schema,
            package.SchemaVersion,
            DateTimeOffset.UtcNow,
            json,
            "test");
    }

    private sealed class RecoveryStore(
        EngineeringProjectSnapshot activeSnapshot,
        EngineeringProjectSnapshot publishedSnapshot) : IEngineeringProjectStore
    {
        private readonly EngineeringProjectSnapshot[] _snapshots =
            [activeSnapshot, publishedSnapshot];
        private readonly EngineeringProjectActivation _activation = new(
            activeSnapshot.ProjectKey,
            activeSnapshot.Revision,
            DateTimeOffset.UtcNow,
            "operator");
        private readonly EngineeringProjectPublication _publication = new(
            publishedSnapshot.ProjectKey,
            publishedSnapshot.Revision,
            DateTimeOffset.UtcNow,
            "publisher");

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
            Task.FromResult<EngineeringProjectSnapshot?>(_snapshots
                .Where(x => x.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Revision)
                .FirstOrDefault());

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectSnapshot?>(_snapshots.FirstOrDefault(x =>
                x.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase) &&
                x.Revision == revision));

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
            string projectKey,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectSnapshot>>(_snapshots
                .Where(x => x.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Revision)
                .Take(limit)
                .ToArray());

        public Task<EngineeringProjectPublication?> GetPublicationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectPublication?>(
                projectKey.Equals(_publication.ProjectKey, StringComparison.OrdinalIgnoreCase)
                    ? _publication
                    : null);

        public Task<EngineeringProjectPublication?> PublishRevisionAsync(
            string projectKey,
            long revision,
            string? publishedBy = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectActivation?> GetActivationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectActivation?>(
                projectKey.Equals(_activation.ProjectKey, StringComparison.OrdinalIgnoreCase)
                    ? _activation
                    : null);

        public Task<EngineeringProjectActivation?> RecordActivationAsync(
            string projectKey,
            long revision,
            string? activatedBy = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
