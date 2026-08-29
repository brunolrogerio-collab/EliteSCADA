using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoConnectionOptionsTests
{
    [Fact]
    public void RackSlot_DerivesDestinationTsapWithoutManufacturerHeuristic()
    {
        var options = new S7IsoConnectionOptions(
            "192.0.2.10",
            S7CpuFamily.S71500,
            S7IsoConnectionMode.RackSlot,
            rack: 0,
            slot: 1,
            connectionRole: S7IsoConnectionRole.Basic,
            sourceTsap: 0x0100);

        Assert.Equal((ushort)0x0301, options.EffectiveDestinationTsap);
        Assert.Equal((ushort)0x0100, options.EffectiveSourceTsap);
        Assert.Equal("0x0301", S7IsoConnectionOptions.FormatTsap(options.EffectiveDestinationTsap));
    }

    [Theory]
    [InlineData("0x0301", 0x0301)]
    [InlineData("03.01", 0x0301)]
    [InlineData("0301", 0x0301)]
    [InlineData("769", 769)]
    public void TryParseTsap_AcceptsCanonicalEngineeringForms(string text, int expected)
    {
        Assert.True(S7IsoConnectionOptions.TryParseTsap(text, out var parsed));
        Assert.Equal((ushort)expected, parsed);
    }

    [Fact]
    public void ExplicitTsap_RequiresDestinationTsap()
    {
        Assert.Throws<ArgumentException>(() => new S7IsoConnectionOptions(
            "plc",
            S7CpuFamily.S7300,
            S7IsoConnectionMode.ExplicitTsap));
    }

    [Fact]
    public void RackAndSlot_AreRangeValidated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new S7IsoConnectionOptions(
            "plc",
            S7CpuFamily.S7400,
            S7IsoConnectionMode.RackSlot,
            rack: 8));

        Assert.Throws<ArgumentOutOfRangeException>(() => new S7IsoConnectionOptions(
            "plc",
            S7CpuFamily.S7400,
            S7IsoConnectionMode.RackSlot,
            slot: 32));
    }
}
