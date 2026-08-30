using System.Buffers.Binary;

namespace Scada.Drivers.Iec60870;

public enum Iec104ApciFrameFormat
{
    I,
    S,
    U
}

public enum Iec104UFunction
{
    StartDataTransferActivation,
    StartDataTransferConfirmation,
    StopDataTransferActivation,
    StopDataTransferConfirmation,
    TestFrameActivation,
    TestFrameConfirmation
}

public sealed class Iec104ProtocolException : IOException
{
    public Iec104ProtocolException(string message) : base(message)
    {
    }
}

public sealed record Iec104ApciFrame
{
    private Iec104ApciFrame(
        Iec104ApciFrameFormat format,
        ushort sendSequence,
        ushort receiveSequence,
        Iec104UFunction? uFunction,
        ReadOnlyMemory<byte> asdu)
    {
        Format = format;
        SendSequence = sendSequence;
        ReceiveSequence = receiveSequence;
        UFunction = uFunction;
        Asdu = asdu;
    }

    public Iec104ApciFrameFormat Format { get; }
    public ushort SendSequence { get; }
    public ushort ReceiveSequence { get; }
    public Iec104UFunction? UFunction { get; }
    public ReadOnlyMemory<byte> Asdu { get; }

    public static Iec104ApciFrame I(ushort sendSequence, ushort receiveSequence, ReadOnlySpan<byte> asdu)
    {
        ValidateSequence(sendSequence, nameof(sendSequence));
        ValidateSequence(receiveSequence, nameof(receiveSequence));
        if (asdu.IsEmpty) throw new ArgumentException("IEC-104 I-format frames require an ASDU payload.", nameof(asdu));
        return new Iec104ApciFrame(Iec104ApciFrameFormat.I, sendSequence, receiveSequence, null, asdu.ToArray());
    }

    public static Iec104ApciFrame S(ushort receiveSequence)
    {
        ValidateSequence(receiveSequence, nameof(receiveSequence));
        return new Iec104ApciFrame(Iec104ApciFrameFormat.S, 0, receiveSequence, null, ReadOnlyMemory<byte>.Empty);
    }

    public static Iec104ApciFrame U(Iec104UFunction function) =>
        new(Iec104ApciFrameFormat.U, 0, 0, function, ReadOnlyMemory<byte>.Empty);

    private static void ValidateSequence(ushort sequence, string parameterName)
    {
        if (sequence >= Iec104ApciCodec.SequenceModulo)
            throw new ArgumentOutOfRangeException(parameterName, sequence, "IEC-104 sequence numbers are 15-bit values in the range 0..32767.");
    }
}

public static class Iec104ApciCodec
{
    public const byte StartByte = 0x68;
    public const int SequenceModulo = 32768;
    public const int ControlFieldLength = 4;
    public const int MaximumApduLength = 253;
    public const int MaximumAsduLength = MaximumApduLength - ControlFieldLength;

    public static byte[] Serialize(Iec104ApciFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var asduLength = frame.Asdu.Length;
        if (asduLength > MaximumAsduLength)
            throw new ArgumentOutOfRangeException(nameof(frame), $"IEC-104 ASDU length cannot exceed {MaximumAsduLength} bytes.");

        var apduLength = ControlFieldLength + asduLength;
        if (frame.Format is Iec104ApciFrameFormat.S or Iec104ApciFrameFormat.U && asduLength != 0)
            throw new ArgumentException("IEC-104 S/U frames cannot carry an ASDU payload.", nameof(frame));
        if (frame.Format == Iec104ApciFrameFormat.I && asduLength == 0)
            throw new ArgumentException("IEC-104 I-format frames require an ASDU payload.", nameof(frame));

        var bytes = new byte[2 + apduLength];
        bytes[0] = StartByte;
        bytes[1] = checked((byte)apduLength);

        switch (frame.Format)
        {
            case Iec104ApciFrameFormat.I:
                WriteSequence(bytes.AsSpan(2, 2), frame.SendSequence);
                WriteSequence(bytes.AsSpan(4, 2), frame.ReceiveSequence);
                frame.Asdu.Span.CopyTo(bytes.AsSpan(6));
                break;

            case Iec104ApciFrameFormat.S:
                bytes[2] = 0x01;
                bytes[3] = 0x00;
                WriteSequence(bytes.AsSpan(4, 2), frame.ReceiveSequence);
                break;

            case Iec104ApciFrameFormat.U:
                if (frame.UFunction is null)
                    throw new ArgumentException("IEC-104 U-format frame requires a U function.", nameof(frame));
                bytes[2] = EncodeUFunction(frame.UFunction.Value);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(frame), frame.Format, "Unsupported IEC-104 APCI frame format.");
        }

        return bytes;
    }

    public static Iec104ApciFrame Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 6)
            throw new Iec104ProtocolException("IEC-104 APDU is shorter than the 6-byte minimum frame.");
        if (data[0] != StartByte)
            throw new Iec104ProtocolException($"IEC-104 APDU start byte must be 0x{StartByte:X2}.");

        var apduLength = data[1];
        if (apduLength < ControlFieldLength)
            throw new Iec104ProtocolException("IEC-104 APDU length is smaller than the control field.");
        if (apduLength > MaximumApduLength)
            throw new Iec104ProtocolException($"IEC-104 APDU length cannot exceed {MaximumApduLength} bytes.");
        if (data.Length != apduLength + 2)
            throw new Iec104ProtocolException("IEC-104 APDU length byte does not match the supplied frame length.");

        var c0 = data[2];
        var c1 = data[3];
        var c2 = data[4];
        var c3 = data[5];

        if ((c0 & 0x01) == 0)
        {
            var sendSequence = ReadSequence(data.Slice(2, 2));
            var receiveSequence = ReadSequence(data.Slice(4, 2));
            var asdu = data.Slice(6);
            if (asdu.IsEmpty)
                throw new Iec104ProtocolException("IEC-104 I-format frame does not contain an ASDU.");
            return Iec104ApciFrame.I(sendSequence, receiveSequence, asdu);
        }

        if ((c0 & 0x03) == 0x01)
        {
            if (apduLength != ControlFieldLength || c0 != 0x01 || c1 != 0x00)
                throw new Iec104ProtocolException("Invalid IEC-104 S-format control field.");
            return Iec104ApciFrame.S(ReadSequence(data.Slice(4, 2)));
        }

        if ((c0 & 0x03) == 0x03)
        {
            if (apduLength != ControlFieldLength || c1 != 0x00 || c2 != 0x00 || c3 != 0x00)
                throw new Iec104ProtocolException("Invalid IEC-104 U-format control field.");
            return Iec104ApciFrame.U(DecodeUFunction(c0));
        }

        throw new Iec104ProtocolException("Unknown IEC-104 APCI frame format.");
    }

    public static bool TryParse(ReadOnlySpan<byte> data, out Iec104ApciFrame? frame)
    {
        try
        {
            frame = Parse(data);
            return true;
        }
        catch (Iec104ProtocolException)
        {
            frame = null;
            return false;
        }
    }

    private static void WriteSequence(Span<byte> destination, ushort sequence)
    {
        if (sequence >= SequenceModulo)
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "IEC-104 sequence number must be in the range 0..32767.");
        BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((ushort)(sequence << 1)));
    }

    private static ushort ReadSequence(ReadOnlySpan<byte> source)
    {
        var encoded = BinaryPrimitives.ReadUInt16LittleEndian(source);
        if ((encoded & 0x0001) != 0)
            throw new Iec104ProtocolException("IEC-104 sequence control field contains a reserved low bit.");
        return checked((ushort)(encoded >> 1));
    }

    private static byte EncodeUFunction(Iec104UFunction function) => function switch
    {
        Iec104UFunction.StartDataTransferActivation => 0x07,
        Iec104UFunction.StartDataTransferConfirmation => 0x0B,
        Iec104UFunction.StopDataTransferActivation => 0x13,
        Iec104UFunction.StopDataTransferConfirmation => 0x23,
        Iec104UFunction.TestFrameActivation => 0x43,
        Iec104UFunction.TestFrameConfirmation => 0x83,
        _ => throw new ArgumentOutOfRangeException(nameof(function), function, "Unsupported IEC-104 U-format function.")
    };

    private static Iec104UFunction DecodeUFunction(byte control) => control switch
    {
        0x07 => Iec104UFunction.StartDataTransferActivation,
        0x0B => Iec104UFunction.StartDataTransferConfirmation,
        0x13 => Iec104UFunction.StopDataTransferActivation,
        0x23 => Iec104UFunction.StopDataTransferConfirmation,
        0x43 => Iec104UFunction.TestFrameActivation,
        0x83 => Iec104UFunction.TestFrameConfirmation,
        _ => throw new Iec104ProtocolException($"Unsupported IEC-104 U-format control value 0x{control:X2}.")
    };
}
