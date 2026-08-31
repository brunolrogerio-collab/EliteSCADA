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
    public void RequestFailure_IsContainedAndConvertedToRetry()
    {
        var options = new BacnetSessionOptions(
            BbmdAddress: "192.168.20.1",
            ForeignDeviceTtlSeconds: 120);
        var calls = 0;

        var next = BacnetForeignDeviceRegistrationPolicy.ExecuteAndScheduleNext(
            options,
            () =>
            {
                calls++;
                throw new InvalidOperationException("simulated BBMD request failure");
            });

        Assert.Equal(1, calls);
        Assert.Equal(BacnetForeignDeviceRegistrationAttemptKind.Retry, next.Kind);
        Assert.Equal(TimeSpan.FromSeconds(12), next.Delay);
    }

    [Fact]
    public void SuccessfulRequest_IsConvertedToNormalRenewal()
    {
        var options = new BacnetSessionOptions(
            BbmdAddress: "192.168.20.1",
            ForeignDeviceTtlSeconds: 120);
        var calls = 0;

        var next = BacnetForeignDeviceRegistrationPolicy.ExecuteAndScheduleNext(
            options,
            () => calls++);

        Assert.Equal(1, calls);
        Assert.Equal(BacnetForeignDeviceRegistrationAttemptKind.Renewal, next.Kind);
        Assert.Equal(TimeSpan.FromSeconds(90), next.Delay);
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