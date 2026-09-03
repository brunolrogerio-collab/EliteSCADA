using Scada.Drivers.Modbus;

namespace Scada.Drivers.Tests;

public sealed class ModbusTagAddressCodecTests
{
    [Theory]
    [InlineData("coil:0", ModbusDataArea.Coil, 0)]
    [InlineData("discrete:12", ModbusDataArea.DiscreteInput, 12)]
    [InlineData("holding:65535", ModbusDataArea.HoldingRegister, 65535)]
    [InlineData("input:7", ModbusDataArea.InputRegister, 7)]
    [InlineData("HR:10", ModbusDataArea.HoldingRegister, 10)]
    public void Parse_accepts_runtime_compatible_address_forms(string raw, ModbusDataArea expectedArea, int expectedAddress)
    {
        Assert.True(ModbusTagAddressCodec.TryParse(raw, null, out var area, out var address, out var error), error);
        Assert.Equal(expectedArea, area);
        Assert.Equal(expectedAddress, address);
    }

    [Fact]
    public void Parse_keeps_legacy_numeric_address_only_with_explicit_area_metadata()
    {
        var metadata = new Dictionary<string, string> { ["modbus.area"] = "holding" };
        Assert.True(ModbusTagAddressCodec.TryParse("10", metadata, out var area, out var address, out var error), error);
        Assert.Equal(ModbusDataArea.HoldingRegister, area);
        Assert.Equal((ushort)10, address);

        Assert.False(ModbusTagAddressCodec.TryParse("10", null, out _, out _, out var missingArea));
        Assert.Contains("modbus.area", missingArea);
    }

    [Theory]
    [InlineData(ModbusDataArea.Coil, 0, ModbusAddressReferenceBase.ZeroBased, "coil:0")]
    [InlineData(ModbusDataArea.HoldingRegister, 1, ModbusAddressReferenceBase.OneBased, "holding:0")]
    [InlineData(ModbusDataArea.InputRegister, 40001, ModbusAddressReferenceBase.OneBased, "input:40000")]
    public void Builder_normalizes_explicit_reference_base(
        ModbusDataArea area,
        int reference,
        ModbusAddressReferenceBase referenceBase,
        string expected)
    {
        Assert.True(ModbusTagAddressCodec.TryBuild(area, reference, referenceBase, out var canonical, out var error), error);
        Assert.Equal(expected, canonical);
        Assert.True(ModbusTagAddressCodec.TryParse(canonical, null, out var parsedArea, out var parsedAddress, out error), error);
        Assert.Equal(area, parsedArea);
        Assert.Equal(expected.Split(':')[1], parsedAddress.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65537)]
    public void One_based_builder_rejects_out_of_range_references(int reference)
    {
        Assert.False(ModbusTagAddressCodec.TryBuild(
            ModbusDataArea.HoldingRegister,
            reference,
            ModbusAddressReferenceBase.OneBased,
            out var canonical,
            out var error));
        Assert.Null(canonical);
        Assert.NotNull(error);
    }

    [Fact]
    public void Canonical_format_round_trips_without_metadata()
    {
        foreach (var area in Enum.GetValues<ModbusDataArea>())
        {
            var canonical = ModbusTagAddressCodec.Format(area, 123);
            Assert.True(ModbusTagAddressCodec.TryParse(canonical, null, out var parsedArea, out var parsedAddress, out var error), error);
            Assert.Equal(area, parsedArea);
            Assert.Equal((ushort)123, parsedAddress);
            Assert.Equal(canonical, ModbusTagAddressCodec.Format(parsedArea, parsedAddress));
        }
    }
}
