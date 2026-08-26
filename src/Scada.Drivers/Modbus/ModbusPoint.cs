using Scada.Core.Tags;

namespace Scada.Drivers.Modbus;

public enum ModbusDataArea
{
    Coil,
    DiscreteInput,
    HoldingRegister,
    InputRegister
}

public enum ModbusValueType
{
    Boolean,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Float32,
    Int64,
    UInt64,
    Float64
}

public enum ModbusWordOrder
{
    HighWordFirst,
    LowWordFirst
}

public sealed record ModbusPoint(
    TagDefinition Tag,
    byte UnitId,
    ModbusDataArea Area,
    ushort Address,
    ModbusValueType ValueType,
    bool Writable = false,
    ModbusWordOrder WordOrder = ModbusWordOrder.HighWordFirst,
    double Scale = 1d,
    double Offset = 0d)
{
    public int RegisterCount => ValueType switch
    {
        ModbusValueType.Boolean => Area is ModbusDataArea.Coil or ModbusDataArea.DiscreteInput ? 0 : 1,
        ModbusValueType.Int16 or ModbusValueType.UInt16 => 1,
        ModbusValueType.Int32 or ModbusValueType.UInt32 or ModbusValueType.Float32 => 2,
        ModbusValueType.Int64 or ModbusValueType.UInt64 or ModbusValueType.Float64 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(ValueType))
    };

    public int ElementCount => Area is ModbusDataArea.Coil or ModbusDataArea.DiscreteInput ? 1 : RegisterCount;
    public int EndAddressExclusive => Address + ElementCount;

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Tag);
        if (!double.IsFinite(Scale) || Scale == 0d)
            throw new ArgumentOutOfRangeException(nameof(Scale), "Modbus scale must be finite and non-zero.");
        if (!double.IsFinite(Offset))
            throw new ArgumentOutOfRangeException(nameof(Offset), "Modbus offset must be finite.");
        if (EndAddressExclusive > ushort.MaxValue + 1)
            throw new ArgumentOutOfRangeException(nameof(Address), "Modbus point exceeds the 16-bit address space.");

        if (Area is ModbusDataArea.Coil or ModbusDataArea.DiscreteInput && ValueType != ModbusValueType.Boolean)
            throw new ArgumentException("Coil and discrete-input points must use the Boolean value type.");

        if (Writable && Area is ModbusDataArea.DiscreteInput or ModbusDataArea.InputRegister)
            throw new ArgumentException("Discrete inputs and input registers are read-only Modbus areas.");

        if (Writable && Tag.ReadOnly)
            throw new ArgumentException($"TAG '{Tag.Path}' is read-only but the Modbus point is marked writable.");

        if (ValueType == ModbusValueType.Boolean && Tag.DataType != TagDataType.Boolean)
            throw new ArgumentException($"Boolean Modbus point '{Tag.Path}' requires a Boolean TAG.");

        if (ValueType != ModbusValueType.Boolean && Tag.DataType is TagDataType.Boolean or TagDataType.String or TagDataType.DateTime or TagDataType.Enum)
            throw new ArgumentException($"Numeric Modbus point '{Tag.Path}' requires a numeric TAG type.");
    }
}
