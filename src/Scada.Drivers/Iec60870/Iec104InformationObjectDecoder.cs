using System.Buffers.Binary;
using Scada.Core.Tags;

namespace Scada.Drivers.Iec60870;

public sealed record Iec104DecodedPoint(
    ushort CommonAddress,
    Iec104InformationObjectAddress InformationObjectAddress,
    Iec104TypeId TypeId,
    object Value,
    TagQuality Quality,
    Iec104Cp56DecodeResult? SourceTime)
{
    public DateTimeOffset? SourceTimestamp => SourceTime?.Timestamp;
    public Iec104CauseOfTransmission CauseOfTransmission { get; init; }
}

public static class Iec104InformationObjectDecoder
{
    private const int IoaLength = 3;

    public static bool IsSupported(Iec104TypeId typeId) => typeId is
        Iec104TypeId.MSpNa1 or Iec104TypeId.MSpTb1 or
        Iec104TypeId.MDpNa1 or Iec104TypeId.MDpTb1 or
        Iec104TypeId.MBoNa1 or Iec104TypeId.MBoTb1 or
        Iec104TypeId.MMeNa1 or Iec104TypeId.MMeTd1 or
        Iec104TypeId.MMeNb1 or Iec104TypeId.MMeTe1 or
        Iec104TypeId.MMeNc1 or Iec104TypeId.MMeTf1;

    public static IReadOnlyList<Iec104DecodedPoint> Decode(
        Iec104AsduEnvelope asdu,
        TimeZoneInfo stationTimeZone)
    {
        ArgumentNullException.ThrowIfNull(asdu);
        ArgumentNullException.ThrowIfNull(stationTimeZone);

        var header = asdu.Header;
        if (!IsSupported(header.TypeId))
            throw new NotSupportedException($"IEC-104 Type ID {(byte)header.TypeId} ({header.TypeId}) is not supported by the first monitored-point decoder.");
        if (header.ObjectCount == 0)
            throw new Iec104ProtocolException("IEC-104 monitored ASDU must contain at least one information object.");

        var objectDataLength = GetObjectDataLength(header.TypeId);
        var expectedLength = header.IsSequence
            ? IoaLength + header.ObjectCount * objectDataLength
            : header.ObjectCount * (IoaLength + objectDataLength);

        if (asdu.Payload.Length != expectedLength)
        {
            throw new Iec104ProtocolException(
                $"IEC-104 {header.TypeId} payload length {asdu.Payload.Length} does not match the expected {expectedLength} bytes for {header.ObjectCount} object(s), SQ={(header.IsSequence ? 1 : 0)}.");
        }

        var payload = asdu.Payload.Span;
        var points = new List<Iec104DecodedPoint>(header.ObjectCount);
        var offset = 0;
        Iec104InformationObjectAddress sequenceStart = default;

        if (header.IsSequence)
        {
            sequenceStart = Iec104InformationObjectAddress.Parse(payload.Slice(0, IoaLength));
            offset += IoaLength;
        }

        for (var index = 0; index < header.ObjectCount; index++)
        {
            Iec104InformationObjectAddress ioa;
            if (header.IsSequence)
            {
                var value = sequenceStart.Value + index;
                if (value > Iec104InformationObjectAddress.MaximumValue)
                    throw new Iec104ProtocolException("IEC-104 sequential information-object addressing exceeds the 24-bit IOA range.");
                ioa = new Iec104InformationObjectAddress(value);
            }
            else
            {
                ioa = Iec104InformationObjectAddress.Parse(payload.Slice(offset, IoaLength));
                offset += IoaLength;
            }

            var objectData = payload.Slice(offset, objectDataLength);
            offset += objectDataLength;
            points.Add(DecodePoint(header.CommonAddress, ioa, header.TypeId, objectData, stationTimeZone) with
            {
                CauseOfTransmission = header.CauseOfTransmission
            });
        }

        return points;
    }

    private static Iec104DecodedPoint DecodePoint(
        ushort commonAddress,
        Iec104InformationObjectAddress ioa,
        Iec104TypeId typeId,
        ReadOnlySpan<byte> data,
        TimeZoneInfo stationTimeZone)
    {
        object value;
        Iec104QualityDescriptor quality;
        var semanticUncertain = false;
        var timeOffset = -1;

        switch (typeId)
        {
            case Iec104TypeId.MSpNa1:
            case Iec104TypeId.MSpTb1:
            {
                var siq = data[0];
                value = (siq & 0x01) != 0;
                quality = Iec104QualityDescriptor.FromSiq(siq);
                timeOffset = typeId == Iec104TypeId.MSpTb1 ? 1 : -1;
                break;
            }

            case Iec104TypeId.MDpNa1:
            case Iec104TypeId.MDpTb1:
            {
                var diq = data[0];
                var state = (Iec104DoublePointState)(diq & 0x03);
                value = state;
                quality = Iec104QualityDescriptor.FromDiq(diq);
                semanticUncertain = state is Iec104DoublePointState.Indeterminate0 or Iec104DoublePointState.Indeterminate3;
                timeOffset = typeId == Iec104TypeId.MDpTb1 ? 1 : -1;
                break;
            }

            case Iec104TypeId.MBoNa1:
            case Iec104TypeId.MBoTb1:
                value = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4));
                quality = Iec104QualityDescriptor.FromQds(data[4]);
                timeOffset = typeId == Iec104TypeId.MBoTb1 ? 5 : -1;
                break;

            case Iec104TypeId.MMeNa1:
            case Iec104TypeId.MMeTd1:
            {
                var raw = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(0, 2));
                value = raw / 32768f;
                quality = Iec104QualityDescriptor.FromQds(data[2]);
                timeOffset = typeId == Iec104TypeId.MMeTd1 ? 3 : -1;
                break;
            }

            case Iec104TypeId.MMeNb1:
            case Iec104TypeId.MMeTe1:
                value = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(0, 2));
                quality = Iec104QualityDescriptor.FromQds(data[2]);
                timeOffset = typeId == Iec104TypeId.MMeTe1 ? 3 : -1;
                break;

            case Iec104TypeId.MMeNc1:
            case Iec104TypeId.MMeTf1:
            {
                var bits = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0, 4));
                var decoded = BitConverter.Int32BitsToSingle(bits);
                value = decoded;
                quality = Iec104QualityDescriptor.FromQds(data[4]);
                semanticUncertain = !float.IsFinite(decoded);
                timeOffset = typeId == Iec104TypeId.MMeTf1 ? 5 : -1;
                break;
            }

            default:
                throw new NotSupportedException($"Unsupported monitored IEC-104 Type ID {typeId}.");
        }

        Iec104Cp56DecodeResult? sourceTime = null;
        if (timeOffset >= 0)
            sourceTime = Iec104Cp56Time2a.Decode(data.Slice(timeOffset, Iec104Cp56Time2a.EncodedLength), stationTimeZone);

        return new Iec104DecodedPoint(
            commonAddress,
            ioa,
            typeId,
            value,
            quality.ToTagQuality(semanticUncertain),
            sourceTime);
    }

    private static int GetObjectDataLength(Iec104TypeId typeId) => typeId switch
    {
        Iec104TypeId.MSpNa1 => 1,
        Iec104TypeId.MSpTb1 => 1 + Iec104Cp56Time2a.EncodedLength,
        Iec104TypeId.MDpNa1 => 1,
        Iec104TypeId.MDpTb1 => 1 + Iec104Cp56Time2a.EncodedLength,
        Iec104TypeId.MBoNa1 => 5,
        Iec104TypeId.MBoTb1 => 5 + Iec104Cp56Time2a.EncodedLength,
        Iec104TypeId.MMeNa1 => 3,
        Iec104TypeId.MMeTd1 => 3 + Iec104Cp56Time2a.EncodedLength,
        Iec104TypeId.MMeNb1 => 3,
        Iec104TypeId.MMeTe1 => 3 + Iec104Cp56Time2a.EncodedLength,
        Iec104TypeId.MMeNc1 => 5,
        Iec104TypeId.MMeTf1 => 5 + Iec104Cp56Time2a.EncodedLength,
        _ => throw new NotSupportedException($"Unsupported monitored IEC-104 Type ID {typeId}.")
    };
}