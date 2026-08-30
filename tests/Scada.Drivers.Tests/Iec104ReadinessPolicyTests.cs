using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104ReadinessPolicyTests
{
    [Fact]
    public void RunningWithCompletedGeneralInterrogation_IsReady()
    {
        var evidence = Iec104ReadinessPolicy.Evaluate(
            Iec104SessionState.Running,
            new Dictionary<ushort, Iec104GeneralInterrogationState>
            {
                [1] = Iec104GeneralInterrogationState.Completed,
                [2] = Iec104GeneralInterrogationState.Completed
            },
            generalInterrogationRequired: true);

        Assert.True(evidence.IsReady);
    }

    [Fact]
    public void RunningWithCollectingGeneralInterrogation_IsNotReady()
    {
        var evidence = Iec104ReadinessPolicy.Evaluate(
            Iec104SessionState.Running,
            new Dictionary<ushort, Iec104GeneralInterrogationState>
            {
                [1] = Iec104GeneralInterrogationState.Collecting
            },
            generalInterrogationRequired: true);

        Assert.False(evidence.IsReady);
    }

    [Fact]
    public void RunningWithoutGeneralInterrogation_IsReadyOnlyWhenPolicyDisablesIt()
    {
        var evidence = Iec104ReadinessPolicy.Evaluate(
            Iec104SessionState.Running,
            new Dictionary<ushort, Iec104GeneralInterrogationState>(),
            generalInterrogationRequired: false);

        Assert.True(evidence.IsReady);
    }
}
