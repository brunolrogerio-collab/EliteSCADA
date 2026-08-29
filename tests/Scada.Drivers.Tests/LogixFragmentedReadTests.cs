using Scada.Drivers.AllenBradley;

namespace Scada.Drivers.Tests;

public sealed class LogixFragmentedReadTests
{
    [Fact]
    public void BuildRequest_MatchesRockwellDocumentedSecondSintFragment()
    {
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "TotalCount", LogixNativeType.Sint);

        var request = LogixFragmentedRead.BuildRequest(reference, 1750, 490);

        byte[] expected =
        [
            0x52, 0x06,
            0x91, 0x0A, 0x54, 0x6F, 0x74, 0x61, 0x6C, 0x43, 0x6F, 0x75, 0x6E, 0x74,
            0xD6, 0x06,
            0xEA, 0x01, 0x00, 0x00
        ];
        Assert.Equal(expected, request);
    }

    [Fact]
    public void ParseAndAssemble_RequiresOrderedProgressAndFinalCompletion()
    {
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "TotalCount", LogixNativeType.Sint);
        var source = Enumerable.Range(0, 1750).Select(static value => (byte)(value & 0xFF)).ToArray();

        var fragments = new List<LogixReadFragment>();
        uint offset = 0;
        foreach (var length in new[] { 490, 490, 490, 280 })
        {
            var hasMore = offset + (uint)length < (uint)source.Length;
            var responseData = new byte[2 + length];
            responseData[0] = 0xC2;
            responseData[1] = 0x00;
            source.AsSpan((int)offset, length).CopyTo(responseData.AsSpan(2));
            var response = new LogixCipResponse(
                0xD2,
                hasMore ? (byte)0x06 : (byte)0x00,
                Array.Empty<ushort>(),
                responseData);

            var fragment = LogixFragmentedRead.ParseResponse(reference, offset, response);
            fragments.Add(fragment);
            offset = LogixFragmentedRead.NextByteOffset(fragment);
        }

        var assembled = LogixFragmentedRead.AssembleCompletePayload(reference, 1750, fragments);

        Assert.Equal(source, assembled);
        Assert.Equal(1750u, offset);
        Assert.True(fragments.Take(3).All(static fragment => fragment.HasMore));
        Assert.False(fragments[^1].HasMore);
    }

    [Fact]
    public void AssembleCompletePayload_RejectsOutOfOrderIncompleteAndOversizedTransfers()
    {
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "ArrayTag", LogixNativeType.Dint);
        var typeCode = LogixValueCodec.CipTypeDint;

        Assert.Throws<InvalidDataException>(() => LogixFragmentedRead.AssembleCompletePayload(
            reference,
            2,
            [
                new LogixReadFragment(0, typeCode, [1, 2, 3, 4], true),
                new LogixReadFragment(5, typeCode, [5, 6, 7, 8], false)
            ]));

        var incomplete = Assert.Throws<LogixCipException>(() => LogixFragmentedRead.AssembleCompletePayload(
            reference,
            2,
            [new LogixReadFragment(0, typeCode, [1, 2, 3, 4], true)]));
        Assert.Equal(LogixProtocolError.FragmentationFailed, incomplete.Error);

        var oversized = Assert.Throws<LogixCipException>(() => LogixFragmentedRead.AssembleCompletePayload(
            reference,
            2,
            [new LogixReadFragment(0, typeCode, [1, 2, 3, 4, 5, 6, 7, 8], false)],
            maximumValueBytes: 4));
        Assert.Equal(LogixProtocolError.FragmentationFailed, oversized.Error);
    }

    [Fact]
    public void ParseResponse_FailsClosedOnZeroProgressTypeChangeAndUnsupportedPackedBool()
    {
        var reference = new LogixSymbolReference(LogixTagScope.Controller, "ArrayTag", LogixNativeType.Int);

        Assert.Throws<InvalidDataException>(() => LogixFragmentedRead.ParseResponse(
            reference,
            0,
            new LogixCipResponse(0xD2, 0x06, Array.Empty<ushort>(), [0xC3, 0x00])));

        var mismatch = Assert.Throws<LogixCipException>(() => LogixFragmentedRead.ParseResponse(
            reference,
            0,
            new LogixCipResponse(0xD2, 0x00, Array.Empty<ushort>(), [0xC4, 0x00, 0x01, 0x00])));
        Assert.Equal(LogixProtocolError.TypeMismatch, mismatch.Error);

        var boolReference = new LogixSymbolReference(LogixTagScope.Controller, "PackedFlags", LogixNativeType.Bool);
        Assert.Throws<NotSupportedException>(() => LogixFragmentedRead.BuildRequest(boolReference, 64, 0));
    }
}
