using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class SiemensS7ReadinessPolicyTests
{
    [Fact]
    public void NegotiatedSessionAfterInitialAttempt_IsReady()
    {
        var evidence = SiemensS7ReadinessPolicy.Evaluate(
            sessionEstablished: true,
            negotiatedPduSize: 480,
            initialAcquisitionAttempted: true);

        Assert.True(evidence.IsReady);
    }

    [Fact]
    public void SessionWithoutPduNegotiation_IsNotReady()
    {
        var evidence = SiemensS7ReadinessPolicy.Evaluate(
            sessionEstablished: true,
            negotiatedPduSize: 0,
            initialAcquisitionAttempted: true);

        Assert.False(evidence.IsReady);
    }

    [Fact]
    public void NegotiatedSessionBeforeInitialAttempt_IsNotReady()
    {
        var evidence = SiemensS7ReadinessPolicy.Evaluate(
            sessionEstablished: true,
            negotiatedPduSize: 480,
            initialAcquisitionAttempted: false);

        Assert.False(evidence.IsReady);
    }
}
