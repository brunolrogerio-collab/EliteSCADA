using System.Text.Json;
using Scada.Engineering.Contracts;

namespace Scada.Api.Runtime;

/// <summary>
/// Small original process-symbol library shipped as canonical Dynamo Engineering.
/// These are reusable definitions, not renderer-private SVG/image assets.
/// </summary>
public static class BuiltinDynamoLibrary
{
    public static IReadOnlyCollection<DynamoEngineeringDto> Create() =>
    [
        Dynamo(1, "dynamo.pump.standard", "Bomba centrífuga", "pump", 132, 92,
        [
            Shape(1, "body", "core.ellipse", 20, 12, 68, 68, "#D1D5DB", "#374151", 3),
            Shape(2, "outlet", "core.rectangle", 78, 37, 42, 18, "#D1D5DB", "#374151", 3),
            Text(3, "label", "P", 42, 31, 24, 24),
            StateLamp(4, "running", 4, 4, "#22C55E", "running", "{equipmentPath}.Running"),
            StateLamp(5, "fault", 104, 4, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        "pump.standard",
        Parameters(
            EquipmentPathParameter(),
            TagParameter("running"),
            TagParameter("fault"),
            CommandKeyParameter("startCommandKey"),
            CommandKeyParameter("stopCommandKey"))),

        Dynamo(2, "process.pump.submersible", "Bomba submersível", "pump", 94, 132,
        [
            Shape(10, "body", "core.rectangle", 20, 22, 54, 88, "#CBD5E1", "#334155", 3, 10),
            Shape(11, "intake", "core.ellipse", 25, 90, 44, 26, "#94A3B8", "#334155", 2),
            Text(12, "label", "BS", 34, 50, 28, 24),
            StateLamp(13, "running", 6, 5, "#22C55E", "running", "{equipmentPath}.Running"),
            StateLamp(14, "fault", 70, 5, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: Parameters(
            EquipmentPathParameter(),
            TagParameter("running"),
            TagParameter("fault"),
            CommandKeyParameter("startCommandKey"),
            CommandKeyParameter("stopCommandKey"))),

        Dynamo(3, "process.motor.standard", "Motor padrão", "motor", 106, 92,
        [
            Shape(20, "body", "core.ellipse", 17, 10, 72, 72, "#D1D5DB", "#374151", 3),
            Text(21, "label", "M", 39, 32, 28, 28),
            StateLamp(22, "running", 4, 4, "#22C55E", "running", "{equipmentPath}.Running"),
            StateLamp(23, "fault", 82, 4, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: Parameters(
            EquipmentPathParameter(),
            TagParameter("running"),
            TagParameter("fault"),
            CommandKeyParameter("startCommandKey"),
            CommandKeyParameter("stopCommandKey"))),

        Dynamo(4, "process.motor.vfd", "Motor com inversor", "motor", 138, 96,
        [
            Shape(30, "motor", "core.ellipse", 10, 12, 70, 70, "#D1D5DB", "#374151", 3),
            Text(31, "motor-label", "M", 31, 33, 28, 28),
            Shape(32, "vfd", "core.rectangle", 88, 19, 42, 56, "#E2E8F0", "#475569", 2, 4),
            Text(33, "vfd-label", "VFD", 93, 36, 32, 20, 10),
            StateLamp(34, "running", 4, 4, "#22C55E", "running", "{equipmentPath}.Running"),
            StateLamp(35, "fault", 114, 4, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: Parameters(
            EquipmentPathParameter(),
            TagParameter("running"),
            TagParameter("fault"),
            TagParameter("processValue"),
            TagParameter("setpoint"),
            TagParameter("feedback"),
            CommandKeyParameter("startCommandKey"),
            CommandKeyParameter("stopCommandKey"))),

        Dynamo(5, "process.valve.onoff", "Válvula abre/fecha", "valve", 128, 92,
        [
            Shape(40, "left", "core.rectangle", 29, 31, 38, 38, "#CBD5E1", "#334155", 2, rotation: 45),
            Shape(41, "right", "core.rectangle", 60, 31, 38, 38, "#CBD5E1", "#334155", 2, rotation: 45),
            Shape(42, "actuator", "core.rectangle", 49, 5, 30, 24, "#94A3B8", "#334155", 2, 4),
            StateLamp(43, "open", 5, 5, "#22C55E", "open", "{equipmentPath}.Open"),
            StateLamp(44, "fault", 104, 5, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: Parameters(
            EquipmentPathParameter(),
            TagParameter("open"),
            TagParameter("closed"),
            TagParameter("fault"),
            CommandKeyParameter("openCommandKey"),
            CommandKeyParameter("closeCommandKey"))),

        Dynamo(6, "process.valve.control", "Válvula de controle", "valve", 128, 108,
        [
            Shape(50, "left", "core.rectangle", 29, 47, 38, 38, "#CBD5E1", "#334155", 2, rotation: 45),
            Shape(51, "right", "core.rectangle", 60, 47, 38, 38, "#CBD5E1", "#334155", 2, rotation: 45),
            Shape(52, "stem", "core.rectangle", 61, 29, 5, 20, "#475569", "#475569", 0),
            Shape(53, "actuator", "core.ellipse", 43, 4, 42, 28, "#94A3B8", "#334155", 2),
            Text(54, "label", "%", 51, 8, 26, 20, 10),
            StateLamp(55, "fault", 104, 5, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: Parameters(
            EquipmentPathParameter(),
            TagParameter("processValue"),
            TagParameter("setpoint"),
            TagParameter("feedback"),
            TagParameter("fault"),
            CommandKeyParameter("commandKey"))),

        Dynamo(7, "process.tank.vertical", "Tanque vertical", "tank", 108, 158,
        [
            Shape(60, "vessel", "core.rectangle", 18, 8, 72, 140, "#E2E8F0", "#475569", 3, 18),
            Shape(61, "liquid", "core.rectangle", 23, 74, 62, 68, "#7DD3FC", "#0284C7", 1, 12),
            Text(62, "label", "TK", 39, 30, 30, 24),
            StateLamp(63, "high", 84, 10, "#F59E0B", "high", "{equipmentPath}.High"),
            StateLamp(64, "fault", 84, 132, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: Parameters(
            EquipmentPathParameter(),
            TagParameter("processValue"),
            TagParameter("high"),
            TagParameter("fault"))),

        Dynamo(8, "process.tank.horizontal", "Tanque horizontal", "tank", 168, 100,
        [
            Shape(70, "vessel", "core.rectangle", 18, 18, 132, 66, "#E2E8F0", "#475569", 3, 30),
            Shape(71, "liquid", "core.rectangle", 24, 48, 120, 30, "#7DD3FC", "#0284C7", 1, 15),
            Text(72, "label", "TK", 68, 28, 32, 24),
            StateLamp(73, "high", 144, 10, "#F59E0B", "high", "{equipmentPath}.High"),
            StateLamp(74, "fault", 144, 74, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: Parameters(
            EquipmentPathParameter(),
            TagParameter("processValue"),
            TagParameter("high"),
            TagParameter("fault")))
    ];

    private static DynamoEngineeringDto Dynamo(
        int sequence,
        string key,
        string name,
        string category,
        int width,
        int height,
        IReadOnlyCollection<VisualElementEngineeringDto> elements,
        string? templateKey = null,
        IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto>? parameters = null) =>
        new(
            DefinitionId(sequence),
            key,
            name,
            templateKey,
            Properties: new Dictionary<string, string>
            {
                ["category"] = category,
                ["defaultWidth"] = width.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["defaultHeight"] = height.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["libraryVersion"] = "1.0.0"
            },
            Context: new Dictionary<string, string> { ["usage"] = "process-screen" },
            Metadata: new Dictionary<string, string>
            {
                ["builtinLibrary"] = "true",
                ["equipmentPathBinding"] = "{equipmentPath}",
                ["publicInterfaceVersion"] = "1",
                ["stateModelVersion"] = "1"
            },
            Parameters: parameters,
            Elements: elements);

    private static VisualElementEngineeringDto Shape(
        int sequence,
        string key,
        string type,
        double x,
        double y,
        double width,
        double height,
        string fill,
        string stroke,
        double strokeWidth,
        double cornerRadius = 0,
        double rotation = 0)
    {
        var properties = Properties(
            ("x", x), ("y", y), ("width", width), ("height", height),
            ("fillColor", fill), ("strokeColor", stroke), ("strokeWidth", strokeWidth),
            ("rotation", rotation));
        if (type.Equals("core.rectangle", StringComparison.Ordinal))
        {
            properties["cornerRadius"] = JsonSerializer.SerializeToElement(cornerRadius);
        }

        return new(
            key,
            type,
            Properties: properties,
            Id: ElementId(sequence));
    }

    private static VisualElementEngineeringDto Text(
        int sequence,
        string key,
        string text,
        double x,
        double y,
        double width,
        double height,
        double fontSize = 16) =>
        new(
            key,
            "core.text",
            Properties: Properties(
                ("x", x), ("y", y), ("width", width), ("height", height),
                ("text", text), ("fontSize", fontSize), ("fontWeight", 700),
                ("horizontalAlignment", "center"), ("verticalAlignment", "middle"),
                ("textColor", "#1F2937")),
            Id: ElementId(sequence));

    private static VisualElementEngineeringDto StateLamp(
        int sequence,
        string key,
        double x,
        double y,
        string color,
        string parameterKey,
        string target) =>
        new(
            key,
            "core.ellipse",
            Bindings:
            [
                new EngineeringBindingDto(
                    "visible",
                    EngineeringBindingKind.Tag,
                    target,
                    "read",
                    Metadata: new Dictionary<string, string>
                    {
                        ["dynamoContext"] = "equipmentPath",
                        ["dynamoParameter"] = parameterKey
                    })
            ],
            Properties: Properties(
                ("x", x), ("y", y), ("width", 18), ("height", 18),
                ("fillColor", color), ("strokeColor", "#111827"), ("strokeWidth", 1),
                ("visible", false)),
            Id: ElementId(sequence));

    private static IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto> Parameters(
        params DynamoParameterDefinitionEngineeringDto[] parameters) => parameters;

    private static DynamoParameterDefinitionEngineeringDto EquipmentPathParameter() =>
        new("equipmentPath", DynamoParameterKind.EquipmentPath);

    private static DynamoParameterDefinitionEngineeringDto TagParameter(string key) =>
        new(key, DynamoParameterKind.TagReference);

    private static DynamoParameterDefinitionEngineeringDto CommandKeyParameter(string key) =>
        new(key, DynamoParameterKind.String);

    private static Dictionary<string, JsonElement> Properties(params (string Key, object Value)[] values) =>
        values.ToDictionary(
            pair => pair.Key,
            pair => JsonSerializer.SerializeToElement(pair.Value),
            StringComparer.Ordinal);

    private static Guid DefinitionId(int sequence) =>
        Guid.Parse($"43000000-0000-0000-0000-{sequence:000000000000}");

    private static Guid ElementId(int sequence) =>
        Guid.Parse($"43100000-0000-0000-0000-{sequence:000000000000}");
}
