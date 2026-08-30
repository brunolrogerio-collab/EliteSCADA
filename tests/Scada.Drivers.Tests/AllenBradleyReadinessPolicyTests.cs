using Scada.Drivers.Abstractions;
using Scada.Drivers.AllenBradley;

namespace Scada.Drivers.Tests;

public sealed class AllenBradleyReadinessPolicyTests
{
    [Fact]
    public void DegradedSymbol_DoesNotMakeConnectedSourceUnreadyAfterAcquisition()
    {
        var evidence = AllenBradleyReadinessPolicy.Evaluate(
            connected: true,
            CommunicationDriverOperationalState.Degraded,
            readOperations: 1);

        Assert.True(evidence.IsReady);
    }

    [Fact]
    public void ConnectedWithoutInitialAcquisition_IsNotReady()
    {
        var evidence = AllenBradleyReadinessPolicy.Evaluate(
            connected: true,
            CommunicationDriverOperationalState.Healthy,
            readOperations: 0);

        Assert.False(evidence.IsReady);
    }

    [Fact]
    public void Reconnecting_IsNotReady()
    {
        var evidence = AllenBradleyReadinessPolicy.Evaluate(
            connected: true,
            CommunicationDriverOperationalState.Reconnecting,
            readOperations: 10);

        Assert.False(evidence.IsReady);
    }
}
