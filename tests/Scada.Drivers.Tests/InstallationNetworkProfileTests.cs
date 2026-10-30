using Microsoft.Extensions.Configuration;
using Scada.Api.Engineering;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Bacnet;
using Scada.Drivers.Modbus;

namespace Scada.Drivers.Tests;

public sealed class InstallationNetworkProfileTests
{
    [Fact]
    public void Read_StableDockerProfile_ReportsPublishedAndInternalPortsWithoutSecrets()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InstallationNetwork:Profile"] = "stable-docker",
                ["InstallationNetwork:InstanceId"] = "plant-a",
                ["InstallationNetwork:WebBindAddress"] = "127.0.0.1",
                ["InstallationNetwork:WebPublishedPort"] = "28080",
                ["InstallationNetwork:ApiBindAddress"] = "0.0.0.0",
                ["InstallationNetwork:ApiPublishedPort"] = "25080",
                ["InstallationNetwork:DatabasePublishedPort"] = "25432",
                ["InstallationNetwork:DriverPorts"] = "tcp:127.0.0.1:2502:1502;udp:0.0.0.0:14780:47808",
                ["ConnectionStrings:EliteScada"] = "Host=db;Password=must-not-leak"
            })
            .Build();

        var profile = InstallationNetworkProfileApi.Read(configuration);

        Assert.NotNull(profile);
        Assert.Equal("plant-a", profile.InstanceId);
        Assert.Equal(("127.0.0.1", 28080, 80),
            (profile.Web.BindAddress, profile.Web.PublishedPort, profile.Web.InternalPort));
        Assert.Equal(("0.0.0.0", 25080, 5080),
            (profile.Api.BindAddress, profile.Api.PublishedPort, profile.Api.InternalPort));
        Assert.Equal(("127.0.0.1", 25432, 5432),
            (profile.Database.BindAddress, profile.Database.PublishedPort, profile.Database.InternalPort));
        Assert.False(profile.MediaControl.PublishedToHost);
        Assert.False(profile.MediaPlayback.PublishedToHost);
        Assert.Collection(profile.DriverPorts,
            tcp => Assert.Equal(("TCP", "127.0.0.1", 2502, 1502, true), (tcp.Transport, tcp.BindAddress, tcp.PublishedPort, tcp.InternalPort, tcp.PublishedToHost)),
            udp => Assert.Equal(("UDP", "0.0.0.0", 14780, 47808, true), (udp.Transport, udp.BindAddress, udp.PublishedPort, udp.InternalPort, udp.PublishedToHost)));
        Assert.False(profile.ApplyThroughUi);
        Assert.False(profile.HostMappingIndependentlyVerified);
        Assert.DoesNotContain("must-not-leak", System.Text.Json.JsonSerializer.Serialize(profile));
    }

    [Fact]
    public void Read_DriverPortSettings_IgnoresMalformedEntries()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InstallationNetwork:Profile"] = "stable-docker",
                ["InstallationNetwork:DriverPorts"] = "tcp:127.0.0.1:2502:1502;tcp:127.0.0.1:1502:502;tcp:host.invalid:1:2;icmp:127.0.0.1:10:20;udp:0.0.0.0:0:47808;tcp:127.0.0.1:15080:5080"
            })
            .Build();

        var profile = Assert.IsType<InstallationNetworkProfileView>(InstallationNetworkProfileApi.Read(configuration));

        var mapping = Assert.Single(profile.DriverPorts);
        Assert.Equal(("TCP", "127.0.0.1", 2502, 1502), (mapping.Transport, mapping.BindAddress, mapping.PublishedPort, mapping.InternalPort));
    }

    [Fact]
    public void Read_ReportsConfiguredModbusAndBacnetListenersFromActiveRuntime()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InstallationNetwork:Profile"] = "stable-docker"
            })
            .Build();
        var now = DateTimeOffset.UtcNow;
        var diagnostics = new[]
        {
            new CommunicationDriverDiagnosticSnapshot(
                DataSourceKey: "modbus.tcp.server:plant",
                DataSourceName: "Plant Modbus listener",
                DriverType: ModbusTcpServerDriverDescriptorProvider.DriverTypeId,
                RuntimeInstanceId: "runtime-instance",
                Endpoint: "0.0.0.0:1502",
                State: CommunicationDriverOperationalState.Healthy,
                StateChangedAt: now,
                CapturedAt: now,
                LastSuccessfulCommunicationAt: null,
                LastFailedCommunicationAt: null,
                LastError: null,
                DataAge: null,
                ConfiguredScanInterval: null,
                LastOperationDuration: null,
                AverageOperationDuration: null,
                LastScanDuration: null,
                RecentFailureRate: 0,
                AssociatedTagCount: 1,
                TagQuality: new CommunicationTagQualitySummary(0, 0, 0, 0, 0, 0, 0, 0, 0),
                Counters: new CommunicationDriverCounters(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)),
            new CommunicationDriverDiagnosticSnapshot(
                DataSourceKey: "modbus.tcp:equipment",
                DataSourceName: "Outbound Modbus client",
                DriverType: ModbusTcpDriverDescriptorProvider.DriverTypeId,
                RuntimeInstanceId: "runtime-client",
                Endpoint: "192.0.2.10:502",
                State: CommunicationDriverOperationalState.Healthy,
                StateChangedAt: now,
                CapturedAt: now,
                LastSuccessfulCommunicationAt: null,
                LastFailedCommunicationAt: null,
                LastError: null,
                DataAge: null,
                ConfiguredScanInterval: null,
                LastOperationDuration: null,
                AverageOperationDuration: null,
                LastScanDuration: null,
                RecentFailureRate: 0,
                AssociatedTagCount: 1,
                TagQuality: new CommunicationTagQualitySummary(0, 0, 0, 0, 0, 0, 0, 0, 0),
                Counters: new CommunicationDriverCounters(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)),
            new CommunicationDriverDiagnosticSnapshot(
                DataSourceKey: "bacnet.ip:plant",
                DataSourceName: "Plant BACnet/IP",
                DriverType: BacnetDriverDescriptor.DriverType,
                RuntimeInstanceId: "runtime-bacnet",
                Endpoint: "device:1201",
                State: CommunicationDriverOperationalState.Healthy,
                StateChangedAt: now,
                CapturedAt: now,
                LastSuccessfulCommunicationAt: now,
                LastFailedCommunicationAt: null,
                LastError: null,
                DataAge: null,
                ConfiguredScanInterval: null,
                LastOperationDuration: null,
                AverageOperationDuration: null,
                LastScanDuration: null,
                RecentFailureRate: 0,
                AssociatedTagCount: 1,
                TagQuality: new CommunicationTagQualitySummary(0, 0, 0, 0, 0, 0, 0, 0, 0),
                Counters: new CommunicationDriverCounters(1, 1, 1, 0, 0, 0, 1, 0, 0, 1, 0, 1),
                ProtocolDetails: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["localUdpPort"] = "47808",
                    ["localUdpBindAddress"] = "0.0.0.0"
                })
        };

        var profile = Assert.IsType<InstallationNetworkProfileView>(
            InstallationNetworkProfileApi.Read(configuration, diagnostics));

        Assert.Collection(profile.ConfiguredDriverListeners,
            listener => Assert.Equal(("Plant Modbus listener", "TCP", "0.0.0.0", 1502, "Healthy"),
                (listener.DataSourceName, listener.Transport, listener.BindAddress, listener.InternalPort, listener.State)),
            listener => Assert.Equal(("Plant BACnet/IP", "UDP", "0.0.0.0", 47808, "Healthy"),
                (listener.DataSourceName, listener.Transport, listener.BindAddress, listener.InternalPort, listener.State)));
    }

    [Fact]
    public void Read_UnsupportedOrMalformedProfile_FailsClosedToUnsupportedOrKnownDefaults()
    {
        var unsupported = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InstallationNetwork:Profile"] = "native-service"
            })
            .Build();
        Assert.Null(InstallationNetworkProfileApi.Read(unsupported));

        var malformed = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InstallationNetwork:Profile"] = "stable-docker",
                ["InstallationNetwork:WebPublishedPort"] = "70000",
                ["InstallationNetwork:ApiPublishedPort"] = "not-a-port"
            })
            .Build();
        var profile = Assert.IsType<InstallationNetworkProfileView>(InstallationNetworkProfileApi.Read(malformed));
        Assert.Equal(18080, profile.Web.PublishedPort);
        Assert.Equal(15080, profile.Api.PublishedPort);
    }
}
