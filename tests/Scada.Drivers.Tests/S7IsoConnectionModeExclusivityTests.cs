using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoConnectionModeExclusivityTests
{
    [Fact]
    public void RackSlot_RejectsExplicitDestinationTsap()
    {
        var error = Assert.Throws<ArgumentException>(() => new S7IsoConnectionOptions(
            "127.0.0.1",
            S7CpuFamily.S71500,
            S7IsoConnectionMode.RackSlot,
            rack: 0,
            slot: 1,
            connectionRole: S7IsoConnectionRole.Basic,
            destinationTsap: 0x0301));

        Assert.Equal("destinationTsap", error.ParamName);
    }

    [Fact]
    public void ExplicitTsap_RejectsResidualRackOrSlot()
    {
        var rackError = Assert.Throws<ArgumentException>(() => new S7IsoConnectionOptions(
            "127.0.0.1",
            S7CpuFamily.S71500,
            S7IsoConnectionMode.ExplicitTsap,
            rack: 0,
            destinationTsap: 0x0301));
        var slotError = Assert.Throws<ArgumentException>(() => new S7IsoConnectionOptions(
            "127.0.0.1",
            S7CpuFamily.S71500,
            S7IsoConnectionMode.ExplicitTsap,
            slot: 1,
            destinationTsap: 0x0301));

        Assert.Equal("rack", rackError.ParamName);
        Assert.Equal("slot", slotError.ParamName);
    }

    [Fact]
    public void ExplicitTsap_PreservesOnlyTsapIdentity()
    {
        var options = new S7IsoConnectionOptions(
            "127.0.0.1",
            S7CpuFamily.S71500,
            S7IsoConnectionMode.ExplicitTsap,
            sourceTsap: 0x0100,
            destinationTsap: 0x0301);

        Assert.Null(options.Rack);
        Assert.Null(options.Slot);
        Assert.Equal((ushort)0x0100, options.EffectiveSourceTsap);
        Assert.Equal((ushort)0x0301, options.EffectiveDestinationTsap);
    }
}
