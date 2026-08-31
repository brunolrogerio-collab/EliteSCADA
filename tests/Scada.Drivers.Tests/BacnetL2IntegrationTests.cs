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
        // BACpypes emits Engineering Units as BACnet enum value 62. Driver 4
        // deliberately preserves the observed stack representation rather than
        // inventing a display-name mapping inside protocol acquisition.
        Assert.Equal("62", initial.ObjectState?.Units);

        var changedCov = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
        var covNotifications = 0;
        await using var subscription = (IAsyncDisposable?)await session.TrySubscribeCovAsync(
            analog,
            notification =>
            {
                if (notification.Values.Count == 0) return ValueTask.CompletedTask;
                Interlocked.Increment(ref covNotifications);
                var observed = Convert.ToDouble(notification.Values[0].Value);
                // BACnet peers may send the current value immediately when the COV
                // subscription is established. Evidence for the write path is the
                // later notification carrying the changed Present_Value.
                if (Math.Abs(observed - 33.25d) < 0.001d)
                    changedCov.TrySetResult(observed);
                return ValueTask.CompletedTask;
            });
        Assert.NotNull(subscription);

        await session.WriteAsync(
            analog,
            new[] { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, 33.25f) });

        var readback = await session.ReadAsync(analog);
        Assert.Equal(33.25d, Convert.ToDouble(readback.Values[0].Value), 3);

        var covValue = await changedCov.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(33.25d, covValue, 3);
        Assert.True(Volatile.Read(ref covNotifications) >= 1);
    }
}
