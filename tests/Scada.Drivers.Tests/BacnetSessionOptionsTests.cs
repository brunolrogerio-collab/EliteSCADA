using Scada.Drivers.Bacnet;

namespace Scada.Drivers.Tests;

public sealed class BacnetSessionOptionsTests
{
    [Theory]
    [InlineData("192.168.10.20")]
    [InlineData("192.168.10.20:47809")]
    public void ManualIpv4Target_IsAccepted(string targetAddress)
    {
        var options = new BacnetSessionOptions(TargetAddress: targetAddress);
        options.Validate();
        Assert.Equal(targetAddress, options.TargetAddress);
    }

    [Fact]
    public void InvalidManualTarget_IsRejected()
    {
        var options = new BacnetSessionOptions(TargetAddress: "not-an-ip");
        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void ForeignDeviceRegistration_RequiresBbmdAndUsesBoundedLeaseSchedule()
    {
        var options = new BacnetSessionOptions(
            BbmdAddress: "192.168.20.1",
            ForeignDeviceTtlSeconds: 120);

        options.Validate();

        Assert.Equal(TimeSpan.FromSeconds(90), options.EffectiveForeignDeviceRenewalInterval);
        Assert.Equal(TimeSpan.FromSeconds(12), options.EffectiveForeignDeviceRetryInterval);
        Assert.True(options.EffectiveForeignDeviceRetryInterval < options.EffectiveForeignDeviceRenewalInterval);
        Assert.True(options.EffectiveForeignDeviceRenewalInterval < TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void ForeignDeviceRetry_IsClampedForSmallAndLargeTtlValues()
    {
        var small = new BacnetSessionOptions(BbmdAddress: "192.168.20.1", ForeignDeviceTtlSeconds: 30);
        var large = new BacnetSessionOptions(BbmdAddress: "192.168.20.1", ForeignDeviceTtlSeconds: 1000);

        small.Validate();
        large.Validate();

        Assert.Equal(TimeSpan.FromSeconds(5), small.EffectiveForeignDeviceRetryInterval);
        Assert.Equal(TimeSpan.FromSeconds(30), large.EffectiveForeignDeviceRetryInterval);
    }

    [Fact]
    public void ForeignDeviceTtlWithoutBbmd_IsRejected()
    {
        var options = new BacnetSessionOptions(ForeignDeviceTtlSeconds: 120);
        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void NoForeignDeviceRegistration_HasNoLeaseTimers()
    {
        var options = new BacnetSessionOptions();
        options.Validate();
        Assert.Null(options.EffectiveForeignDeviceRenewalInterval);
        Assert.Null(options.EffectiveForeignDeviceRetryInterval);
    }
}
