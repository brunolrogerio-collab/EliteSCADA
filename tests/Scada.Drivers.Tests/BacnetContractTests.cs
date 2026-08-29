using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Bacnet;

namespace Scada.Drivers.Tests;

public sealed class BacnetContractTests
{
    [Fact]
    public void PortableAddress_RoundTripsStableNumericIdentity()
    {
        var binding = new BacnetBinding(
            DeviceInstance: 1201,
            ObjectType: 0,
            ObjectInstance: 37,
            PropertyIdentifier: 85,
            ArrayIndex: 2,
            UseCov: true,
            WritePriority: 8);

        Assert.True(BacnetBinding.TryParse(binding.PortableAddress, out var parsed, out var error), error);
        Assert.NotNull(parsed);
        Assert.Equal(binding.DeviceInstance, parsed!.DeviceInstance);
        Assert.Equal(binding.ObjectType, parsed.ObjectType);
        Assert.Equal(binding.ObjectInstance, parsed.ObjectInstance);
        Assert.Equal(binding.PropertyIdentifier, parsed.PropertyIdentifier);
        Assert.Equal(binding.ArrayIndex, parsed.ArrayIndex);
        Assert.Equal("bacnet:device=1201;object=0:37;property=85;index=2", binding.PortableAddress);
    }

    [Theory]
    [InlineData(4194303u)]
    [InlineData(uint.MaxValue)]
    public void Binding_RejectsReservedOrOutOfRangeDeviceInstance(uint deviceInstance)
    {
        var binding = new BacnetBinding(deviceInstance, 0, 1, 85);
        Assert.Throws<ArgumentOutOfRangeException>(binding.Validate);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)17)]
    public void Binding_RejectsInvalidWritePriority(byte priority)
    {
        var binding = new BacnetBinding(1, 0, 1, 85, WritePriority: priority);
        Assert.Throws<ArgumentOutOfRangeException>(binding.Validate);
    }

    [Fact]
    public void Point_RejectsWritableBindingForReadOnlyTag()
    {
        var tag = TagDefinition.Create("AI1", "Plant.AI1", TagDataType.Float, source: "bacnet-1", readOnly: true);
        var point = new BacnetPoint(tag, new BacnetBinding(1, 0, 1, 85), Writable: true);
        Assert.Throws<ArgumentException>(point.Validate);
    }

    [Fact]
    public void Descriptor_AdvertisesHybridAcquisitionWithoutSecureConnect()
    {
        var descriptor = BacnetDriverDescriptor.Instance;
        Assert.Equal("bacnet.ip", descriptor.DriverType);
        Assert.Contains(DriverAcquisitionMode.Subscription, descriptor.AcquisitionModes);
        Assert.Contains(DriverAcquisitionMode.Polling, descriptor.AcquisitionModes);
        Assert.Contains(DriverAcquisitionMode.Hybrid, descriptor.AcquisitionModes);
        Assert.True(descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Discover));
        Assert.True(descriptor.EngineeringCapabilities.HasFlag(DriverEngineeringCapabilities.Browse));
        Assert.DoesNotContain("Secure Connect", descriptor.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, true, TagQuality.Good)]
    [InlineData(0u, true, TagQuality.Good)]
    [InlineData(2u, true, TagQuality.Uncertain)]
    [InlineData(10u, true, TagQuality.BadConfiguration)]
    [InlineData(11u, true, TagQuality.BadCommunication)]
    [InlineData(7u, true, TagQuality.BadDevice)]
    [InlineData(999u, true, TagQuality.Uncertain)]
    [InlineData(null, false, TagQuality.BadCommunication)]
    public void Reliability_MapsIntoCommonEliteScadaQuality(uint? reliability, bool communicationSucceeded, TagQuality expected)
        => Assert.Equal(expected, BacnetQualityMapper.FromReliability(reliability, communicationSucceeded));
}
