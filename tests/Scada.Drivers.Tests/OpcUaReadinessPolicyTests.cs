using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaReadinessPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void Readiness_RequiresSecureSessionAndActivatedAcquisition(
        bool secureSessionEstablished,
        bool acquisitionActivated,
        bool expected)
    {
        var evidence = OpcUaReadinessPolicy.Evaluate(secureSessionEstablished, acquisitionActivated);

        Assert.Equal(expected, evidence.IsReady);
    }

    [Fact]
    public void FirstValue_IsDeliberatelyNotPartOfReadinessContract()
    {
        var evidence = OpcUaReadinessPolicy.Evaluate(
            secureSessionEstablished: true,
            acquisitionActivated: true);

        Assert.True(evidence.IsReady);
    }
}
