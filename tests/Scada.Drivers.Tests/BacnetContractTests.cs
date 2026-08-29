using System.IO.BACnet;
using Scada.Core.Events;
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
    [InlineData(11u, true, TagQuality.BadDevice)]
    [InlineData(7u, true, TagQuality.BadDevice)]
    [InlineData(999u, true, TagQuality.Uncertain)]
    [InlineData(null, false, TagQuality.BadCommunication)]
    public void Reliability_MapsIntoCommonEliteScadaQuality(uint? reliability, bool communicationSucceeded, TagQuality expected)
        => Assert.Equal(expected, BacnetQualityMapper.FromReliability(reliability, communicationSucceeded));

    [Fact]
    public void ObjectState_FaultOverridesOtherwiseGoodReliability()
        => Assert.Equal(
            TagQuality.BadDevice,
            BacnetQualityMapper.FromObjectState(new BacnetObjectState(Reliability: 0, Fault: true)));

    [Fact]
    public void ObjectState_OutOfServiceAndOverrideAreUncertainButAlarmAloneIsGood()
    {
        Assert.Equal(TagQuality.Uncertain, BacnetQualityMapper.FromObjectState(new BacnetObjectState(OutOfService: true)));
        Assert.Equal(TagQuality.Uncertain, BacnetQualityMapper.FromObjectState(new BacnetObjectState(Overridden: true)));
        Assert.Equal(TagQuality.Good, BacnetQualityMapper.FromObjectState(new BacnetObjectState(InAlarm: true)));
    }

    [Fact]
    public void GenericNullWriteEncoding_IsRejectedAndRelinquishRequiresPriority()
    {
        var noPriority = new BacnetBinding(1, 2, 3, 85);
        Assert.Throws<InvalidOperationException>(() => BacnetValueCodec.Encode(null, TagDataType.Double, noPriority));
        Assert.Throws<InvalidOperationException>(() => BacnetValueCodec.EncodeRelinquish(noPriority));

        var explicitPriority = noPriority with { WritePriority = 8 };
        var relinquish = Assert.Single(BacnetValueCodec.EncodeRelinquish(explicitPriority));
        Assert.Equal(BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL, relinquish.Tag);
        Assert.Null(relinquish.Value);
    }

    [Fact]
    public async Task Driver_UsesCompanionObjectStateForPublishedQuality()
    {
        var cache = new CurrentTagCache(new InMemoryScadaEventBus());
        var registry = new InMemoryTagRegistry();
        var tag = TagDefinition.Create("AI1", "Plant.Bacnet.AI1", TagDataType.Double, source: "bacnet-test");
        var binding = new BacnetBinding(10, 0, 1, 85, UseCov: false);
        var point = new BacnetPoint(tag, binding);
        await using var session = new StubSession(new BacnetPropertyReadResult(
            binding,
            new[] { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, 12.5f) },
            DateTimeOffset.UtcNow,
            new BacnetObjectState(Reliability: 0, Fault: true),
            UsedReadPropertyMultiple: true));
        await using var driver = new BacnetIpDriver(
            "bacnet-test",
            "BACnet Test",
            cache,
            registry,
            new[] { point },
            session,
            scanRate: TimeSpan.FromMilliseconds(20));

        await driver.StartAsync();
        TagValue? current = null;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (cache.TryGet(tag.Id, out current) && current is not null) break;
            await Task.Delay(20);
        }

        Assert.NotNull(current);
        Assert.Equal(TagQuality.BadDevice, current!.Quality);
        Assert.Equal(12.5d, Convert.ToDouble(current.Value), 3);
        await driver.StopAsync();
    }

    private sealed class StubSession(BacnetPropertyReadResult sample) : IBacnetSession
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<BacnetDeviceObservation> ResolveDeviceAsync(uint deviceInstance, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public async IAsyncEnumerable<BacnetDeviceObservation> DiscoverAsync(int? maximumResults = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
        public Task<BacnetPropertyReadResult> ReadAsync(BacnetBinding binding, CancellationToken cancellationToken = default)
            => Task.FromResult(sample);
        public Task WriteAsync(BacnetBinding binding, IReadOnlyCollection<BacnetValue> values, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<IDisposable?> TrySubscribeCovAsync(BacnetBinding binding, Func<BacnetPropertyReadResult, ValueTask> onNotification, CancellationToken cancellationToken = default)
            => Task.FromResult<IDisposable?>(null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
