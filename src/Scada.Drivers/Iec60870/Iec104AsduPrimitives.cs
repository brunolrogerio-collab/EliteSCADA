using System.Buffers.Binary;
using Scada.Core.Tags;

namespace Scada.Drivers.Iec60870;

public enum Iec104TypeId : byte
{
    MSpNa1 = 1,
    MDpNa1 = 3,
    MBoNa1 = 7,
    MMeNa1 = 9,
    MMeNb1 = 11,
    MMeNc1 = 13,
    MSpTb1 = 30,
    MDpTb1 = 31,
    MBoTb1 = 33,
    MMeTd1 = 34,
    MMeTe1 = 35,
    MMeTf1 = 36,
    CScNa1 = 45,
    CDcNa1 = 46,
    CSeNa1 = 48,
    CSeNb1 = 49,
    CSeNc1 = 50,
    CIcNa1 = 100
}

public readonly record struct Iec104CauseOfTransmission
{
    public Iec104CauseOfTransmission(byte causeCode, byte originatorAddress = 0, bool isNegativeConfirmation = false, bool isTest = false)
    {
        if (causeCode > 0x3F)
            throw new ArgumentOutOfRangeException(nameof(causeCode), causeCode, "IEC-104 Cause of Transmission code must fit in 6 bits.");

        CauseCode = causeCode;
        OriginatorAddress = originatorAddress;
        IsNegativeConfirmation = isNegativeConfirmation;
        IsTest = isTest;
    }

    public byte CauseCode { get; }
    public byte OriginatorAddress { get; }
    public bool IsNegativeConfirmation { get; }
    public bool IsTest { get; }

    public static Iec104CauseOfTransmission Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2)
            throw new Iec104ProtocolException("IEC-104 Cause of Transmission requires two octets in the initial CS104 profile.");

        var first = data[0];
        return new Iec104CauseOfTransmission(
            (byte)(first & 0x3F),
            data[1],
            (first & 0x40) != 0,
            (first & 0x80) != 0);
    }

    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < 2)
            throw new ArgumentException("IEC-104 Cause of Transmission destination requires two octets.", nameof(destination));

        destination[0] = (byte)(CauseCode |
            (IsNegativeConfirmation ? 0x40 : 0x00) |
            (IsTest ? 0x80 : 0x00));
        destination[1] = OriginatorAddress;
    }
}

public readonly record struct Iec104AsduHeader(
    Iec104TypeId TypeId,
    byte ObjectCount,
    bool IsSequence,
    Iec104CauseOfTransmission CauseOfTransmission,
    ushort CommonAddress)
{
    public const int EncodedLength = 6;

    public static Iec104AsduHeader Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < EncodedLength)
            throw new Iec104ProtocolException($"IEC-104 ASDU header requires {EncodedLength} octets.");

        var vsq = data[1];
        return new Iec104AsduHeader(
            (Iec104TypeId)data[0],
            (byte)(vsq & 0x7F),
            (vsq & 0x80) != 0,
            Iec104CauseOfTransmission.Parse(data.Slice(2, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4, 2)));
    }

    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < EncodedLength)
            throw new ArgumentException($"IEC-104 ASDU header destination requires {EncodedLength} octets.", nameof(destination));
        if (ObjectCount > 0x7F)
            throw new ArgumentOutOfRangeException(nameof(ObjectCount), ObjectCount, "IEC-104 VSQ object count must fit in 7 bits.");

        destination[0] = (byte)TypeId;
        destination[1] = (byte)(ObjectCount | (IsSequence ? 0x80 : 0x00));
        CauseOfTransmission.WriteTo(destination.Slice(2, 2));
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), CommonAddress);
    }
}

public sealed record Iec104AsduEnvelope(Iec104AsduHeader Header, ReadOnlyMemory<byte> Payload)
{
    public static Iec104AsduEnvelope Create(Iec104AsduHeader header, ReadOnlySpan<byte> payload) =>
        new(header, payload.ToArray());
}

public static class Iec104AsduCodec
{
    public const int MaximumEncodedLength = Iec104ApciCodec.MaximumAsduLength;
    public const int MaximumPayloadLength = MaximumEncodedLength - Iec104AsduHeader.EncodedLength;

    public static Iec104AsduEnvelope Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < Iec104AsduHeader.EncodedLength)
            throw new Iec104ProtocolException("IEC-104 ASDU is shorter than its fixed header.");
        if (data.Length > MaximumEncodedLength)
            throw new Iec104ProtocolException($"IEC-104 ASDU cannot exceed {MaximumEncodedLength} octets in the initial profile.");

        var header = Iec104AsduHeader.Parse(data);
        return Iec104AsduEnvelope.Create(header, data.Slice(Iec104AsduHeader.EncodedLength));
    }

    public static byte[] Serialize(Iec104AsduEnvelope asdu)
    {
        ArgumentNullException.ThrowIfNull(asdu);
        if (asdu.Payload.Length > MaximumPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(asdu), $"IEC-104 ASDU payload cannot exceed {MaximumPayloadLength} octets.");

        var bytes = new byte[Iec104AsduHeader.EncodedLength + asdu.Payload.Length];
        asdu.Header.WriteTo(bytes);
        asdu.Payload.Span.CopyTo(bytes.AsSpan(Iec104AsduHeader.EncodedLength));
        return bytes;
    }
}

public readonly record struct Iec104InformationObjectAddress
{
    public const int MaximumValue = 0x00FF_FFFF;

    public Iec104InformationObjectAddress(int value)
    {
        if (value is < 0 or > MaximumValue)
            throw new ArgumentOutOfRangeException(nameof(value), value, "IEC-104 Information Object Address must fit in 24 bits.");
        Value = value;
    }

    public int Value { get; }

    public static Iec104InformationObjectAddress Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 3)
            throw new Iec104ProtocolException("IEC-104 Information Object Address requires three octets in the initial CS104 profile.");

        return new Iec104InformationObjectAddress(data[0] | (data[1] << 8) | (data[2] << 16));
    }

    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < 3)
            throw new ArgumentException("IEC-104 Information Object Address destination requires three octets.", nameof(destination));

        destination[0] = (byte)(Value & 0xFF);
        destination[1] = (byte)((Value >> 8) & 0xFF);
        destination[2] = (byte)((Value >> 16) & 0xFF);
    }
}

public enum Iec104DoublePointState : byte
{
    Indeterminate0 = 0,
    Off = 1,
    On = 2,
    Indeterminate3 = 3
}

public readonly record struct Iec104QualityDescriptor(
    bool Overflow,
    bool Blocked,
    bool Substituted,
    bool NotTopical,
    bool Invalid)
{
    public static Iec104QualityDescriptor FromSiq(byte siq) =>
        new(false, (siq & 0x10) != 0, (siq & 0x20) != 0, (siq & 0x40) != 0, (siq & 0x80) != 0);

    public static Iec104QualityDescriptor FromDiq(byte diq) =>
        new(false, (diq & 0x10) != 0, (diq & 0x20) != 0, (diq & 0x40) != 0, (diq & 0x80) != 0);

    public static Iec104QualityDescriptor FromQds(byte qds) =>
        new((qds & 0x01) != 0, (qds & 0x10) != 0, (qds & 0x20) != 0, (qds & 0x40) != 0, (qds & 0x80) != 0);

    public TagQuality ToTagQuality(bool semanticUncertain = false)
    {
        if (Invalid) return TagQuality.BadDevice;
        if (NotTopical) return TagQuality.Stale;
        if (Substituted || Blocked || Overflow || semanticUncertain) return TagQuality.Uncertain;
        return TagQuality.Good;
    }
}
