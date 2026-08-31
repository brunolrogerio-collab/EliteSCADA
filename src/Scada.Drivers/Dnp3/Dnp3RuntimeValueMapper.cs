using Scada.Core.Tags;

namespace Scada.Drivers.Dnp3;

public static class Dnp3RuntimeValueMapper
{
    public static object Normalize(Dnp3Point point, object? value)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (value is null)
            throw new ArgumentException("DNP3 measurement value cannot be null for a configured point.", nameof(value));

        return point.Binding.PointKind switch
        {
            Dnp3PointKind.BinaryInput or Dnp3PointKind.BinaryOutputStatus => RequireBoolean(value),
            Dnp3PointKind.DoubleBitBinaryInput => NormalizeDoubleBit(value),
            Dnp3PointKind.Counter or Dnp3PointKind.FrozenCounter => NormalizeCounter(point.Tag.DataType, value),
            Dnp3PointKind.AnalogInput or Dnp3PointKind.AnalogOutputStatus => NormalizeAnalog(point.Tag.DataType, value),
            _ => throw new ArgumentOutOfRangeException(nameof(point), point.Binding.PointKind, null)
        };
    }

    public static object NormalizeAnalogCommand(Dnp3Point point, object? value)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (point.Binding.PointKind != Dnp3PointKind.AnalogOutputStatus)
            throw new ArgumentException("Analog command normalization requires an Analog Output Status point.", nameof(point));
        if (value is null)
            throw new ArgumentException("DNP3 analog command value cannot be null.", nameof(value));

        return NormalizeAnalog(point.Tag.DataType, value);
    }

    private static bool RequireBoolean(object value) => value switch
    {
        bool boolean => boolean,
        _ => throw new ArgumentException($"DNP3 binary point requires Boolean value, received {value.GetType().Name}.", nameof(value))
    };

    private static string NormalizeDoubleBit(object value) => value switch
    {
        Dnp3DoubleBitState state => state.ToString(),
        string text when Enum.TryParse<Dnp3DoubleBitState>(text, ignoreCase: false, out var parsed) => parsed.ToString(),
        _ => throw new ArgumentException("Double-Bit Binary value must preserve one of the four canonical DNP3 states.", nameof(value))
    };

    private static object NormalizeCounter(TagDataType dataType, object value) => dataType switch
    {
        TagDataType.Int32 when value is ushort u16 => Dnp3ValueConversions.Counter16ToCanonical(u16),
        TagDataType.Int32 when value is int i32 && i32 >= 0 => i32,
        TagDataType.Int64 when value is uint u32 => Dnp3ValueConversions.Counter32ToCanonical(u32),
        TagDataType.Int64 when value is long i64 && i64 >= 0 => i64,
        _ => throw new ArgumentException($"DNP3 counter value is incompatible with canonical type {dataType}.", nameof(value))
    };

    private static object NormalizeAnalog(TagDataType dataType, object value) => dataType switch
    {
        // Explicit boxing prevents the switch expression from selecting a wider
        // numeric common type (for example Double) before converting to object.
        TagDataType.Int16 when value is short i16 => (object)i16,
        TagDataType.Int32 when value is int i32 => (object)i32,
        TagDataType.Float when value is float f32 && float.IsFinite(f32) => (object)f32,
        TagDataType.Double when value is double f64 && double.IsFinite(f64) => (object)f64,
        _ => throw new ArgumentException($"DNP3 analog value is incompatible with canonical type {dataType}.", nameof(value))
    };
}
