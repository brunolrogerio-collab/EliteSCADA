namespace Scada.Core.Tags;

/// <summary>
/// Canonical logical bit projection and mutation semantics for fixed-width integer TAGs.
/// Friendly path syntax is presentation only; callers must provide the stable TAG identity
/// through <see cref="TagValueReference"/>.
/// </summary>
public static class TagBitSemantics
{
    public static bool TryValidateReference(
        TagDefinition tag,
        TagValueReference reference,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(reference);

        if (reference.TagId == Guid.Empty)
        {
            error = "TAG bit reference identity cannot be empty.";
            return false;
        }

        if (reference.TagId != tag.Id)
        {
            error = "TAG bit reference does not match the authoritative TAG identity.";
            return false;
        }

        if (reference.Selector is null)
        {
            error = "TAG bit reference requires a selector.";
            return false;
        }

        return TryValidateSelector(tag.DataType, reference.Selector, out error);
    }

    public static bool TryValidateSelector(
        TagDataType dataType,
        TagValueSelector selector,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(selector);

        if (selector.Kind != TagValueSelectorKind.Bit)
        {
            error = $"Unsupported TAG value selector kind '{selector.Kind}'.";
            return false;
        }

        var width = GetBitWidth(dataType);
        if (width is null)
        {
            error = $"TAG data type '{dataType}' does not support logical bit selection.";
            return false;
        }

        if (selector.Index < 0 || selector.Index >= width.Value)
        {
            error = $"Bit index {selector.Index} is outside the valid range 0..{width.Value - 1} for {dataType}.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryProject(
        TagDefinition tag,
        TagValueReference reference,
        TagValue sourceValue,
        out TagValue? projectedValue,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(sourceValue);
        projectedValue = null;

        if (!TryValidateReference(tag, reference, out error))
        {
            return false;
        }

        if (sourceValue.TagId != tag.Id)
        {
            error = "Source TAG value does not match the authoritative TAG identity.";
            return false;
        }

        if (sourceValue.Value is null)
        {
            projectedValue = CopySample(sourceValue, null);
            error = null;
            return true;
        }

        if (!TryReadBit(tag.DataType, sourceValue.Value, reference.Selector!.Index, out var bitValue))
        {
            error = $"Source value type does not match canonical TAG data type '{tag.DataType}'.";
            return false;
        }

        projectedValue = CopySample(sourceValue, bitValue);
        error = null;
        return true;
    }

    public static bool TrySetBit(
        TagDefinition tag,
        TagValueReference reference,
        object? currentValue,
        bool bitValue,
        out object? updatedValue,
        out string? error)
    {
        updatedValue = null;

        if (tag.ReadOnly)
        {
            error = "TAG is read-only and cannot accept a logical bit mutation.";
            return false;
        }

        if (!TryValidateReference(tag, reference, out error))
        {
            return false;
        }

        if (currentValue is null)
        {
            error = "Logical bit mutation requires a current authoritative integer value.";
            return false;
        }

        var index = reference.Selector!.Index;
        switch (tag.DataType)
        {
            case TagDataType.Int16 when currentValue is short value:
            {
                var bits = unchecked((ushort)value);
                var mask = (ushort)(1u << index);
                bits = bitValue ? (ushort)(bits | mask) : (ushort)(bits & ~mask);
                updatedValue = unchecked((short)bits);
                error = null;
                return true;
            }

            case TagDataType.Int32 when currentValue is int value:
            {
                var bits = unchecked((uint)value);
                var mask = 1u << index;
                bits = bitValue ? bits | mask : bits & ~mask;
                updatedValue = unchecked((int)bits);
                error = null;
                return true;
            }

            case TagDataType.Int64 when currentValue is long value:
            {
                var bits = unchecked((ulong)value);
                var mask = 1UL << index;
                bits = bitValue ? bits | mask : bits & ~mask;
                updatedValue = unchecked((long)bits);
                error = null;
                return true;
            }

            default:
                error = $"Current value type does not match canonical TAG data type '{tag.DataType}'.";
                return false;
        }
    }

    public static string FormatDisplayReference(string tagReference, TagValueReference reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagReference);
        ArgumentNullException.ThrowIfNull(reference);

        if (reference.Selector is null)
        {
            return tagReference;
        }

        if (reference.Selector.Kind != TagValueSelectorKind.Bit || reference.Selector.Index < 0)
        {
            throw new ArgumentException("Reference contains an invalid selector for display.", nameof(reference));
        }

        return $"{tagReference}.{reference.Selector.Index:D2}";
    }

    public static int? GetBitWidth(TagDataType dataType)
        => dataType switch
        {
            TagDataType.Int16 => 16,
            TagDataType.Int32 => 32,
            TagDataType.Int64 => 64,
            _ => null
        };

    private static bool TryReadBit(
        TagDataType dataType,
        object value,
        int index,
        out bool bitValue)
    {
        switch (dataType)
        {
            case TagDataType.Int16 when value is short int16:
                bitValue = ((unchecked((ushort)int16) >> index) & 1u) == 1u;
                return true;

            case TagDataType.Int32 when value is int int32:
                bitValue = ((unchecked((uint)int32) >> index) & 1u) == 1u;
                return true;

            case TagDataType.Int64 when value is long int64:
                bitValue = ((unchecked((ulong)int64) >> index) & 1UL) == 1UL;
                return true;

            default:
                bitValue = false;
                return false;
        }
    }

    private static TagValue CopySample(TagValue source, object? value)
        => new(source.TagId, value, source.Timestamp, source.Quality, source.Source)
        {
            SourceTimestamp = source.SourceTimestamp,
            ServerTimestamp = source.ServerTimestamp
        };
}
