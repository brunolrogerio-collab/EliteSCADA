using Scada.Core.Tags;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104AsduPrimitivesTests
{
    [Fact]
    public void AsduHeaderRoundTripPreservesVsqCotAndCommonAddress()
    {
        var header = new Iec104AsduHeader(
            Iec104TypeId.MSpTb1,
            3,
            true,
            new Iec104CauseOfTransmission(7, originatorAddress: 2, isNegativeConfirmation: true, isTest: true),
            0x1234);
        var asdu = Iec104AsduEnvelope.Create(header, new byte[] { 0x11, 0x22, 0x33 });

        var encoded = Iec104AsduCodec.Serialize(asdu);

        Assert.Equal((byte)Iec104TypeId.MSpTb1, encoded[0]);
        Assert.Equal((byte)0x83, encoded[1]);
        Assert.Equal((byte)0xC7, encoded[2]);
        Assert.Equal((byte)0x02, encoded[3]);
        Assert.Equal((byte)0x34, encoded[4]);
        Assert.Equal((byte)0x12, encoded[5]);

        var decoded = Iec104AsduCodec.Parse(encoded);
        Assert.Equal(header, decoded.Header);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33 }, decoded.Payload.ToArray());
    }

    [Fact]
    public void InformationObjectAddressUsesCanonicalThreeOctetLittleEndianEncoding()
    {
        var address = new Iec104InformationObjectAddress(0x123456);
        var bytes = new byte[3];

        address.WriteTo(bytes);

        Assert.Equal(new byte[] { 0x56, 0x34, 0x12 }, bytes);
        Assert.Equal(address, Iec104InformationObjectAddress.Parse(bytes));
    }

    [Fact]
    public void InformationObjectAddressRejectsValuesOutside24Bits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Iec104InformationObjectAddress(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Iec104InformationObjectAddress(0x01000000));
    }

    [Theory]
    [InlineData(0x80, TagQuality.BadDevice)]
    [InlineData(0x40, TagQuality.Stale)]
    [InlineData(0x20, TagQuality.Uncertain)]
    [InlineData(0x10, TagQuality.Uncertain)]
    [InlineData(0x00, TagQuality.Good)]
    public void SiqQualityMapsWithLockedEliteScadaPrecedence(byte siq, TagQuality expected)
    {
        Assert.Equal(expected, Iec104QualityDescriptor.FromSiq(siq).ToTagQuality());
    }

    [Fact]
    public void QdsInvalidOutranksNotTopicalAndOverflow()
    {
        var quality = Iec104QualityDescriptor.FromQds(0xC1);

        Assert.True(quality.Invalid);
        Assert.True(quality.NotTopical);
        Assert.True(quality.Overflow);
        Assert.Equal(TagQuality.BadDevice, quality.ToTagQuality());
    }

    [Theory]
    [InlineData(Iec104DoublePointState.Indeterminate0, true)]
    [InlineData(Iec104DoublePointState.Off, false)]
    [InlineData(Iec104DoublePointState.On, false)]
    [InlineData(Iec104DoublePointState.Indeterminate3, true)]
    public void DoublePointIndeterminateStatesCanBePublishedAsUncertain(Iec104DoublePointState state, bool semanticUncertain)
    {
        var quality = Iec104QualityDescriptor.FromDiq((byte)state);

        Assert.Equal(semanticUncertain ? TagQuality.Uncertain : TagQuality.Good, quality.ToTagQuality(semanticUncertain));
    }
}
