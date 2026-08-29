using Scada.Core.Tags;

namespace Scada.Drivers.OpcUa;

public static class OpcUaRuntimeValueValidator
{
    public static void ValidateWrite(TagDefinition tag, object? value)
    {
        ArgumentNullException.ThrowIfNull(tag);

        if (tag.ReadOnly)
        {
            throw new InvalidOperationException($"OPC UA TAG '{tag.Path}' is read-only.");
        }

        if (value is null)
        {
            throw new ArgumentException($"OPC UA TAG '{tag.Path}' does not accept a null write value.", nameof(value));
        }

        var valid = tag.DataType switch
        {
            TagDataType.Boolean => value is bool,
            TagDataType.Int16 => value is short,
            TagDataType.Int32 => value is int,
            TagDataType.Int64 => value is long,
            TagDataType.Float => value is float,
            TagDataType.Double => value is double,
            TagDataType.String => value is string,
            TagDataType.DateTime => value is DateTime or DateTimeOffset,
            TagDataType.Enum => value is Enum or sbyte or byte or short or ushort or int or uint or long,
            _ => false
        };

        if (!valid)
        {
            throw new ArgumentException(
                $"Write value type '{value.GetType().Name}' is not valid for OPC UA TAG '{tag.Path}' with canonical type '{tag.DataType}'.",
                nameof(value));
        }
    }
}
