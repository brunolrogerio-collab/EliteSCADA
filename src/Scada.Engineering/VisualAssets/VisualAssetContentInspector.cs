using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Scada.Engineering.VisualAssets;

public sealed record VisualAssetContentInspection(
    string MediaType,
    int? PixelWidth,
    int? PixelHeight,
    byte[] CanonicalContent,
    IReadOnlyDictionary<string, string>? CanonicalMetadata = null);

public static class VisualAssetContentInspector
{
    public const string SvgMediaType = "image/svg+xml";

    public static VisualAssetContentInspection InspectAndCanonicalize(ReadOnlySpan<byte> content)
    {
        try
        {
            var raster = RasterImageInspector.Inspect(content);
            return new VisualAssetContentInspection(
                raster.MediaType,
                raster.PixelWidth,
                raster.PixelHeight,
                content.ToArray());
        }
        catch (InvalidDataException rasterFailure)
        {
            try
            {
                return StaticSvgInspector.InspectAndSanitize(content);
            }
            catch (InvalidDataException svgFailure)
            {
                throw new InvalidDataException(
                    $"Visual asset is neither a structurally valid supported raster image nor an accepted static SVG. Raster: {rasterFailure.Message} SVG: {svgFailure.Message}",
                    svgFailure);
            }
        }
    }
}

public static partial class StaticSvgInspector
{
    public const string MetadataPrefix = "elitescada.svg.";
    public const string MetadataVersionKey = MetadataPrefix + "version";
    public const string MetadataPaletteKey = MetadataPrefix + "palette";
    public const string MetadataSlotsKey = MetadataPrefix + "slots";
    public const string MetadataViewBoxKey = MetadataPrefix + "viewBox";
    public const string MetadataUnsupportedPaintKey = MetadataPrefix + "unsupportedPaint";

    private const int MaximumElements = 10_000;
    private const int MaximumDepth = 64;
    private const long MaximumCharacters = 4L * 1024L * 1024L;
    private const string SvgNamespace = "http://www.w3.org/2000/svg";
    private const string XLinkNamespace = "http://www.w3.org/1999/xlink";

    private static readonly JsonSerializerOptions MetadataJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly HashSet<string> AllowedElements = new(StringComparer.Ordinal)
    {
        "svg", "g", "defs", "symbol", "use",
        "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
        "text", "tspan", "title", "desc",
        "clipPath", "mask", "linearGradient", "radialGradient", "stop",
        "pattern", "marker"
    };

    private static readonly IReadOnlyDictionary<string, string> NamedColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["black"] = "#000000",
            ["silver"] = "#C0C0C0",
            ["gray"] = "#808080",
            ["grey"] = "#808080",
            ["white"] = "#FFFFFF",
            ["maroon"] = "#800000",
            ["red"] = "#FF0000",
            ["purple"] = "#800080",
            ["fuchsia"] = "#FF00FF",
            ["green"] = "#008000",
            ["lime"] = "#00FF00",
            ["olive"] = "#808000",
            ["yellow"] = "#FFFF00",
            ["navy"] = "#000080",
            ["blue"] = "#0000FF",
            ["teal"] = "#008080",
            ["aqua"] = "#00FFFF",
            ["orange"] = "#FFA500",
            ["transparent"] = "#00000000"
        };

    public static VisualAssetContentInspection InspectAndSanitize(ReadOnlySpan<byte> content)
    {
        if (content.IsEmpty)
            throw new InvalidDataException("SVG payload is empty.");

        XDocument document;
        try
        {
            using var stream = new MemoryStream(content.ToArray(), writable: false);
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumCharacters,
                MaxCharactersFromEntities = 0,
                IgnoreComments = false,
                IgnoreProcessingInstructions = false
            };
            using var reader = XmlReader.Create(stream, settings);
            document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            throw new InvalidDataException($"SVG XML is invalid or unsafe: {ex.Message}", ex);
        }

        if (document.DocumentType is not null)
            throw new InvalidDataException("SVG document types/entities are not allowed.");
        if (document.Root is null ||
            document.Root.Name.LocalName != "svg" ||
            document.Root.Name.NamespaceName != SvgNamespace)
            throw new InvalidDataException("SVG root must be <svg> in the canonical SVG namespace.");

        document.DescendantNodes().OfType<XComment>().Remove();
        document.DescendantNodes().OfType<XProcessingInstruction>().Remove();

        var elements = document.Root.DescendantsAndSelf().ToArray();
        if (elements.Length > MaximumElements)
            throw new InvalidDataException($"SVG contains more than {MaximumElements} elements.");

        foreach (var element in elements)
        {
            if (Depth(element) > MaximumDepth)
                throw new InvalidDataException($"SVG nesting exceeds the maximum depth of {MaximumDepth}.");
            if (element.Name.NamespaceName != SvgNamespace || !AllowedElements.Contains(element.Name.LocalName))
                throw new InvalidDataException($"SVG element <{element.Name.LocalName}> is not part of the accepted static subset.");

            foreach (var attribute in element.Attributes().ToArray())
                ValidateAttribute(attribute);

            if (element.Name.LocalName is not ("text" or "tspan"))
            {
                foreach (var whitespace in element.Nodes().OfType<XText>().Where(node => string.IsNullOrWhiteSpace(node.Value)).ToArray())
                    whitespace.Remove();
            }

            var ordered = element.Attributes()
                .OrderBy(attribute => attribute.IsNamespaceDeclaration ? 0 : 1)
                .ThenBy(attribute => attribute.Name.NamespaceName, StringComparer.Ordinal)
                .ThenBy(attribute => attribute.Name.LocalName, StringComparer.Ordinal)
                .ToArray();
            element.RemoveAttributes();
            foreach (var attribute in ordered)
                element.Add(attribute);
        }

        var metadata = BuildCanonicalMetadata(document.Root);
        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = false,
            NewLineHandling = NewLineHandling.None,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        }))
        {
            document.Root.Save(writer);
        }

        var canonical = Encoding.UTF8.GetBytes(builder.ToString());
        return new VisualAssetContentInspection(
            VisualAssetContentInspector.SvgMediaType,
            PixelWidth: null,
            PixelHeight: null,
            CanonicalContent: canonical,
            CanonicalMetadata: metadata);
    }

    private static IReadOnlyDictionary<string, string> BuildCanonicalMetadata(XElement root)
    {
        var palette = new SortedDictionary<string, PaintUse>(StringComparer.Ordinal);
        var unsupported = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var element in root.DescendantsAndSelf())
        {
            CapturePaint(element, "fill", isFill: true, palette, unsupported);
            CapturePaint(element, "stroke", isFill: false, palette, unsupported);
        }

        var slots = root.DescendantsAndSelf()
            .Select(element => new { Element = element, Slot = element.Attribute("data-elitescada-slot")?.Value.Trim() })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Slot))
            .GroupBy(entry => entry.Slot!, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var scoped = group.SelectMany(entry => entry.Element.DescendantsAndSelf()).Distinct().ToArray();
                return new SvgPaintSlotMetadata(
                    group.Key,
                    scoped.Any(element => HasEditablePaint(element, "fill")),
                    scoped.Any(element => HasEditablePaint(element, "stroke")),
                    scoped.Any(HasEditableStrokeWidth));
            })
            .ToArray();

        var paletteMetadata = palette
            .Select(entry => new SvgPaintPaletteMetadata(entry.Key, entry.Value.Fill, entry.Value.Stroke))
            .ToArray();

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MetadataVersionKey] = "1",
            [MetadataPaletteKey] = JsonSerializer.Serialize(paletteMetadata, MetadataJson),
            [MetadataSlotsKey] = JsonSerializer.Serialize(slots, MetadataJson)
        };

        var viewBox = root.Attribute("viewBox")?.Value.Trim();
        if (!string.IsNullOrWhiteSpace(viewBox))
            metadata[MetadataViewBoxKey] = viewBox;

        if (unsupported.Count > 0)
            metadata[MetadataUnsupportedPaintKey] = JsonSerializer.Serialize(unsupported, MetadataJson);

        return metadata;
    }

    private static void CapturePaint(
        XElement element,
        string property,
        bool isFill,
        IDictionary<string, PaintUse> palette,
        ISet<string> unsupported)
    {
        var raw = ReadPresentationValue(element, property);
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var normalized = NormalizeStaticPaint(raw);
        if (normalized is null)
        {
            if (!raw.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
                unsupported.Add($"{property}:{raw.Trim()}");
            return;
        }

        if (!palette.TryGetValue(normalized, out var use))
            use = new PaintUse();

        palette[normalized] = isFill
            ? use with { Fill = true }
            : use with { Stroke = true };
    }

    private static bool HasEditablePaint(XElement element, string property)
    {
        var raw = ReadPresentationValue(element, property);
        return !string.IsNullOrWhiteSpace(raw) && NormalizeStaticPaint(raw) is not null;
    }

    private static bool HasEditableStrokeWidth(XElement element)
    {
        var raw = ReadPresentationValue(element, "stroke-width");
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        var text = raw.Trim();
        if (text.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            text = text[..^2].Trim();
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
            double.IsFinite(number) &&
            number >= 0;
    }

    private static string? ReadPresentationValue(XElement element, string property)
    {
        var direct = element.Attribute(property)?.Value;
        if (!string.IsNullOrWhiteSpace(direct))
            return direct.Trim();

        var style = element.Attribute("style")?.Value;
        if (string.IsNullOrWhiteSpace(style))
            return null;

        foreach (var declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = declaration.IndexOf(':');
            if (separator <= 0)
                continue;
            if (declaration[..separator].Trim().Equals(property, StringComparison.OrdinalIgnoreCase))
                return declaration[(separator + 1)..].Trim();
        }

        return null;
    }

    public static string? NormalizeStaticPaint(string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (NamedColors.TryGetValue(value, out var named))
            return named;

        var hex = HexColorRegex().Match(value);
        if (hex.Success)
        {
            var digits = hex.Groups[1].Value.ToUpperInvariant();
            return digits.Length switch
            {
                3 => $"#{digits[0]}{digits[0]}{digits[1]}{digits[1]}{digits[2]}{digits[2]}",
                4 => $"#{digits[0]}{digits[0]}{digits[1]}{digits[1]}{digits[2]}{digits[2]}{digits[3]}{digits[3]}",
                6 or 8 => $"#{digits}",
                _ => null
            };
        }

        var rgb = RgbColorRegex().Match(value);
        if (rgb.Success &&
            byte.TryParse(rgb.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var red) &&
            byte.TryParse(rgb.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var green) &&
            byte.TryParse(rgb.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var blue))
        {
            return $"#{red:X2}{green:X2}{blue:X2}";
        }

        var rgba = RgbaColorRegex().Match(value);
        if (rgba.Success &&
            byte.TryParse(rgba.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out red) &&
            byte.TryParse(rgba.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out green) &&
            byte.TryParse(rgba.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out blue) &&
            double.TryParse(rgba.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var alpha) &&
            double.IsFinite(alpha) &&
            alpha >= 0 &&
            alpha <= 1)
        {
            return $"#{red:X2}{green:X2}{blue:X2}{(int)Math.Round(alpha * 255, MidpointRounding.AwayFromZero):X2}";
        }

        return null;
    }

    private static void ValidateAttribute(XAttribute attribute)
    {
        if (attribute.IsNamespaceDeclaration)
        {
            if (attribute.Value is not (SvgNamespace or XLinkNamespace))
                throw new InvalidDataException($"SVG namespace '{attribute.Value}' is not allowed.");
            return;
        }

        var namespaceName = attribute.Name.NamespaceName;
        if (namespaceName.Length != 0 && namespaceName != XLinkNamespace)
            throw new InvalidDataException($"SVG attribute namespace '{namespaceName}' is not allowed.");

        var name = attribute.Name.LocalName;
        if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"SVG event-handler attribute '{name}' is not allowed.");

        var value = attribute.Value.Trim();
        if (name.Equals("data-elitescada-slot", StringComparison.Ordinal))
        {
            if (!SvgSlotRegex().IsMatch(value))
                throw new InvalidDataException("SVG data-elitescada-slot must be a stable 1-64 character token using letters, digits, dot, underscore or hyphen.");
        }

        if (name.Equals("href", StringComparison.OrdinalIgnoreCase))
        {
            if (value.Length == 0 || !value.StartsWith('#') || value.Length == 1)
                throw new InvalidDataException("SVG href references must target an internal fragment only.");
        }

        if (DangerousValueRegex().IsMatch(value))
            throw new InvalidDataException($"SVG attribute '{name}' contains an external or active-content reference.");

        foreach (Match match in UrlFunctionRegex().Matches(value))
        {
            var target = match.Groups[1].Value.Trim().Trim('"', '\'');
            if (!target.StartsWith('#') || target.Length == 1)
                throw new InvalidDataException($"SVG attribute '{name}' contains a non-local url() reference.");
        }

        if (value.Contains("url(", StringComparison.OrdinalIgnoreCase) &&
            UrlFunctionRegex().Matches(value).Count == 0)
            throw new InvalidDataException($"SVG attribute '{name}' contains an invalid url() reference.");
    }

    private static int Depth(XElement element)
    {
        var depth = 1;
        for (var parent = element.Parent; parent is not null; parent = parent.Parent)
            depth++;
        return depth;
    }

    private sealed record PaintUse(bool Fill = false, bool Stroke = false);
    private sealed record SvgPaintPaletteMetadata(string Color, bool Fill, bool Stroke);
    private sealed record SvgPaintSlotMetadata(string Name, bool Fill, bool Stroke, bool StrokeWidth);

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SvgSlotRegex();

    [GeneratedRegex("^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{4}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();

    [GeneratedRegex(@"(?i)^rgb\(\s*([0-9]{1,3})\s*,\s*([0-9]{1,3})\s*,\s*([0-9]{1,3})\s*\)$", RegexOptions.CultureInvariant)]
    private static partial Regex RgbColorRegex();

    [GeneratedRegex(@"(?i)^rgba\(\s*([0-9]{1,3})\s*,\s*([0-9]{1,3})\s*,\s*([0-9]{1,3})\s*,\s*(0(?:\.\d+)?|1(?:\.0+)?)\s*\)$", RegexOptions.CultureInvariant)]
    private static partial Regex RgbaColorRegex();

    [GeneratedRegex(@"(?i)(?:javascript\s*:|data\s*:|https?\s*:|file\s*:|ftp\s*:|//|@import|expression\s*\(|behavior\s*:|-moz-binding)")]
    private static partial Regex DangerousValueRegex();

    [GeneratedRegex(@"(?i)url\(\s*([^\)]+)\s*\)")]
    private static partial Regex UrlFunctionRegex();
}
