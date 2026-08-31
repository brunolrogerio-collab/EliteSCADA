using Scada.Drivers.Bacnet;

namespace Scada.Drivers.Tests;

public sealed class BacnetForeignDeviceRegistrationPolicyTests
{
    [Fact]
    public void InitialAttempt_IsImmediateAndExplicitlyCategorized()
    {
        var options = new BacnetSessionOptions(
            BbmdAddress: "192.168.20.1",
            ForeignDeviceTtlSeconds: 120);

        var attempt = BacnetForeignDeviceRegistrationPolicy.Initial(options);

        Assert.Equal(BacnetForeignDeviceRegistrationAttemptKind.Initial, attempt.Kind);
        Assert.Equal(TimeSpan.Zero, attempt.Delay);
    }

    [Fact]
    public void SuccessfulAttempt_SchedulesRenewalBeforeTtlExpiry()
    {
        var options = new BacnetSessionOptions(
            BbmdAddress: "192.168.20.1",
            ForeignDeviceTtlSeconds: 120);

        var attempt = BacnetForeignDeviceRegistrationPolicy.AfterSuccess(options);

        Assert.Equal(BacnetForeignDeviceRegistrationAttemptKind.Renewal, attempt.Kind);
        Assert.Equal(TimeSpan.FromSeconds(90), attempt.Delay);
        Assert.True(attempt.Delay < TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void FailedAttempt_SchedulesBoundedRetryInsteadOfNormalRenewal()
    {
        var options = new BacnetSessionOptions(
            BbmdAddress: "192.168.20.1",
            ForeignDeviceTtlSeconds: 120);

        var attempt = BacnetForeignDeviceRegistrationPolicy.AfterFailure(options);

        Assert.Equal(BacnetForeignDeviceRegistrationAttemptKind.Retry, attempt.Kind);
        Assert.Equal(TimeSpan.FromSeconds(12), attempt.Delay);
        Assert.True(attempt.Delay < options.EffectiveForeignDeviceRenewalInterval);
    }

    [Fact]
    public void MissingFdrConfiguration_FailsClosed()
    {
        var options = new BacnetSessionOptions();

        Assert.Throws<InvalidOperationException>(() => BacnetForeignDeviceRegistrationPolicy.Initial(options));
        Assert.Throws<InvalidOperationException>(() => BacnetForeignDeviceRegistrationPolicy.AfterSuccess(options));
        Assert.Throws<InvalidOperationException>(() => BacnetForeignDeviceRegistrationPolicy.AfterFailure(options));
    }
}