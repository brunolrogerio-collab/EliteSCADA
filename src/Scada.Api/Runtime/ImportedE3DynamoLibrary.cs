using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scada.Engineering.Contracts;

namespace Scada.Api.Runtime;

/// <summary>
/// Explicit-import converter for curated, normalized vector candidates from
/// Elipse E3 CSV exports. This library is intentionally not auto-seeded into
/// new workspaces: import candidates remain available to the converter/tests,
/// while a user must explicitly choose to reintroduce them into a project.
/// Exports are reduced to portable drawing geometry; project paths and original
/// TAG addresses are deliberately not retained.
/// </summary>
public static class ImportedE3DynamoLibrary
{
    public const string Version = "1.4.0";

    private static readonly Lazy<IReadOnlyCollection<DynamoEngineeringDto>> Definitions = new(Load);

    public static IReadOnlyCollection<DynamoEngineeringDto> Create() => Definitions.Value;

    private static IReadOnlyCollection<DynamoEngineeringDto> Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("ImportedE3DynamoLibraryData.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Imported Elipse E3 Dynamo data resource is missing.");
        using var document = JsonDocument.Parse(stream);

        return document.RootElement.EnumerateArray().Select(BuildDefinition)
            .Append(BuildQuarterArcCandidate())
            .ToArray();
    }

    private static DynamoEngineeringDto BuildDefinition(JsonElement source)
    {
        var key = source.GetProperty("key").GetString()!;
        var name = source.GetProperty("name").GetString()!;
        var sourceFile = source.GetProperty("sourceFile").GetString()!;
        var width = source.GetProperty("width").GetDouble();
        var height = source.GetProperty("height").GetDouble();
        var parameters = new Dictionary<string, DynamoParameterDefinitionEngineeringDto>(StringComparer.OrdinalIgnoreCase);
        var shapeRecords = new List<ImportedShapeRecord>();
        var sequence = 0;

        foreach (var shape in source.GetProperty("elements").EnumerateArray())
        {
            var elementKey = shape.GetProperty("key").GetString()!;
            var type = shape.GetProperty("type").GetString()!;
            var state = OptionalString(shape, "state");
            var parameterBase = OptionalString(shape, "parameter");
            var groupId = OptionalString(shape, "groupId");
            var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["x"] = JsonSerializer.SerializeToElement(shape.GetProperty("x").GetDouble()),
                ["y"] = JsonSerializer.SerializeToElement(shape.GetProperty("y").GetDouble()),
                ["width"] = JsonSerializer.SerializeToElement(shape.GetProperty("width").GetDouble()),
                ["height"] = JsonSerializer.SerializeToElement(shape.GetProperty("height").GetDouble()),
                ["rotation"] = JsonSerializer.SerializeToElement(shape.GetProperty("rotation").GetDouble()),
                // E3 CSV export is ordered back-to-front. Preserve that order
                // explicitly after flattening nested DrawGroup hierarchies.
                ["zIndex"] = JsonSerializer.SerializeToElement(
                    shape.TryGetProperty("zIndex", out var zIndex) ? zIndex.GetDouble() : sequence)
            };

            if (type != "core.text")
            {
                properties["strokeColor"] = JsonSerializer.SerializeToElement(shape.GetProperty("strokeColor").GetString() ?? "#34495A");
                properties["strokeWidth"] = JsonSerializer.SerializeToElement(shape.GetProperty("strokeWidth").GetDouble());
            }

            if (type is "core.rectangle" or "core.ellipse" or "core.polygon")
            {
                properties["fillStyle"] = JsonSerializer.SerializeToElement(OptionalString(shape, "fillStyle") ?? "solid");
                properties["fillColor"] = JsonSerializer.SerializeToElement(shape.GetProperty("fillColor").GetString() ?? "#C7D0D8");
                if (shape.TryGetProperty("fillSecondaryColor", out var secondaryColor))
                    properties["fillSecondaryColor"] = secondaryColor.Clone();
                if (shape.TryGetProperty("gradientDirection", out var gradientDirection))
                    properties["gradientDirection"] = gradientDirection.Clone();
                if (type == "core.rectangle")
                    properties["cornerRadius"] = JsonSerializer.SerializeToElement(shape.GetProperty("cornerRadius").GetDouble());
            }

            if (type == "core.arc")
            {
                properties["arcStartAngle"] = JsonSerializer.SerializeToElement(shape.GetProperty("arcStartAngle").GetDouble());
                properties["arcEndAngle"] = JsonSerializer.SerializeToElement(shape.GetProperty("arcEndAngle").GetDouble());
                properties["arcStyle"] = JsonSerializer.SerializeToElement(OptionalString(shape, "arcStyle") ?? "pie");
                properties["fillStyle"] = JsonSerializer.SerializeToElement(OptionalString(shape, "fillStyle") ?? "solid");
                properties["fillColor"] = JsonSerializer.SerializeToElement(shape.GetProperty("fillColor").GetString() ?? "#C7D0D8");
                if (shape.TryGetProperty("fillSecondaryColor", out var arcSecondaryColor))
                    properties["fillSecondaryColor"] = arcSecondaryColor.Clone();
                if (shape.TryGetProperty("gradientDirection", out var arcGradientDirection))
                    properties["gradientDirection"] = arcGradientDirection.Clone();
            }

            if (type == "core.bezier")
            {
                properties["bezierPath"] = JsonSerializer.SerializeToElement(
                    OptionalString(shape, "bezierPath") ?? "M 0 0 C 28 0 24 24 100 28 L 100 100 L 0 100 Z");
                properties["fillStyle"] = JsonSerializer.SerializeToElement(OptionalString(shape, "fillStyle") ?? "solid");
                properties["fillColor"] = JsonSerializer.SerializeToElement(shape.GetProperty("fillColor").GetString() ?? "#C7D0D8");
                if (shape.TryGetProperty("fillSecondaryColor", out var bezierSecondaryColor))
                    properties["fillSecondaryColor"] = bezierSecondaryColor.Clone();
                if (shape.TryGetProperty("gradientDirection", out var bezierGradientDirection))
                    properties["gradientDirection"] = bezierGradientDirection.Clone();
            }

            if (type == "core.text")
            {
                var text = OptionalString(shape, "text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    properties["text"] = JsonSerializer.SerializeToElement(text);
                    properties["fontSize"] = JsonSerializer.SerializeToElement(shape.GetProperty("fontSize").GetDouble());
                    properties["fontWeight"] = JsonSerializer.SerializeToElement(600);
                    properties["textColor"] = JsonSerializer.SerializeToElement(shape.GetProperty("strokeColor").GetString() ?? "#1F2937");
                    properties["horizontalAlignment"] = JsonSerializer.SerializeToElement("center");
                    properties["verticalAlignment"] = JsonSerializer.SerializeToElement("middle");
                }
            }

            if (type == "core.polygon" && shape.TryGetProperty("points", out var points))
                properties["points"] = points.Clone();
            if (type == "core.polygon")
                properties["polygonFillRule"] = JsonSerializer.SerializeToElement(OptionalString(shape, "polygonFillRule") ?? "evenodd");

            var bindings = new List<EngineeringBindingDto>();
            if (state is "running" or "fault" && !string.IsNullOrWhiteSpace(parameterBase))
            {
                var suffix = state == "running" ? "Running" : "Fault";
                var parameterKey = parameterBase + suffix;
                parameters.TryAdd(parameterKey, new DynamoParameterDefinitionEngineeringDto(
                    parameterKey,
                    DynamoParameterKind.TagReference));
                properties["visible"] = JsonSerializer.SerializeToElement(false);
                bindings.Add(new EngineeringBindingDto(
                    "visible",
                    EngineeringBindingKind.Tag,
                    $"{{equipmentPath}}.{(parameterBase.StartsWith("pump", StringComparison.Ordinal) ? "Pumps." + parameterBase[4..] + "." : string.Empty)}{suffix}",
                    "read",
                    Metadata: new Dictionary<string, string>
                    {
                        ["dynamoContext"] = "equipmentPath",
                        ["dynamoParameter"] = parameterKey
                    }));
            }

            var element = new VisualElementEngineeringDto(
                elementKey,
                type,
                Bindings: bindings,
                Properties: properties,
                Metadata: new Dictionary<string, string>
                {
                    ["sourcePrimitive"] = "Elipse E3 drawing export"
                },
                Id: StableId($"{key}:{sequence}"));
            shapeRecords.Add(new ImportedShapeRecord(
                element,
                groupId,
                properties["zIndex"].GetDouble(),
                properties["x"].GetDouble(),
                properties["y"].GetDouble()));
            sequence++;
        }

        var groupRecords = source.TryGetProperty("groups", out var groupsElement)
            ? groupsElement.EnumerateArray().Select(group => new ImportedGroupRecord(
                group.GetProperty("id").GetString()!,
                OptionalString(group, "parentId"),
                group.GetProperty("x").GetDouble(),
                group.GetProperty("y").GetDouble(),
                group.GetProperty("width").GetDouble(),
                group.GetProperty("height").GetDouble())).ToArray()
            : Array.Empty<ImportedGroupRecord>();
        var elements = BuildRootElements(key, shapeRecords, groupRecords);

        var linkCount = source.GetProperty("sourceLinkCount").GetInt32();
        var hasStateLayers = elements.Any(element => element.Bindings?.Count > 0);
        return new DynamoEngineeringDto(
            StableId(key),
            key,
            $"E3 · {name}",
            Properties: new Dictionary<string, string>
            {
                ["category"] = source.GetProperty("category").GetString() ?? "process",
                ["defaultWidth"] = width.ToString(CultureInfo.InvariantCulture),
                ["defaultHeight"] = height.ToString(CultureInfo.InvariantCulture),
                ["libraryVersion"] = Version,
                ["visualStyle"] = "detailed-2d"
            },
            Context: new Dictionary<string, string>
            {
                ["usage"] = "process-screen",
                ["view"] = "front-orthographic"
            },
            Metadata: new Dictionary<string, string>
            {
                ["builtinLibrary"] = "false",
                ["importedDynamoLibrary"] = "true",
                ["assetOrigin"] = "elipse-e3-import",
                ["sourceFormat"] = "Elipse E3 CSV",
                ["sourceFile"] = sourceFile,
                ["sourceLinkCount"] = linkCount.ToString(CultureInfo.InvariantCulture),
                ["tagMappingStatus"] = hasStateLayers ? "parameters-required" : linkCount > 0 ? "review-required" : "static-candidate"
            },
            Parameters: parameters.Values.ToArray(),
            Elements: elements);
    }

    private static string? OptionalString(JsonElement source, string name) =>
        source.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyCollection<VisualElementEngineeringDto> BuildRootElements(
        string definitionKey,
        IReadOnlyCollection<ImportedShapeRecord> shapes,
        IReadOnlyCollection<ImportedGroupRecord> groups)
    {
        IReadOnlyCollection<VisualElementEngineeringDto> BuildChildren(string? parentGroupId, double originX, double originY)
        {
            var children = new List<(VisualElementEngineeringDto Element, double Order)>();
            foreach (var shape in shapes.Where(item => string.Equals(item.GroupId, parentGroupId, StringComparison.Ordinal)))
                children.Add((WithRelativePosition(shape.Element, shape.X - originX, shape.Y - originY), shape.Order));

            foreach (var group in groups.Where(item => string.Equals(item.ParentId, parentGroupId, StringComparison.Ordinal)))
            {
                var order = DescendantShapes(group.Id).Select(item => item.Order).DefaultIfEmpty(double.MaxValue).Min();
                if (order == double.MaxValue) continue;

                var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["x"] = JsonSerializer.SerializeToElement(group.X - originX),
                    ["y"] = JsonSerializer.SerializeToElement(group.Y - originY),
                    ["width"] = JsonSerializer.SerializeToElement(group.Width),
                    ["height"] = JsonSerializer.SerializeToElement(group.Height),
                    ["rotation"] = JsonSerializer.SerializeToElement(0d),
                    ["zIndex"] = JsonSerializer.SerializeToElement(order)
                };
                children.Add((new VisualElementEngineeringDto(
                    Key: $"e3-group-{group.Id}",
                    Type: "core.group",
                    Properties: properties,
                    Children: BuildChildren(group.Id, group.X, group.Y),
                    Metadata: new Dictionary<string, string>
                    {
                        ["sourcePrimitive"] = "Elipse E3 DrawGroup"
                    },
                    Id: StableId($"{definitionKey}:group:{group.Id}")), order));
            }

            return children.OrderBy(item => item.Order).Select(item => item.Element).ToArray();

            IEnumerable<ImportedShapeRecord> DescendantShapes(string groupId)
            {
                foreach (var shape in shapes.Where(item => string.Equals(item.GroupId, groupId, StringComparison.Ordinal)))
                    yield return shape;
                foreach (var nested in groups.Where(item => string.Equals(item.ParentId, groupId, StringComparison.Ordinal)))
                    foreach (var shape in DescendantShapes(nested.Id))
                        yield return shape;
            }
        }

        return BuildChildren(null, 0, 0);
    }

    private static VisualElementEngineeringDto WithRelativePosition(
        VisualElementEngineeringDto element,
        double x,
        double y)
    {
        var properties = element.Properties is null
            ? new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            : new Dictionary<string, JsonElement>(element.Properties, StringComparer.Ordinal);
        properties["x"] = JsonSerializer.SerializeToElement(x);
        properties["y"] = JsonSerializer.SerializeToElement(y);
        return element with { Properties = properties };
    }

    private sealed record ImportedShapeRecord(
        VisualElementEngineeringDto Element,
        string? GroupId,
        double Order,
        double X,
        double Y);

    private sealed record ImportedGroupRecord(
        string Id,
        string? ParentId,
        double X,
        double Y,
        double Width,
        double Height);

    private static DynamoEngineeringDto BuildQuarterArcCandidate()
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["x"] = JsonSerializer.SerializeToElement(10d),
            ["y"] = JsonSerializer.SerializeToElement(10d),
            ["width"] = JsonSerializer.SerializeToElement(120d),
            ["height"] = JsonSerializer.SerializeToElement(120d),
            ["rotation"] = JsonSerializer.SerializeToElement(90d),
            ["zIndex"] = JsonSerializer.SerializeToElement(0d),
            ["arcStartAngle"] = JsonSerializer.SerializeToElement(0d),
            ["arcEndAngle"] = JsonSerializer.SerializeToElement(90d),
            ["arcStyle"] = JsonSerializer.SerializeToElement("pie"),
            ["fillStyle"] = JsonSerializer.SerializeToElement("gradient"),
            ["strokeColor"] = JsonSerializer.SerializeToElement("#C76400"),
            ["strokeWidth"] = JsonSerializer.SerializeToElement(2d),
            ["strokeStyle"] = JsonSerializer.SerializeToElement("solid"),
            ["fillColor"] = JsonSerializer.SerializeToElement("#C76400"),
            ["fillSecondaryColor"] = JsonSerializer.SerializeToElement("#F7E3D3"),
            ["gradientDirection"] = JsonSerializer.SerializeToElement("diagonal-down")
        };
        return new DynamoEngineeringDto(
            StableId("e3.process.quarter-arc"),
            "e3.process.quarter-arc",
            "E3 · Curva de 90°",
            Properties: new Dictionary<string, string>
            {
                ["category"] = "process",
                ["defaultWidth"] = "140",
                ["defaultHeight"] = "140",
                ["libraryVersion"] = Version,
                ["visualStyle"] = "detailed-2d"
            },
            Context: new Dictionary<string, string>
            {
                ["usage"] = "process-screen",
                ["view"] = "front-orthographic"
            },
            Metadata: new Dictionary<string, string>
            {
                ["builtinLibrary"] = "false",
                ["importedDynamoLibrary"] = "true",
                ["assetOrigin"] = "elipse-e3-import",
                ["sourceFormat"] = "Elipse E3 CSV",
                ["sourceFile"] = "curva.CSV",
                ["sourceLinkCount"] = "0",
                ["tagMappingStatus"] = "static-candidate"
            },
            Elements:
            [
                new VisualElementEngineeringDto(
                    "e3-arc-001",
                    "core.arc",
                    Properties: properties,
                    Metadata: new Dictionary<string, string>
                    {
                        ["sourcePrimitive"] = "Elipse E3 DrawArc"
                    },
                    Id: StableId("e3.process.quarter-arc:0"))
            ]);
    }

    private static Guid StableId(string identity)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("EliteSCADA/ImportedE3/" + identity));
        return new Guid(hash.AsSpan(0, 16));
    }
}
