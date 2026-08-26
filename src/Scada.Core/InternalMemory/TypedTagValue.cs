using Scada.Core.Tags;

namespace Scada.Core.InternalMemory;

/// <summary>
/// A TAG value paired with its declared TAG data type. Construction is strict:
/// values must already have the exact runtime type expected by the TAG type.
/// No numeric/string coercion is performed.
/// </summary>
public sealed record TypedTagValue
{
    public TypedTagValue(TagDataType dataType, object? value)
    {
        EnsureCompatible(dataType, value, nameof(value));
        DataType = dataType;
        Value = value!;
    }

    public TagDataType DataType { get; }
    public object Value { get; }

    public static TypedTagValue CreateDefault(TagDataType dataType) => new(dataType, dataType switch
    {
        TagDataType.Boolean => false,
        TagDataType.Int16 => (short)0,
        TagDataType.Int32 => 0,
        TagDataType.Int64 => 0L,
        TagDataType.Float => 0F,
        TagDataType.Double => 0D,
        TagDataType.String => string.Empty,
        TagDataType.DateTime => DateTimeOffset.UnixEpoch,
        TagDataType.Enum => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Unsupported TAG data type.")
    });

    public static void EnsureCompatible(TagDataType dataType, object? value, string? parameterName = null)
    {
        if (value is null)
            throw new ArgumentNullException(parameterName ?? nameof(value), "TAG values cannot be null in the Internal Memory foundation.");

        var compatible = dataType switch
        {
            TagDataType.Boolean => value is bool,
            TagDataType.Int16 => value is short,
            TagDataType.Int32 => value is int,
            TagDataType.Int64 => value is long,
            TagDataType.Float => value is float,
            TagDataType.Double => value is double,
            TagDataType.String => value is string,
            TagDataType.DateTime => value is DateTimeOffset,
            TagDataType.Enum => value is int,
            _ => false
        };

        if (!compatible)
        {
            throw new ArgumentException(
                $"Value runtime type '{value.GetType().FullName}' is incompatible with TAG data type '{dataType}'. No implicit coercion is allowed.",
                parameterName ?? nameof(value));
        }
    }
}

/// <summary>
/// Isolated runtime/Engineering bridge contract for memory TAGs. The canonical
/// Engineering DTO still requires coordinator-owned schema integration.
/// </summary>
public sealed record MemoryTagDefinition
{
    public MemoryTagDefinition(TagDefinition tag, TypedTagValue? initialValue = null)
    {
        ArgumentNullException.ThrowIfNull(tag);

        if (tag.Id == Guid.Empty)
            throw new ArgumentException("Memory TAG stable ID cannot be empty.", nameof(tag));

        Tag = tag;
        InitialValue = initialValue ?? TypedTagValue.CreateDefault(tag.DataType);

        if (InitialValue.DataType != tag.DataType)
        {
            throw new ArgumentException(
                $"Initial value data type '{InitialValue.DataType}' does not match TAG data type '{tag.DataType}'.",
                nameof(initialValue));
        }
    }

    public TagDefinition Tag { get; }
    public TypedTagValue InitialValue { get; }
}

internal static class MemoryTagDefinitionSet
{
    public static Dictionary<Guid, MemoryTagDefinition> Materialize(IEnumerable<MemoryTagDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var result = new Dictionary<Guid, MemoryTagDefinition>();
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            if (!result.TryAdd(definition.Tag.Id, definition))
                throw new ArgumentException($"Duplicate memory TAG stable ID '{definition.Tag.Id}'.", nameof(definitions));
        }

        return result;
    }
}
