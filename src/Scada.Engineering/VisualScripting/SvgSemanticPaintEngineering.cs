using System.Text.Json;

namespace Scada.Engineering.VisualScripting;

/// <summary>
/// Stable public namespace for dynamic SVG semantic paint destinations.
/// Destination names are derived only from sanitized Visual Asset slot metadata:
/// svg.slot.&lt;slot&gt;.fill|stroke|strokeWidth.
/// </summary>
public static class SvgSemanticPaintEngineering
{
    public const string Prefix = "svg.slot.";

    public enum PaintProperty
    {
        Fill,
        Stroke,
        StrokeWidth
    }

    public sealed record Destination(string Slot, PaintProperty Property)
    {
        public bool IsColor => Property is PaintProperty.Fill or PaintProperty.Stroke;
        public bool IsNumber => Property == PaintProperty.StrokeWidth;
    }

    public static bool TryParse(string? key, out Destination destination)
    {
        destination = null!;
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var tail = key[Prefix.Length..];
        var separator = tail.LastIndexOf('.');
        if (separator <= 0 || separator >= tail.Length - 1)
            return false;

        var slot = tail[..separator];
        if (!ValidSlot(slot))
            return false;

        var property = tail[(separator + 1)..] switch
        {
            "fill" => PaintProperty.Fill,
            "stroke" => PaintProperty.Stroke,
            "strokeWidth" => PaintProperty.StrokeWidth,
            _ => (PaintProperty?)null
        };
        if (!property.HasValue)
            return false;

        destination = new Destination(slot, property.Value);
        return true;
    }

    public static string Key(string slot, PaintProperty property)
    {
        if (!ValidSlot(slot))
            throw new ArgumentException("SVG semantic slot name is invalid.", nameof(slot));
        var suffix = property switch
        {
            PaintProperty.Fill => "fill",
            PaintProperty.Stroke => "stroke",
            PaintProperty.StrokeWidth => "strokeWidth",
            _ => throw new ArgumentOutOfRangeException(nameof(property))
        };
        return $"{Prefix}{slot}.{suffix}";
    }

    public static bool ValidateMappedValue(Destination destination, JsonElement value)
    {
        if (destination.IsColor)
        {
            return value.ValueKind == JsonValueKind.String &&
                HexColor(value.GetString());
        }

        return value.ValueKind == JsonValueKind.Number &&
            value.TryGetDouble(out var width) &&
            double.IsFinite(width) &&
            width >= 0 &&
            width <= 10_000;
    }

    public static bool HexColor(string? value) =>
        value is { Length: 7 or 9 } &&
        value[0] == '#' &&
        value.Skip(1).All(Uri.IsHexDigit);

    private static bool ValidSlot(string value)
    {
        if (value.Length is < 1 or > 64 || !char.IsAsciiLetter(value[0]))
            return false;
        foreach (var ch in value)
        {
            if (!(char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or '-'))
                return false;
        }
        return true;
    }
}
