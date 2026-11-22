using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.ESPHome;
using Scada.Drivers.ESPHome.Protocol;

namespace Scada.Drivers.Tests;

public sealed class EspHomeEntityMappingTests
{
    [Fact]
    public void Materialization_MapsOnlyBoundedProcessSemantics()
    {
        var inventory = Inventory(
        [
            new(EspHomeEntityKind.Switch, 0x10, 0, "pump", "Pump", 0),
            new(EspHomeEntityKind.BinarySensor, 0x11, 0, "motion", "Motion", 0, "motion"),
            new(EspHomeEntityKind.Sensor, 0x12, 0, "temp", "Temperature", 0, "temperature", "°C"),
            new(EspHomeEntityKind.Light, 0x13, 0, "light", "Light", 0, SupportsBrightness: true, SupportsRgb: true),
            new(EspHomeEntityKind.Cover, 0x14, 0, "blind", "Blind", 0, SupportsPosition: true, SupportsStop: true),
            new(EspHomeEntityKind.Fan, 0x15, 0, "fan", "Fan", 0, SupportsFanSpeed: true, SupportedFanSpeedCount: 5),
            new(EspHomeEntityKind.Select, 0x16, 0, "mode", "Mode", 0, Options: ["auto", "manual"]),
            new(EspHomeEntityKind.Number, 0x17, 0, "config", "Config Number", 1, MinValue: 0, MaxValue: 10, Step: 1),
            new(EspHomeEntityKind.TextSensor, 0x18, 0, "wifi", "WiFi Info", 2),
            new(EspHomeEntityKind.Button, 0x19, 0, "restart", "Restart", 0),
            new(EspHomeEntityKind.Climate, 0x20, 0, "hvac", "HVAC", 0),
            new(EspHomeEntityKind.Lock, 0x21, 0, "lock", "Lock", 0)
        ]);

        var candidate = EspHomeEntityMapper.BuildMaterialization(inventory);

        Assert.Equal("AA:BB:CC:DD:EE:FF", candidate.Equipment.StableDeviceIdentity);
        Assert.Contains(candidate.Tags!, x => x.PortableAddress == "switch:0:00000010.state" && !x.ReadOnly);
        Assert.Contains(candidate.Tags!, x => x.PortableAddress == "sensor:0:00000012.state" && x.EngineeringUnit == "°C");
        Assert.Contains(candidate.Tags!, x => x.PortableAddress == "light:0:00000013.brightness" && !x.ReadOnly);
        Assert.Contains(candidate.Tags!, x => x.PortableAddress == "light:0:00000013.red" && x.ReadOnly);
        Assert.Contains(candidate.Tags!, x => x.PortableAddress == "cover:0:00000014.position" && !x.ReadOnly);
        Assert.Contains(candidate.Tags!, x => x.PortableAddress == "fan:0:00000015.speedLevel" && !x.ReadOnly);
        Assert.Contains(candidate.Tags!, x => x.PortableAddress == "select:0:00000016.state" && !x.ReadOnly);

        Assert.DoesNotContain(candidate.Tags!, x => x.PortableAddress == "number:0:00000017.state");
        Assert.DoesNotContain(candidate.Tags!, x => x.PortableAddress == "textsensor:0:00000018.state");
        Assert.DoesNotContain(candidate.Tags!, x => x.PortableAddress!.StartsWith("button:", StringComparison.Ordinal));

        Assert.Contains(candidate.Equipment.Capabilities!, x => x.Kind == "OnOff");
        Assert.Contains(candidate.Equipment.Capabilities!, x => x.Kind == "Motion");
        Assert.Contains(candidate.Equipment.Capabilities!, x => x.Kind == "Temperature");
        Assert.Contains(candidate.Equipment.Capabilities!, x => x.Kind == "ColorLight");
        Assert.Contains(candidate.Equipment.Capabilities!, x => x.Kind == "Cover");
        Assert.Contains(candidate.Equipment.Capabilities!, x => x.Kind == "Fan");

        Assert.Contains(candidate.Commands!, x => x.CandidateId == "switch:0:00000010.command.on");
        Assert.Contains(candidate.Commands!, x => x.CandidateId == "cover:0:00000014.command.open");
        Assert.Contains("parameterless-command-not-representable", candidate.Equipment.Metadata!["unmappedEntities"]);
        Assert.Contains("climate-traits-deferred", candidate.Equipment.Metadata!["unmappedEntities"]);
        Assert.Contains("lock-semantics-deferred", candidate.Equipment.Metadata!["unmappedEntities"]);
    }

    [Fact]
    public void BinarySensor_WithoutExplicitDeviceClass_GetsTagWithoutFalseCapability()
    {
        var inventory = Inventory([new(EspHomeEntityKind.BinarySensor, 0x30, 0, "input", "Input", 0)]);
        var candidate = EspHomeEntityMapper.BuildMaterialization(inventory);

        Assert.Single(candidate.Tags!);
        Assert.Empty(candidate.Equipment.Capabilities!);
    }

    [Theory]
    [InlineData("switch:0:00000010.state", EspHomeEntityKind.Switch, 0u, 0x10u, "state")]
    [InlineData("fan:2:ABCDEF01.speedLevel", EspHomeEntityKind.Fan, 2u, 0xABCDEF01u, "speedLevel")]
    public void EntityAddress_RoundTrips(string text, EspHomeEntityKind kind, uint device, uint key, string field)
    {
        Assert.True(EspHomeEntityAddress.TryParse(text, out var address));
        Assert.Equal(kind, address.Kind);
        Assert.Equal(device, address.DeviceId);
        Assert.Equal(key, address.Key);
        Assert.Equal(field, address.Field);
        Assert.Equal(text, address.ToString());
    }

    [Fact]
    public async Task Discovery_ProducesReadOnlyCompositeCandidate_WithoutSecretMaterial()
    {
        var inventory = Inventory([new(EspHomeEntityKind.Switch, 0x10, 0, "pump", "Pump", 0)]);
        var provider = new EspHomeEngineeringProvider(
            "project",
            "source",
            null,
            null,
            (_, _) => new StubClient(inventory));

        var context = new DriverEngineeringDataSourceContext(
            "source",
            "ESPHome",
            EspHomeNativeContract.DriverType,
            new Dictionary<string, string>
            {
                ["host"] = "192.0.2.10",
                ["port"] = "6053",
                ["encryptionMode"] = "plaintext"
            },
            new Dictionary<string, string>());

        var results = new List<DriverDiscoveryCandidate>();
        await foreach (var item in provider.DiscoverAsync(new DriverDiscoveryRequest(context)))
            results.Add(item);

        var candidate = Assert.Single(results);
        Assert.Equal("AA:BB:CC:DD:EE:FF", candidate.StableIdentity);
        Assert.NotNull(candidate.Materialization);
        Assert.Contains(candidate.Materialization!.Tags!, x => x.PortableAddress == "switch:0:00000010.state");
        Assert.DoesNotContain("secret", string.Join("|", candidate.SuggestedSettings!.Values), StringComparison.OrdinalIgnoreCase);
    }

    private static EspHomeNativeInventory Inventory(IReadOnlyCollection<EspHomeEntityDescriptor> entities) =>
        new(
            new EspHomeApiVersion(1, 15),
            new EspHomeDeviceIdentity(
                "AA:BB:CC:DD:EE:FF",
                "aa:bb:cc:dd:ee:ff",
                "node",
                "Node",
                "2026.9.0",
                "2026-09-30",
                "Espressif",
                "ESP32",
                "fixture",
                "1",
                false),
            entities);

    private sealed class StubClient(EspHomeNativeInventory inventory) : IEspHomeNativeClient
    {
        public bool Connected { get; private set; }
        public ValueTask<EspHomeNativeInventory> ConnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Connected = true;
            return ValueTask.FromResult(inventory);
        }
        public ValueTask SubscribeStatesAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<EspHomeStateUpdate> ReceiveStateAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromException<EspHomeStateUpdate>(new NotSupportedException());
        public ValueTask SendCommandAsync(EspHomeCommand command, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new NotSupportedException());
        public ValueTask SendPingAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
        {
            Connected = false;
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
