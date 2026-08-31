using System.Buffers.Binary;
using Scada.Core.Tags;
using Scada.Drivers.Iec60870;

namespace Scada.Drivers.Tests;

public sealed class Iec104ConvergenceEvidenceTests
{
    [Fact]
    public void Decoder_PreservesCotQualityAndCp56SourceTimeTogether()
    {
        var payload = new byte[11];
        new Iec104InformationObjectAddress(321).WriteTo(payload.AsSpan(0, 3));
        payload[3] = 0x00; // SPI=false, Good quality.
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), 56_789);
        payload[6] = 23;
        payload[7] = 1;
        payload[8] = 31;
        payload[9] = 8;
        payload[10] = 26;

        var cause = new Iec104CauseOfTransmission(
            causeCode: 3,
            originatorAddress: 17);
        var asdu = Iec104AsduEnvelope.Create(
            new Iec104AsduHeader(
                Iec104TypeId.MSpTb1,
                ObjectCount: 1,
                IsSequence: false,
                cause,
                CommonAddress: 1),
            payload);

        var point = Assert.Single(Iec104InformationObjectDecoder.Decode(asdu, TimeZoneInfo.Utc));

        Assert.Equal(cause, point.CauseOfTransmission);
        Assert.Equal(TagQuality.Good, point.Quality);
        Assert.Equal(new DateTimeOffset(2026, 8, 31, 1, 23, 56, 789, TimeSpan.Zero), point.SourceTimestamp);
        Assert.Equal(321, point.InformationObjectAddress.Value);
    }
}
