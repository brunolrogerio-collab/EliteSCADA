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
}
