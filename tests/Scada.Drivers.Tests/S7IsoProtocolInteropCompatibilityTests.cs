using System.Buffers.Binary;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoProtocolInteropCompatibilityTests
{
    [Fact]
    public void ConnectionConfirm_CanonicalLayout_IsAccepted()
    {
        var packet = new byte[]
        {
            0x03, 0x00, 0x00, 0x0E,
            0x09, 0xD0,
            0x00, 0x01,
            0x00, 0x01,
            0x00,
            0xC0, 0x01, 0x0A
        };

        S7IsoProtocol.ValidateConnectionConfirm(packet);
    }

    [Fact]
    public void ConnectionConfirm_PythonSnap7Server312ShiftedLayout_IsAcceptedOnlyWhenReferenceMatches()
    {
        var packet = new byte[]
        {
            0x03, 0x00, 0x00, 0x0F,
            0x0A, 0xD0,
            0x00,
            0x00, 0x01,
            0x00, 0x01,
            0x00,
            0xC0, 0x01, 0x0A
        };

        S7IsoProtocol.ValidateConnectionConfirm(packet);

        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(7, 2), 0x0022);
        Assert.Throws<S7IsoProtocolException>(() => S7IsoProtocol.ValidateConnectionConfirm(packet));
    }
}
