using Scada.Drivers.Dnp3;

namespace Scada.Drivers.Tests;

public sealed class Dnp3ReadinessPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void ReadinessRequiresOnlineAssociationAndStartupIntegrity(bool associationOnline, bool startupIntegrityCompleted, bool expected)
    {
        var evidence = Dnp3ReadinessPolicy.Evaluate(associationOnline, startupIntegrityCompleted);
        Assert.Equal(expected, evidence.IsReady);
    }

    [Fact]
    public void SnapshotReadinessUsesOnlineStateAndCompletedStartupIntegrity()
    {
        var snapshot = new Dnp3SessionDiagnosticSnapshot(null, Dnp3SessionState.Online, DateTimeOffset.UtcNow, StartupIntegrityScans: 1);
        Assert.True(Dnp3ReadinessPolicy.Evaluate(snapshot).IsReady);
    }

    [Fact]
    public void SnapshotReadinessIsFalseDuringReconnectAfterPriorIntegrity()
    {
        var snapshot = new Dnp3SessionDiagnosticSnapshot(null, Dnp3SessionState.Reconnecting, DateTimeOffset.UtcNow, StartupIntegrityScans: 3);
        Assert.False(Dnp3ReadinessPolicy.Evaluate(snapshot).IsReady);
    }
}
