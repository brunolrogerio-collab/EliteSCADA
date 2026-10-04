using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualAssets;

namespace Scada.Api.Runtime;

/// <summary>
/// First replacement-generation catalog. Kept separate from the original 72
/// definitions so existing project references retain their identity/artwork.
/// New artwork is canonical editable vector geometry, not a second renderer.
/// </summary>
public static class BuiltinDynamoCatalogV1
{
    public const string Version = "1.0.0";
    private const string Outline = "#263746";
    private const string Steel = "#A9BAC5";
    private const string Light = "#E7EEF2";
    private const string Dark = "#526879";

    public static IReadOnlyCollection<DynamoEngineeringDto> Create() =>
        CreateGeometryDefinitions().Select(ComposeSvgArtwork).ToArray();

    public static IReadOnlyCollection<(VisualAssetEngineeringDto Asset, VisualAssetPayload Payload)> CreateArtworkAssets()
    {
        return CreateGeometryDefinitions().Select(definition =>
        {
            var content = Encoding.UTF8.GetBytes(BuildArtworkSvg(definition));
            var inspection = VisualAssetContentInspector.InspectAndCanonicalize(content);
            var payload = VisualAssetPayload.Create(inspection.MediaType, inspection.CanonicalContent);
            var asset = new VisualAssetEngineeringDto(
                StableElementId("asset:" + definition.Key),
                AssetKey(definition.Key), definition.Name, $"{AssetKey(definition.Key)}.svg", payload.MediaType,
                payload.ByteLength, payload.Sha256, Description: "Original EliteSCADA Dynamo vector artwork generated from editable geometry.",
                Metadata: new Dictionary<string, string>(inspection.CanonicalMetadata ?? new Dictionary<string, string>(), StringComparer.Ordinal)
                {
                    ["catalogGeneration"] = "1", ["assetOrigin"] = "original-elitescada-vector-factory",
                    ["reviewStatus"] = "first-party-product-artwork"
                });
            return (asset, payload);
        }).ToArray();
    }

    private static IReadOnlyCollection<DynamoEngineeringDto> CreateGeometryDefinitions()
    {
        var result = new List<DynamoEngineeringDto>(26);
        result.AddRange(new[] { "round", "square", "rectangular", "stacked" }.Select((shape, i) => Lamp(shape, i)));
        result.AddRange(new[] { "raised", "flush", "guarded", "illuminated" }.Select((shape, i) => Button(shape, i)));
        result.AddRange(new[] { "tefc", "finned", "vertical", "foot-mounted", "large-frame", "vfd-package" }
            .Select((shape, i) => Equipment("motor", shape, $"Motor {MotorName(shape)}", i)));
        result.AddRange(new[] { "gate", "globe", "ball", "butterfly", "diaphragm", "control" }
            .Select((shape, i) => Equipment("valve", shape, $"Válvula {ValveName(shape)}", i)));
        result.AddRange(new[] { "contact-mono-horizontal", "contact-mono-vertical", "contact-tri-horizontal",
            "contact-tri-vertical", "isolator-horizontal", "isolator-vertical" }
            .Select((shape, i) => Equipment("electrical", shape, $"Contato elétrico {ContactName(shape)}", i)));
        return result;
    }

    private static DynamoEngineeringDto ComposeSvgArtwork(DynamoEngineeringDto definition)
    {
        var geometry = definition.Elements ?? Array.Empty<VisualElementEngineeringDto>();
        var stateElement = geometry.FirstOrDefault(element => element.PropertyMaps?.Any(map => map.PropertyKey == "fillColor") == true);
        var svgProperties = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["x"] = JsonSerializer.SerializeToElement(0d), ["y"] = JsonSerializer.SerializeToElement(0d),
            ["width"] = JsonSerializer.SerializeToElement(double.Parse(definition.Properties!["defaultWidth"], System.Globalization.CultureInfo.InvariantCulture)),
            ["height"] = JsonSerializer.SerializeToElement(double.Parse(definition.Properties["defaultHeight"], System.Globalization.CultureInfo.InvariantCulture)),
            ["assetRef"] = JsonSerializer.SerializeToElement(new { assetId = StableElementId("asset:" + definition.Key).ToString("D") }),
            ["svgPaintOverrides"] = JsonSerializer.SerializeToElement(new { version = 1, palette = new { }, slots = new { } })
        };
        var maps = stateElement?.PropertyMaps?.Select(map => map with { PropertyKey = "svg.slot.state.fill" }).ToArray();
        var metadata = stateElement?.Metadata is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(stateElement.Metadata, StringComparer.Ordinal);
        var symbol = new VisualElementEngineeringDto(
            "artwork", "core.svgSymbol", Properties: svgProperties,
            Metadata: metadata, PropertyMaps: maps,
            Actions: geometry.SelectMany(element => element.Actions ?? []).ToArray(),
            Id: StableElementId(definition.Key + ":svg-artwork"));
        var labels = geometry.Where(element => element.Type == "core.text").ToArray();
        return definition with { Elements = [symbol, .. labels] };
    }

    private static string BuildArtworkSvg(DynamoEngineeringDto definition)
    {
        var width = double.Parse(definition.Properties!["defaultWidth"], System.Globalization.CultureInfo.InvariantCulture);
        var height = double.Parse(definition.Properties["defaultHeight"], System.Globalization.CultureInfo.InvariantCulture);
        XNamespace svgNamespace = "http://www.w3.org/2000/svg";
        var root = new XElement(svgNamespace + "svg",
            new XAttribute("viewBox", FormattableString.Invariant($"0 0 {width} {height}")),
            new XAttribute("width", FormattableString.Invariant($"{width}px")),
            new XAttribute("height", FormattableString.Invariant($"{height}px")));
        foreach (var element in definition.Elements ?? [])
        {
            if (element.Type == "core.text" || element.Properties is null) continue;
            var p = element.Properties;
            if (!Number(p, "x", out var x) || !Number(p, "y", out var y) ||
                !Number(p, "width", out var w) || !Number(p, "height", out var h)) continue;
            var fill = String(p, "fillColor", "none");
            var stroke = String(p, "strokeColor", "none");
            var strokeWidth = Number(p, "strokeWidth", out var sw) ? sw : 0;
            var rotated = Number(p, "rotation", out var angle) && Math.Abs(angle) > 0.001;
            var cx = x + w / 2;
            var cy = y + h / 2;
            var attributes = new List<object>
            {
                new XAttribute("fill", fill), new XAttribute("stroke", stroke), new XAttribute("stroke-width", FormattableString.Invariant($"{strokeWidth:0.###}")),
                new XAttribute("vector-effect", "non-scaling-stroke")
            };
            if (stateElementKey(definition) == element.Key) attributes.Add(new XAttribute("data-elitescada-slot", "state"));
            if (rotated) attributes.Add(new XAttribute("transform", FormattableString.Invariant($"rotate({angle:0.###} {cx:0.###} {cy:0.###})")));
            XElement? shape = element.Type switch
            {
                "core.rectangle" => new XElement(svgNamespace + "rect", new XAttribute("x", x), new XAttribute("y", y), new XAttribute("width", w), new XAttribute("height", h), new XAttribute("rx", Number(p, "cornerRadius", out var radius) ? radius : 0), attributes),
                "core.ellipse" => new XElement(svgNamespace + "ellipse", new XAttribute("cx", cx), new XAttribute("cy", cy), new XAttribute("rx", w / 2), new XAttribute("ry", h / 2), attributes),
                "core.polygon" when p.TryGetValue("points", out var points) && points.ValueKind == JsonValueKind.Array => new XElement(svgNamespace + "polygon",
                    new XAttribute("points", string.Join(" ", points.EnumerateArray().Select(point => FormattableString.Invariant($"{x + point.GetProperty("x").GetDouble():0.###},{y + point.GetProperty("y").GetDouble():0.###}")))), attributes),
                _ => null
            };
            if (shape is not null) root.Add(shape);
        }
        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static string? stateElementKey(DynamoEngineeringDto definition) =>
        definition.Elements?.FirstOrDefault(element => element.PropertyMaps?.Any(map => map.PropertyKey == "fillColor") == true)?.Key;

    private static bool Number(IReadOnlyDictionary<string, JsonElement> values, string key, out double number) =>
        values.TryGetValue(key, out var value) && value.TryGetDouble(out number) || SetZero(out number);

    private static bool SetZero(out double number) { number = 0; return false; }
    private static string String(IReadOnlyDictionary<string, JsonElement> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;
    private static string AssetKey(string key) => $"builtin.dynamo.v1.{key.Replace('.', '-')}";

    private static DynamoEngineeringDto Lamp(string shape, int index)
    {
        const int w = 110, h = 84;
        var x = new List<VisualElementEngineeringDto>();
        var square = shape is "square" or "rectangular";
        var sx = shape == "rectangular" ? 16d : 28d;
        var sy = shape == "rectangular" ? 26d : 18d;
        var sw = shape == "rectangular" ? 78d : 54d;
        var sh = shape == "rectangular" ? 34d : 54d;
        x.Add(Shape("bezel", square ? "core.rectangle" : "core.ellipse", sx, sy, sw, sh, "#D6E0E6", Outline, square ? 5 : 1));
        x.Add(Shape("lens", square ? "core.rectangle" : "core.ellipse", sx + 7, sy + 7, sw - 14, sh - 14, "#16A34A", Outline, square ? 3 : 1));
        if (shape == "stacked")
        {
            x.Clear();
            x.Add(Shape("mount", "core.rectangle", 42, 59, 26, 10, Dark, Outline, 1));
            x.Add(Shape("red-lens", "core.ellipse", 34, 10, 42, 24, "#DC2626", Outline, 2));
            x.Add(Shape("amber-lens", "core.ellipse", 34, 30, 42, 24, "#EAB308", Outline, 2));
            x.Add(Shape("green-lens", "core.ellipse", 34, 50, 42, 24, "#16A34A", Outline, 2));
        }
        const bool stateful = true;
        var parameters = new List<DynamoParameterDefinitionEngineeringDto>
        {
            new("state", DynamoParameterKind.TagReference),
            new("offColor", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement("#46535C")),
            new("runningColor", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement("#16A34A")),
            new("faultColor", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement("#EAB308")),
            new("communicationBadColor", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement("#DC2626")),
            new("inhibitedColor", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement("#1687C9")),
            new("animationEnabled", DynamoParameterKind.Boolean, DefaultValue: JsonSerializer.SerializeToElement(true)),
            new("fixedState", DynamoParameterKind.Number, DefaultValue: JsonSerializer.SerializeToElement(1))
        };
        var metadata = Metadata("indicator.lamp", shape);
        metadata["stateProfile"] = "0=off;1..4=user-configurable";
        metadata["implementedSourceModes"] = stateful ? "numeric-tag-state" : "fixed-artwork";
        metadata["outlineIndependent"] = "true";
        var mapped = x.LastOrDefault(e => e.Key == "lens") ?? x.LastOrDefault(e => e.Key == "green-lens");
        if (mapped is not null)
            x[x.IndexOf(mapped)] = WithStateMap(mapped, "{equipmentPath}.State", ["#46535C", "#16A34A", "#EAB308", "#DC2626", "#1687C9"], "state", "off,running,fault,communicationBad,inhibited");
        return Definition($"indicator.lamp.{shape}", $"Sinalizador {LampName(shape)}", "indicators", w, h, x, parameters, metadata);
    }

    private static DynamoEngineeringDto Button(string style, int index)
    {
        var x = new List<VisualElementEngineeringDto>();
        var guarded = style == "guarded";
        var square = style is "flush" or "illuminated";
        x.Add(Shape("panel", "core.rectangle", 10, 9, 90, 66, "#D6E0E6", Outline, 3, 8));
        if (guarded)
        {
            x.Add(Poly("guard-left", [(14, 52), (28, 20), (36, 58)], Steel));
            x.Add(Poly("guard-right", [(96, 52), (82, 20), (74, 58)], Steel));
        }
        var buttonType = square ? "core.rectangle" : "core.ellipse";
        x.Add(Shape("button", buttonType, 30, 20, 50, 44, style == "illuminated" ? "#1687C9" : "#7B8E9B", Outline, 3, square ? 5 : 1, raised: style is "raised" or "illuminated"));
        x.Add(Shape("button-face", buttonType, 36, 25, 38, 32, style == "illuminated" ? "#73C9ED" : "#C9D7DF", "#F8FAFC", 1, square ? 3 : 1));
        x.Add(Text("caption", "PB", 38, 33, 34, 14));
        var metadata = Metadata("operator.button", style);
        metadata["interactionContract"] = "canonical-authorized-visual-action";
        metadata["pressFeedback"] = "tag-state-mapped-released/pressed";
        var parameters = new[]
        {
            new DynamoParameterDefinitionEngineeringDto("equipmentPath", DynamoParameterKind.EquipmentPath),
            new DynamoParameterDefinitionEngineeringDto("targetTag", DynamoParameterKind.TagReference),
            new DynamoParameterDefinitionEngineeringDto("command", DynamoParameterKind.Command),
            new DynamoParameterDefinitionEngineeringDto("state", DynamoParameterKind.TagReference),
            new DynamoParameterDefinitionEngineeringDto("releasedColor", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement("#7B8E9B")),
            new DynamoParameterDefinitionEngineeringDto("pressedColor", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement("#1687C9")),
            new DynamoParameterDefinitionEngineeringDto("analogValue", DynamoParameterKind.Number),
            new DynamoParameterDefinitionEngineeringDto("booleanValue", DynamoParameterKind.Boolean, DefaultValue: JsonSerializer.SerializeToElement(true)),
            new DynamoParameterDefinitionEngineeringDto("animationEnabled", DynamoParameterKind.Boolean, DefaultValue: JsonSerializer.SerializeToElement(true)),
            new DynamoParameterDefinitionEngineeringDto("fixedState", DynamoParameterKind.Number, DefaultValue: JsonSerializer.SerializeToElement(0))
        };
        var buttonElement = x.Single(element => element.Key == "button");
        x[x.IndexOf(buttonElement)] = WithStateMap(buttonElement, "{equipmentPath}.State", ["#7B8E9B", "#1687C9"], "state", "released,pressed");
        var clickAction = index switch
        {
            0 => new VisualNavigationActionEngineeringDto("click", VisualNavigationActionKind.ExecuteCommand, CommandParameterKey: "command"),
            1 => new VisualNavigationActionEngineeringDto("click", VisualNavigationActionKind.SetTagValue, "{targetTag}",
                new Dictionary<string, JsonElement> { ["value"] = JsonSerializer.SerializeToElement("{analogValue}") }),
            2 => new VisualNavigationActionEngineeringDto("click", VisualNavigationActionKind.ToggleTagBoolean, "{targetTag}"),
            _ => new VisualNavigationActionEngineeringDto("click", VisualNavigationActionKind.SetTagValue, "{targetTag}",
                new Dictionary<string, JsonElement> { ["value"] = JsonSerializer.SerializeToElement("{booleanValue}") })
        };
        buttonElement = x.Single(element => element.Key == "button");
        x[x.IndexOf(buttonElement)] = buttonElement with { Actions = [clickAction] };
        return Definition($"operator.button.{style}", $"Botão {ButtonName(style)}", "controls", 110, 84, x, parameters, metadata);
    }

    private static DynamoEngineeringDto Equipment(string family, string variant, string name, int index)
    {
        var x = family == "motor" ? MotorArtwork(variant) : family == "valve" ? ValveArtwork(variant) : ContactArtwork(variant);
        var key = $"{family}.{variant}";
        var category = family switch { "motor" => "motors", "valve" => "valves", _ => "electrical-contacts" };
        var metadata = Metadata($"equipment.{family}", variant);
        metadata["analogProcessValues"] = "not-displayed";
        metadata["stateProfile"] = family == "valve" ? "0=closed;1=open;2=fault;3=communication-failure;4=inhibited" : "0=stopped;1=running;2=fault;3=communication-failure;4=inhibited";
        if (family is "motor" or "valve")
        {
            var target = x.FirstOrDefault(e => e.Key == "state-body");
            if (target is not null)
                x[x.IndexOf(target)] = WithStateMap(target, "{equipmentPath}.State",
                    ["#93B99A", "#16A34A", "#DC2626", "#EAB308", "#76838B"], "state",
                    family == "valve" ? "closed,open,fault,communicationBad,inhibited" : "stopped,running,fault,communicationBad,inhibited");
            x.AddRange(CreateStateLabels(family));
        }
        var parameters = new List<DynamoParameterDefinitionEngineeringDto>
        {
            new("equipmentPath", DynamoParameterKind.EquipmentPath),
            new("state", DynamoParameterKind.TagReference),
            new(family == "valve" ? "closedColor" : "stoppedColor", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement("#93B99A")),
            new(family == "valve" ? "openColor" : "runningColor", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement("#16A34A")),
            new("faultColor", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement("#DC2626")),
            new("communicationBadColor", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement("#EAB308")),
            new("inhibitedColor", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement("#76838B")),
            new("animationEnabled", DynamoParameterKind.Boolean, DefaultValue: JsonSerializer.SerializeToElement(true)),
            new("fixedState", DynamoParameterKind.Number, DefaultValue: JsonSerializer.SerializeToElement(0)),
            new(family == "valve" ? "closedText" : "stoppedText", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement(family == "valve" ? "FECHADA" : "PARADO")),
            new(family == "valve" ? "openText" : "runningText", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement(family == "valve" ? "ABERTA" : "LIGADO")),
            new("faultText", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement("FALHA")),
            new("communicationBadText", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement("SEM COMUNICAÇÃO")),
            new("inhibitedText", DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement("BLOQUEADO")),
            new("command", DynamoParameterKind.Command)
        };
        if (family is "motor" or "valve")
        {
            var bodyIndex = x.FindIndex(element => element.Key == "state-body");
            if (bodyIndex >= 0)
                x[bodyIndex] = x[bodyIndex] with
                {
                    Actions = [new VisualNavigationActionEngineeringDto("click", VisualNavigationActionKind.ExecuteCommand, CommandParameterKey: "command")]
                };
            metadata["stateSource"] = "numeric-tag-enum";
            metadata["statePriority"] = "one-exclusive-state-per-enum-value; invalid-or-missing-sample-uses-base-state";
        }
        return Definition(key, name, category, 132, 100, x, parameters, metadata);
    }

    private static IEnumerable<VisualElementEngineeringDto> CreateStateLabels(string family)
    {
        var labels = family == "valve"
            ? new[] { "closedText", "openText", "faultText", "communicationBadText", "inhibitedText" }
            : new[] { "stoppedText", "runningText", "faultText", "communicationBadText", "inhibitedText" };
        for (var state = 0; state < labels.Length; state++)
        {
            var key = $"state-label-{state}";
            var text = Text(key, "", 0, 82, 132, 16, 10);
            var properties = new Dictionary<string, JsonElement>(text.Properties!, StringComparer.Ordinal)
            {
                ["text"] = JsonSerializer.SerializeToElement("{" + labels[state] + "}"),
                ["visible"] = JsonSerializer.SerializeToElement(false)
            };
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dynamoStateLabelIndex"] = state.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["dynamoFixedStateParameter"] = "fixedState",
                ["dynamoAnimationEnabledParameter"] = "animationEnabled"
            };
            yield return text with
            {
                Properties = properties,
                Metadata = metadata,
                BooleanConditions = [new VisualBooleanConditionEngineeringDto(
                    "visible", VisualBooleanConditionKind.NumericInterval,
                    new VisualValueSourceEngineeringDto(VisualValueSourceKind.Tag, VisualExpressionValueType.Number,
                        Target: "{dynamoParameter:state}"),
                    Minimum: state, Maximum: state + 1)]
            };
        }
    }

    private static List<VisualElementEngineeringDto> MotorArtwork(string kind)
    {
        var x = new List<VisualElementEngineeringDto>();
        if (kind == "vertical")
        {
            x.Add(Shape("base", "core.rectangle", 36, 72, 60, 9, Dark, Outline, 1));
            x.Add(Shape("state-body", "core.rectangle", 45, 20, 42, 55, "#93B99A", Outline, 2, 12));
            x.Add(Shape("terminal", "core.rectangle", 54, 10, 24, 14, Steel, Outline, 1, 2));
        }
        else
        {
            x.Add(Shape("foot", "core.rectangle", 30, 68, 70, 8, Dark, Outline, 1));
            x.Add(Shape("state-body", "core.rectangle", 26, 24, 76, 46, "#93B99A", Outline, 2, kind == "large-frame" ? 5 : 18, raised: kind == "tefc"));
            x.Add(Shape("end-left", "core.ellipse", 19, 29, 18, 36, Steel, Outline, 2));
            x.Add(Shape("end-right", "core.ellipse", 91, 29, 18, 36, Steel, Outline, 2));
            x.Add(Shape("shaft", "core.rectangle", 106, 42, 19, 9, Light, Outline, 1));
            if (kind is "finned" or "large-frame")
                for (var i = 0; i < 5; i++) x.Add(Shape($"fin-{i}", "core.rectangle", 38 + i * 10, 29, 2, 36, Dark, Dark, 0));
            if (kind == "vfd-package")
            {
                x.Add(Shape("drive", "core.rectangle", 88, 5, 34, 24, Light, Outline, 2, 3));
                x.Add(Text("drive-label", "VFD", 91, 11, 28, 12));
            }
        }
        x.Add(Text("equipment-label", "M", 49, 40, 28, 18));
        return x;
    }

    private static List<VisualElementEngineeringDto> ValveArtwork(string kind)
    {
        var x = new List<VisualElementEngineeringDto>();
        x.Add(Shape("pipe-left", "core.rectangle", 6, 45, 32, 10, Steel, Outline, 2));
        x.Add(Shape("pipe-right", "core.rectangle", 94, 45, 32, 10, Steel, Outline, 2));
        x.Add(Shape("state-body", "core.rectangle", 34, 34, 64, 32, "#93B99A", Outline, 2, kind == "ball" ? 16 : 3));
        switch (kind)
        {
            case "gate":
                x.Add(Shape("stem", "core.rectangle", 63, 14, 6, 24, Dark, Outline, 1));
                x.Add(Shape("wheel", "core.ellipse", 50, 4, 32, 16, Light, Outline, 2)); break;
            case "globe":
                x.Add(Poly("body-left", [(38, 36), (63, 50), (38, 64)], Light));
                x.Add(Poly("body-right", [(94, 36), (69, 50), (94, 64)], Light));
                x.Add(Shape("stem", "core.rectangle", 63, 15, 6, 23, Dark, Outline, 1)); break;
            case "ball":
                x.Add(Shape("ball", "core.ellipse", 49, 40, 34, 20, Light, Outline, 2));
                x.Add(Shape("bore", "core.rectangle", 58, 47, 17, 6, "#1687C9", Outline, 1)); break;
            case "butterfly":
                x.Add(Shape("disc", "core.rectangle", 61, 36, 8, 28, Light, Outline, 2, rotation: -18));
                x.Add(Shape("stem", "core.rectangle", 63, 13, 5, 23, Dark, Outline, 1)); break;
            case "diaphragm":
                x.Add(Poly("diaphragm", [(38, 37), (66, 48), (94, 37), (94, 63), (66, 52), (38, 63)], Light));
                x.Add(Shape("actuator", "core.rectangle", 53, 14, 26, 19, Steel, Outline, 2, 4)); break;
            default:
                x.Add(Poly("body-left", [(38, 35), (66, 50), (38, 65)], Light));
                x.Add(Poly("body-right", [(94, 35), (66, 50), (94, 65)], Light));
                x.Add(Shape("actuator", "core.rectangle", 53, 10, 26, 22, Steel, Outline, 2, 4)); break;
        }
        x.Add(Text("equipment-label", "V", 55, 43, 22, 14));
        return x;
    }

    private static List<VisualElementEngineeringDto> ContactArtwork(string kind)
    {
        var vertical = kind.EndsWith("vertical", StringComparison.Ordinal);
        var triple = kind.Contains("tri", StringComparison.Ordinal);
        var x = new List<VisualElementEngineeringDto>();
        var poles = triple ? 3 : 1;
        for (var pole = 0; pole < poles; pole++)
        {
            var offset = triple ? pole * 30d : 0d;
            if (vertical)
            {
                x.Add(Shape($"contact-{pole}-fixed", "core.rectangle", 50 + offset, 5, 8, 28, Steel, Outline, 1));
                x.Add(Shape($"contact-{pole}-moving", "core.rectangle", 49 + offset, 39, 10, 40, Dark, Outline, 1, rotation: pole % 2 == 0 ? 16 : -16));
            }
            else
            {
                x.Add(Shape($"contact-{pole}-fixed", "core.rectangle", 8, 27 + offset, 37, 8, Steel, Outline, 1));
                x.Add(Shape($"contact-{pole}-moving", "core.rectangle", 53, 26 + offset, 57, 10, Dark, Outline, 1, rotation: pole % 2 == 0 ? -16 : 16));
            }
        }
        x.Add(Text("equipment-label", triple ? "3~" : "1~", 44, 78, 44, 14, 10));
        return x;
    }

    private static VisualElementEngineeringDto WithStateMap(VisualElementEngineeringDto element, string target, string[] colors, string parameter, string profile)
    {
        var map = new VisualPropertyMapEngineeringDto("fillColor",
            new(VisualValueSourceKind.Tag, VisualExpressionValueType.Number, Target: target),
            colors.Select((color, state) => new VisualPropertyMapRuleEngineeringDto(JsonSerializer.SerializeToElement(color), Minimum: state, Maximum: state + 1)).ToArray(),
            JsonSerializer.SerializeToElement(colors[0]));
        var metadata = element.Metadata is null ? new Dictionary<string, string>() : new(element.Metadata);
        metadata["dynamoStateColorParameter"] = parameter;
        metadata["dynamoStateColorProfile"] = profile;
        metadata["dynamoAnimationEnabledParameter"] = "animationEnabled";
        metadata["dynamoFixedStateParameter"] = "fixedState";
        return element with { PropertyMaps = [.. element.PropertyMaps ?? [], map], Metadata = metadata };
    }

    private static DynamoEngineeringDto Definition(string key, string name, string category, int width, int height,
        List<VisualElementEngineeringDto> elements, IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto> parameters,
        Dictionary<string, string> metadata)
    {
        var idBytes = MD5.HashData(Encoding.UTF8.GetBytes("elitescada:" + key));
        elements = elements.Select(element => element with { Id = StableElementId(key + ":" + element.Key) }).ToList();
        return new(new Guid(idBytes), key, name, Properties: new Dictionary<string, string>
        {
            ["category"] = category, ["defaultWidth"] = width.ToString(), ["defaultHeight"] = height.ToString(),
            ["libraryVersion"] = Version, ["visualStyle"] = "svg-composed", ["visualFinish"] = "industrial-vector-v1",
            ["catalogGeneration"] = "1", ["catalogStatus"] = "active"
        }, Context: new Dictionary<string, string> { ["usage"] = "process-screen", ["view"] = "front-orthographic" },
            Metadata: metadata, Parameters: parameters, Elements: elements);
    }

    private static Dictionary<string, string> Metadata(string family, string variant) => new(StringComparer.Ordinal)
    {
        ["builtinLibrary"] = "true", ["assetOrigin"] = "original-elitescada-vector", ["catalogGeneration"] = "1",
        ["catalogStatus"] = "active", ["libraryVersion"] = Version, ["familyKey"] = family,
        ["visualVariant"] = variant, ["visualReferencePolicy"] = "editable-original-vector; no third-party art embedded"
    };

    private static VisualElementEngineeringDto Shape(string key, string type, double x, double y, double w, double h,
        string fill, string stroke, double sw, double radius = 0, double rotation = 0, bool raised = false)
    {
        var props = new Dictionary<string, JsonElement>
        {
            ["x"] = JsonSerializer.SerializeToElement(x), ["y"] = JsonSerializer.SerializeToElement(y),
            ["width"] = JsonSerializer.SerializeToElement(w), ["height"] = JsonSerializer.SerializeToElement(h),
            ["fillStyle"] = JsonSerializer.SerializeToElement(raised ? "gradient" : "solid"),
            ["fillColor"] = JsonSerializer.SerializeToElement(fill), ["strokeColor"] = JsonSerializer.SerializeToElement(stroke),
            ["strokeWidth"] = JsonSerializer.SerializeToElement(sw), ["rotation"] = JsonSerializer.SerializeToElement(rotation)
        };
        if (type == "core.rectangle") props["cornerRadius"] = JsonSerializer.SerializeToElement(radius);
        if (raised) { props["fillSecondaryColor"] = JsonSerializer.SerializeToElement("#F8FAFC"); props["gradientDirection"] = JsonSerializer.SerializeToElement("vertical"); }
        return new(key, type, Properties: props, Id: StableElementId(key));
    }

    private static VisualElementEngineeringDto Poly(string key, (double X, double Y)[] points, string fill)
    {
        var left = points.Min(point => point.X);
        var top = points.Min(point => point.Y);
        var right = points.Max(point => point.X);
        var bottom = points.Max(point => point.Y);
        return new(key, "core.polygon", Properties: new Dictionary<string, JsonElement>
        {
            ["x"] = JsonSerializer.SerializeToElement(left), ["y"] = JsonSerializer.SerializeToElement(top),
            ["width"] = JsonSerializer.SerializeToElement(right - left), ["height"] = JsonSerializer.SerializeToElement(bottom - top),
            ["points"] = JsonSerializer.SerializeToElement(points.Select(p => new { x = p.X - left, y = p.Y - top })),
            ["fillColor"] = JsonSerializer.SerializeToElement(fill), ["strokeColor"] = JsonSerializer.SerializeToElement(Outline),
            ["strokeWidth"] = JsonSerializer.SerializeToElement(2d)
        }, Id: StableElementId(key));
    }

    private static VisualElementEngineeringDto Text(string key, string text, double x, double y, double w, double h, double size = 12) =>
        new(key, "core.text", Properties: new Dictionary<string, JsonElement>
        {
            ["x"] = JsonSerializer.SerializeToElement(x), ["y"] = JsonSerializer.SerializeToElement(y),
            ["width"] = JsonSerializer.SerializeToElement(w), ["height"] = JsonSerializer.SerializeToElement(h),
            ["text"] = JsonSerializer.SerializeToElement(text), ["fontSize"] = JsonSerializer.SerializeToElement(size),
            ["fontWeight"] = JsonSerializer.SerializeToElement(700), ["horizontalAlignment"] = JsonSerializer.SerializeToElement("center"),
            ["verticalAlignment"] = JsonSerializer.SerializeToElement("middle"), ["textColor"] = JsonSerializer.SerializeToElement("#14212B")
        }, Id: StableElementId(key));

    private static Guid StableElementId(string key) => new(MD5.HashData(Encoding.UTF8.GetBytes("elitescada-element:" + key)));
    private static string LampName(string value) => value switch { "round" => "redondo", "square" => "quadrado", "rectangular" => "retangular", _ => "torre" };
    private static string ButtonName(string value) => value switch { "raised" => "elevado", "flush" => "faceado", "guarded" => "protegido", _ => "iluminado" };
    private static string MotorName(string value) => value switch { "tefc" => "fechado", "vfd-package" => "com inversor", "foot-mounted" => "com pés", "large-frame" => "carcaça grande", _ => value };
    private static string ValveName(string value) => value switch { "ball" => "esfera", "gate" => "gaveta", "globe" => "globo", "butterfly" => "borboleta", "diaphragm" => "diafragma", _ => "de controle" };
    private static string ContactName(string value) => value.Replace("contact-", "").Replace("isolator-", "seccionadora ").Replace("mono", "monofilar").Replace("tri", "trifilar").Replace("horizontal", "horizontal").Replace("vertical", "vertical");
}
