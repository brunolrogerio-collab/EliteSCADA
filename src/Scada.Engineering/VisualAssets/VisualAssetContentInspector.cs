using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Scada.Engineering.VisualAssets;

public sealed record VisualAssetContentInspection(
    string MediaType,
    int? PixelWidth,
    int? PixelHeight,
    byte[] CanonicalContent);

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
    private const int MaximumElements = 10_000;
    private const int MaximumDepth = 64;
    private const long MaximumCharacters = 4L * 1024L * 1024L;
    private const string SvgNamespace = "http://www.w3.org/2000/svg";
    private const string XLinkNamespace = "http://www.w3.org/1999/xlink";

    private static readonly HashSet<string> AllowedElements = new(StringComparer.Ordinal)
    {
        "svg", "g", "defs", "symbol", "use",
        "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
        "text", "tspan", "title", "desc",
        "clipPath", "mask", "linearGradient", "radialGradient", "stop",
        "pattern", "marker"
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

        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = false,
            NewLineHandling = NewLineHandling.None,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        }))
        {
            document.Root.Save(writer, SaveOptions.DisableFormatting);
        }

        var canonical = Encoding.UTF8.GetBytes(builder.ToString());
        return new VisualAssetContentInspection(
            VisualAssetContentInspector.SvgMediaType,
            PixelWidth: null,
            PixelHeight: null,
            CanonicalContent: canonical);
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

    [GeneratedRegex(@"(?i)(?:javascript\s*:|data\s*:|https?\s*:|file\s*:|ftp\s*:|//|@import|expression\s*\(|behavior\s*:|-moz-binding)")]
    private static partial Regex DangerousValueRegex();

    [GeneratedRegex(@"(?i)url\(\s*([^\)]+)\s*\)")]
    private static partial Regex UrlFunctionRegex();
}
