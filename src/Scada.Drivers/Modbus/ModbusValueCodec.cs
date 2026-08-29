using System.Buffers.Binary;
using System.Globalization;
using Scada.Core.Tags;

namespace Scada.Drivers.Modbus;

public static class ModbusValueCodec
{
    public static object DecodeBit(ModbusPoint point, bool value)
    {
        point.Validate();
        if (point.ValueType != ModbusValueType.Boolean)
            throw new ArgumentException("Bit decoding requires a Boolean Modbus point.", nameof(point));
        return value;
    }

    public static object DecodeRegisters(ModbusPoint point, ReadOnlySpan<ushort> registers)
    {
        point.Validate();
        if (point.Area is ModbusDataArea.Coil or ModbusDataArea.DiscreteInput)
            throw new ArgumentException("Register decoding cannot be used for bit areas.", nameof(point));
        if (registers.Length < point.RegisterCount)
            throw new ArgumentException($"Point requires {point.RegisterCount} register(s), but only {registers.Length} were supplied.", nameof(registers));

        var ordered = OrderForDecode(registers[..point.RegisterCount], point.WordOrder);
        var bytes = new byte[ordered.Length * 2];
        for (var i = 0; i < ordered.Length; i++)
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(i * 2, 2), ordered[i]);

        if (point.ValueType == ModbusValueType.Boolean && point.AddressSelector is not null)
            return (ordered[0] & (1 << point.AddressSelector.Index)) != 0;

        double raw = point.ValueType switch
        {
            ModbusValueType.Boolean => ordered[0] == 0 ? 0d : 1d,
            ModbusValueType.Int16 => BinaryPrimitives.ReadInt16BigEndian(bytes),
            ModbusValueType.UInt16 => BinaryPrimitives.ReadUInt16BigEndian(bytes),
            ModbusValueType.Int32 => BinaryPrimitives.ReadInt32BigEndian(bytes),
            ModbusValueType.UInt32 => BinaryPrimitives.ReadUInt32BigEndian(bytes),
            ModbusValueType.Float32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(bytes)),
            ModbusValueType.Int64 => BinaryPrimitives.ReadInt64BigEndian(bytes),
            ModbusValueType.UInt64 => BinaryPrimitives.ReadUInt64BigEndian(bytes),
            ModbusValueType.Float64 => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(bytes)),
            _ => throw new ArgumentOutOfRangeException(nameof(point.ValueType))
        };

        if (point.ValueType == ModbusValueType.Boolean)
            return raw != 0d;

        var engineering = raw * point.Scale + point.Offset;
        return CoerceToTagType(engineering, point.Tag.DataType);
    }

    public static bool EncodeBit(ModbusPoint point, object? engineeringValue)
    {
        point.Validate();
        if (point.ValueType != ModbusValueType.Boolean)
            throw new ArgumentException("Bit encoding requires a Boolean Modbus point.", nameof(point));
        return Convert.ToBoolean(engineeringValue, CultureInfo.InvariantCulture);
    }

    public static ushort ApplyRegisterBit(ModbusPoint point, ushort registerValue, object? engineeringValue)
    {
        point.Validate();
        if (point.Area != ModbusDataArea.HoldingRegister || point.AddressSelector is null)
            throw new ArgumentException("Register bit mutation requires a selected HoldingRegister point.", nameof(point));

        var bit = Convert.ToBoolean(engineeringValue, CultureInfo.InvariantCulture);
        var mask = checked((ushort)(1 << point.AddressSelector.Index));
        return bit
            ? checked((ushort)(registerValue | mask))
            : checked((ushort)(registerValue & ~mask));
    }

    public static ushort[] EncodeRegisters(ModbusPoint point, object? engineeringValue)
    {
        point.Validate();
        if (point.Area is ModbusDataArea.Coil or ModbusDataArea.DiscreteInput)
            throw new ArgumentException("Register encoding cannot be used for bit areas.", nameof(point));

        if (point.ValueType == ModbusValueType.Boolean)
        {
            if (point.AddressSelector is not null)
                throw new InvalidOperationException("Selected register bits require read-modify-write and cannot be encoded as a whole register value.");
            return new[] { Convert.ToBoolean(engineeringValue, CultureInfo.InvariantCulture) ? (ushort)1 : (ushort)0 };
        }

        var engineering = Convert.ToDouble(engineeringValue, CultureInfo.InvariantCulture);
        if (!double.IsFinite(engineering))
            throw new ArgumentOutOfRangeException(nameof(engineeringValue), "Engineering value must be finite.");
        var raw = (engineering - point.Offset) / point.Scale;

        var bytes = new byte[point.RegisterCount * 2];
        switch (point.ValueType)
        {
            case ModbusValueType.Int16:
                BinaryPrimitives.WriteInt16BigEndian(bytes, checked((short)Math.Round(raw)));
                break;
            case ModbusValueType.UInt16:
                BinaryPrimitives.WriteUInt16BigEndian(bytes, checked((ushort)Math.Round(raw)));
                break;
            case ModbusValueType.Int32:
                BinaryPrimitives.WriteInt32BigEndian(bytes, checked((int)Math.Round(raw)));
                break;
            case ModbusValueType.UInt32:
                BinaryPrimitives.WriteUInt32BigEndian(bytes, checked((uint)Math.Round(raw)));
                break;
            case ModbusValueType.Float32:
                BinaryPrimitives.WriteInt32BigEndian(bytes, BitConverter.SingleToInt32Bits(checked((float)raw)));
                break;
            case ModbusValueType.Int64:
                BinaryPrimitives.WriteInt64BigEndian(bytes, checked((long)Math.Round(raw)));
                break;
            case ModbusValueType.UInt64:
                BinaryPrimitives.WriteUInt64BigEndian(bytes, checked((ulong)Math.Round(raw)));
                break;
            case ModbusValueType.Float64:
                BinaryPrimitives.WriteInt64BigEndian(bytes, BitConverter.DoubleToInt64Bits(raw));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(point.ValueType));
        }

        var registers = new ushort[point.RegisterCount];
        for (var i = 0; i < registers.Length; i++)
            registers[i] = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(i * 2, 2));

        if (point.WordOrder == ModbusWordOrder.LowWordFirst)
            Array.Reverse(registers);
        return registers;
    }

    private static ushort[] OrderForDecode(ReadOnlySpan<ushort> registers, ModbusWordOrder order)
    {
        var result = registers.ToArray();
        if (order == ModbusWordOrder.LowWordFirst)
            Array.Reverse(result);
        return result;
    }

    private static object CoerceToTagType(double value, TagDataType dataType) => dataType switch
    {
        TagDataType.Int16 => (object)checked((short)Math.Round(value)),
        TagDataType.Int32 => (object)checked((int)Math.Round(value)),
        TagDataType.Int64 => (object)checked((long)Math.Round(value)),
        TagDataType.Float => (object)checked((float)value),
        TagDataType.Double => value,
        _ => throw new InvalidOperationException($"TAG data type '{dataType}' is not numeric.")
    };
}
