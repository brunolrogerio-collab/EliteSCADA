using Scada.Drivers.Abstractions;
using Scada.Drivers.Bacnet;

namespace Scada.Drivers.Tests;

public sealed class BacnetReadinessPolicyTests
{
    [Fact]
    public void DegradedPointEvidence_DoesNotMakeReachableAcquisitionUnready()
    {
        var evidence = BacnetReadinessPolicy.Evaluate(
            deviceReachable: true,
            CommunicationDriverOperationalState.Degraded,
            configuredPointCount: 3);

        Assert.True(evidence.IsReady);
    }

    [Fact]
    public void Reconnecting_IsNotReadyEvenWhenPreviousReachabilityWasKnown()
    {
        var evidence = BacnetReadinessPolicy.Evaluate(
            deviceReachable: true,
            CommunicationDriverOperationalState.Reconnecting,
            configuredPointCount: 3);

        Assert.False(evidence.IsReady);
    }

    [Fact]
    public void UnknownReachability_IsNotReady()
    {
        var evidence = BacnetReadinessPolicy.Evaluate(
            deviceReachable: null,
            CommunicationDriverOperationalState.Healthy,
            configuredPointCount: 1);

        Assert.False(evidence.IsReady);
    }
}
