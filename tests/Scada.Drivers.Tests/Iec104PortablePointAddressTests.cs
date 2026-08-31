using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104PortablePointAddressTests
{
    [Theory]
    [InlineData(0, 0, "ca=0;ioa=0")]
    [InlineData(1, 77, "ca=1;ioa=77")]
    [InlineData(65535, 16777215, "ca=65535;ioa=16777215")]
    public void Address_RoundTripsCanonicalIdentity(ushort commonAddress, int ioa, string expected)
    {
        var address = new Iec104PortablePointAddress(commonAddress, ioa);

        Assert.Equal(expected, address.ToString());
        Assert.Equal(address, Iec104PortablePointAddress.Parse(expected));
    }

    [Fact]
    public void TryParse_AcceptsFieldOrderAndCaseButEmitsCanonicalForm()
    {
        Assert.True(Iec104PortablePointAddress.TryParse(" IOA=42 ; CA=7 ", out var address));

        Assert.Equal((ushort)7, address.CommonAddress);
        Assert.Equal(42, address.InformationObjectAddress);
        Assert.Equal("ca=7;ioa=42", address.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("ca=1")]
    [InlineData("ioa=2")]
    [InlineData("ca=1;ioa=-1")]
    [InlineData("ca=1;ioa=16777216")]
    [InlineData("ca=65536;ioa=1")]
    [InlineData("ca=1;ca=2;ioa=3")]
    [InlineData("ca=1;ioa=2;type=1")]
    public void TryParse_RejectsMalformedOrExtendedIdentity(string value)
    {
        Assert.False(Iec104PortablePointAddress.TryParse(value, out _));
    }

    [Fact]
    public void Constructor_RejectsIoaOutside24BitRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Iec104PortablePointAddress(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Iec104PortablePointAddress(1, 0x1000000));
    }
}
