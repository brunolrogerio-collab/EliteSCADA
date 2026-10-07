using Google.Protobuf;
using Scada.Drivers.ESPHome.Protocol;
using Scada.Drivers.ESPHome.Protocol.Generated;

namespace Scada.Drivers.Tests;

public sealed class EspHomeNativeProtocolFoundationTests
{
    [Fact]
    public void PlaintextFrame_RoundTripsHelloAndVarints()
    {
        var hello = EspHomeNativeHandshake.CreateHello("EliteSCADA-L0");
        var bytes = EspHomePlaintextFrameCodec.Encode(EspHomeNativeMessageId.HelloRequest, hello);
        var frame = EspHomePlaintextFrameCodec.Decode(bytes, out var consumed);
        Assert.Equal(bytes.Length, consumed);
        Assert.Equal(EspHomeNativeMessageId.HelloRequest, frame.MessageType);

        var parsed = HelloRequest.Parser.ParseFrom(frame.Payload.Span);
        Assert.Equal("EliteSCADA-L0", parsed.ClientInfo);
        Assert.Equal(1u, parsed.ApiVersionMajor);
        Assert.Equal(15u, parsed.ApiVersionMinor);
    }

    [Fact]
    public void PlaintextFrame_RejectsMalformedAndTruncatedInput()
    {
        Assert.Throws<InvalidDataException>(() => EspHomePlaintextFrameCodec.Decode(new byte[] { 1, 0, 0 }, out _));
        Assert.Throws<EndOfStreamException>(() => EspHomePlaintextFrameCodec.Decode(new byte[] { 0, 0x80 }, out _));
        Assert.Throws<EndOfStreamException>(() => EspHomePlaintextFrameCodec.Decode(new byte[] { 0, 3, 1, 1 }, out _));
        Assert.Throws<InvalidDataException>(() => EspHomePlaintextFrameCodec.Decode(new byte[] { 0, 0x80, 0x80, 0x80, 0x80, 0x10 }, out _));
    }

    [Fact]
    public void PlaintextFrame_EnforcesPayloadAndMessageTypeBounds()
    {
        var oversizedLength = new byte[] { 0, 0x81, 0x80, 0x80, 0x02, 1 };
        Assert.Throws<InvalidDataException>(() => EspHomePlaintextFrameCodec.Decode(oversizedLength, out _, 1024));

        var typeOverUInt16 = new byte[] { 0, 0, 0x80, 0x80, 0x04 };
        Assert.Throws<InvalidDataException>(() => EspHomePlaintextFrameCodec.Decode(typeOverUInt16, out _));
    }

    [Fact]
    public void Negotiation_RejectsMajorMismatch_AndGatesApi15CapabilityMessage()
    {
        Assert.Throws<NotSupportedException>(() => EspHomeNativeHandshake.Negotiate(new HelloResponse { ApiVersionMajor = 2, ApiVersionMinor = 0 }));

        var oldPeer = EspHomeNativeHandshake.Negotiate(new HelloResponse { ApiVersionMajor = 1, ApiVersionMinor = 14 });
        Assert.Equal(new EspHomeApiVersion(1, 14), oldPeer);
        Assert.False(EspHomeNativeHandshake.SupportsDeviceCapabilities(oldPeer));

        var currentPeer = EspHomeNativeHandshake.Negotiate(new HelloResponse { ApiVersionMajor = 1, ApiVersionMinor = 16 });
        Assert.Equal(new EspHomeApiVersion(1, 15), currentPeer);
        Assert.True(EspHomeNativeHandshake.SupportsDeviceCapabilities(currentPeer));
    }

    [Fact]
    public void DeviceInfo_UsesMacAsStablePhysicalIdentity_NotEndpoint()
    {
        var mapped = EspHomeDeviceInfoMapper.Map(new DeviceInfoResponse
        {
            Name = "plant-sensor",
            FriendlyName = "Plant Sensor",
            MacAddress = "ac-bc-32-89-0e-a9",
            EsphomeVersion = "2026.9.0",
            CompilationTime = "2026-09-30 10:20:00",
            Manufacturer = "Espressif",
            Model = "ESP32",
            ProjectName = "elite.fixture",
            ProjectVersion = "1",
            HasDeepSleep = true
        });

        Assert.Equal("AC:BC:32:89:0E:A9", mapped.StableDeviceIdentity);
        Assert.Equal("ac-bc-32-89-0e-a9", mapped.OriginalMacAddress);
        Assert.True(mapped.HasDeepSleep);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-mac")]
    [InlineData("AA:BB:CC:DD:EE")]
    public void DeviceInfo_RejectsUnavailableOrInvalidStableIdentity(string mac) =>
        Assert.Throws<InvalidDataException>(() => EspHomeDeviceInfoMapper.NormalizeMac(mac));

    [Fact]
    public void ProtobufParser_ToleratesUnknownAdditiveFields()
    {
        var known = new HelloResponse { ApiVersionMajor = 1, ApiVersionMinor = 15, ServerInfo = "ESPHome" }.ToByteArray();
        var withUnknown = known.Concat(new byte[] { 0xA0, 0x06, 0x01 }).ToArray(); // field 100, varint 1
        var parsed = HelloResponse.Parser.ParseFrom(withUnknown);
        Assert.Equal(1u, parsed.ApiVersionMajor);
        Assert.Equal(15u, parsed.ApiVersionMinor);
    }

    [Fact]
    public void CurrentProtocol_ReservesLegacyPasswordIds()
    {
        Assert.Equal((ushort)3, EspHomeNativeMessageId.AuthenticationRequestReserved);
        Assert.Equal((ushort)4, EspHomeNativeMessageId.AuthenticationResponseReserved);
    }
}
