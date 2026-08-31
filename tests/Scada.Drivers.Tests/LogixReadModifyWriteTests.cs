using Scada.Drivers.AllenBradley;

namespace Scada.Drivers.Tests;

public sealed class LogixReadModifyWriteTests
{
    [Fact]
    public void BuildRequest_MatchesRockwellControlWordExample()
    {
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "ControlWord", LogixNativeType.Dint);

        var request = LogixReadModifyWrite.BuildRequest(
            reference,
            [0x04, 0x00, 0x00, 0x00],
            [0xDF, 0xFF, 0xFF, 0xFF]);

        byte[] expected =
        [
            0x4E, 0x07,
            0x91, 0x0B, 0x43, 0x6F, 0x6E, 0x74, 0x72, 0x6F, 0x6C, 0x57, 0x6F, 0x72, 0x64, 0x00,
            0x04, 0x00,
            0x04, 0x00, 0x00, 0x00,
            0xDF, 0xFF, 0xFF, 0xFF
        ];

        Assert.Equal(expected, request);
    }

    [Theory]
    [InlineData(LogixNativeType.Sint, 7, true, 0, 0x80, 0xFF)]
    [InlineData(LogixNativeType.Int, 8, true, 1, 0x01, 0xFF)]
    [InlineData(LogixNativeType.Dint, 31, false, 3, 0x00, 0x7F)]
    [InlineData(LogixNativeType.Lint, 63, false, 7, 0x00, 0x7F)]
    public void BuildBitRequest_UsesFullWidthMasksAndCanonicalLsbBitNumbering(
        LogixNativeType nativeType,
        int bitIndex,
        bool value,
        int affectedByte,
        byte expectedOrByte,
        byte expectedAndByte)
    {
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "Word", nativeType);
        var request = LogixReadModifyWrite.BuildBitRequest(reference, bitIndex, value);
        var pathLength = LogixCipCodec.EncodeSymbolicPath(reference).Length;
        var maskSizeOffset = 2 + pathLength;
        var maskSize = request[maskSizeOffset];
        var orOffset = maskSizeOffset + 2;
        var andOffset = orOffset + maskSize;

        Assert.Equal(LogixValueCodec.GetNativeIntegerBitWidth(nativeType)!.Value / 8, maskSize);
        Assert.Equal(expectedOrByte, request[orOffset + affectedByte]);
        Assert.Equal(expectedAndByte, request[andOffset + affectedByte]);

        for (var index = 0; index < maskSize; index++)
        {
            if (index == affectedByte) continue;
            Assert.Equal(0x00, request[orOffset + index]);
            Assert.Equal(0xFF, request[andOffset + index]);
        }
    }

    [Fact]
    public void BuildBitRequest_RejectsUnsupportedTypeAndOutOfRangeBit()
    {
        var real = new LogixSymbolReference(LogixTagScope.Controller, "Value", LogixNativeType.Real);
        Assert.Throws<NotSupportedException>(() => LogixReadModifyWrite.BuildBitRequest(real, 0, true));

        var dint = new LogixSymbolReference(LogixTagScope.Controller, "Word", LogixNativeType.Dint);
        Assert.Throws<ArgumentOutOfRangeException>(() => LogixReadModifyWrite.BuildBitRequest(dint, 32, true));
    }

    [Fact]
    public void BuildRequest_RequiresFullNativeWidthForDataIntegrity()
    {
        var dint = new LogixSymbolReference(LogixTagScope.Controller, "Word", LogixNativeType.Dint);

        var error = Assert.Throws<ArgumentException>(() => LogixReadModifyWrite.BuildRequest(
            dint,
            [0x01, 0x00],
            [0xFF, 0xFF]));

        Assert.Contains("exactly 4 bytes", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateResponse_RequiresReadModifyWriteReplyServiceAndSuccess()
    {
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "Word", LogixNativeType.Dint);

        LogixReadModifyWrite.ValidateResponse(
            reference,
            new LogixCipResponse(0xCE, 0x00, Array.Empty<ushort>(), Array.Empty<byte>()));

        Assert.Throws<InvalidDataException>(() => LogixReadModifyWrite.ValidateResponse(
            reference,
            new LogixCipResponse(0xCD, 0x00, Array.Empty<ushort>(), Array.Empty<byte>())));

        var error = Assert.Throws<LogixCipException>(() => LogixReadModifyWrite.ValidateResponse(
            reference,
            new LogixCipResponse(0xCE, 0x0F, Array.Empty<ushort>(), Array.Empty<byte>())));
        Assert.Equal(LogixProtocolError.AccessDenied, error.Error);
    }
}
