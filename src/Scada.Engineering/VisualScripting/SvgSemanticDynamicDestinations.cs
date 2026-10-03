using System.Text.Json;

namespace Scada.Engineering.VisualScripting;

internal enum SvgSemanticDynamicPaintProperty
{
    Fill,
    Stroke,
    StrokeWidth
}

internal sealed record SvgSemanticDynamicDestination(
    string Key,
    string Slot,
    SvgSemanticDynamicPaintProperty Property)
{
    public bool IsColor => Property is SvgSemanticDynamicPaintProperty.Fill or SvgSemanticDynamicPaintProperty.Stroke;
    public bool IsNumber => Property == SvgSemanticDynamicPaintProperty.StrokeWidth;
}

internal static class SvgSemanticDynamicDestinations
{
    public const string Prefix = "svg.slot.";

    public static bool TryParse(string? key, out SvgSemanticDynamicDestination destination)
    {
        destination = null!;
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var suffix = key[Prefix.Length..];
        var separator = suffix.LastIndexOf('.');
        if (separator <= 0 || separator == suffix.Length - 1)
            return false;

        var slot = suffix[..separator];
        if (!ValidSlot(slot))
            return false;

        var property = suffix[(separator + 1)..] switch
        {
            "fill" => SvgSemanticDynamicPaintProperty.Fill,
            "stroke" => SvgSemanticDynamicPaintProperty.Stroke,
            "strokeWidth" => SvgSemanticDynamicPaintProperty.StrokeWidth,
            _ => (SvgSemanticDynamicPaintProperty?)null
        };
        if (!property.HasValue)
            return false;

        destination = new(key, slot, property.Value);
        return true;
    }

    public static bool ValidateMappedValue(SvgSemanticDynamicDestination destination, JsonElement value)
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

    private static bool HexColor(string? value) =>
        value is { Length: 7 or 9 } &&
        value[0] == '#' &&
        value.Skip(1).All(Uri.IsHexDigit);

    private static bool ValidSlot(string slot)
    {
        if (slot.Length is < 1 or > 64 || !char.IsAsciiLetter(slot[0]))
            return false;
        return slot.Skip(1).All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '.' or '_' or '-');
    }
}
