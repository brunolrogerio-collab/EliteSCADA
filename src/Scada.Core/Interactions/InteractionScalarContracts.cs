using System.Globalization;

namespace Scada.Core.Interactions;

public enum InteractionScalarKind
{
    Boolean,
    Integer,
    Number,
    String,
    Enum,
    Duration,
    Percentage
}

public sealed record InteractionScalarSchema(
    InteractionScalarKind Kind,
    decimal? Minimum = null,
    decimal? Maximum = null,
    int? MaximumLength = null,
    IReadOnlyList<string>? EnumValues = null,
    string? Unit = null);

public sealed record InteractionScalarValue(
    InteractionScalarKind Kind,
    string SerializedValue)
{
    public static InteractionScalarValue Boolean(bool value)
        => new(InteractionScalarKind.Boolean, value ? "true" : "false");

    public static InteractionScalarValue Integer(long value)
        => new(InteractionScalarKind.Integer, value.ToString(CultureInfo.InvariantCulture));

    public static InteractionScalarValue Number(decimal value)
        => new(InteractionScalarKind.Number, value.ToString("G29", CultureInfo.InvariantCulture));

    public static InteractionScalarValue String(string value)
        => new(InteractionScalarKind.String, value);

    public static InteractionScalarValue Enum(string value)
        => new(InteractionScalarKind.Enum, value);

    public static InteractionScalarValue Duration(TimeSpan value)
        => new(InteractionScalarKind.Duration, value.ToString("c", CultureInfo.InvariantCulture));

    public static InteractionScalarValue Percentage(decimal value)
        => new(InteractionScalarKind.Percentage, value.ToString("G29", CultureInfo.InvariantCulture));
}

public static class InteractionScalarContract
{
    public const int Version = 1;
    public const int MaximumSerializedLength = 4096;
    public const int MaximumEnumValueCount = 128;
    public const int MaximumEnumValueLength = 160;
    public const int MaximumUnitLength = 64;

    public static InteractionScalarSchema NormalizeSchema(InteractionScalarSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var unit = Optional(schema.Unit, MaximumUnitLength, "unit");
        var maximumLength = schema.MaximumLength;
        if (maximumLength is <= 0 or > MaximumSerializedLength)
            throw new ArgumentException($"Scalar maximum length must be between 1 and {MaximumSerializedLength}.", nameof(schema));

        if (schema.Minimum is not null && schema.Maximum is not null && schema.Minimum > schema.Maximum)
            throw new ArgumentException("Scalar minimum cannot exceed maximum.", nameof(schema));

        IReadOnlyList<string>? enumValues = null;
        if (schema.Kind == InteractionScalarKind.Enum)
        {
            if (schema.EnumValues is null || schema.EnumValues.Count == 0)
                throw new ArgumentException("Enum scalar schema requires at least one allowed value.", nameof(schema));
            if (schema.EnumValues.Count > MaximumEnumValueCount)
                throw new ArgumentException($"Enum scalar schema supports at most {MaximumEnumValueCount} allowed values.", nameof(schema));

            var normalized = schema.EnumValues
                .Select(value => Required(value, MaximumEnumValueLength, "enum value"))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            if (normalized.Length != schema.EnumValues.Count)
                throw new ArgumentException("Enum scalar schema cannot contain duplicate allowed values.", nameof(schema));

            enumValues = normalized;
        }
        else if (schema.EnumValues is { Count: > 0 })
        {
            throw new ArgumentException("Enum values are only valid for Enum scalar schemas.", nameof(schema));
        }

        if (schema.Kind is InteractionScalarKind.Boolean or InteractionScalarKind.String or InteractionScalarKind.Enum)
        {
            if (schema.Minimum is not null || schema.Maximum is not null)
                throw new ArgumentException("Numeric minimum/maximum bounds are not valid for this scalar kind.", nameof(schema));
        }

        if (schema.Kind != InteractionScalarKind.String && maximumLength is not null)
            throw new ArgumentException("MaximumLength is only valid for String scalar schemas.", nameof(schema));

        if (schema.Kind == InteractionScalarKind.Percentage &&
            ((schema.Minimum is not null && schema.Minimum < 0m) ||
             (schema.Maximum is not null && schema.Maximum > 100m)))
        {
            throw new ArgumentException("Percentage schema bounds must stay within 0..100.", nameof(schema));
        }

        return schema with
        {
            MaximumLength = schema.Kind == InteractionScalarKind.String
                ? maximumLength ?? MaximumSerializedLength
                : null,
            EnumValues = enumValues,
            Unit = unit
        };
    }

    public static InteractionScalarValue Validate(
        InteractionScalarSchema schema,
        InteractionScalarValue value)
    {
        var normalizedSchema = NormalizeSchema(schema);
        ArgumentNullException.ThrowIfNull(value);

        if (value.Kind != normalizedSchema.Kind)
            throw new ArgumentException(
                $"Scalar kind '{value.Kind}' does not match schema kind '{normalizedSchema.Kind}'.",
                nameof(value));

        if (value.SerializedValue is null)
            throw new ArgumentException("Scalar serialized value is required.", nameof(value));
        if (value.SerializedValue.Length > MaximumSerializedLength)
            throw new ArgumentException($"Scalar serialized value exceeds {MaximumSerializedLength} characters.", nameof(value));

        switch (normalizedSchema.Kind)
        {
            case InteractionScalarKind.Boolean:
                if (value.SerializedValue is not ("true" or "false"))
                    throw new ArgumentException("Boolean scalar must use canonical 'true' or 'false'.", nameof(value));
                break;

            case InteractionScalarKind.Integer:
                if (!long.TryParse(value.SerializedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) ||
                    integer.ToString(CultureInfo.InvariantCulture) != value.SerializedValue)
                    throw new ArgumentException("Integer scalar is not in canonical Int64 representation.", nameof(value));
                ValidateBounds(integer, normalizedSchema, nameof(value));
                break;

            case InteractionScalarKind.Number:
                if (!decimal.TryParse(value.SerializedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ||
                    number.ToString("G29", CultureInfo.InvariantCulture) != value.SerializedValue)
                    throw new ArgumentException("Number scalar is not in canonical decimal representation.", nameof(value));
                ValidateBounds(number, normalizedSchema, nameof(value));
                break;

            case InteractionScalarKind.String:
                if (value.SerializedValue.Length > normalizedSchema.MaximumLength)
                    throw new ArgumentException($"String scalar exceeds {normalizedSchema.MaximumLength} characters.", nameof(value));
                break;

            case InteractionScalarKind.Enum:
                if (!normalizedSchema.EnumValues!.Contains(value.SerializedValue, StringComparer.Ordinal))
                    throw new ArgumentException($"Enum scalar value '{value.SerializedValue}' is not allowed.", nameof(value));
                break;

            case InteractionScalarKind.Duration:
                if (!TimeSpan.TryParseExact(value.SerializedValue, "c", CultureInfo.InvariantCulture, out var duration) ||
                    duration.ToString("c", CultureInfo.InvariantCulture) != value.SerializedValue ||
                    duration < TimeSpan.Zero)
                    throw new ArgumentException("Duration scalar must be a non-negative canonical TimeSpan constant value.", nameof(value));
                ValidateBounds((decimal)duration.TotalMilliseconds, normalizedSchema, nameof(value));
                break;

            case InteractionScalarKind.Percentage:
                if (!decimal.TryParse(value.SerializedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var percentage) ||
                    percentage.ToString("G29", CultureInfo.InvariantCulture) != value.SerializedValue ||
                    percentage is < 0m or > 100m)
                    throw new ArgumentException("Percentage scalar must be canonical decimal text in the 0..100 range.", nameof(value));
                ValidateBounds(percentage, normalizedSchema, nameof(value));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(schema), normalizedSchema.Kind, "Unsupported scalar kind.");
        }

        return value;
    }

    private static void ValidateBounds(decimal value, InteractionScalarSchema schema, string parameterName)
    {
        if (schema.Minimum is not null && value < schema.Minimum)
            throw new ArgumentOutOfRangeException(parameterName, $"Scalar value is below minimum {schema.Minimum}.");
        if (schema.Maximum is not null && value > schema.Maximum)
            throw new ArgumentOutOfRangeException(parameterName, $"Scalar value exceeds maximum {schema.Maximum}.");
    }

    private static string Required(string? value, int maximumLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Scalar {field} is required.");
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"Scalar {field} exceeds {maximumLength} characters.");
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return Required(value, maximumLength, field);
    }
}
