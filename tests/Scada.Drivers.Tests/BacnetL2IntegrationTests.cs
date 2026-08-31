using System.Diagnostics;
using System.IO.BACnet;
using Scada.Drivers.Bacnet;

namespace Scada.Drivers.Tests;

public sealed class BacnetL2IntegrationTests
{
    private const uint DeviceInstance = 599001;
    private const uint RpOnlyDeviceInstance = 599002;

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

        var analog = AnalogPresentValueBinding(DeviceInstance);
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

    [Fact]
    [Trait("Category", "BacnetL2Integration")]
    public async Task Session_FallsBackToReadPropertyWhenIndependentPeerRejectsRpm()
    {
        var options = new BacnetSessionOptions(
            LocalPort: 47812,
            RequestTimeout: TimeSpan.FromSeconds(1),
            Retries: 1,
            DiscoveryWindow: TimeSpan.FromSeconds(2),
            TargetAddress: "127.0.0.1:47811");

        await using var session = new SystemIoBacnetSession(options);
        await session.StartAsync();

        var device = await session.ResolveDeviceAsync(RpOnlyDeviceInstance);
        Assert.Equal(RpOnlyDeviceInstance, device.DeviceInstance);
        Assert.Equal((ushort)999, device.VendorId);

        var result = await session.ReadAsync(AnalogPresentValueBinding(RpOnlyDeviceInstance));
        Assert.NotEmpty(result.Values);
        Assert.Equal(21.5d, Convert.ToDouble(result.Values[0].Value), 3);
        Assert.False(result.UsedReadPropertyMultiple);
        Assert.Equal("62", result.ObjectState?.Units);
    }

    [Fact]
    [Trait("Category", "BacnetL2Integration")]
    public async Task Session_InvalidatesLostDeviceRouteAndResolvesAgainAfterPeerRestart()
    {
        var container = Environment.GetEnvironmentVariable("ELITESCADA_BACNET_L2_RESTART_CONTAINER");
        Assert.False(string.IsNullOrWhiteSpace(container));

        var options = new BacnetSessionOptions(
            LocalPort: 47810,
            RequestTimeout: TimeSpan.FromMilliseconds(500),
            Retries: 1,
            DiscoveryWindow: TimeSpan.FromSeconds(1),
            TargetAddress: "127.0.0.1:47808");

        await using var session = new SystemIoBacnetSession(options);
        await session.StartAsync();
        var analog = AnalogPresentValueBinding(DeviceInstance);

        var beforeLoss = await session.ResolveDeviceAsync(DeviceInstance);
        Assert.Equal((ushort)999, beforeLoss.VendorId);
        Assert.NotEmpty((await session.ReadAsync(analog)).Values);

        await RunDockerAsync("stop", container!);
        await Assert.ThrowsAnyAsync<Exception>(() => session.ReadAsync(analog));

        // A transport read failure must remove the cached Device Instance route.
        // With the peer still down, ResolveDeviceAsync must perform a fresh
        // Who-Is and time out instead of returning the stale cached address.
        await Assert.ThrowsAsync<TimeoutException>(() => session.ResolveDeviceAsync(DeviceInstance));

        await RunDockerAsync("start", container!);
        await WaitForPeerHealthAsync();

        var afterRestart = await session.ResolveDeviceAsync(DeviceInstance);
        Assert.Equal(DeviceInstance, afterRestart.DeviceInstance);
        Assert.Equal((ushort)999, afterRestart.VendorId);

        var recovered = await session.ReadAsync(analog);
        Assert.Equal(21.5d, Convert.ToDouble(recovered.Values[0].Value), 3);
        Assert.True(recovered.UsedReadPropertyMultiple);
    }

    private static BacnetBinding AnalogPresentValueBinding(uint deviceInstance)
        => new(
            deviceInstance,
            (uint)BacnetObjectTypes.OBJECT_ANALOG_VALUE,
            1,
            (uint)BacnetPropertyIds.PROP_PRESENT_VALUE,
            UseCov: true);

    private static async Task RunDockerAsync(string command, string container)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "docker",
            ArgumentList = { command, container },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Could not start docker CLI for BACnet L2 fault injection.");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"docker {command} failed: {stdout} {stderr}");
    }

    private static async Task WaitForPeerHealthAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(15);
        Exception? lastError = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync("http://127.0.0.1:18080/health");
                if (response.IsSuccessStatusCode) return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException($"BACpypes peer did not become healthy after restart. Last error: {lastError?.GetType().Name}");
    }
}
