using System.Text.Json;
using Scada.Core.Tags;
using Scada.Drivers.HomeAssistant;

namespace Scada.Drivers.Tests;

public sealed class HomeAssistantMappingTests
{
    [Fact]
    public void RegistryDisplay_ParsesCompactDocumentedShape()
    {
        using var document = JsonDocument.Parse("""
            {
              "entity_categories":{"0":"config","1":"diagnostic"},
              "entities":[
                {"ei":"light.living_room","pl":"hue","ai":"living_room","di":"device-1","en":"Living Room","hn":true},
                {"ei":"sensor.rssi","pl":"hue","di":"device-1","en":"RSSI","ec":1,"hb":true}
              ]
            }
            """);

        var entries = HomeAssistantRegistryDisplayParser.Parse(document.RootElement);

        Assert.Equal(2, entries.Count);
        Assert.Equal("device-1", entries[0].DeviceId);
        Assert.Equal("living_room", entries[0].AreaId);
        Assert.Equal("diagnostic", entries[1].EntityCategory);
        Assert.True(entries[1].Hidden);
    }

    [Fact]
    public void SelectedInventory_RequiresExplicitSelection_AndDoesNotMirrorInstance()
    {
        var states = new[]
        {
            State("switch.pump", "off", new { friendly_name = "Pump" }),
            State("sensor.temperature", "21.5", new { device_class = "temperature", unit_of_measurement = "°C" })
        };
        var registry = new[]
        {
            Registry("switch.pump", "demo", deviceId: "device-1"),
            Registry("sensor.temperature", "demo", deviceId: "device-1")
        };

        var empty = HomeAssistantSelectedInventoryBuilder.Build(states, registry, Array.Empty<string>());
        var selected = HomeAssistantSelectedInventoryBuilder.Build(states, registry, ["switch.pump"]);

        Assert.Empty(empty.Entities);
        Assert.Contains(empty.Issues, x => x.Code == "HA_SELECTION_REQUIRED");
        Assert.Single(selected.Entities);
        Assert.Equal("switch.pump", selected.Entities.Single().EntityId);
    }

    [Fact]
    public void Switch_MapsBooleanTagCommandsAndOnOffCapability()
    {
        var inventory = Inventory(
            State("switch.pump", "off", new { friendly_name = "Pump" }),
            Registry("switch.pump", "demo", deviceId: "device-1", areaId: "plant_room"));

        var mapped = HomeAssistantEntityMapper.Build("ha-main", inventory);
        var candidate = Assert.Single(mapped.Candidates);

        var state = Assert.Single(candidate.Tags!);
        Assert.Equal(TagDataType.Boolean, state.DataType);
        Assert.False(state.ReadOnly);
        Assert.Equal("entity:switch.pump:state", state.PortableAddress);
        Assert.Equal("entity_id_mutable", state.Metadata!["identityStability"]);
        Assert.Contains(candidate.Commands!, x => x.Metadata!["service"] == "turn_on");
        Assert.Contains(candidate.Commands!, x => x.Metadata!["service"] == "turn_off");
        Assert.Contains(candidate.Equipment.Capabilities!, x => x.Kind == "OnOff");
        Assert.Equal("HA Area:plant_room", candidate.Equipment.LocationPlaceholder);
        Assert.Contains(mapped.Issues, x => x.Code == "HA_STABLE_ENTITY_IDENTITY_UNAVAILABLE");
    }

    [Fact]
    public void Light_MapsOnlyDeclaredBrightnessAndColorCapabilities()
    {
        var inventory = Inventory(
            State("light.office", "on", new
            {
                friendly_name = "Office",
                brightness = 128,
                supported_color_modes = new[] { "brightness", "rgb" },
                rgb_color = new[] { 10, 20, 30 }
            }),
            Registry("light.office", "hue", deviceId: "device-light"));

        var mapped = HomeAssistantEntityMapper.Build("ha-main", inventory);
        var candidate = Assert.Single(mapped.Candidates);

        Assert.Contains(candidate.Equipment.Capabilities!, x => x.Kind == "Light");
        Assert.Contains(candidate.Equipment.Capabilities!, x => x.Kind == "Dimmer");
        Assert.Contains(candidate.Equipment.Capabilities!, x => x.Kind == "ColorLight");
        var brightness = Assert.Single(candidate.Tags!.Where(x => x.CandidateId.EndsWith(".brightness", StringComparison.Ordinal)));
        Assert.Equal("%", brightness.EngineeringUnit);
        Assert.False(brightness.ReadOnly);
        var color = Assert.Single(candidate.Tags!.Where(x => x.CandidateId.EndsWith(".rgb_color", StringComparison.Ordinal)));
        Assert.Equal(TagDataType.String, color.DataType);
        Assert.Equal("rgb-0-255-triplet", color.Metadata!["valueEncoding"]);
    }

    [Fact]
    public void Sensors_MapExplicitDeviceClasses_AndKeepUnknownGeneric()
    {
        var states = new[]
        {
            State("sensor.room_temp", "22.5", new { device_class = "temperature", unit_of_measurement = "°C" }),
            State("sensor.energy", "42.1", new { device_class = "energy", unit_of_measurement = "kWh" }),
            State("sensor.vendor_metric", "abc", new { friendly_name = "Vendor metric" })
        };
        var registry = states.Select(x => Registry(x.EntityId, "demo", deviceId: "device-sensors")).ToArray();
        var selected = HomeAssistantSelectedInventoryBuilder.Build(states, registry, states.Select(x => x.EntityId).ToArray());

        var mapped = HomeAssistantEntityMapper.Build("ha-main", selected);
        var candidate = Assert.Single(mapped.Candidates);

        Assert.Contains(candidate.Equipment.Capabilities!, x => x.Kind == "Temperature");
        Assert.Contains(candidate.Equipment.Capabilities!, x => x.Kind == "Energy");
        Assert.DoesNotContain(candidate.Equipment.Capabilities!, x => x.CapabilityId.Contains("vendor_metric", StringComparison.Ordinal));
        var generic = Assert.Single(candidate.Tags!.Where(x => x.Metadata!["haEntityId"] == "sensor.vendor_metric"));
        Assert.True(generic.ReadOnly);
        Assert.Equal(TagDataType.String, generic.DataType);
    }

    [Theory]
    [InlineData("door", "Contact")]
    [InlineData("motion", "Motion")]
    [InlineData("occupancy", "Occupancy")]
    [InlineData("moisture", "Leak")]
    [InlineData("smoke", "Smoke")]
    public void BinarySensor_UsesOnlyExplicitDeviceClassSemantics(string deviceClass, string capability)
    {
        var inventory = Inventory(
            State("binary_sensor.test", "on", new { device_class = deviceClass }),
            Registry("binary_sensor.test", "demo", deviceId: "device-binary"));

        var mapped = HomeAssistantEntityMapper.Build("ha-main", inventory);
        var candidate = Assert.Single(mapped.Candidates);

        Assert.Contains(candidate.Equipment.Capabilities!, x => x.Kind == capability);
        Assert.Equal(TagDataType.Boolean, Assert.Single(candidate.Tags!).DataType);
        Assert.True(Assert.Single(candidate.Tags!).ReadOnly);
    }

    [Fact]
    public void Cover_MapsPositionAndOnlySupportedWrites()
    {
        var inventory = Inventory(
            State("cover.blind", "open", new
            {
                device_class = "blind",
                current_position = 45,
                supported_features = 7
            }),
            Registry("cover.blind", "demo", deviceId: "device-cover"));

        var mapped = HomeAssistantEntityMapper.Build("ha-main", inventory);
        var candidate = Assert.Single(mapped.Candidates);

        Assert.Contains(candidate.Equipment.Capabilities!, x => x.Kind == "Cover");
        var position = Assert.Single(candidate.Tags!.Where(x => x.CandidateId.EndsWith(".current_position", StringComparison.Ordinal)));
        Assert.False(position.ReadOnly);
        Assert.Equal("set_cover_position", position.Metadata!["writeService"]);
        Assert.Contains(candidate.Commands!, x => x.Metadata!["service"] == "open_cover");
        Assert.Contains(candidate.Commands!, x => x.Metadata!["service"] == "close_cover");
    }

    [Fact]
    public void UnknownAndConfigurationEntities_DoNotPoisonSupportedSelection()
    {
        var states = new[]
        {
            State("switch.good", "on", new { }),
            State("weather.home", "sunny", new { }),
            State("sensor.rssi", "-55", new { device_class = "signal_strength", unit_of_measurement = "dBm" })
        };
        var registry = new[]
        {
            Registry("switch.good", "demo", deviceId: "device-1"),
            Registry("weather.home", "demo", deviceId: "device-2"),
            Registry("sensor.rssi", "demo", deviceId: "device-1", category: "diagnostic")
        };
        var inventory = HomeAssistantSelectedInventoryBuilder.Build(
            states, registry, states.Select(x => x.EntityId).ToArray());

        var mapped = HomeAssistantEntityMapper.Build("ha-main", inventory);

        Assert.Contains(mapped.Candidates.SelectMany(x => x.Tags!), x => x.Metadata!["haEntityId"] == "switch.good");
        Assert.DoesNotContain(mapped.Candidates.SelectMany(x => x.Tags!), x => x.Metadata!["haEntityId"] == "weather.home");
        Assert.DoesNotContain(mapped.Candidates.SelectMany(x => x.Tags!), x => x.Metadata!["haEntityId"] == "sensor.rssi");
        Assert.Contains(mapped.Issues, x => x.Code == "HA_DOMAIN_UNMAPPED");
        Assert.Contains(mapped.Issues, x => x.Code == "HA_ENTITY_CATEGORY_EXCLUDED");
    }

    [Fact]
    public void DeviceGrouping_IsStableAcrossEntityRename_ButEntityBindingIsExplicitlyMutable()
    {
        var original = Inventory(
            State("switch.old_name", "off", new { }),
            Registry("switch.old_name", "demo", deviceId: "stable-device"));
        var renamed = Inventory(
            State("switch.new_name", "off", new { }),
            Registry("switch.new_name", "demo", deviceId: "stable-device"));

        var first = Assert.Single(HomeAssistantEntityMapper.Build("ha-main", original).Candidates);
        var secondSet = HomeAssistantEntityMapper.Build("ha-main", renamed);
        var second = Assert.Single(secondSet.Candidates);

        Assert.Equal(first.Equipment.StableDeviceIdentity, second.Equipment.StableDeviceIdentity);
        Assert.NotEqual(Assert.Single(first.Tags!).PortableAddress, Assert.Single(second.Tags!).PortableAddress);
        Assert.Equal("entity_id_mutable", Assert.Single(second.Tags!).Metadata!["identityStability"]);
        Assert.Contains(secondSet.Issues, x => x.Code == "HA_STABLE_ENTITY_IDENTITY_UNAVAILABLE");
    }

    [Fact]
    public void EntityWithoutDevice_UsesHonestLogicalEquipment_NotInventedPhysicalIdentity()
    {
        var inventory = Inventory(
            State("sensor.logical_value", "12", new { }),
            Registry("sensor.logical_value", "template"));

        var mapped = HomeAssistantEntityMapper.Build("ha-main", inventory);
        var candidate = Assert.Single(mapped.Candidates);

        Assert.Equal("logical", candidate.Equipment.SourceRole);
        Assert.Equal("false", candidate.Equipment.Metadata!["physicalIdentity"]);
        Assert.Contains(":logical", candidate.Equipment.StableDeviceIdentity, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpersLockFanAndClimateRemainBoundedOutOfScope()
    {
        var states = new[]
        {
            State("input_boolean.helper", "on", new { }),
            State("lock.front", "locked", new { }),
            State("fan.office", "on", new { }),
            State("climate.office", "heat", new { })
        };
        var registry = states.Select(x => Registry(x.EntityId, "demo", deviceId: "device-mixed")).ToArray();
        var inventory = HomeAssistantSelectedInventoryBuilder.Build(states, registry, states.Select(x => x.EntityId).ToArray());

        var mapped = HomeAssistantEntityMapper.Build("ha-main", inventory);

        Assert.Empty(mapped.Candidates);
        Assert.Contains(mapped.Issues, x => x.Code == "HA_NON_PROCESS_ENTITY_EXCLUDED");
        Assert.Contains(mapped.Issues, x => x.Code == "HA_LOCK_OUT_OF_SCOPE");
        Assert.Equal(2, mapped.Issues.Count(x => x.Code == "DEFERRED_ENTITY_SCOPE"));
    }

    private static HomeAssistantSelectedInventory Inventory(
        HomeAssistantState state,
        HomeAssistantRegistryDisplayEntry registry) =>
        HomeAssistantSelectedInventoryBuilder.Build([state], [registry], [state.EntityId]);

    private static HomeAssistantState State(string entityId, string state, object attributes) =>
        new(
            entityId,
            state,
            JsonSerializer.SerializeToElement(attributes),
            DateTimeOffset.Parse("2026-10-06T19:00:00Z"),
            DateTimeOffset.Parse("2026-10-06T19:00:00Z"),
            JsonSerializer.SerializeToElement(new { id = "ctx" }));

    private static HomeAssistantRegistryDisplayEntry Registry(
        string entityId,
        string platform,
        string? deviceId = null,
        string? areaId = null,
        string? category = null) =>
        new(entityId, platform, areaId, deviceId, entityId, category, Hidden: false);
}
