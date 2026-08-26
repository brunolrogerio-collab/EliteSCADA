using System.Text.Json;
using System.Text.Json.Serialization;
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

public sealed class PublishedRuntimeActivationServiceTests
{
    [Fact]
    public async Task ActivateAsync_CommitsPublishedRevisionStopsFallbackAndSwitchesFacade()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[10] = 321;
        server.Start();

        var runtimeTagId = Guid.NewGuid();
        var package = CreatePackage(server.Port, runtimeTagId);
        var snapshot = CreateSnapshot(package);
        var store = new FakeEngineeringProjectStore(snapshot, allowActivation: true);
        var persistence = CreatePersistence(store, out var exchange);

        var externalBus = new InMemoryScadaEventBus();
        var fallbackRegistry = new InMemoryTagRegistry();
        var fallbackCache = new CurrentTagCache(externalBus);
        using var fallbackAlarms = new InMemoryAlarmEngine(externalBus);
        var fallbackTag = TagDefinition.Create(
            "Demo fallback",
            "Demo.Fallback",
            TagDataType.Double,
            "builtin.simulation");

        await using var simulation = new SimulationDriver(
            fallbackCache,
            fallbackRegistry,
            new[] { new SimulationPoint(fallbackTag, SimulationSignalType.Constant, ConstantValue: 12) },
            TimeSpan.FromMilliseconds(15));
        await simulation.StartAsync();

        await using var runtime = new EngineeringRuntimeCoordinator(
            externalBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));
        var facade = new ScadaRuntimeFacade(
            fallbackRegistry,
            fallbackCache,
            fallbackAlarms,
            simulation,
            runtime);
        var activation = new PublishedRuntimeActivationService(
            persistence,
            exchange,
            runtime,
            simulation);

        var outcome = await activation.ActivateAsync("plant-a", "integration-test");

        Assert.True(outcome.Activated);
        Assert.Equal(1, outcome.Activation!.ActiveRevision);
        Assert.Equal(DriverState.Stopped, simulation.Status.State);
        Assert.True(facade.IsEngineeringActive);
        Assert.Equal("engineering", facade.Describe().Mode);
        Assert.Equal(1, facade.Describe().Revision);
        Assert.True(facade.TryGetTag(runtimeTagId, out var runtimeTag));
        Assert.Equal("Plant.Runtime.Value", runtimeTag!.Path);
        Assert.False(facade.TryGetTag(fallbackTag.Id, out _));
        Assert.True(facade.TryGetCurrent(runtimeTagId, out var current));
        Assert.Equal(321d, Convert.ToDouble(current!.Value));
    }

    [Fact]
    public async Task ActivateAsync_PersistenceRejectionRestartsSimulationAndKeepsFacadeOnFallback()
    {
        await using var server = new TestModbusTcpServer();
        server.HoldingRegisters[10] = 222;
        server.Start();

        var package = CreatePackage(server.Port, Guid.NewGuid());
        var snapshot = CreateSnapshot(package);
        var store = new FakeEngineeringProjectStore(snapshot, allowActivation: false);
        var persistence = CreatePersistence(store, out var exchange);

        var externalBus = new InMemoryScadaEventBus();
        var fallbackRegistry = new InMemoryTagRegistry();
        var fallbackCache = new CurrentTagCache(externalBus);
        using var fallbackAlarms = new InMemoryAlarmEngine(externalBus);
        var fallbackTag = TagDefinition.Create(
            "Demo fallback",
            "Demo.Fallback",
            TagDataType.Double,
            "builtin.simulation");

        await using var simulation = new SimulationDriver(
            fallbackCache,
            fallbackRegistry,
            new[] { new SimulationPoint(fallbackTag, SimulationSignalType.Constant, ConstantValue: 12) },
            TimeSpan.FromMilliseconds(15));
        await simulation.StartAsync();

        await using var runtime = new EngineeringRuntimeCoordinator(
            externalBus,
            new EngineeringDriverCompiler(),
            TimeSpan.FromSeconds(2));
        var facade = new ScadaRuntimeFacade(
            fallbackRegistry,
            fallbackCache,
            fallbackAlarms,
            simulation,
            runtime);
        var activation = new PublishedRuntimeActivationService(
            persistence,
            exchange,
            runtime,
            simulation);

        var outcome = await activation.ActivateAsync("plant-a", "integration-test");

        Assert.False(outcome.Activated);
        Assert.NotNull(outcome.Runtime);
        Assert.Contains(
            outcome.Runtime!.RuntimeIssues,
            issue => issue.Code == "RUNTIME_ACTIVATION_COMMIT_FAILED" && issue.IsError);
        Assert.Null(runtime.Describe().Revision);
        Assert.Equal(DriverState.Running, simulation.Status.State);
        Assert.False(facade.IsEngineeringActive);
        Assert.Equal("simulation", facade.Describe().Mode);
        Assert.True(facade.TryGetTag(fallbackTag.Id, out _));
    }

    private static EngineeringProjectPersistenceService CreatePersistence(
        IEngineeringProjectStore store,
        out EngineeringExchangeService exchange)
    {
        var exchangeBus = new InMemoryScadaEventBus();
        var exchangeAlarms = new InMemoryAlarmEngine(exchangeBus);
        exchange = new EngineeringExchangeService(new InMemoryTagRegistry(), exchangeAlarms);
        return new EngineeringProjectPersistenceService(exchange, store);
    }

    private static EngineeringPackage CreatePackage(int port, Guid tagId)
    {
        var tag = new TagEngineeringDto(
            tagId,
            "Runtime Value",
            "Plant.Runtime.Value",
            TagDataType.Int16,
            Source: "plc-a",
            Address: "holding:10");

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

    private static EngineeringProjectSnapshot CreateSnapshot(EngineeringPackage package)
    {
        var json = JsonSerializer.Serialize(package, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        });

        return new EngineeringProjectSnapshot(
            1,
            "plant-a",
            "Plant A",
            package.Schema,
            package.SchemaVersion,
            DateTimeOffset.UtcNow,
            json,
            "test");
    }

    private sealed class FakeEngineeringProjectStore(
        EngineeringProjectSnapshot snapshot,
        bool allowActivation) : IEngineeringProjectStore
    {
        private EngineeringProjectActivation? _activation;
        private readonly EngineeringProjectPublication _publication = new(
            snapshot.ProjectKey,
            snapshot.Revision,
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
            Task.FromResult<EngineeringProjectSnapshot?>(
                projectKey == snapshot.ProjectKey ? snapshot : null);

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectSnapshot?>(
                projectKey == snapshot.ProjectKey && revision == snapshot.Revision ? snapshot : null);

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
            string projectKey,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectSnapshot>>(
                projectKey == snapshot.ProjectKey ? new[] { snapshot } : Array.Empty<EngineeringProjectSnapshot>());

        public Task<EngineeringProjectPublication?> GetPublicationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectPublication?>(
                projectKey == snapshot.ProjectKey ? _publication : null);

        public Task<EngineeringProjectPublication?> PublishRevisionAsync(
            string projectKey,
            long revision,
            string? publishedBy = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EngineeringProjectActivation?> GetActivationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(projectKey == snapshot.ProjectKey ? _activation : null);

        public Task<EngineeringProjectActivation?> RecordActivationAsync(
            string projectKey,
            long revision,
            string? activatedBy = null,
            CancellationToken cancellationToken = default)
        {
            if (!allowActivation || projectKey != snapshot.ProjectKey || revision != _publication.PublishedRevision)
                return Task.FromResult<EngineeringProjectActivation?>(null);

            _activation = new EngineeringProjectActivation(
                projectKey,
                revision,
                DateTimeOffset.UtcNow,
                activatedBy);
            return Task.FromResult<EngineeringProjectActivation?>(_activation);
        }
    }
}
