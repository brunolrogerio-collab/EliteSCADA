using System.Text;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Dnp3.OpenDnp3;

namespace Scada.Drivers.Tests;

public sealed class OpenDnp3HostProtocolTests
{
    [Fact]
    public void ParsesTimestampedDoubleBitEventWithoutLosingQuality()
    {
        var line = "V1\tMEASUREMENT\tDoubleBitBinaryInput\t12\t4\t3\t1\t1\t1\t0\t0\t1\t0\t0\t0\t0\t0\t0\t1788240600000\t1\tenum\t2";

        var message = Assert.IsType<OpenDnp3HostMeasurementMessage>(OpenDnp3HostProtocol.Parse(line));
        var measurement = message.Measurement;

        Assert.Equal(Dnp3PointKind.DoubleBitBinaryInput, measurement.PointKind);
        Assert.Equal((ushort)12, measurement.Index);
        Assert.Equal(new Dnp3ObjectVariation(4, 3), measurement.Variation);
        Assert.True(measurement.IsEvent);
        Assert.Equal(Dnp3DoubleBitState.DeterminedOn, Assert.IsType<Dnp3DoubleBitState>(measurement.Value));
        Assert.True(measurement.Flags.HasFlags);
        Assert.True(measurement.Flags.Online);
        Assert.True(measurement.Flags.RemoteForced);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1788240600000), measurement.SourceTimestamp);
        Assert.True(measurement.SourceTimestampSynchronized);
    }

    [Fact]
    public void ParsesCommandFailureWithOutstationStatusAndMessage()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("interlock active"));
        var message = Assert.IsType<OpenDnp3HostCommandMessage>(
            OpenDnp3HostProtocol.Parse($"V1\tCOMMAND\t42\t0\tAUTOMATION_INHIBIT\t{encoded}"));

        Assert.Equal(42, message.RequestId);
        Assert.False(message.Result.Succeeded);
        Assert.Equal("AUTOMATION_INHIBIT", message.Result.Status);
        Assert.Equal("interlock active", message.Result.Message);
    }

    [Fact]
    public void ParsesNativeDiagnosticEvidence()
    {
        var message = Assert.IsType<OpenDnp3HostDiagnosticMessage>(
            OpenDnp3HostProtocol.Parse("V1\tDIAGNOSTIC\tEVENT_BUFFER_OVERFLOW"));

        Assert.Equal("EVENT_BUFFER_OVERFLOW", message.Kind);
    }

    [Fact]
    public void BuildsCrobWithoutVendorTypesInManagedContract()
    {
        var profile = new Dnp3BinaryCommandProfile
        {
            Mode = Dnp3CommandMode.SelectBeforeOperate,
            TripCloseCode = Dnp3TripCloseCode.Trip,
            Count = 2,
            TrueOperation = Dnp3BinaryOperation.PulseOn,
            OnTime = TimeSpan.FromMilliseconds(250),
            OffTime = TimeSpan.FromMilliseconds(100)
        };

        var line = OpenDnp3HostProtocol.BuildBinaryCommand(7, 3, Dnp3BinaryOperation.PulseOn, profile);

        Assert.Equal("V1\tBINARY\t7\t3\tPulseOn\tSelectBeforeOperate\tTrip\t2\t250\t100", line);
    }

    [Fact]
    public void BuildsFloat64AnalogCommandUsingInvariantWireFormat()
    {
        var profile = new Dnp3AnalogCommandProfile(Dnp3CommandMode.DirectOperate, Dnp3AnalogOutputVariation.Float64);

        var line = OpenDnp3HostProtocol.BuildAnalogCommand(8, 5, 12.5d, profile);

        Assert.Equal("V1\tANALOG\t8\t5\tFloat64\tDirectOperate\t12.5", line);
    }

    [Fact]
    public void RejectsUnknownProtocolVersion()
    {
        Assert.Throws<FormatException>(() => OpenDnp3HostProtocol.Parse("V2\tREADY"));
    }
}
