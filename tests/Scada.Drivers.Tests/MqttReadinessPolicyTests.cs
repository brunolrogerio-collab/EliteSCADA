using Scada.Drivers.Mqtt;

namespace Scada.Drivers.Tests;

public sealed class MqttReadinessPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void Readiness_RequiresAuthenticationAndAcceptedSubscriptions(
        bool brokerAuthenticated,
        bool subscriptionsAccepted,
        bool expected)
    {
        var evidence = MqttReadinessPolicy.Evaluate(brokerAuthenticated, subscriptionsAccepted);

        Assert.Equal(expected, evidence.IsReady);
    }

    [Fact]
    public void FirstTelemetrySample_IsNotPartOfReadinessContract()
    {
        var evidence = MqttReadinessPolicy.Evaluate(
            brokerAuthenticated: true,
            subscriptionsAccepted: true);

        Assert.True(evidence.IsReady);
    }
}
