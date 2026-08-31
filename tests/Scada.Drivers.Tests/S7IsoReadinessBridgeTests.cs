using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoReadinessBridgeTests
{
    [Fact]
    public void S7Driver_AdvertisesSharedReadinessCapability()
    {
        Assert.True(typeof(ICommunicationDriverReadinessSource).IsAssignableFrom(typeof(S7IsoDriver)));
    }

    [Theory]
    [InlineData(S7IsoRuntimeReadinessState.NotStarted, CommunicationDriverReadinessState.NotStarted)]
    [InlineData(S7IsoRuntimeReadinessState.Starting, CommunicationDriverReadinessState.Starting)]
    [InlineData(S7IsoRuntimeReadinessState.Ready, CommunicationDriverReadinessState.Ready)]
    [InlineData(S7IsoRuntimeReadinessState.Faulted, CommunicationDriverReadinessState.Faulted)]
    [InlineData(S7IsoRuntimeReadinessState.Stopped, CommunicationDriverReadinessState.Stopped)]
    public void S7SpecificReadiness_MapsToSharedHostContract(
        S7IsoRuntimeReadinessState s7State,
        CommunicationDriverReadinessState expectedState)
    {
        var capturedAt = DateTimeOffset.UtcNow;
        IS7IsoRuntimeReadinessSource source = new StubReadinessSource(new S7IsoRuntimeReadinessSnapshot(
            "s7-primary",
            s7State,
            capturedAt.AddMilliseconds(-5),
            capturedAt,
            s7State == S7IsoRuntimeReadinessState.Ready ? capturedAt.AddMilliseconds(-1) : null,
            s7State == S7IsoRuntimeReadinessState.Ready ? (ushort)240 : null,
            s7State == S7IsoRuntimeReadinessState.Ready,
            s7State == S7IsoRuntimeReadinessState.Ready ? 1 : 0,
            s7State == S7IsoRuntimeReadinessState.Faulted ? "protocol failed" : null));

        var common = (ICommunicationDriverReadinessSource)source;
        var snapshot = common.GetCommunicationReadiness();

        Assert.Equal("s7-primary", snapshot.DataSourceKey);
        Assert.Equal("siemens.s7.iso", snapshot.DriverType);
        Assert.Equal(expectedState, snapshot.State);
        Assert.Equal(capturedAt, snapshot.ObservedAt);
        Assert.Equal(s7State == S7IsoRuntimeReadinessState.Faulted ? "protocol failed" : null, snapshot.Reason);
        Assert.Equal(s7State == S7IsoRuntimeReadinessState.Ready ? "240" : string.Empty, snapshot.Details!["negotiatedPduSize"]);
    }

    private sealed class StubReadinessSource(S7IsoRuntimeReadinessSnapshot snapshot) : IS7IsoRuntimeReadinessSource
    {
        public S7IsoRuntimeReadinessSnapshot GetS7IsoRuntimeReadiness() => snapshot;
    }
}
