using Scada.Core.Tags;

namespace Scada.Drivers.SiemensS7Iso;

public enum S7IsoArea : byte
{
    Input = 0x81,
    Output = 0x82,
    Merker = 0x83,
    DataBlock = 0x84
}

public enum S7IsoValueType
{
    Boolean,
    Byte,
    UInt16,
    Int16,
    UInt32,
    Int32,
    Float32,
    Int64,
    Float64,
    String,
    DateTime
}

public enum S7IsoValueOrder
{
    Normal,
    ByteSwap,
    WordSwap,
    ByteAndWordSwap
}

public sealed record S7IsoPoint(
    TagDefinition Tag,
    S7IsoArea Area,
    int ByteOffset,
    S7IsoValueType ValueType,
    ushort DbNumber = 0,
    byte BitOffset = 0,
    bool Writable = false,
    byte StringLength = 0,
    S7IsoValueOrder ValueOrder = S7IsoValueOrder.Normal)
{
    public int ByteLength => ValueType switch
    {
        S7IsoValueType.Boolean or S7IsoValueType.Byte => 1,
        S7IsoValueType.UInt16 or S7IsoValueType.Int16 => 2,
        S7IsoValueType.UInt32 or S7IsoValueType.Int32 or S7IsoValueType.Float32 => 4,
        S7IsoValueType.Int64 or S7IsoValueType.Float64 or S7IsoValueType.DateTime => 8,
        S7IsoValueType.String => checked(StringLength + 2),
        _ => throw new ArgumentOutOfRangeException(nameof(ValueType))
    };

    internal byte S7AnyTransportSize => ValueType switch
    {
        S7IsoValueType.Boolean => 0x01,
        S7IsoValueType.Byte or S7IsoValueType.Int64 or S7IsoValueType.Float64 or S7IsoValueType.String or S7IsoValueType.DateTime => 0x02,
        S7IsoValueType.UInt16 => 0x04,
        S7IsoValueType.Int16 => 0x05,
        S7IsoValueType.UInt32 => 0x06,
        S7IsoValueType.Int32 => 0x07,
        S7IsoValueType.Float32 => 0x08,
        _ => throw new ArgumentOutOfRangeException(nameof(ValueType))
    };

    internal ushort S7AnyElementCount => ValueType switch
    {
        S7IsoValueType.Boolean => 1,
        S7IsoValueType.Byte or S7IsoValueType.Int64 or S7IsoValueType.Float64 or S7IsoValueType.String or S7IsoValueType.DateTime => checked((ushort)ByteLength),
        _ => 1
    };

    internal int AddressInBits => checked(ByteOffset * 8 + (ValueType == S7IsoValueType.Boolean ? BitOffset : 0));

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Tag);
        if (!Enum.IsDefined(Area))
            throw new ArgumentOutOfRangeException(nameof(Area), "S7 area is not supported.");
        if (!Enum.IsDefined(ValueType))
            throw new ArgumentOutOfRangeException(nameof(ValueType), "S7 value type is not supported.");
        if (!Enum.IsDefined(ValueOrder))
            throw new ArgumentOutOfRangeException(nameof(ValueOrder), "S7 value order is not supported.");
        if (ByteOffset < 0 || ByteOffset > 2_097_151)
            throw new ArgumentOutOfRangeException(nameof(ByteOffset), "S7 byte offset exceeds the 24-bit S7ANY address range.");
        if (BitOffset > 7)
            throw new ArgumentOutOfRangeException(nameof(BitOffset), "S7 bit offset must be from 0 to 7.");
        if (ValueType != S7IsoValueType.Boolean &&
            (long)ByteOffset + ByteLength - 1 > 2_097_151)
            throw new ArgumentOutOfRangeException(nameof(ByteOffset), "S7 point payload exceeds the 24-bit S7ANY address range.");

        if (Area == S7IsoArea.DataBlock)
        {
            if (DbNumber == 0)
                throw new ArgumentOutOfRangeException(nameof(DbNumber), "S7 DB points require a non-zero DB number.");
        }
        else if (DbNumber != 0)
        {
            throw new ArgumentException("S7 DB number is valid only for DataBlock points.", nameof(DbNumber));
        }

        if (ValueType != S7IsoValueType.Boolean && BitOffset != 0)
            throw new ArgumentException("S7 bit offset is valid only for Boolean points.", nameof(BitOffset));

        if (ValueType == S7IsoValueType.String)
        {
            if (StringLength is < 1 or > 254)
                throw new ArgumentOutOfRangeException(nameof(StringLength), "S7 STRING length must be from 1 to 254.");
        }
        else if (StringLength != 0)
        {
            throw new ArgumentException("S7 string length is valid only for String points.", nameof(StringLength));
        }

        if (Writable && Area == S7IsoArea.Input)
            throw new ArgumentException("S7 input-area points are read-only.");
        if (Writable && Tag.ReadOnly)
            throw new ArgumentException($"TAG '{Tag.Path}' is read-only but the S7 point is marked writable.");

        ValidateTagDataType();
        ValidateValueOrder();
    }

    private void ValidateTagDataType()
    {
        var valid = ValueType switch
        {
            S7IsoValueType.Boolean => Tag.DataType == TagDataType.Boolean,
            S7IsoValueType.Byte or S7IsoValueType.Int16 => Tag.DataType == TagDataType.Int16,
            S7IsoValueType.UInt16 or S7IsoValueType.Int32 => Tag.DataType == TagDataType.Int32,
            S7IsoValueType.UInt32 or S7IsoValueType.Int64 => Tag.DataType == TagDataType.Int64,
            S7IsoValueType.Float32 => Tag.DataType == TagDataType.Float,
            S7IsoValueType.Float64 => Tag.DataType == TagDataType.Double,
            S7IsoValueType.String => Tag.DataType == TagDataType.String,
            S7IsoValueType.DateTime => Tag.DataType == TagDataType.DateTime,
            _ => false
        };

        if (!valid)
            throw new ArgumentException(
                $"S7 value type '{ValueType}' is incompatible with TAG type '{Tag.DataType}' for '{Tag.Path}'.");
    }

    private void ValidateValueOrder()
    {
        if (ValueOrder == S7IsoValueOrder.Normal) return;

        var numeric = ValueType is
            S7IsoValueType.UInt16 or S7IsoValueType.Int16 or
            S7IsoValueType.UInt32 or S7IsoValueType.Int32 or S7IsoValueType.Float32 or
            S7IsoValueType.Int64 or S7IsoValueType.Float64;

        if (!numeric)
            throw new ArgumentException($"S7 value ordering is valid only for multi-byte numeric points, not '{ValueType}'.");

        if ((ValueOrder is S7IsoValueOrder.WordSwap or S7IsoValueOrder.ByteAndWordSwap) && ByteLength < 4)
            throw new ArgumentException("S7 word swap requires a value at least 32 bits wide.");
    }
}