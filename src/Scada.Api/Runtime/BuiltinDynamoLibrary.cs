using System.Text.Json;
using Scada.Engineering.Contracts;

namespace Scada.Api.Runtime;

/// <summary>
/// Original EliteSCADA industrial symbol library rendered exclusively through the
/// canonical visual-object model. Every equipment family ships in three visual
/// styles without introducing image-only assets or a second renderer:
/// detailed 2D, front-facing dimensional (gradient/shadow) and high-performance HMI.
/// </summary>
public static class BuiltinDynamoLibrary
{
    private enum VisualStyle
    {
        Detailed2D,
        DimensionalFront,
        HighPerformance
    }

    private static readonly VisualStyle[] Styles =
    [
        VisualStyle.Detailed2D,
        VisualStyle.DimensionalFront,
        VisualStyle.HighPerformance
    ];

    public static IReadOnlyCollection<DynamoEngineeringDto> Create()
    {
        var definitions = new List<DynamoEngineeringDto>(30);
        foreach (var style in Styles)
        {
            definitions.Add(CentrifugalPump(style));
            definitions.Add(SubmersiblePump(style));
            definitions.Add(StandardMotor(style));
            definitions.Add(MotorWithVfd(style));
            definitions.Add(OnOffValve(style));
            definitions.Add(ControlValve(style));
            definitions.Add(VerticalTank(style));
            definitions.Add(HorizontalTank(style));
            definitions.Add(CentrifugalBlower(style));
            definitions.Add(ProcessIndicator(style));
        }

        return definitions;
    }

    private static DynamoEngineeringDto CentrifugalPump(VisualStyle style)
    {
        const int family = 1;
        var sequence = DefinitionSequence(family, style);
        if (style == VisualStyle.HighPerformance)
        {
            return Dynamo(sequence, "dynamo.pump.standard", "Bomba centrífuga", "pump", style, 132, 92,
            [
                FlatShape(E(family, style, 1), "suction", "core.rectangle", 4, 38, 30, 16, "#A7B0B7", "#374151", 2, 3),
                FlatShape(E(family, style, 2), "casing", "core.ellipse", 27, 16, 64, 64, "#C5CDD3", "#374151", 2),
                FlatShape(E(family, style, 3), "impeller", "core.ellipse", 46, 35, 26, 26, "#F3F4F6", "#4B5563", 2),
                FlatShape(E(family, style, 4), "discharge", "core.rectangle", 78, 30, 46, 16, "#A7B0B7", "#374151", 2, 3),
                FlatShape(E(family, style, 5), "base", "core.rectangle", 27, 78, 72, 7, "#6B7280", "#374151", 1, 2),
                Text(E(family, style, 6), "label", "P", 47, 38, 22, 20, 13, "#111827"),
                StateLamp(E(family, style, 7), "running", 5, 5, "#16A34A", "running", "{equipmentPath}.Running"),
                StateLamp(E(family, style, 8), "fault", 107, 5, "#DC2626", "fault", "{equipmentPath}.Fault")
            ],
            parameters: PumpParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "dynamo.pump.standard", "Bomba centrífuga", "pump", style, 160, 110,
        [
            MaterialShape(E(family, style, 1), "suction-pipe", "core.rectangle", 1, 47, 37, 18, "#B8C4CF", "#F8FAFC", "#334155", 2, 4, dimensional, "vertical"),
            MaterialShape(E(family, style, 2), "suction-flange", "core.ellipse", 4, 40, 16, 32, "#AAB8C5", "#F8FAFC", "#334155", 2, 0, dimensional, "horizontal"),
            MaterialShape(E(family, style, 3), "casing", "core.ellipse", 34, 18, 82, 82, "#B8C4CF", "#F8FAFC", "#334155", 3, 0, dimensional, "diagonal-down", dimensional),
            MaterialShape(E(family, style, 4), "casing-inner", "core.ellipse", 48, 32, 54, 54, "#DDE4EA", "#FFFFFF", "#64748B", 2, 0, dimensional, "diagonal-down"),
            MaterialShape(E(family, style, 5), "impeller", "core.ellipse", 64, 48, 22, 22, "#64748B", "#CBD5E1", "#334155", 2, 0, dimensional, "diagonal-up"),
            MaterialShape(E(family, style, 6), "discharge-neck", "core.rectangle", 99, 15, 23, 38, "#B8C4CF", "#F8FAFC", "#334155", 2, 5, dimensional, "horizontal"),
            MaterialShape(E(family, style, 7), "discharge-pipe", "core.rectangle", 110, 8, 39, 18, "#B8C4CF", "#F8FAFC", "#334155", 2, 4, dimensional, "vertical"),
            MaterialShape(E(family, style, 8), "discharge-flange", "core.ellipse", 142, 5, 14, 24, "#AAB8C5", "#F8FAFC", "#334155", 2, 0, dimensional, "horizontal"),
            FlatShape(E(family, style, 9), "foot-left", "core.rectangle", 47, 91, 16, 8, "#64748B", "#334155", 1, 2),
            FlatShape(E(family, style, 10), "foot-right", "core.rectangle", 90, 91, 16, 8, "#64748B", "#334155", 1, 2),
            FlatShape(E(family, style, 11), "base", "core.rectangle", 34, 98, 85, 8, "#475569", "#334155", 1, 2),
            Text(E(family, style, 12), "label", "P", 64, 50, 22, 18, 11, "#1F2937"),
            StateLamp(E(family, style, 13), "running", 5, 5, "#22C55E", "running", "{equipmentPath}.Running"),
            StateLamp(E(family, style, 14), "fault", 137, 5, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: PumpParameters());
    }

    private static DynamoEngineeringDto SubmersiblePump(VisualStyle style)
    {
        const int family = 2;
        var sequence = DefinitionSequence(family, style);
        if (style == VisualStyle.HighPerformance)
        {
            return Dynamo(sequence, "process.pump.submersible", "Bomba submersível", "pump", style, 94, 132,
            [
                FlatShape(E(family, style, 1), "body", "core.rectangle", 22, 22, 50, 86, "#C5CDD3", "#374151", 2, 10),
                FlatShape(E(family, style, 2), "intake", "core.ellipse", 27, 88, 40, 25, "#9CA3AF", "#374151", 2),
                FlatShape(E(family, style, 3), "outlet", "core.rectangle", 69, 28, 20, 14, "#A7B0B7", "#374151", 2, 3),
                Text(E(family, style, 4), "label", "BS", 33, 51, 28, 22, 11, "#111827"),
                StateLamp(E(family, style, 5), "running", 6, 5, "#16A34A", "running", "{equipmentPath}.Running"),
                StateLamp(E(family, style, 6), "fault", 70, 5, "#DC2626", "fault", "{equipmentPath}.Fault")
            ],
            parameters: PumpParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.pump.submersible", "Bomba submersível", "pump", style, 112, 160,
        [
            MaterialShape(E(family, style, 1), "body", "core.rectangle", 29, 30, 54, 100, "#AEBCC8", "#F8FAFC", "#334155", 3, 13, dimensional, "horizontal", dimensional),
            MaterialShape(E(family, style, 2), "top-cap", "core.rectangle", 34, 22, 44, 18, "#CBD5E1", "#FFFFFF", "#334155", 2, 8, dimensional, "vertical"),
            FlatShape(E(family, style, 3), "cable-gland", "core.rectangle", 43, 10, 12, 15, "#64748B", "#334155", 2, 3),
            FlatShape(E(family, style, 4), "cable", "core.rectangle", 16, 6, 7, 58, "#374151", "#111827", 1, 3, -18),
            MaterialShape(E(family, style, 5), "outlet-neck", "core.rectangle", 78, 31, 16, 27, "#B8C4CF", "#F8FAFC", "#334155", 2, 4, dimensional, "horizontal"),
            MaterialShape(E(family, style, 6), "outlet", "core.rectangle", 91, 28, 18, 16, "#B8C4CF", "#F8FAFC", "#334155", 2, 3, dimensional, "vertical"),
            MaterialShape(E(family, style, 7), "intake", "core.ellipse", 34, 115, 44, 28, "#94A3B8", "#DDE4EA", "#334155", 2, 0, dimensional, "vertical"),
            FlatShape(E(family, style, 8), "grille-1", "core.rectangle", 39, 126, 4, 13, "#475569", "#334155", 0, 1),
            FlatShape(E(family, style, 9), "grille-2", "core.rectangle", 48, 126, 4, 14, "#475569", "#334155", 0, 1),
            FlatShape(E(family, style, 10), "grille-3", "core.rectangle", 57, 126, 4, 14, "#475569", "#334155", 0, 1),
            FlatShape(E(family, style, 11), "grille-4", "core.rectangle", 66, 126, 4, 13, "#475569", "#334155", 0, 1),
            Text(E(family, style, 12), "label", "BS", 42, 70, 28, 22, 11, "#1F2937"),
            StateLamp(E(family, style, 13), "running", 6, 5, "#22C55E", "running", "{equipmentPath}.Running"),
            StateLamp(E(family, style, 14), "fault", 88, 5, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: PumpParameters());
    }

    private static DynamoEngineeringDto StandardMotor(VisualStyle style)
    {
        const int family = 3;
        var sequence = DefinitionSequence(family, style);
        if (style == VisualStyle.HighPerformance)
        {
            return Dynamo(sequence, "process.motor.standard", "Motor padrão", "motor", style, 106, 92,
            [
                FlatShape(E(family, style, 1), "body", "core.rectangle", 17, 17, 69, 58, "#C5CDD3", "#374151", 2, 22),
                FlatShape(E(family, style, 2), "shaft", "core.rectangle", 83, 39, 20, 10, "#9CA3AF", "#374151", 1, 2),
                FlatShape(E(family, style, 3), "terminal", "core.rectangle", 38, 8, 28, 15, "#D1D5DB", "#374151", 1, 3),
                FlatShape(E(family, style, 4), "base", "core.rectangle", 23, 75, 60, 8, "#6B7280", "#374151", 1, 2),
                Text(E(family, style, 5), "label", "M", 38, 33, 28, 25, 13, "#111827"),
                StateLamp(E(family, style, 6), "running", 4, 4, "#16A34A", "running", "{equipmentPath}.Running"),
                StateLamp(E(family, style, 7), "fault", 82, 4, "#DC2626", "fault", "{equipmentPath}.Fault")
            ],
            parameters: MotorParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.motor.standard", "Motor padrão", "motor", style, 150, 102,
        [
            MaterialShape(E(family, style, 1), "body", "core.rectangle", 28, 23, 86, 58, "#AEBCC8", "#F8FAFC", "#334155", 3, 22, dimensional, "vertical", dimensional),
            MaterialShape(E(family, style, 2), "end-bell-left", "core.ellipse", 20, 27, 24, 50, "#94A3B8", "#DDE4EA", "#334155", 2, 0, dimensional, "horizontal"),
            MaterialShape(E(family, style, 3), "end-bell-right", "core.ellipse", 101, 27, 24, 50, "#94A3B8", "#DDE4EA", "#334155", 2, 0, dimensional, "horizontal"),
            FlatShape(E(family, style, 4), "shaft", "core.rectangle", 118, 45, 27, 11, "#94A3B8", "#475569", 1, 2),
            MaterialShape(E(family, style, 5), "terminal", "core.rectangle", 54, 9, 35, 22, "#CBD5E1", "#F8FAFC", "#334155", 2, 4, dimensional, "vertical"),
            FlatShape(E(family, style, 6), "fin-1", "core.rectangle", 42, 31, 2, 42, "#64748B", "#64748B", 0),
            FlatShape(E(family, style, 7), "fin-2", "core.rectangle", 51, 29, 2, 46, "#64748B", "#64748B", 0),
            FlatShape(E(family, style, 8), "fin-3", "core.rectangle", 60, 29, 2, 46, "#64748B", "#64748B", 0),
            FlatShape(E(family, style, 9), "fin-4", "core.rectangle", 69, 29, 2, 46, "#64748B", "#64748B", 0),
            FlatShape(E(family, style, 10), "fin-5", "core.rectangle", 78, 29, 2, 46, "#64748B", "#64748B", 0),
            FlatShape(E(family, style, 11), "fin-6", "core.rectangle", 87, 29, 2, 46, "#64748B", "#64748B", 0),
            FlatShape(E(family, style, 12), "foot-left", "core.rectangle", 38, 76, 20, 11, "#64748B", "#334155", 1, 2),
            FlatShape(E(family, style, 13), "foot-right", "core.rectangle", 88, 76, 20, 11, "#64748B", "#334155", 1, 2),
            FlatShape(E(family, style, 14), "base", "core.rectangle", 31, 87, 84, 8, "#475569", "#334155", 1, 2),
            Text(E(family, style, 15), "label", "M", 61, 41, 24, 22, 12, "#1F2937"),
            StateLamp(E(family, style, 16), "running", 5, 4, "#22C55E", "running", "{equipmentPath}.Running"),
            StateLamp(E(family, style, 17), "fault", 126, 4, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: MotorParameters());
    }

    private static DynamoEngineeringDto MotorWithVfd(VisualStyle style)
    {
        const int family = 4;
        var sequence = DefinitionSequence(family, style);
        if (style == VisualStyle.HighPerformance)
        {
            return Dynamo(sequence, "process.motor.vfd", "Motor com inversor", "motor", style, 138, 96,
            [
                FlatShape(E(family, style, 1), "motor", "core.ellipse", 8, 15, 66, 66, "#C5CDD3", "#374151", 2),
                FlatShape(E(family, style, 2), "shaft", "core.rectangle", 69, 43, 18, 8, "#9CA3AF", "#374151", 1, 2),
                FlatShape(E(family, style, 3), "vfd", "core.rectangle", 89, 17, 41, 58, "#D1D5DB", "#374151", 2, 4),
                Text(E(family, style, 4), "motor-label", "M", 28, 35, 26, 22, 12, "#111827"),
                Text(E(family, style, 5), "vfd-label", "VFD", 94, 37, 31, 18, 9, "#111827"),
                StateLamp(E(family, style, 6), "running", 4, 4, "#16A34A", "running", "{equipmentPath}.Running"),
                StateLamp(E(family, style, 7), "fault", 114, 4, "#DC2626", "fault", "{equipmentPath}.Fault")
            ],
            parameters: VfdMotorParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.motor.vfd", "Motor com inversor", "motor", style, 196, 112,
        [
            MaterialShape(E(family, style, 1), "motor-body", "core.rectangle", 19, 29, 83, 57, "#AEBCC8", "#F8FAFC", "#334155", 3, 22, dimensional, "vertical", dimensional),
            MaterialShape(E(family, style, 2), "motor-end", "core.ellipse", 12, 33, 22, 49, "#94A3B8", "#DDE4EA", "#334155", 2, 0, dimensional, "horizontal"),
            FlatShape(E(family, style, 3), "shaft", "core.rectangle", 98, 51, 24, 9, "#94A3B8", "#475569", 1, 2),
            FlatShape(E(family, style, 4), "motor-base", "core.rectangle", 31, 88, 76, 8, "#475569", "#334155", 1, 2),
            MaterialShape(E(family, style, 5), "vfd", "core.rectangle", 132, 16, 52, 76, "#D8E0E8", "#FFFFFF", "#334155", 2, 6, dimensional, "horizontal", dimensional),
            FlatShape(E(family, style, 6), "vfd-screen", "core.rectangle", 142, 27, 32, 18, "#334155", "#0F172A", 1, 2),
            FlatShape(E(family, style, 7), "vfd-key-1", "core.rectangle", 144, 52, 8, 7, "#94A3B8", "#475569", 1, 1),
            FlatShape(E(family, style, 8), "vfd-key-2", "core.rectangle", 156, 52, 8, 7, "#94A3B8", "#475569", 1, 1),
            FlatShape(E(family, style, 9), "vfd-key-3", "core.rectangle", 168, 52, 8, 7, "#94A3B8", "#475569", 1, 1),
            Text(E(family, style, 10), "motor-label", "M", 49, 45, 24, 20, 11, "#1F2937"),
            Text(E(family, style, 11), "vfd-label", "VFD", 142, 66, 32, 16, 9, "#1F2937"),
            StateLamp(E(family, style, 12), "running", 4, 4, "#22C55E", "running", "{equipmentPath}.Running"),
            StateLamp(E(family, style, 13), "fault", 173, 4, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: VfdMotorParameters());
    }

    private static DynamoEngineeringDto OnOffValve(VisualStyle style)
    {
        const int family = 5;
        var sequence = DefinitionSequence(family, style);
        if (style == VisualStyle.HighPerformance)
        {
            return Dynamo(sequence, "process.valve.onoff", "Válvula abre/fecha", "valve", style, 128, 92,
            [
                FlatShape(E(family, style, 1), "pipe-left", "core.rectangle", 3, 44, 32, 9, "#9CA3AF", "#4B5563", 1, 2),
                Polygon(E(family, style, 2), "body-left", 31, 31, 34, 34, [(0d, 0d), (34d, 17d), (0d, 34d)], "#C5CDD3", "#374151", 2),
                Polygon(E(family, style, 3), "body-right", 63, 31, 34, 34, [(34d, 0d), (0d, 17d), (34d, 34d)], "#C5CDD3", "#374151", 2),
                FlatShape(E(family, style, 4), "pipe-right", "core.rectangle", 94, 44, 31, 9, "#9CA3AF", "#4B5563", 1, 2),
                FlatShape(E(family, style, 5), "stem", "core.rectangle", 61, 20, 5, 17, "#6B7280", "#374151", 1),
                FlatShape(E(family, style, 6), "actuator", "core.rectangle", 49, 4, 29, 19, "#D1D5DB", "#374151", 2, 4),
                StateLamp(E(family, style, 7), "open", 5, 5, "#16A34A", "open", "{equipmentPath}.Open"),
                StateLamp(E(family, style, 8), "fault", 104, 5, "#DC2626", "fault", "{equipmentPath}.Fault")
            ],
            parameters: OnOffValveParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.valve.onoff", "Válvula abre/fecha", "valve", style, 164, 112,
        [
            MaterialShape(E(family, style, 1), "pipe-left", "core.rectangle", 2, 54, 43, 12, "#AAB8C5", "#F8FAFC", "#475569", 1, 3, dimensional, "vertical"),
            MaterialShape(E(family, style, 2), "flange-left", "core.rectangle", 29, 46, 10, 28, "#94A3B8", "#DDE4EA", "#334155", 2, 2, dimensional, "horizontal"),
            Polygon(E(family, style, 3), "body-left", 39, 38, 43, 43, [(0d, 0d), (43d, 21.5d), (0d, 43d)], "#B8C4CF", "#334155", 2, dimensional ? "#F8FAFC" : null, "diagonal-down", dimensional),
            Polygon(E(family, style, 4), "body-right", 80, 38, 43, 43, [(43d, 0d), (0d, 21.5d), (43d, 43d)], "#B8C4CF", "#334155", 2, dimensional ? "#F8FAFC" : null, "diagonal-up", dimensional),
            MaterialShape(E(family, style, 5), "flange-right", "core.rectangle", 124, 46, 10, 28, "#94A3B8", "#DDE4EA", "#334155", 2, 2, dimensional, "horizontal"),
            MaterialShape(E(family, style, 6), "pipe-right", "core.rectangle", 132, 54, 30, 12, "#AAB8C5", "#F8FAFC", "#475569", 1, 3, dimensional, "vertical"),
            FlatShape(E(family, style, 7), "stem", "core.rectangle", 79, 27, 5, 17, "#64748B", "#334155", 1, 1),
            MaterialShape(E(family, style, 8), "actuator", "core.rectangle", 62, 4, 39, 25, "#94A3B8", "#DDE4EA", "#334155", 2, 5, dimensional, "vertical", dimensional),
            FlatShape(E(family, style, 9), "actuator-top", "core.rectangle", 69, 0, 25, 6, "#475569", "#334155", 1, 2),
            StateLamp(E(family, style, 10), "open", 5, 5, "#22C55E", "open", "{equipmentPath}.Open"),
            StateLamp(E(family, style, 11), "fault", 140, 5, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: OnOffValveParameters());
    }

    private static DynamoEngineeringDto ControlValve(VisualStyle style)
    {
        const int family = 6;
        var sequence = DefinitionSequence(family, style);
        if (style == VisualStyle.HighPerformance)
        {
            return Dynamo(sequence, "process.valve.control", "Válvula de controle", "valve", style, 128, 108,
            [
                FlatShape(E(family, style, 1), "pipe-left", "core.rectangle", 3, 60, 31, 9, "#9CA3AF", "#4B5563", 1, 2),
                Polygon(E(family, style, 2), "body-left", 31, 47, 34, 34, [(0d, 0d), (34d, 17d), (0d, 34d)], "#C5CDD3", "#374151", 2),
                Polygon(E(family, style, 3), "body-right", 63, 47, 34, 34, [(34d, 0d), (0d, 17d), (34d, 34d)], "#C5CDD3", "#374151", 2),
                FlatShape(E(family, style, 4), "pipe-right", "core.rectangle", 94, 60, 31, 9, "#9CA3AF", "#4B5563", 1, 2),
                FlatShape(E(family, style, 5), "stem", "core.rectangle", 61, 29, 5, 24, "#6B7280", "#374151", 1),
                FlatShape(E(family, style, 6), "actuator", "core.ellipse", 43, 4, 42, 28, "#D1D5DB", "#374151", 2),
                Text(E(family, style, 7), "label", "%", 51, 8, 26, 20, 10, "#111827"),
                StateLamp(E(family, style, 8), "fault", 104, 5, "#DC2626", "fault", "{equipmentPath}.Fault")
            ],
            parameters: ControlValveParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.valve.control", "Válvula de controle", "valve", style, 166, 136,
        [
            MaterialShape(E(family, style, 1), "pipe-left", "core.rectangle", 2, 78, 44, 12, "#AAB8C5", "#F8FAFC", "#475569", 1, 3, dimensional, "vertical"),
            MaterialShape(E(family, style, 2), "flange-left", "core.rectangle", 30, 69, 10, 30, "#94A3B8", "#DDE4EA", "#334155", 2, 2, dimensional, "horizontal"),
            Polygon(E(family, style, 3), "body-left", 39, 60, 43, 43, [(0d, 0d), (43d, 21.5d), (0d, 43d)], "#B8C4CF", "#334155", 2, dimensional ? "#F8FAFC" : null, "diagonal-down", dimensional),
            Polygon(E(family, style, 4), "body-right", 80, 60, 43, 43, [(43d, 0d), (0d, 21.5d), (43d, 43d)], "#B8C4CF", "#334155", 2, dimensional ? "#F8FAFC" : null, "diagonal-up", dimensional),
            MaterialShape(E(family, style, 5), "flange-right", "core.rectangle", 124, 69, 10, 30, "#94A3B8", "#DDE4EA", "#334155", 2, 2, dimensional, "horizontal"),
            MaterialShape(E(family, style, 6), "pipe-right", "core.rectangle", 132, 78, 32, 12, "#AAB8C5", "#F8FAFC", "#475569", 1, 3, dimensional, "vertical"),
            FlatShape(E(family, style, 7), "stem", "core.rectangle", 79, 43, 5, 24, "#64748B", "#334155", 1, 1),
            MaterialShape(E(family, style, 8), "actuator", "core.ellipse", 52, 9, 60, 37, "#AEBCC8", "#F8FAFC", "#334155", 2, 0, dimensional, "vertical", dimensional),
            FlatShape(E(family, style, 9), "actuator-cap", "core.rectangle", 68, 4, 28, 7, "#475569", "#334155", 1, 2),
            Text(E(family, style, 10), "label", "%", 70, 17, 24, 18, 10, "#1F2937"),
            StateLamp(E(family, style, 11), "fault", 140, 5, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: ControlValveParameters());
    }

    private static DynamoEngineeringDto VerticalTank(VisualStyle style)
    {
        const int family = 7;
        var sequence = DefinitionSequence(family, style);
        if (style == VisualStyle.HighPerformance)
        {
            return Dynamo(sequence, "process.tank.vertical", "Tanque vertical", "tank", style, 108, 158,
            [
                FlatShape(E(family, style, 1), "vessel", "core.rectangle", 18, 8, 72, 140, "#D1D5DB", "#475569", 2, 18),
                FlatShape(E(family, style, 2), "liquid", "core.rectangle", 23, 77, 62, 65, "#93C5FD", "#64748B", 1, 10),
                FlatShape(E(family, style, 3), "nozzle", "core.rectangle", 48, 2, 12, 10, "#9CA3AF", "#475569", 1, 2),
                FlatShape(E(family, style, 4), "leg-left", "core.rectangle", 29, 144, 10, 10, "#6B7280", "#475569", 1, 2),
                FlatShape(E(family, style, 5), "leg-right", "core.rectangle", 69, 144, 10, 10, "#6B7280", "#475569", 1, 2),
                Text(E(family, style, 6), "label", "TK", 39, 30, 30, 24, 11, "#111827"),
                StateLamp(E(family, style, 7), "high", 84, 10, "#D97706", "high", "{equipmentPath}.High"),
                StateLamp(E(family, style, 8), "fault", 84, 132, "#DC2626", "fault", "{equipmentPath}.Fault")
            ],
            parameters: TankParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.tank.vertical", "Tanque vertical", "tank", style, 128, 186,
        [
            MaterialShape(E(family, style, 1), "vessel", "core.rectangle", 26, 25, 76, 132, "#C3CDD6", "#F8FAFC", "#475569", 3, 28, dimensional, "horizontal", dimensional),
            MaterialShape(E(family, style, 2), "top-head", "core.ellipse", 26, 15, 76, 32, "#B8C4CF", "#F8FAFC", "#475569", 2, 0, dimensional, "vertical"),
            MaterialShape(E(family, style, 3), "bottom-head", "core.ellipse", 26, 137, 76, 31, "#AEBCC8", "#F8FAFC", "#475569", 2, 0, dimensional, "vertical"),
            FlatShape(E(family, style, 4), "liquid", "core.rectangle", 32, 93, 64, 55, "#7DD3FC", "#0284C7", 1, 12),
            FlatShape(E(family, style, 5), "liquid-line", "core.rectangle", 31, 91, 66, 3, "#0284C7", "#0284C7", 0, 1),
            MaterialShape(E(family, style, 6), "top-nozzle", "core.rectangle", 56, 3, 16, 18, "#94A3B8", "#DDE4EA", "#475569", 1, 3, dimensional, "horizontal"),
            FlatShape(E(family, style, 7), "side-nozzle", "core.rectangle", 101, 63, 19, 12, "#94A3B8", "#475569", 1, 2),
            FlatShape(E(family, style, 8), "leg-left", "core.rectangle", 39, 160, 10, 18, "#64748B", "#475569", 1, 2),
            FlatShape(E(family, style, 9), "leg-right", "core.rectangle", 80, 160, 10, 18, "#64748B", "#475569", 1, 2),
            FlatShape(E(family, style, 10), "foot-left", "core.rectangle", 33, 176, 22, 6, "#475569", "#334155", 1, 1),
            FlatShape(E(family, style, 11), "foot-right", "core.rectangle", 74, 176, 22, 6, "#475569", "#334155", 1, 1),
            Text(E(family, style, 12), "label", "TK", 48, 54, 32, 22, 11, "#1F2937"),
            StateLamp(E(family, style, 13), "high", 104, 19, "#F59E0B", "high", "{equipmentPath}.High"),
            StateLamp(E(family, style, 14), "fault", 104, 147, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: TankParameters());
    }

    private static DynamoEngineeringDto HorizontalTank(VisualStyle style)
    {
        const int family = 8;
        var sequence = DefinitionSequence(family, style);
        if (style == VisualStyle.HighPerformance)
        {
            return Dynamo(sequence, "process.tank.horizontal", "Tanque horizontal", "tank", style, 168, 100,
            [
                FlatShape(E(family, style, 1), "vessel", "core.rectangle", 18, 18, 132, 66, "#D1D5DB", "#475569", 2, 30),
                FlatShape(E(family, style, 2), "liquid", "core.rectangle", 24, 50, 120, 28, "#93C5FD", "#64748B", 1, 14),
                FlatShape(E(family, style, 3), "leg-left", "core.rectangle", 42, 80, 10, 12, "#6B7280", "#475569", 1, 2),
                FlatShape(E(family, style, 4), "leg-right", "core.rectangle", 116, 80, 10, 12, "#6B7280", "#475569", 1, 2),
                Text(E(family, style, 5), "label", "TK", 68, 28, 32, 24, 11, "#111827"),
                StateLamp(E(family, style, 6), "high", 144, 10, "#D97706", "high", "{equipmentPath}.High"),
                StateLamp(E(family, style, 7), "fault", 144, 74, "#DC2626", "fault", "{equipmentPath}.Fault")
            ],
            parameters: TankParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.tank.horizontal", "Tanque horizontal", "tank", style, 196, 120,
        [
            MaterialShape(E(family, style, 1), "vessel", "core.rectangle", 24, 26, 145, 68, "#C3CDD6", "#F8FAFC", "#475569", 3, 32, dimensional, "vertical", dimensional),
            MaterialShape(E(family, style, 2), "head-left", "core.ellipse", 17, 27, 38, 66, "#AEBCC8", "#F8FAFC", "#475569", 2, 0, dimensional, "horizontal"),
            MaterialShape(E(family, style, 3), "head-right", "core.ellipse", 150, 27, 38, 66, "#AEBCC8", "#F8FAFC", "#475569", 2, 0, dimensional, "horizontal"),
            FlatShape(E(family, style, 4), "liquid", "core.rectangle", 31, 59, 131, 29, "#7DD3FC", "#0284C7", 1, 13),
            FlatShape(E(family, style, 5), "liquid-line", "core.rectangle", 31, 57, 132, 3, "#0284C7", "#0284C7", 0, 1),
            MaterialShape(E(family, style, 6), "top-nozzle", "core.rectangle", 91, 10, 16, 20, "#94A3B8", "#DDE4EA", "#475569", 1, 3, dimensional, "horizontal"),
            FlatShape(E(family, style, 7), "side-nozzle", "core.rectangle", 183, 53, 11, 14, "#94A3B8", "#475569", 1, 2),
            FlatShape(E(family, style, 8), "saddle-left", "core.rectangle", 50, 89, 25, 16, "#64748B", "#475569", 1, 3),
            FlatShape(E(family, style, 9), "saddle-right", "core.rectangle", 123, 89, 25, 16, "#64748B", "#475569", 1, 3),
            FlatShape(E(family, style, 10), "base-left", "core.rectangle", 43, 103, 39, 6, "#475569", "#334155", 1, 1),
            FlatShape(E(family, style, 11), "base-right", "core.rectangle", 116, 103, 39, 6, "#475569", "#334155", 1, 1),
            Text(E(family, style, 12), "label", "TK", 82, 37, 32, 22, 11, "#1F2937"),
            StateLamp(E(family, style, 13), "high", 171, 8, "#F59E0B", "high", "{equipmentPath}.High"),
            StateLamp(E(family, style, 14), "fault", 171, 88, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: TankParameters());
    }

    private static DynamoEngineeringDto CentrifugalBlower(VisualStyle style)
    {
        const int family = 9;
        var sequence = DefinitionSequence(family, style);
        if (style == VisualStyle.HighPerformance)
        {
            return Dynamo(sequence, "process.blower.centrifugal", "Soprador centrífugo", "compressor", style, 154, 108,
            [
                FlatShape(E(family, style, 1), "casing", "core.ellipse", 28, 20, 70, 70, "#C5CDD3", "#374151", 2),
                FlatShape(E(family, style, 2), "hub", "core.ellipse", 48, 40, 30, 30, "#F3F4F6", "#4B5563", 2),
                FlatShape(E(family, style, 3), "inlet", "core.rectangle", 4, 45, 30, 18, "#A7B0B7", "#374151", 2, 3),
                FlatShape(E(family, style, 4), "outlet-neck", "core.rectangle", 86, 18, 20, 30, "#A7B0B7", "#374151", 2, 3),
                FlatShape(E(family, style, 5), "outlet", "core.rectangle", 96, 13, 42, 18, "#A7B0B7", "#374151", 2, 3),
                FlatShape(E(family, style, 6), "base", "core.rectangle", 24, 90, 88, 8, "#6B7280", "#374151", 1, 2),
                Text(E(family, style, 7), "label", "B", 51, 44, 24, 22, 11, "#111827"),
                StateLamp(E(family, style, 8), "running", 5, 5, "#16A34A", "running", "{equipmentPath}.Running"),
                StateLamp(E(family, style, 9), "fault", 130, 5, "#DC2626", "fault", "{equipmentPath}.Fault")
            ],
            parameters: BlowerParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.blower.centrifugal", "Soprador centrífugo", "compressor", style, 176, 126,
        [
            MaterialShape(E(family, style, 1), "casing", "core.ellipse", 31, 26, 82, 82, "#AEBCC8", "#F8FAFC", "#334155", 3, 0, dimensional, "diagonal-down", dimensional),
            MaterialShape(E(family, style, 2), "hub", "core.ellipse", 54, 49, 36, 36, "#DDE4EA", "#FFFFFF", "#475569", 2, 0, dimensional, "diagonal-up"),
            FlatShape(E(family, style, 3), "blade-1", "core.rectangle", 68, 50, 7, 17, "#64748B", "#475569", 1, 2, 15),
            FlatShape(E(family, style, 4), "blade-2", "core.rectangle", 74, 61, 7, 17, "#64748B", "#475569", 1, 2, 75),
            FlatShape(E(family, style, 5), "blade-3", "core.rectangle", 62, 68, 7, 17, "#64748B", "#475569", 1, 2, 135),
            MaterialShape(E(family, style, 6), "inlet", "core.rectangle", 4, 58, 35, 19, "#B8C4CF", "#F8FAFC", "#334155", 2, 4, dimensional, "vertical"),
            MaterialShape(E(family, style, 7), "inlet-flange", "core.ellipse", 1, 53, 13, 29, "#94A3B8", "#DDE4EA", "#334155", 2, 0, dimensional, "horizontal"),
            MaterialShape(E(family, style, 8), "outlet-neck", "core.rectangle", 99, 24, 24, 36, "#B8C4CF", "#F8FAFC", "#334155", 2, 4, dimensional, "horizontal"),
            MaterialShape(E(family, style, 9), "outlet", "core.rectangle", 112, 13, 44, 19, "#B8C4CF", "#F8FAFC", "#334155", 2, 4, dimensional, "vertical"),
            MaterialShape(E(family, style, 10), "outlet-flange", "core.ellipse", 149, 10, 14, 25, "#94A3B8", "#DDE4EA", "#334155", 2, 0, dimensional, "horizontal"),
            FlatShape(E(family, style, 11), "foot-left", "core.rectangle", 48, 102, 18, 9, "#64748B", "#334155", 1, 2),
            FlatShape(E(family, style, 12), "foot-right", "core.rectangle", 91, 102, 18, 9, "#64748B", "#334155", 1, 2),
            FlatShape(E(family, style, 13), "base", "core.rectangle", 37, 111, 86, 8, "#475569", "#334155", 1, 2),
            Text(E(family, style, 14), "label", "B", 61, 58, 22, 18, 10, "#1F2937"),
            StateLamp(E(family, style, 15), "running", 5, 5, "#22C55E", "running", "{equipmentPath}.Running"),
            StateLamp(E(family, style, 16), "fault", 151, 5, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: BlowerParameters());
    }

    private static DynamoEngineeringDto ProcessIndicator(VisualStyle style)
    {
        const int family = 10;
        var sequence = DefinitionSequence(family, style);
        if (style == VisualStyle.HighPerformance)
        {
            return Dynamo(sequence, "process.instrument.indicator", "Indicador de processo", "instrument", style, 96, 112,
            [
                FlatShape(E(family, style, 1), "stem", "core.rectangle", 44, 68, 8, 30, "#6B7280", "#374151", 1, 2),
                FlatShape(E(family, style, 2), "face", "core.ellipse", 14, 8, 68, 68, "#F9FAFB", "#374151", 2),
                FlatShape(E(family, style, 3), "inner", "core.ellipse", 22, 16, 52, 52, "#E5E7EB", "#9CA3AF", 1),
                Text(E(family, style, 4), "label", "PI", 32, 30, 32, 22, 11, "#111827"),
                FlatShape(E(family, style, 5), "connection", "core.rectangle", 34, 96, 28, 8, "#9CA3AF", "#374151", 1, 2),
                StateLamp(E(family, style, 6), "fault", 72, 5, "#DC2626", "fault", "{equipmentPath}.Fault")
            ],
            parameters: IndicatorParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.instrument.indicator", "Indicador de processo", "instrument", style, 124, 146,
        [
            MaterialShape(E(family, style, 1), "outer-case", "core.ellipse", 16, 10, 88, 88, "#AEBCC8", "#F8FAFC", "#334155", 3, 0, dimensional, "diagonal-down", dimensional),
            MaterialShape(E(family, style, 2), "face", "core.ellipse", 24, 18, 72, 72, "#F8FAFC", "#FFFFFF", "#64748B", 2, 0, dimensional, "vertical"),
            FlatShape(E(family, style, 3), "tick-1", "core.rectangle", 57, 21, 3, 10, "#475569", "#475569", 0, 1),
            FlatShape(E(family, style, 4), "tick-2", "core.rectangle", 81, 31, 3, 10, "#475569", "#475569", 0, 1, 45),
            FlatShape(E(family, style, 5), "tick-3", "core.rectangle", 88, 54, 3, 10, "#475569", "#475569", 0, 1, 90),
            FlatShape(E(family, style, 6), "tick-4", "core.rectangle", 35, 31, 3, 10, "#475569", "#475569", 0, 1, -45),
            FlatShape(E(family, style, 7), "needle", "core.rectangle", 58, 51, 3, 29, "#DC2626", "#991B1B", 1, 1, 35),
            FlatShape(E(family, style, 8), "hub", "core.ellipse", 54, 54, 11, 11, "#475569", "#1F2937", 1),
            Text(E(family, style, 9), "label", "PI", 44, 68, 32, 16, 10, "#1F2937"),
            FlatShape(E(family, style, 10), "stem", "core.rectangle", 57, 96, 8, 31, "#64748B", "#334155", 1, 2),
            MaterialShape(E(family, style, 11), "connection", "core.rectangle", 43, 125, 36, 9, "#94A3B8", "#DDE4EA", "#334155", 1, 2, dimensional, "horizontal"),
            StateLamp(E(family, style, 12), "fault", 100, 5, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: IndicatorParameters());
    }

    private static DynamoEngineeringDto Dynamo(
        int sequence,
        string familyKey,
        string familyName,
        string category,
        VisualStyle style,
        int width,
        int height,
        IReadOnlyCollection<VisualElementEngineeringDto> elements,
        IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto>? parameters = null) =>
        new(
            DefinitionId(sequence),
            VariantKey(familyKey, style),
            VariantName(familyName, style),
            TemplateKey: null,
            Properties: new Dictionary<string, string>
            {
                ["category"] = category,
                ["defaultWidth"] = width.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["defaultHeight"] = height.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["libraryVersion"] = "1.2.0",
                ["visualStyle"] = StyleKey(style)
            },
            Context: new Dictionary<string, string>
            {
                ["usage"] = "process-screen",
                ["view"] = "front-orthographic"
            },
            Metadata: new Dictionary<string, string>
            {
                ["builtinLibrary"] = "true",
                ["assetOrigin"] = "original-elitescada-vector",
                ["equipmentPathBinding"] = "{equipmentPath}",
                ["publicInterfaceVersion"] = "1",
                ["stateModelVersion"] = "1",
                ["familyKey"] = familyKey,
                ["visualStyle"] = StyleKey(style),
                ["view"] = "front-orthographic",
                ["performanceProfile"] = style == VisualStyle.HighPerformance ? "high-performance" : "rich"
            },
            Parameters: parameters,
            Elements: elements);

    private static VisualElementEngineeringDto FlatShape(
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
        double rotation = 0) =>
        Shape(sequence, key, type, x, y, width, height, fill, stroke, strokeWidth, cornerRadius, rotation);

    private static VisualElementEngineeringDto MaterialShape(
        int sequence,
        string key,
        string type,
        double x,
        double y,
        double width,
        double height,
        string fill,
        string secondaryFill,
        string stroke,
        double strokeWidth,
        double cornerRadius,
        bool dimensional,
        string gradientDirection,
        bool shadow = false,
        double rotation = 0) =>
        Shape(
            sequence, key, type, x, y, width, height, fill, stroke, strokeWidth, cornerRadius, rotation,
            dimensional ? secondaryFill : null,
            gradientDirection,
            shadow);

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
        double rotation = 0,
        string? secondaryFill = null,
        string gradientDirection = "vertical",
        bool shadow = false)
    {
        var properties = Properties(
            ("x", x), ("y", y), ("width", width), ("height", height),
            ("fillStyle", secondaryFill is null ? "solid" : "gradient"),
            ("fillColor", fill),
            ("strokeColor", stroke), ("strokeWidth", strokeWidth),
            ("rotation", rotation));

        if (secondaryFill is not null)
        {
            properties["fillSecondaryColor"] = JsonSerializer.SerializeToElement(secondaryFill);
            properties["gradientDirection"] = JsonSerializer.SerializeToElement(gradientDirection);
        }

        if (shadow)
        {
            properties["shadowEnabled"] = JsonSerializer.SerializeToElement(true);
            properties["shadowColor"] = JsonSerializer.SerializeToElement("#0F172A55");
            properties["shadowOffsetX"] = JsonSerializer.SerializeToElement(2d);
            properties["shadowOffsetY"] = JsonSerializer.SerializeToElement(3d);
            properties["shadowBlur"] = JsonSerializer.SerializeToElement(4d);
        }

        if (type.Equals("core.rectangle", StringComparison.Ordinal))
            properties["cornerRadius"] = JsonSerializer.SerializeToElement(cornerRadius);

        return new(key, type, Properties: properties, Id: ElementId(sequence));
    }

    private static VisualElementEngineeringDto Polygon(
        int sequence,
        string key,
        double x,
        double y,
        double width,
        double height,
        IReadOnlyCollection<(double X, double Y)> points,
        string fill,
        string stroke,
        double strokeWidth,
        string? secondaryFill = null,
        string gradientDirection = "vertical",
        bool shadow = false)
    {
        var structuralPoints = points.Select(point => new Dictionary<string, double>
        {
            ["x"] = point.X,
            ["y"] = point.Y
        }).ToArray();

        var properties = Properties(
            ("x", x), ("y", y), ("width", width), ("height", height),
            ("fillStyle", secondaryFill is null ? "solid" : "gradient"),
            ("fillColor", fill),
            ("strokeColor", stroke), ("strokeWidth", strokeWidth),
            ("points", structuralPoints));

        if (secondaryFill is not null)
        {
            properties["fillSecondaryColor"] = JsonSerializer.SerializeToElement(secondaryFill);
            properties["gradientDirection"] = JsonSerializer.SerializeToElement(gradientDirection);
        }

        if (shadow)
        {
            properties["shadowEnabled"] = JsonSerializer.SerializeToElement(true);
            properties["shadowColor"] = JsonSerializer.SerializeToElement("#0F172A44");
            properties["shadowOffsetX"] = JsonSerializer.SerializeToElement(1d);
            properties["shadowOffsetY"] = JsonSerializer.SerializeToElement(2d);
            properties["shadowBlur"] = JsonSerializer.SerializeToElement(3d);
        }

        return new(key, "core.polygon", Properties: properties, Id: ElementId(sequence));
    }

    private static VisualElementEngineeringDto Text(
        int sequence,
        string key,
        string text,
        double x,
        double y,
        double width,
        double height,
        double fontSize = 16,
        string textColor = "#1F2937") =>
        new(
            key,
            "core.text",
            Properties: Properties(
                ("x", x), ("y", y), ("width", width), ("height", height),
                ("text", text), ("fontSize", fontSize), ("fontWeight", 700),
                ("horizontalAlignment", "center"), ("verticalAlignment", "middle"),
                ("textColor", textColor)),
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

    private static IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto> PumpParameters() =>
        Parameters(
            EquipmentPathParameter(),
            TagParameter("running"),
            TagParameter("fault"),
            CommandKeyParameter("startCommandKey"),
            CommandKeyParameter("stopCommandKey"));

    private static IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto> MotorParameters() =>
        PumpParameters();

    private static IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto> VfdMotorParameters() =>
        Parameters(
            EquipmentPathParameter(),
            TagParameter("running"),
            TagParameter("fault"),
            TagParameter("processValue"),
            TagParameter("setpoint"),
            TagParameter("feedback"),
            CommandKeyParameter("startCommandKey"),
            CommandKeyParameter("stopCommandKey"));

    private static IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto> OnOffValveParameters() =>
        Parameters(
            EquipmentPathParameter(),
            TagParameter("open"),
            TagParameter("closed"),
            TagParameter("fault"),
            CommandKeyParameter("openCommandKey"),
            CommandKeyParameter("closeCommandKey"));

    private static IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto> ControlValveParameters() =>
        Parameters(
            EquipmentPathParameter(),
            TagParameter("processValue"),
            TagParameter("setpoint"),
            TagParameter("feedback"),
            TagParameter("fault"),
            CommandKeyParameter("commandKey"));

    private static IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto> TankParameters() =>
        Parameters(
            EquipmentPathParameter(),
            TagParameter("processValue"),
            TagParameter("high"),
            TagParameter("fault"));

    private static IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto> BlowerParameters() =>
        Parameters(
            EquipmentPathParameter(),
            TagParameter("running"),
            TagParameter("fault"),
            TagParameter("processValue"),
            CommandKeyParameter("startCommandKey"),
            CommandKeyParameter("stopCommandKey"));

    private static IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto> IndicatorParameters() =>
        Parameters(
            EquipmentPathParameter(),
            TagParameter("processValue"),
            TagParameter("fault"));

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

    private static int DefinitionSequence(int familySequence, VisualStyle style) =>
        familySequence + style switch
        {
            VisualStyle.Detailed2D => 0,
            VisualStyle.DimensionalFront => 10,
            VisualStyle.HighPerformance => 20,
            _ => throw new ArgumentOutOfRangeException(nameof(style))
        };

    private static int E(int familySequence, VisualStyle style, int localSequence) =>
        1000 + DefinitionSequence(familySequence, style) * 100 + localSequence;

    private static string VariantKey(string familyKey, VisualStyle style) =>
        style switch
        {
            VisualStyle.Detailed2D => familyKey,
            VisualStyle.DimensionalFront => $"{familyKey}.front-3d",
            VisualStyle.HighPerformance => $"{familyKey}.high-performance",
            _ => throw new ArgumentOutOfRangeException(nameof(style))
        };

    private static string VariantName(string familyName, VisualStyle style) =>
        style switch
        {
            VisualStyle.Detailed2D => familyName,
            VisualStyle.DimensionalFront => $"{familyName} — 3D frontal",
            VisualStyle.HighPerformance => $"{familyName} — High Performance",
            _ => throw new ArgumentOutOfRangeException(nameof(style))
        };

    private static string StyleKey(VisualStyle style) =>
        style switch
        {
            VisualStyle.Detailed2D => "detailed-2d",
            VisualStyle.DimensionalFront => "dimensional-front",
            VisualStyle.HighPerformance => "high-performance",
            _ => throw new ArgumentOutOfRangeException(nameof(style))
        };

    private static Guid DefinitionId(int sequence) =>
        Guid.Parse($"43000000-0000-0000-0000-{sequence:000000000000}");

    private static Guid ElementId(int sequence) =>
        Guid.Parse($"43100000-0000-0000-0000-{sequence:000000000000}");
}
