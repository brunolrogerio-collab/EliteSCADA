using Scada.Drivers.Abstractions;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104TagBindingCatalogContractTests
{
    [Fact]
    public void Descriptor_MonitoredTypeValuesAreExactlyRuntimeDecodable()
    {
        var field = Assert.Single(
            Iec104DriverDescriptorProvider.SharedDescriptor.ConfigurationSchema.TagBindingFields,
            candidate => candidate.Key == "iec104.typeId");

        Assert.Equal(12, field.AllowedValues?.Count);
        foreach (var raw in field.AllowedValues ?? Array.Empty<string>())
        {
            Assert.True(Enum.TryParse<Iec104TypeId>(raw, ignoreCase: true, out var typeId), raw);
            Assert.True(Iec104InformationObjectDecoder.IsSupported(typeId), raw);
        }
    }

    [Fact]
    public void Descriptor_CommandTypeValuesAreExactlyRuntimeCommandTypes()
    {
        var field = Assert.Single(
            Iec104DriverDescriptorProvider.SharedDescriptor.ConfigurationSchema.TagBindingFields,
            candidate => candidate.Key == "iec104.commandTypeId");

        Assert.Equal(
            new[] { "CScNa1", "CDcNa1", "CSeNa1", "CSeNb1", "CSeNc1" },
            field.AllowedValues);
        foreach (var raw in field.AllowedValues ?? Array.Empty<string>())
            Assert.True(Enum.TryParse<Iec104TypeId>(raw, ignoreCase: true, out _), raw);
    }

    [Theory]
    [InlineData("M_SP_NA_1", Iec104TypeId.MSpNa1)]
    [InlineData("MSpNa1", Iec104TypeId.MSpNa1)]
    [InlineData("1", Iec104TypeId.MSpNa1)]
    [InlineData("C_SE_NC_1", Iec104TypeId.CSeNc1)]
    [InlineData("CSeNc1", Iec104TypeId.CSeNc1)]
    public void Codec_PreservesStandardLegacyAndNumericCompatibility(string raw, Iec104TypeId expected)
    {
        Assert.True(Iec104TypeIdCodec.TryParse(raw, out var actual));
        Assert.Equal(expected, actual);
    }
}
