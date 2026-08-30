using Scada.Drivers.Dnp3;

namespace Scada.Drivers.Tests;

public sealed class Dnp3ReadinessPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void Readiness_RequiresOnlineAssociationAndStartupIntegrity(
        bool associationOnline,
        bool startupIntegrityCompleted,
        bool expected)
    {
        var evidence = Dnp3ReadinessPolicy.Evaluate(associationOnline, startupIntegrityCompleted);

        Assert.Equal(expected, evidence.IsReady);
    }
}
