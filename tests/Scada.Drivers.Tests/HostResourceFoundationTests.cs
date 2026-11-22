using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.HostResources;
using Scada.Drivers.Serial;

namespace Scada.Drivers.Tests;

public sealed class HostResourceFoundationTests
{
    [Fact]
    public void ResourceId_NormalizesAndResourceKindRejectsInvalidToken()
    {
        var resourceId = new HostResourceId("  ZWAVE-CONTROLLER-MAIN  ");

        Assert.Equal("zwave-controller-main", resourceId.Value);
        Assert.Throws<ArgumentException>(() => new HostResourceKind("bad kind"));
    }

    [Fact]
    public void Registry_AllowsSamePhysicalDeviceAtNewLocator()
    {
        var registry = new HostResourceRegistry();
        var first = Controller("COM3", "usb:zwave:0001");
        registry.Observe(first);

        var moved = Controller("COM7", "usb:zwave:0001");
        var current = registry.Observe(moved);

        Assert.Equal("COM7", current.Locator.Value);
        Assert.Equal(first.PhysicalIdentity.StableKey, current.PhysicalIdentity.StableKey);
        Assert.NotEqual(first.Locator.Value, current.Locator.Value);
    }

    [Fact]
    public void Registry_RejectsWrongPhysicalDeviceOnOldLocator()
    {
        var registry = new HostResourceRegistry();
        registry.Observe(Controller("COM3", "usb:zwave:0001"));

        var error = Assert.Throws<HostResourceIdentityMismatchException>(() =>
            registry.Observe(Controller("COM3", "usb:zwave:9999")));

        Assert.Equal("usb:zwave:0001", error.ExpectedPhysicalIdentity);
        Assert.Equal("usb:zwave:9999", error.ObservedPhysicalIdentity);
    }

    [Fact]
    public void ExclusiveLease_DeniesSecondOwnerThenAllowsReacquireAfterRelease()
    {
        var registry = new HostResourceRegistry();
        var descriptor = registry.Observe(Controller("COM3", "usb:zwave:0001"));
        var coordinator = new HostResourceLeaseCoordinator(registry);

        using (coordinator.Acquire(descriptor.ResourceId, "runtime-a", () => true))
        {
            Assert.Throws<InvalidOperationException>(() =>
                coordinator.Acquire(descriptor.ResourceId, "runtime-b", () => true));
        }

        using var second = coordinator.Acquire(descriptor.ResourceId, "runtime-b", () => true);
        Assert.Equal("runtime-b", second.OwnerId);
    }

    [Fact]
    public void CommunicationRuntimeAuthoritySeam_DeniesLeaseWhenExternalEffectsAreNotOwned()
    {
        var registry = new HostResourceRegistry();
        var descriptor = registry.Observe(Controller("COM3", "usb:zwave:0001"));
        var coordinator = new HostResourceLeaseCoordinator(registry);
        var services = new CommunicationDriverRuntimeServices(
            "project-a",
            new CurrentTagCache(new InMemoryScadaEventBus()),
            new InMemoryTagRegistry(),
            EffectAuthority: () => false);

        Assert.False(services.CanOwnExternalEffects);
        Assert.Throws<HostResourceAuthorityDeniedException>(() =>
            services.AcquireHostResource(coordinator, descriptor.ResourceId, "runtime-standby"));
    }

    [Fact]
    public void ZWaveControllerFixture_CarriesFoundationMetadataWithoutProtocolBehavior()
    {
        var descriptor = Controller("/dev/serial/by-id/zwave-main", "usb:zwave:0001");

        Assert.Equal(HostResourceKinds.ZWaveController, descriptor.ResourceKind);
        Assert.Equal(HostResourceLocatorKind.SerialPort, descriptor.Locator.Kind);
        Assert.Equal("700-series", descriptor.Family);
        Assert.Equal("ZSTICK-7", descriptor.Model);
        Assert.Equal("7.19.3", descriptor.Firmware);
        Assert.Equal("EU", descriptor.Metadata![ZWaveControllerHostResource.RfRegionMetadataKey]);
        Assert.Equal("homeid:0xA1B2C3D4", descriptor.Metadata[ZWaveControllerHostResource.NvmIdentityMetadataKey]);
    }

    [Fact]
    public void Registry_PreventsOnePhysicalDeviceFromBindingToTwoLogicalResources()
    {
        var registry = new HostResourceRegistry();
        registry.Observe(Controller("COM3", "usb:zwave:0001"));
        var duplicate = Controller("COM7", "usb:zwave:0001") with
        {
            ResourceId = new HostResourceId("zwave-controller-backup")
        };

        Assert.Throws<InvalidOperationException>(() => registry.Observe(duplicate));
    }

    private static HostResourceDescriptor Controller(string locator, string physicalIdentity) =>
        ZWaveControllerHostResource.Create(
            "zwave-controller-main",
            new HostSerialLineSettings(locator),
            new HostResourcePhysicalIdentity(
                physicalIdentity,
                new Dictionary<string, string>
                {
                    ["usbSerial"] = "0001",
                    ["vidPid"] = "0658:0200"
                }),
            family: "700-series",
            model: "ZSTICK-7",
            firmware: "7.19.3",
            rfRegion: "EU",
            nvmIdentity: "homeid:0xA1B2C3D4",
            replacement: new HostResourceReplacementMetadata(
                MigrationReference: "migration:not-started"));
}
