using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Sidecars;
using Scada.Drivers.Abstractions;
using Scada.Drivers.HostResources;
using Scada.Drivers.Serial;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class ZigbeeNativeRuntimeTests
{
    private const string DataSourceKey = "zigbee.home";
    private const string NetworkIdentity = "zigbee-network:home-01";
    private const string IeeeAddress = "00124b0000000001";

    [Fact]
    public void Planner_UsesStableDataSourceAssociationAndPlansOnlyBoundedBooleanState()
    {
        var fixture = CreateEngineering();
        var result = new ZigbeeNativeCommunicationRuntimePlanner().Plan(fixture.Package, fixture.DataSource);

        Assert.True(result.CanActivate, string.Join(" | ", result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<ZigbeeNativeCommunicationRuntimePlan>(result.Plan);
        var point = Assert.Single(plan.Points);
        Assert.Equal(IeeeAddress, point.IeeeAddress);
        Assert.Equal(1, point.Endpoint);
        Assert.True(point.Writable);
        Assert.Equal("protected-material-v1:zigbee-network-key", plan.NetworkKeyReference);
        Assert.Equal(NetworkIdentity, plan.NetworkIdentity);
    }

    [Fact]
    public void Planner_RejectsStaleStableDataSourceIdentityEvenWhenLegacyKeyMatches()
    {
        var fixture = CreateEngineering();
        var staleTag = fixture.Tag with { DataSourceId = Guid.NewGuid() };
        var package = fixture.Package with { Tags = [staleTag] };

        var result = new ZigbeeNativeCommunicationRuntimePlanner().Plan(package, fixture.DataSource);

        Assert.False(result.CanActivate);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue => issue.Code == "ZIGBEE_NATIVE_TAG_COUNT_INVALID");
    }

    [Fact]
    public void Planner_RejectsNonBooleanAndDuplicatePhysicalBindings()
    {
        var fixture = CreateEngineering();
        var nonBoolean = fixture.Tag with { DataType = TagDataType.Int32 };
        var result = new ZigbeeNativeCommunicationRuntimePlanner().Plan(
            fixture.Package with { Tags = [nonBoolean] },
            fixture.DataSource);

        Assert.False(result.CanActivate);
        Assert.Contains(result.Issues, issue => issue.Code == "ZIGBEE_NATIVE_TAG_TYPE_UNSUPPORTED");

        var duplicate = fixture.Tag with { Id = Guid.NewGuid(), Path = "Home.Light.OutputCopy" };
        var duplicateResult = new ZigbeeNativeCommunicationRuntimePlanner().Plan(
            fixture.Package with { Tags = [fixture.Tag, duplicate] },
            fixture.DataSource);

        Assert.False(duplicateResult.CanActivate);
        Assert.Contains(duplicateResult.Issues, issue => issue.Code == "ZIGBEE_NATIVE_TAG_ADDRESS_DUPLICATE");
    }

    [Fact]
    public void Factory_RejectsCoordinatorOutsideQualifiedFamilyOrNetworkIdentity()
    {
        var fixture = CreateEngineering();
        var plan = Assert.IsType<ZigbeeNativeCommunicationRuntimePlan>(
            new ZigbeeNativeCommunicationRuntimePlanner().Plan(fixture.Package, fixture.DataSource).Plan);

        var wrongFamilyRegistry = CreateCoordinatorRegistry("ember", "CC2652P2", NetworkIdentity);
        var wrongFamilyFactory = CreateFactory(wrongFamilyRegistry);
        var familyError = Assert.Throws<NotSupportedException>(() =>
            wrongFamilyFactory.Create(plan, CreateServices(() => false)));
        Assert.Contains("TI zStack/CC2652", familyError.Message, StringComparison.OrdinalIgnoreCase);

        var wrongNetworkRegistry = CreateCoordinatorRegistry("ti-zStack", "CC2652P2", "zigbee-network:other");
        var wrongNetworkFactory = CreateFactory(wrongNetworkRegistry);
        var networkError = Assert.Throws<InvalidOperationException>(() =>
            wrongNetworkFactory.Create(plan, CreateServices(() => false)));
        Assert.Contains("does not match", networkError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Driver_StandbyCannotStartOrWriteAndWriteRequiresMatchingPhysicalReadback()
    {
        var fixture = CreateEngineering();
        var plan = Assert.IsType<ZigbeeNativeCommunicationRuntimePlan>(
            new ZigbeeNativeCommunicationRuntimePlanner().Plan(fixture.Package, fixture.DataSource).Plan);
        var canOwnEffects = false;
        var services = CreateServices(() => canOwnEffects);
        var runtime = new FakeNativeZigbeeSession { WriteReadback = false };
        await using var driver = new ZigbeeNativeCommunicationDriver(plan, services, runtime);

        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.StartAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await driver.WriteAsync(fixture.TagId, true));
        Assert.Equal(0, runtime.StartCount);
        Assert.Equal(0, runtime.WriteCount);
        Assert.Equal(DriverState.Stopped, driver.Status.State);

        canOwnEffects = true;
        await driver.StartAsync();
        Assert.Equal(1, runtime.StartCount);

        runtime.Publish(new NativeZigbeeObservation("00124b00000000ff", 1, true, "device-report", DateTimeOffset.UtcNow));
        Assert.False(services.Cache.TryGet(fixture.TagId, out _));
        runtime.Publish(new NativeZigbeeObservation(IeeeAddress, 1, true, "device-report", DateTimeOffset.UtcNow));
        await WaitUntilAsync(() => services.Cache.TryGet(fixture.TagId, out var report) && report?.Source == "zigbee.native.device-report");
        Assert.True(services.Cache.TryGet(fixture.TagId, out var reported));
        Assert.True(Assert.IsType<bool>(reported!.Value));

        var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await driver.WriteAsync(fixture.TagId, true));
        Assert.Contains("did not reconcile", mismatch.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(services.Cache.TryGet(fixture.TagId, out var observed));
        Assert.False(Assert.IsType<bool>(observed!.Value));
        Assert.Equal(TagQuality.Good, observed.Quality);
        Assert.Equal("zigbee.native.write-readback", observed.Source);

        runtime.WriteReadback = true;
        await driver.WriteAsync(fixture.TagId, true);
        Assert.True(services.Cache.TryGet(fixture.TagId, out observed));
        Assert.True(Assert.IsType<bool>(observed!.Value));
        Assert.Equal(2, runtime.WriteCount);
    }

    [Fact]
    public async Task Driver_PromotionAndDemotionUseLifecycleAndCommittedRuntimeSurvivesRequestCancellation()
    {
        var fixture = CreateEngineering();
        var plan = Assert.IsType<ZigbeeNativeCommunicationRuntimePlan>(
            new ZigbeeNativeCommunicationRuntimePlanner().Plan(fixture.Package, fixture.DataSource).Plan);
        var canOwnEffects = false;
        var services = CreateServices(() => canOwnEffects);
        var runtime = new FakeNativeZigbeeSession();
        await using var driver = new ZigbeeNativeCommunicationDriver(plan, services, runtime);
        using var activationRequest = new CancellationTokenSource();

        await Assert.ThrowsAsync<InvalidOperationException>(() => driver.StartAsync(activationRequest.Token));
        Assert.Equal(0, runtime.StartCount);
        Assert.False(runtime.IsRunning);

        canOwnEffects = true;
        await driver.StartAsync(activationRequest.Token);
        activationRequest.Cancel();
        Assert.True(runtime.IsRunning);
        Assert.Equal(DriverState.Running, driver.Status.State);

        canOwnEffects = false;
        await driver.StopAsync();
        Assert.False(runtime.IsRunning);
        Assert.Equal(1, runtime.StopCount);
        Assert.Equal(DriverState.Stopped, driver.Status.State);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await driver.WriteAsync(fixture.TagId, true));

        canOwnEffects = true;
        await driver.StartAsync();
        Assert.Equal(2, runtime.StartCount);
        Assert.True(runtime.IsRunning);
        await driver.StopAsync();
        Assert.False(runtime.IsRunning);
        Assert.Equal(2, runtime.StopCount);
    }

    [Fact]
    public async Task Driver_StartFailureIsVisibleAndDisposeStillReleasesRuntime()
    {
        var fixture = CreateEngineering();
        var plan = Assert.IsType<ZigbeeNativeCommunicationRuntimePlan>(
            new ZigbeeNativeCommunicationRuntimePlanner().Plan(fixture.Package, fixture.DataSource).Plan);
        var services = CreateServices(() => true);
        var runtime = new FakeNativeZigbeeSession
        {
            StartFailure = new IOException("sidecar readiness failed")
        };
        var driver = new ZigbeeNativeCommunicationDriver(plan, services, runtime);

        var failure = await Assert.ThrowsAsync<IOException>(() => driver.StartAsync());
        Assert.Contains("sidecar readiness failed", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DriverState.Faulted, driver.Status.State);
        Assert.False(runtime.IsRunning);

        await driver.DisposeAsync();
        Assert.Equal(1, runtime.DisposeCount);
    }

    [Fact]
    public async Task Driver_ReadAndStopFailuresRemainVisibleAndDoNotPublishGoodState()
    {
        var fixture = CreateEngineering();
        var plan = Assert.IsType<ZigbeeNativeCommunicationRuntimePlan>(
            new ZigbeeNativeCommunicationRuntimePlanner().Plan(fixture.Package, fixture.DataSource).Plan);
        var services = CreateServices(() => true);
        var runtime = new FakeNativeZigbeeSession
        {
            ReadFailure = new TimeoutException("device read timeout")
        };
        await using var driver = new ZigbeeNativeCommunicationDriver(plan, services, runtime);
        await driver.StartAsync();

        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await driver.ReadAsync(fixture.TagId));
        Assert.True(services.Cache.TryGet(fixture.TagId, out var failedRead));
        Assert.Null(failedRead!.Value);
        Assert.Equal(TagQuality.BadCommunication, failedRead.Quality);
        Assert.Equal(1, driver.GetCommunicationDiagnostics().Counters.FailedOperations);

        runtime.ReadFailure = null;
        runtime.StopFailure = new IOException("coordinator close failed");
        await Assert.ThrowsAsync<IOException>(() => driver.StopAsync());
        Assert.Equal(DriverState.Faulted, driver.Status.State);
        Assert.Contains("coordinator close failed", driver.GetCommunicationDiagnostics().LastError);
        await driver.DisposeAsync();
        Assert.Equal(1, runtime.DisposeCount);
    }

    [Fact]
    public async Task RpcServer_AuthenticatesLoopbackChildAndReturnsDeviceReadback()
    {
        await using var server = new ZigbeeNativeRpcServer();
        server.Start();

        using (var rejectedClient = new TcpClient())
        {
            await rejectedClient.ConnectAsync(IPAddress.Loopback, server.Port);
            var stream = rejectedClient.GetStream();
            using var rejectedReader = new StreamReader(stream, new UTF8Encoding(false), leaveOpen: true);
            await using var rejectedWriter = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            await rejectedWriter.WriteLineAsync(JsonSerializer.Serialize(new
            {
                type = "hello",
                token = new string('f', 64),
                artifactId = "elitescada.zigbee.native",
                artifactVersion = "1.0.0",
                schemaVersion = 1
            }));
            Assert.Null(await rejectedReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2)));
        }

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, server.Port);
        var clientStream = client.GetStream();
        using var reader = new StreamReader(clientStream, new UTF8Encoding(false), leaveOpen: true);
        await using var writer = new StreamWriter(clientStream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            type = "hello",
            token = server.Token,
            artifactId = "elitescada.zigbee.native",
            artifactVersion = "1.0.0",
            schemaVersion = 1
        }));

        var welcomeLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2))
            ?? throw new IOException("Native Zigbee RPC server closed before the welcome frame.");
        using (var welcome = JsonDocument.Parse(welcomeLine))
            Assert.Equal("welcome", welcome.RootElement.GetProperty("type").GetString());

        var readTask = server.ReadStateAsync(IeeeAddress, 1).AsTask();
        var requestLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2))
            ?? throw new IOException("Native Zigbee RPC server closed before the request frame.");
        using (var request = JsonDocument.Parse(requestLine))
        {
            var root = request.RootElement;
            Assert.Equal("read", root.GetProperty("method").GetString());
            Assert.Equal(IeeeAddress, root.GetProperty("ieee").GetString());
            await writer.WriteLineAsync(JsonSerializer.Serialize(new
            {
                type = "response",
                id = root.GetProperty("id").GetString(),
                ok = true,
                accepted = false,
                value = true
            }));
        }

        Assert.True(await readTask.WaitAsync(TimeSpan.FromSeconds(2)));
        await server.StopAsync();
        Assert.Throws<InvalidOperationException>(() => _ = server.Port);
    }

    private static (EngineeringPackage Package, DataSourceEngineeringDto DataSource, TagEngineeringDto Tag, Guid TagId)
        CreateEngineering()
    {
        var dataSourceId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var dataSource = new DataSourceEngineeringDto(
            dataSourceId,
            DataSourceKey,
            "Home Zigbee",
            ZigbeeNativeDriverDescriptorProvider.DriverType,
            Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["hostResourceId"] = "zigbee-main",
                ["networkIdentity"] = NetworkIdentity,
                ["panId"] = "0x1a62",
                ["extendedPanId"] = "00124b0001abcdef",
                ["channel"] = "15",
                ["baudRate"] = "115200"
            },
            SecretReferences: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["networkKey"] = "protected-material-v1:zigbee-network-key"
            });
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            ZigbeeNativeDriverDescriptorProvider.TagBindingSchemaId,
            SchemaVersion: 1,
            $"{IeeeAddress}/1/genOnOff/onOff");
        var tag = new TagEngineeringDto(
            tagId,
            "Output",
            "Home.Light.Output",
            TagDataType.Boolean,
            Source: DataSourceKey,
            ReadOnly: false,
            CommunicationBinding: binding,
            DataSourceId: dataSourceId);
        var package = new EngineeringPackage(
            "scada.engineering",
            15,
            DateTimeOffset.UtcNow,
            [tag],
            Array.Empty<AlarmEngineeringDto>(),
            [dataSource]);
        return (package, dataSource, tag, tagId);
    }

    private static CommunicationDriverRuntimeServices CreateServices(Func<bool> authority) => new(
        "project-zigbee",
        new CurrentTagCache(new InMemoryScadaEventBus()),
        new InMemoryTagRegistry(),
        EffectAuthority: authority);

    private static HostResourceRegistry CreateCoordinatorRegistry(string family, string model, string networkIdentity)
    {
        var registry = new HostResourceRegistry();
        var descriptor = ZigbeeCoordinatorHostResource.Create(
            "zigbee-main",
            HostResourceLocator.FromSerialPort(new HostSerialLineSettings("/dev/ttyACM0")),
            new HostResourcePhysicalIdentity("usb:ti:0001"),
            family,
            model,
            "2026.1",
            networkIdentity);
        registry.Observe(descriptor);
        return registry;
    }

    private static ZigbeeNativeCommunicationRuntimeFactory CreateFactory(HostResourceRegistry registry) => new(
        registry,
        new HostResourceLeaseCoordinator(registry),
        new ZigbeeNativeSidecarDeploymentOptions("node", "/not-started", "/not-started/state"));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
                throw new TimeoutException("Expected Native Zigbee report was not published.");
            await Task.Delay(10);
        }
    }

    private sealed class FakeNativeZigbeeSession : INativeZigbeeSidecarSession
    {
        private static readonly CommunicationDriverDiagnosticSnapshot EmptyDiagnostics = CreateEmptyDiagnostics();

        private Action<NativeZigbeeObservation>? _observationReceived;
        public event Action<NativeZigbeeObservation>? ObservationReceived
        {
            add => _observationReceived += value;
            remove => _observationReceived -= value;
        }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int WriteCount { get; private set; }
        public int DisposeCount { get; private set; }
        public bool IsRunning { get; private set; }
        public bool ReadValue { get; set; }
        public bool WriteReadback { get; set; }
        public Exception? StartFailure { get; set; }
        public Exception? ReadFailure { get; set; }
        public Exception? StopFailure { get; set; }

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCount++;
            if (StartFailure is not null) throw StartFailure;
            IsRunning = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            if (StopFailure is not null) throw StopFailure;
            IsRunning = false;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> ReadStateAsync(ZigbeeNativePoint point, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ReadFailure is not null) throw ReadFailure;
            return ValueTask.FromResult(ReadValue);
        }

        public ValueTask<bool> WriteAndReadBackAsync(
            ZigbeeNativePoint point,
            bool value,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCount++;
            return ValueTask.FromResult(WriteReadback);
        }

        public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics() => EmptyDiagnostics;

        public void Publish(NativeZigbeeObservation observation) => _observationReceived?.Invoke(observation);

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            IsRunning = false;
            return ValueTask.CompletedTask;
        }

        private static CommunicationDriverDiagnosticSnapshot CreateEmptyDiagnostics()
        {
            var now = DateTimeOffset.UtcNow;
            return new CommunicationDriverDiagnosticSnapshot(
                "",
                "",
                "",
                "",
                null,
                CommunicationDriverOperationalState.Stopped,
                now,
                now,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                0,
                new CommunicationTagQualitySummary(0, 0, 0, 0, 0, 0, 0, 0, 0),
                new CommunicationDriverCounters(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        }
    }
}
