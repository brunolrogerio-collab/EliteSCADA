using System.IO.BACnet;
using Scada.Drivers.Bacnet;

namespace Scada.Drivers.Tests;

public sealed class BacnetL2IntegrationTests
{
    private const uint DeviceInstance = 599001;

    [Fact]
    [Trait("Category", "BacnetL2Integration")]
    public async Task Session_DiscoversReadsWritesAndReceivesCovAgainstIndependentPeer()
    {
        var options = new BacnetSessionOptions(
            LocalPort: 47809,
            RequestTimeout: TimeSpan.FromSeconds(2),
            Retries: 2,
            DiscoveryWindow: TimeSpan.FromSeconds(3),
            TargetAddress: "127.0.0.1:47808");

        await using var session = new SystemIoBacnetSession(options);
        await session.StartAsync();

        var device = await session.ResolveDeviceAsync(DeviceInstance);
        Assert.Equal(DeviceInstance, device.DeviceInstance);
        Assert.Equal((ushort)999, device.VendorId);

        var analog = new BacnetBinding(
            DeviceInstance,
            (uint)BacnetObjectTypes.OBJECT_ANALOG_VALUE,
            1,
            (uint)BacnetPropertyIds.PROP_PRESENT_VALUE,
            UseCov: true);

        var initial = await session.ReadAsync(analog);
        Assert.NotEmpty(initial.Values);
        Assert.Equal(21.5d, Convert.ToDouble(initial.Values[0].Value), 3);
        Assert.True(initial.UsedReadPropertyMultiple);
        Assert.Equal("degreesCelsius", initial.ObjectState?.Units, ignoreCase: true);

        var cov = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var subscription = (IAsyncDisposable?)await session.TrySubscribeCovAsync(
            analog,
            notification =>
            {
                if (notification.Values.Count > 0)
                    cov.TrySetResult(Convert.ToDouble(notification.Values[0].Value));
                return ValueTask.CompletedTask;
            });
        Assert.NotNull(subscription);

        await session.WriteAsync(
            analog,
            new[] { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, 33.25f) });

        var readback = await session.ReadAsync(analog);
        Assert.Equal(33.25d, Convert.ToDouble(readback.Values[0].Value), 3);

        var covValue = await cov.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(33.25d, covValue, 3);
    }
}
