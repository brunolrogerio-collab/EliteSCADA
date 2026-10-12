using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Sidecars;
using Scada.Drivers.Abstractions;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class KnxIpRuntimeTests
{
    [Fact]
    public void PlannerAcceptsOnlyBoundedSecureReferencesAndDistinctCommandAndStateAddresses()
    {
        var fixture = CreateEngineering();
        var result = new KnxIpCommunicationRuntimePlanner().Plan(fixture.Package, fixture.Source);

        Assert.True(result.CanActivate, string.Join(" | ", result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<KnxIpCommunicationRuntimePlan>(result.Plan);
        var point = Assert.Single(plan.Points);
        Assert.Equal("9.001", point.Dpt);
        Assert.Equal("1/2/3", point.WriteGroupAddress);
        Assert.Equal("1/2/4", point.StateGroupAddress);
        Assert.Equal(HostMaterialSlotReference.Marker, plan.UserPasswordReference);
        Assert.Equal(HostMaterialSlotReference.Marker, plan.DeviceAuthenticationReference);
        Assert.Equal(HostMaterialSlotReference.Marker, plan.DataSecureKeyringReference);

        var unsupported = fixture.Source with
        {
            SecretReferences = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["userPassword"] = HostMaterialSlotReference.Marker,
                ["deviceAuthentication"] = HostMaterialSlotReference.Marker,
                ["dataSecureKeyring"] = HostMaterialSlotReference.Marker,
                ["password"] = "legacy://secret"
            }
        };
        var rejected = new KnxIpCommunicationRuntimePlanner().Plan(fixture.Package, unsupported);
        Assert.False(rejected.CanActivate);
        Assert.Contains(rejected.Issues, issue => issue.Code == "KNX_IP_SECRET_FIELD_UNSUPPORTED");
    }

    [Theory]
    [InlineData("9.999", "KNX_IP_TAG_BINDING_SETTINGS_INVALID")]
    [InlineData("9.001", "KNX_IP_WRITE_STATE_ADDRESS_COLLISION")]
    public void PlannerRejectsUnsupportedDptAndCommandStateCollision(string dpt, string expectedIssue)
    {
        var fixture = CreateEngineering();
        var settings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["dpt"] = dpt,
            ["stateGroupAddress"] = dpt == "9.001" ? "1/2/3" : "1/2/4"
        };
        var tag = fixture.Tag with
        {
            CommunicationBinding = fixture.Tag.CommunicationBinding! with { Settings = settings }
        };
        var package = fixture.Package with { Tags = [tag] };

        var result = new KnxIpCommunicationRuntimePlanner().Plan(package, fixture.Source);

        Assert.False(result.CanActivate);
        Assert.Contains(result.Issues, issue => issue.Code == expectedIssue);
    }

    [Fact]
    public void PlannerRejectsCredentialSettingsAndCrossPointCommandStateCollisions()
    {
        var fixture = CreateEngineering();
        var withCredential = fixture.Source with
        {
            Settings = new Dictionary<string, string>(fixture.Source.Settings!, StringComparer.OrdinalIgnoreCase)
            {
                ["userPassword"] = "must-not-be-in-engineering"
            }
        };
        var credentialResult = new KnxIpCommunicationRuntimePlanner().Plan(fixture.Package, withCredential);
        Assert.Contains(credentialResult.Issues, issue => issue.Code == "KNX_IP_SETTING_UNSUPPORTED");

        var secondTag = fixture.Tag with
        {
            Id = Guid.NewGuid(),
            Path = "Home.Living.Other",
            Name = "Other",
            ReadOnly = true,
            Address = "1/2/5",
            CommunicationBinding = fixture.Tag.CommunicationBinding! with
            {
                PortableAddress = "1/2/5",
                Settings = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["dpt"] = "9.001",
                    ["stateGroupAddress"] = "1/2/3"
                }
            }
        };
        var package = fixture.Package with { Tags = [fixture.Tag, secondTag] };
        var collisionResult = new KnxIpCommunicationRuntimePlanner().Plan(package, fixture.Source);
        Assert.Contains(collisionResult.Issues, issue => issue.Code == "KNX_IP_WRITE_STATE_ADDRESS_COLLISION");
    }

    [Fact]
    public async Task DriverUsesOnlySecureObservationsAndRequiresAuthoritativeWriteReadback()
    {
        var fixture = CreateEngineering();
        var plan = Assert.IsType<KnxIpCommunicationRuntimePlan>(
            new KnxIpCommunicationRuntimePlanner().Plan(fixture.Package, fixture.Source).Plan);
        var canOwn = true;
        var services = CreateServices(() => canOwn);
        var session = new FakeKnxSession { ReadValue = 2.5f, WriteReadback = 2.5f };
        await using var driver = new KnxIpCommunicationDriver(plan, services, session);

        await driver.StartAsync();
        await driver.ReadAsync(fixture.TagId);
        Assert.True(services.Cache.TryGet(fixture.TagId, out var read));
        Assert.Equal(2.5f, read!.Value);
        Assert.Equal("knx.ip.secure-readback", read.Source);

        session.Publish(new KnxIpObservation(fixture.TagId, 88.0f, DateTimeOffset.UtcNow));
        await WaitUntilAsync(() => services.Cache.TryGet(fixture.TagId, out var observed) && observed?.Source == "knx.ip.data-secure-telegram");
        Assert.True(services.Cache.TryGet(fixture.TagId, out var current));
        Assert.Equal(88.0f, current!.Value);
        Assert.Equal(TagQuality.Good, current.Quality);

        session.WriteReadback = 4.0f;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await driver.WriteAsync(fixture.TagId, 2.5f));
        Assert.True(services.Cache.TryGet(fixture.TagId, out current));
        Assert.Equal(88.0f, current!.Value);

        session.WriteReadback = 2.501f;
        await driver.WriteAsync(fixture.TagId, 2.5f);
        Assert.True(services.Cache.TryGet(fixture.TagId, out current));
        Assert.Equal("knx.ip.secure-write-readback", current!.Source);
        Assert.Equal(2.501f, current.Value);

        canOwn = false;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await driver.WriteAsync(fixture.TagId, 2.5f));
        Assert.Equal(2, session.WriteCount);
    }

    [Fact]
    public async Task RpcAuthenticatesTheChildAndRejectsWriteWithoutAcceptedReadback()
    {
        var fixture = CreateEngineering();
        var plan = Assert.IsType<KnxIpCommunicationRuntimePlan>(
            new KnxIpCommunicationRuntimePlanner().Plan(fixture.Package, fixture.Source).Plan);
        var point = Assert.Single(plan.Points);
        await using var rpc = new KnxIpRpcServer(plan.Points);
        KnxIpObservation? observation = null;
        rpc.ObservationReceived += value => observation = value;
        rpc.Start();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, rpc.Port);
        var stream = client.GetStream();
        using var reader = new StreamReader(stream, new UTF8Encoding(false), leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            type = "hello",
            token = rpc.Token,
            artifactId = "elitescada.knx.ip",
            artifactVersion = "1.0.0",
            runtimeVersion = "3.13.x",
            schemaVersion = 1
        }));
        var welcomeLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2))
            ?? throw new IOException("KNX/IP RPC server closed before welcome.");
        using (var welcome = JsonDocument.Parse(welcomeLine))
            Assert.Equal("welcome", welcome.RootElement.GetProperty("type").GetString());

        var read = rpc.ReadAsync(point).AsTask();
        var readRequestLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2))
            ?? throw new IOException("KNX/IP RPC server closed before read request.");
        using (var request = JsonDocument.Parse(readRequestLine))
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new
            {
                type = "response", id = request.RootElement.GetProperty("id").GetString(),
                ok = true, accepted = false, value = 18.5
            }));
        }
        Assert.Equal(18.5d, await read.WaitAsync(TimeSpan.FromSeconds(2)));

        var write = rpc.WriteAndReadBackAsync(point, 18.5f).AsTask();
        var writeRequestLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2))
            ?? throw new IOException("KNX/IP RPC server closed before write request.");
        using (var request = JsonDocument.Parse(writeRequestLine))
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new
            {
                type = "response", id = request.RootElement.GetProperty("id").GetString(),
                ok = true, accepted = false, value = 18.5
            }));
        }
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => write.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal("KNX_IP_WRITE_NOT_ACCEPTED", failure.Message);

        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            type = "report", tagId = fixture.TagId, source = "data-secure-status",
            secure = true, value = 1.5, observedAt = DateTimeOffset.UtcNow
        }));
        await WaitUntilAsync(() => observation is not null);
        Assert.Equal(fixture.TagId, observation!.TagId);
        Assert.Equal(1.5d, observation.Value);
        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            type = "report", tagId = fixture.TagId, source = "data-secure-status",
            secure = false, value = 999.0
        }));
        await Task.Delay(30);
        Assert.Equal(1.5d, observation.Value);
    }

    [Fact]
    public void CanonicalRuntimeCompositionIncludesKnxIpDescriptorAndPlanner()
    {
        var components = CommunicationDriverRuntimeComposition.BuildForCurrentSchema();

        var registration = components.GetRequired(KnxIpDriverDescriptorProvider.DriverType);
        Assert.IsType<KnxIpCommunicationRuntimePlanner>(registration.Planner);
        Assert.Equal(KnxIpDriverDescriptorProvider.DriverType, registration.Descriptor.DriverType);
    }

    private static (EngineeringPackage Package, DataSourceEngineeringDto Source, TagEngineeringDto Tag, Guid TagId) CreateEngineering()
    {
        var sourceId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var source = new DataSourceEngineeringDto(
            sourceId,
            "knx.living",
            "KNX Living Room",
            KnxIpDriverDescriptorProvider.DriverType,
            Settings: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["gatewayHost"] = "192.0.2.10",
                ["gatewayPort"] = "3671",
                ["individualAddress"] = "1.1.10",
                ["userId"] = "1"
            },
            SecretReferences: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["userPassword"] = HostMaterialSlotReference.Marker,
                ["deviceAuthentication"] = HostMaterialSlotReference.Marker,
                ["dataSecureKeyring"] = HostMaterialSlotReference.Marker
            });
        var binding = new CommunicationTagBinding(
            CommunicationTagBinding.CurrentContractVersion,
            KnxIpDriverDescriptorProvider.TagBindingSchemaId,
            SchemaVersion: 1,
            "1/2/3",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dpt"] = "9.001",
                ["stateGroupAddress"] = "1/2/4"
            });
        var tag = new TagEngineeringDto(
            tagId,
            "Setpoint",
            "Home.Living.Setpoint",
            TagDataType.Float,
            Source: source.Key,
            Address: binding.PortableAddress,
            ReadOnly: false,
            CommunicationBinding: binding,
            DataSourceId: sourceId);
        var package = new EngineeringPackage("scada.engineering", 15, DateTimeOffset.UtcNow,
            [tag], Array.Empty<AlarmEngineeringDto>(), [source]);
        return (package, source, tag, tagId);
    }

    private static CommunicationDriverRuntimeServices CreateServices(Func<bool> authority) => new(
        "project-knx",
        new CurrentTagCache(new InMemoryScadaEventBus()),
        new InMemoryTagRegistry(),
        EffectAuthority: authority);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline) throw new TimeoutException("KNX/IP observation was not delivered.");
            await Task.Delay(10);
        }
    }

    private sealed class FakeKnxSession : IKnxIpSidecarSession
    {
        private static readonly CommunicationDriverDiagnosticSnapshot EmptyDiagnostics = CreateDiagnostics();
        private Action<KnxIpObservation>? _observations;
        public event Action<KnxIpObservation>? ObservationReceived
        {
            add => _observations += value;
            remove => _observations -= value;
        }
        public float ReadValue { get; set; }
        public float WriteReadback { get; set; }
        public int WriteCount { get; private set; }

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
        public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<object> ReadAsync(KnxIpPoint point, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<object>(ReadValue);
        }
        public ValueTask<object> WriteAndReadBackAsync(KnxIpPoint point, object value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCount++;
            return ValueTask.FromResult<object>(WriteReadback);
        }
        public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics() => EmptyDiagnostics;
        public void Publish(KnxIpObservation value) => _observations?.Invoke(value);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static CommunicationDriverDiagnosticSnapshot CreateDiagnostics()
        {
            var now = DateTimeOffset.UtcNow;
            return new CommunicationDriverDiagnosticSnapshot(
                "", "", "", "", null, CommunicationDriverOperationalState.Stopped, now, now,
                null, null, null, null, null, null, null, null, 0, 0,
                new CommunicationTagQualitySummary(0, 0, 0, 0, 0, 0, 0, 0, 0),
                new CommunicationDriverCounters(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        }
    }
}
