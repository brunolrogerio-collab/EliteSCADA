using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Sidecars;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Bacnet;
using Scada.Drivers.ESPHome;
using Scada.Drivers.HomeAssistant;
using Scada.Drivers.Shelly;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpHomeBuildingExamplesTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void HomeBuildingGuidesCoverEveryDeclaredConnectionAndBindingField(string locale)
    {
        var catalog = ContextualHelpCatalog.Build(locale);
        (string Topic, CommunicationDriverTypeDescriptor Descriptor)[] drivers = [
            ("drivers.bacnet-setup", BacnetDriverDescriptor.Instance),
            ("drivers.knx-setup", new KnxIpDriverDescriptorProvider().Descriptor),
            ("drivers.homeassistant-setup", new HomeAssistantDriverDescriptorProvider().Descriptor),
            ("drivers.esphome-setup", new EspHomeDriverDescriptorProvider().Descriptor),
            ("drivers.shelly-setup", new ShellyDriverDescriptorProvider().Descriptor)
        ];
        foreach (var (id, descriptor) in drivers)
        {
            var guide = catalog.Topics.Single(t => t.Id == id);
            var keys = guide.Sections.SelectMany(s => s.Fields ?? []).Select(f => f.Key).ToHashSet();
            foreach (var field in descriptor.ConfigurationSchema.DataSourceFields.Concat(descriptor.ConfigurationSchema.TagBindingFields))
                Assert.Contains(field.Key, keys);
        }
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void PublishedHomeExamplesUseActualAddressCodecsAndSelectedMaterialization(string locale)
    {
        using var bacnet = Example(locale, "drivers.bacnet-setup");
        var b = bacnet.RootElement;
        var binding = new BacnetBinding(b.GetProperty("deviceInstance").GetUInt32(), b.GetProperty("objectType").GetUInt32(),
            b.GetProperty("objectInstance").GetUInt32(), b.GetProperty("propertyIdentifier").GetUInt32(), UseCov: b.GetProperty("useCov").GetBoolean());
        Assert.True(BacnetBinding.TryParse(binding.PortableAddress, out var parsed, out var error), error);
        Assert.Equal(binding.StableIdentity, parsed!.StableIdentity);
        Assert.True(BacnetRuntimeConfigurationParser.TryCreate(new Dictionary<string, string> { ["deviceInstance"] = "1201", ["targetAddress"] = "192.0.2.20:47808" }, out var configuration, out var errors), string.Join(";", errors));
        Assert.Equal(binding.DeviceInstance, configuration!.DeviceInstance);

        using var esp = Example(locale, "drivers.esphome-setup");
        var address = esp.RootElement.GetProperty("address").GetString();
        Assert.True(EspHomeEntityAddress.TryParse(address, out var entity));
        Assert.Equal(address, entity.ToString());
        Assert.Equal(EspHomeEntityKind.Switch, entity.Kind);
        Assert.Equal("state", entity.Field);
        Assert.True(Enum.TryParse<EspHomeWriteKind>(esp.RootElement.GetProperty("writeKind").GetString(), true, out var write));
        Assert.Equal(EspHomeWriteKind.SwitchState, write);

        using var hab = Example(locale, "drivers.homeassistant-setup");
        Assert.True(HomeAssistantCommunicationRuntimePlanner.TryParsePortableAddress(hab.RootElement.GetProperty("state").GetString()!, out var selectedId, out var stateField));
        Assert.Equal("state", stateField);
        Assert.True(HomeAssistantCommunicationRuntimePlanner.TryParsePortableAddress(hab.RootElement.GetProperty("attribute").GetString()!, out var lightId, out var brightness));
        Assert.Equal("brightness", brightness);
        HomeAssistantState State(string id) => new(id, "21.5", JsonSerializer.SerializeToElement(new { device_class = "temperature", unit_of_measurement = "°C" }), null, null, JsonSerializer.SerializeToElement(new { }));
        HomeAssistantState[] states = [State(selectedId), State(lightId)];
        var empty = HomeAssistantSelectedInventoryBuilder.Build(states, [], []);
        Assert.Empty(empty.Entities);
        Assert.Empty(HomeAssistantEntityMapper.Build("hab", empty).Candidates);
        var inventory = HomeAssistantSelectedInventoryBuilder.Build(states, [], [selectedId]);
        Assert.Equal(selectedId, Assert.Single(inventory.Entities).EntityId);
        var candidate = Assert.Single(HomeAssistantEntityMapper.Build("hab", inventory).Candidates);
        Assert.Equal(hab.RootElement.GetProperty("state").GetString(), Assert.Single(candidate.Tags!).PortableAddress);

        using var shelly = Example(locale, "drivers.shelly-setup");
        var mapped = ShellyComponentMapper.BuildMaterialization(new ShellyDeviceInfo("demo-device", "ShellyPlus1", null, null, null),
            JsonSerializer.SerializeToElement(new { components = new[] { new { key = "switch:0" } } }),
            JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["switch:0"] = new { output = false, apower = 12.5 } }));
        var output = Assert.Single(mapped.Tags!, t => t.PortableAddress == shelly.RootElement.GetProperty("address").GetString());
        Assert.False(output.ReadOnly);
        Assert.Equal(shelly.RootElement.GetProperty("writeMethod").GetString(), output.Metadata!["writeMethod"]);
        Assert.Equal(shelly.RootElement.GetProperty("writeParameter").GetString(), output.Metadata["writeParameter"]);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void PublishedKnxContractAcceptsCanonicalBindingAndRejectsTheDocumentedExtraSetting(string locale)
    {
        using var example = Example(locale, "drivers.knx-setup");
        var e = example.RootElement;
        var source = new DataSourceEngineeringDto(Guid.NewGuid(), "knx.demo", "KNX demo", KnxIpDriverDescriptorProvider.DriverType,
            Settings: new Dictionary<string, string> { ["gatewayHost"] = "192.0.2.40", ["gatewayPort"] = "3671", ["individualAddress"] = "1.1.250", ["userId"] = "1" },
            SecretReferences: new Dictionary<string, string> { ["userPassword"] = HostMaterialSlotReference.Marker, ["deviceAuthentication"] = HostMaterialSlotReference.Marker, ["dataSecureKeyring"] = HostMaterialSlotReference.Marker });
        var binding = new CommunicationTagBinding(1, KnxIpDriverDescriptorProvider.TagBindingSchemaId, 1, e.GetProperty("writeGroupAddress").GetString()!,
            new Dictionary<string, string> { ["dpt"] = e.GetProperty("dpt").GetString()!, ["stateGroupAddress"] = e.GetProperty("stateGroupAddress").GetString()! });
        var tag = new TagEngineeringDto(Guid.NewGuid(), "Temperature", "Home.Temperature", Enum.Parse<TagDataType>(e.GetProperty("tagType").GetString()!),
            Source: source.Key, Address: binding.PortableAddress, ReadOnly: false, CommunicationBinding: binding, DataSourceId: source.Id);
        var package = new EngineeringPackage(EngineeringExchangeService.CurrentSchema, EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow, [tag], [], DataSources: [source]);
        var planner = new KnxIpCommunicationRuntimePlanner();
        var result = planner.Plan(package, source);
        Assert.True(result.CanActivate, string.Join(";", result.Issues.Select(i => i.Message)));
        var extra = new Dictionary<string, string>(binding.EffectiveSettings) { ["writeGroupAddress"] = binding.PortableAddress };
        var invalid = package with { Tags = [tag with { CommunicationBinding = binding with { Settings = extra } }] };
        Assert.Contains(planner.Plan(invalid, source).Issues, i => i.Code == "KNX_IP_TAG_BINDING_SETTINGS_INVALID" && i.IsError);
        var collision = new Dictionary<string, string>(binding.EffectiveSettings) { ["stateGroupAddress"] = binding.PortableAddress };
        Assert.Contains(planner.Plan(package with { Tags = [tag with { CommunicationBinding = binding with { Settings = collision } }] }, source).Issues,
            i => i.Code == "KNX_IP_WRITE_STATE_ADDRESS_COLLISION" && i.IsError);
    }

    private static JsonDocument Example(string locale, string id) => JsonDocument.Parse(Assert.Single(
        ContextualHelpCatalog.Build(locale).Topics.Single(t => t.Id == id).Sections, s => s.Code is not null).Code!);
}
