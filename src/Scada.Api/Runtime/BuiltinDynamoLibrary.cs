using System.Text.Json;
using Scada.Engineering.Contracts;

namespace Scada.Api.Runtime;

/// <summary>
/// Original EliteSCADA industrial symbol library rendered exclusively through the
/// canonical visual-object model. Every equipment family ships in three visual
/// styles without introducing image-only assets or a second renderer:
/// detailed 2D, front-facing dimensional (gradient/shadow) and high-performance HMI.
///
/// Artwork research references: Opto 22's Image Library/SVG Editors
/// (https://www.opto22.com/support/resources-tools/image-library-svg-editors)
/// for equipment illustration and recoloring ideas; Wikimedia Commons P&amp;ID
/// symbols (https://commons.wikimedia.org/wiki/Category:P%26ID_symbols) for
/// process-symbol conventions. These are references only: retain original,
/// editable EliteSCADA geometry and preserve equipment bindings/state behavior.
/// C-DYNAMO-ARTWORK-02 keeps those public/runtime contracts stable while allowing
/// the built-in silhouettes and per-style finish to evolve professionally.
/// </summary>
public static class BuiltinDynamoLibrary
{
    public const string Version = "2.0.0";

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
        var definitions = new List<DynamoEngineeringDto>(72);
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
            definitions.Add(AdditionalFamily(style, 101, "process.compressor.reciprocating", "Compressor alternativo", "compressor", 150, 112));
            definitions.Add(AdditionalFamily(style, 131, "process.compressor.screw", "Compressor de parafuso", "compressor", 164, 104));
            definitions.Add(AdditionalFamily(style, 161, "process.valve.butterfly", "Válvula borboleta", "valve", 142, 112));
            definitions.Add(AdditionalFamily(style, 191, "process.valve.ball", "Válvula esfera", "valve", 142, 112));
            definitions.Add(AdditionalFamily(style, 221, "process.valve.gate", "Válvula gaveta", "valve", 142, 132));
            definitions.Add(AdditionalFamily(style, 251, "process.exchanger.shell-tube", "Trocador casco e tubos", "process", 168, 126));
            definitions.Add(AdditionalFamily(style, 281, "process.filter.strainer", "Filtro tipo Y", "process", 142, 122));
            definitions.Add(AdditionalFamily(style, 311, "process.mixer.agitator", "Agitador de tanque", "process", 132, 174));
            definitions.Add(AdditionalFamily(style, 341, "electrical.transformer.power", "Transformador de potência", "substation", 150, 150));
            definitions.Add(AdditionalFamily(style, 371, "electrical.breaker", "Disjuntor de potência", "substation", 130, 148));
            definitions.Add(AdditionalFamily(style, 401, "electrical.disconnector", "Seccionadora", "substation", 148, 126));
            definitions.Add(AdditionalFamily(style, 431, "electrical.earthing-switch", "Chave de aterramento", "substation", 132, 126));
            definitions.Add(AdditionalFamily(style, 461, "electrical.generator", "Gerador síncrono", "electrical", 154, 112));
            definitions.Add(AdditionalFamily(style, 491, "electrical.current-transformer", "Transformador de corrente", "substation", 112, 154));
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
                FlatShape(E(family, style, 1), "suction", "core.rectangle", 4, 41, 31, 14, "#A7B0B7", "#374151", 2, 3),
                FlatShape(E(family, style, 9), "suction-flange", "core.ellipse", 3, 35, 13, 26, "#B7C0C6", "#374151", 1.5),
                BezierShape(E(family, style, 2), "casing", 28, 13, 65, 68,
                    "M 6 55 C 6 28 22 9 48 7 C 72 5 91 19 94 39 C 97 57 86 76 69 87 C 53 98 30 95 17 82 C 9 74 6 65 6 55 Z",
                    "#C5CDD3", "#374151", 2),
                FlatShape(E(family, style, 3), "impeller", "core.ellipse", 48, 36, 25, 25, "#F3F4F6", "#4B5563", 2),
                FlatShape(E(family, style, 10), "discharge-neck", "core.rectangle", 77, 19, 18, 28, "#A7B0B7", "#374151", 2, 4),
                FlatShape(E(family, style, 4), "discharge", "core.rectangle", 89, 12, 35, 14, "#A7B0B7", "#374151", 2, 3),
                FlatShape(E(family, style, 11), "discharge-flange", "core.ellipse", 118, 8, 12, 22, "#B7C0C6", "#374151", 1.5),
                FlatShape(E(family, style, 12), "foot-left", "core.rectangle", 39, 73, 15, 8, "#6B7280", "#374151", 1, 2),
                FlatShape(E(family, style, 13), "foot-right", "core.rectangle", 72, 73, 15, 8, "#6B7280", "#374151", 1, 2),
                ArcShape(E(family, style, 14), "casing-scroll", 39, 25, 43, 43, 205, 505, "arc",
                    "#00000000", "#6B7780", 1.5),
                FlatShape(E(family, style, 5), "base", "core.rectangle", 31, 80, 64, 6, "#5B646B", "#374151", 1, 2),
                Text(E(family, style, 6), "label", "P", 50, 39, 22, 18, 12, "#111827"),
                StateLamp(E(family, style, 7), "running", 5, 5, "#16A34A", "running", "{equipmentPath}.Running"),
                StateLamp(E(family, style, 8), "fault", 107, 5, "#DC2626", "fault", "{equipmentPath}.Fault")
            ],
            parameters: PumpParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "dynamo.pump.standard", "Bomba centrífuga", "pump", style, 160, 110,
        [
            MaterialShape(E(family, style, 1), "suction-pipe", "core.rectangle", 1, 49, 39, 17,
                "#B8C4CF", "#F8FAFC", "#334155", 2, 4, dimensional, "vertical"),
            MaterialShape(E(family, style, 2), "suction-flange", "core.ellipse", 4, 41, 16, 32,
                "#AAB8C5", "#F8FAFC", "#334155", 2, 0, dimensional, "horizontal"),
            BezierShape(E(family, style, 3), "casing", 34, 17, 87, 84,
                "M 7 55 C 7 27 24 8 50 6 C 74 4 93 18 96 39 C 99 58 88 76 71 88 C 55 99 32 97 18 84 C 10 76 7 66 7 55 Z",
                "#B8C4CF", "#334155", 3,
                dimensional ? "#F8FAFC" : null, "diagonal-down", dimensional),
            MaterialShape(E(family, style, 4), "casing-inner", "core.ellipse", 50, 34, 51, 51,
                "#DDE4EA", "#FFFFFF", "#64748B", 2, 0, dimensional, "diagonal-down"),
            MaterialShape(E(family, style, 5), "impeller", "core.ellipse", 64, 48, 23, 23,
                "#64748B", "#CBD5E1", "#334155", 2, 0, dimensional, "diagonal-up"),
            MaterialShape(E(family, style, 6), "discharge-neck", "core.rectangle", 100, 16, 23, 39,
                "#B8C4CF", "#F8FAFC", "#334155", 2, 5, dimensional, "horizontal"),
            MaterialShape(E(family, style, 7), "discharge-pipe", "core.rectangle", 111, 8, 39, 18,
                "#B8C4CF", "#F8FAFC", "#334155", 2, 4, dimensional, "vertical"),
            MaterialShape(E(family, style, 8), "discharge-flange", "core.ellipse", 143, 5, 14, 24,
                "#AAB8C5", "#F8FAFC", "#334155", 2, 0, dimensional, "horizontal"),
            FlatShape(E(family, style, 9), "foot-left", "core.rectangle", 48, 91, 16, 8, "#64748B", "#334155", 1, 2),
            FlatShape(E(family, style, 10), "foot-right", "core.rectangle", 90, 91, 16, 8, "#64748B", "#334155", 1, 2),
            ArcShape(E(family, style, 15), "casing-scroll", 49, 33, 55, 55, 205, 505, "arc",
                "#00000000", dimensional ? "#516979" : "#64748B", 1.5),
            FlatShape(E(family, style, 11), "base", "core.rectangle", 35, 99, 84, 7, "#475569", "#334155", 1, 2),
            Text(E(family, style, 12), "label", "P", 65, 51, 22, 18, 11, "#1F2937"),
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
                // Side-elevation silhouette follows the Product Owner coordinate scaffold:
                // stepped shaft/front bearing, long cylindrical frame, top terminal box,
                // rear fan cowl and two mounting feet. Geometry remains native/editable.
                Polygon(E(family, style, 1), "body", 25, 20, 61, 50,
                    [(7d, 0d), (51d, 0d), (61d, 8d), (61d, 42d), (51d, 50d), (7d, 50d), (0d, 42d), (0d, 8d)],
                    "#C5CDD3", "#374151", 2),
                FlatShape(E(family, style, 18), "end-bell-left", "core.ellipse", 15, 25, 22, 40, "#AEB7BE", "#374151", 1.5),
                FlatShape(E(family, style, 19), "end-bell-right", "core.ellipse", 79, 27, 18, 36, "#AEB7BE", "#374151", 1.5),
                FlatShape(E(family, style, 2), "shaft", "core.rectangle", 91, 41, 12, 8, "#8D989F", "#374151", 1, 2),
                FlatShape(E(family, style, 3), "terminal", "core.rectangle", 48, 7, 26, 16, "#D1D5DB", "#374151", 1.5, 3),
                FlatShape(E(family, style, 20), "foot-left", "core.rectangle", 35, 67, 16, 10, "#7D898F", "#374151", 1, 2),
                FlatShape(E(family, style, 21), "foot-right", "core.rectangle", 68, 67, 16, 10, "#7D898F", "#374151", 1, 2),
                FlatShape(E(family, style, 4), "base", "core.rectangle", 29, 76, 61, 7, "#5F6A70", "#374151", 1, 2),
                Text(E(family, style, 5), "label", "M", 47, 35, 24, 20, 12, "#111827"),
                StateLamp(E(family, style, 6), "running", 4, 4, "#16A34A", "running", "{equipmentPath}.Running"),
                StateLamp(E(family, style, 7), "fault", 82, 4, "#DC2626", "fault", "{equipmentPath}.Fault")
            ],
            parameters: MotorParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.motor.standard", "Motor padrão", "motor", style, 150, 102,
        [
            Polygon(E(family, style, 1), "body", 38, 21, 78, 57,
                [(9d, 0d), (67d, 0d), (78d, 10d), (78d, 47d), (67d, 57d), (9d, 57d), (0d, 47d), (0d, 10d)],
                "#AEBCC8", "#334155", 3, dimensional ? "#F8FAFC" : null, "vertical", dimensional),
            MaterialShape(E(family, style, 2), "end-bell-left", "core.ellipse", 23, 27, 29, 45,
                "#94A3B8", "#E2E8ED", "#334155", 2, 0, dimensional, "horizontal"),
            MaterialShape(E(family, style, 3), "end-bell-right", "core.ellipse", 105, 28, 27, 43,
                "#94A3B8", "#DDE4EA", "#334155", 2, 0, dimensional, "horizontal"),
            FlatShape(E(family, style, 4), "shaft", "core.rectangle", 124, 45, 22, 10, "#94A3B8", "#475569", 1, 2),
            MaterialShape(E(family, style, 5), "terminal", "core.rectangle", 61, 8, 36, 20,
                "#CBD5E1", "#F8FAFC", "#334155", 2, 4, dimensional, "vertical"),
            FlatShape(E(family, style, 6), "fin-1", "core.rectangle", 49, 29, 2, 42, "#64748B", "#64748B", 0),
            FlatShape(E(family, style, 7), "fin-2", "core.rectangle", 58, 27, 2, 46, "#64748B", "#64748B", 0),
            FlatShape(E(family, style, 8), "fin-3", "core.rectangle", 67, 27, 2, 46, "#64748B", "#64748B", 0),
            FlatShape(E(family, style, 9), "fin-4", "core.rectangle", 76, 27, 2, 46, "#64748B", "#64748B", 0),
            FlatShape(E(family, style, 10), "fin-5", "core.rectangle", 85, 27, 2, 46, "#64748B", "#64748B", 0),
            FlatShape(E(family, style, 11), "fin-6", "core.rectangle", 94, 29, 2, 42, "#64748B", "#64748B", 0),
            FlatShape(E(family, style, 12), "foot-left", "core.rectangle", 46, 74, 19, 12, "#64748B", "#334155", 1, 2),
            FlatShape(E(family, style, 13), "foot-right", "core.rectangle", 92, 74, 19, 12, "#64748B", "#334155", 1, 2),
            FlatShape(E(family, style, 14), "base", "core.rectangle", 40, 85, 78, 8, "#475569", "#334155", 1, 2),
            FlatShape(E(family, style, 18), "terminal-cover", "core.rectangle", 65, 4, 28, 6, "#94A3B8", "#334155", 1, 2),
            FlatShape(E(family, style, 19), "cable-gland", "core.ellipse", 73, 1, 10, 8, "#71808A", "#334155", 1),
            FlatShape(E(family, style, 20), "nameplate", "core.rectangle", 72, 40, 22, 13, "#E7ECEF", "#52606A", 1, 2),
            Text(E(family, style, 15), "label", "M", 74, 40, 18, 13, 10, "#1F2937"),
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
                BezierShape(E(family, style, 2), "body-left", 31, 31, 34, 34,
                    "M 0 12 C 28 12 46 27 100 50 C 46 73 28 88 0 88 Z",
                    "#C5CDD3", "#374151", 2),
                BezierShape(E(family, style, 3), "body-right", 63, 31, 34, 34,
                    "M 100 12 C 72 12 54 27 0 50 C 54 73 72 88 100 88 Z",
                    "#C5CDD3", "#374151", 2),
                FlatShape(E(family, style, 4), "pipe-right", "core.rectangle", 94, 44, 31, 9, "#9CA3AF", "#4B5563", 1, 2),
                FlatShape(E(family, style, 5), "stem", "core.rectangle", 61, 20, 5, 17, "#6B7280", "#374151", 1),
                BezierShape(E(family, style, 6), "actuator", 49, 4, 29, 19,
                    "M 8 18 C 8 7 22 3 50 3 C 78 3 92 7 92 18 L 92 82 C 92 93 78 97 50 97 C 22 97 8 93 8 82 Z",
                    "#D1D5DB", "#374151", 2),
                FlatShape(E(family, style, 9), "bonnet", "core.ellipse", 56, 18, 15, 10, "#AEB7BE", "#374151", 1),
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
            BezierShape(E(family, style, 3), "body-left", 39, 38, 43, 43,
                "M 0 10 C 26 10 46 26 100 50 C 46 74 26 90 0 90 Z",
                "#B8C4CF", "#334155", 2, dimensional ? "#F8FAFC" : null, "diagonal-down", dimensional),
            BezierShape(E(family, style, 4), "body-right", 80, 38, 43, 43,
                "M 100 10 C 74 10 54 26 0 50 C 54 74 74 90 100 90 Z",
                "#B8C4CF", "#334155", 2, dimensional ? "#F8FAFC" : null, "diagonal-up", dimensional),
            MaterialShape(E(family, style, 5), "flange-right", "core.rectangle", 124, 46, 10, 28, "#94A3B8", "#DDE4EA", "#334155", 2, 2, dimensional, "horizontal"),
            MaterialShape(E(family, style, 6), "pipe-right", "core.rectangle", 132, 54, 30, 12, "#AAB8C5", "#F8FAFC", "#475569", 1, 3, dimensional, "vertical"),
            FlatShape(E(family, style, 7), "stem", "core.rectangle", 79, 27, 5, 17, "#64748B", "#334155", 1, 1),
            BezierShape(E(family, style, 8), "actuator", 62, 4, 39, 25,
                "M 7 18 C 7 7 22 3 50 3 C 78 3 93 7 93 18 L 93 82 C 93 93 78 97 50 97 C 22 97 7 93 7 82 Z",
                "#94A3B8", "#334155", 2, dimensional ? "#DDE4EA" : null, "vertical", dimensional),
            FlatShape(E(family, style, 9), "actuator-top", "core.rectangle", 69, 0, 25, 6, "#475569", "#334155", 1, 2),
            MaterialShape(E(family, style, 12), "bonnet", "core.ellipse", 71, 24, 21, 13,
                "#AAB8C5", "#E4EBEF", "#334155", 1.5, 0, dimensional, "vertical"),
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
                BezierShape(E(family, style, 2), "body-left", 31, 47, 34, 34,
                    "M 0 11 C 28 11 46 26 100 50 C 46 74 28 89 0 89 Z",
                    "#C5CDD3", "#374151", 2),
                BezierShape(E(family, style, 3), "body-right", 63, 47, 34, 34,
                    "M 100 11 C 72 11 54 26 0 50 C 54 74 72 89 100 89 Z",
                    "#C5CDD3", "#374151", 2),
                FlatShape(E(family, style, 4), "pipe-right", "core.rectangle", 94, 60, 31, 9, "#9CA3AF", "#4B5563", 1, 2),
                FlatShape(E(family, style, 5), "stem", "core.rectangle", 61, 29, 5, 24, "#6B7280", "#374151", 1),
                BezierShape(E(family, style, 6), "actuator", 43, 4, 42, 28,
                    "M 4 50 C 7 18 25 5 50 5 C 75 5 93 18 96 50 C 93 82 75 95 50 95 C 25 95 7 82 4 50 Z",
                    "#D1D5DB", "#374151", 2),
                Text(E(family, style, 7), "label", "%", 51, 8, 26, 20, 10, "#111827"),
                FlatShape(E(family, style, 9), "bonnet", "core.ellipse", 55, 26, 18, 11, "#AEB7BE", "#374151", 1),
                StateLamp(E(family, style, 8), "fault", 104, 5, "#DC2626", "fault", "{equipmentPath}.Fault")
            ],
            parameters: ControlValveParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.valve.control", "Válvula de controle", "valve", style, 166, 136,
        [
            MaterialShape(E(family, style, 1), "pipe-left", "core.rectangle", 2, 78, 44, 12, "#AAB8C5", "#F8FAFC", "#475569", 1, 3, dimensional, "vertical"),
            MaterialShape(E(family, style, 2), "flange-left", "core.rectangle", 30, 69, 10, 30, "#94A3B8", "#DDE4EA", "#334155", 2, 2, dimensional, "horizontal"),
            BezierShape(E(family, style, 3), "body-left", 39, 60, 43, 43,
                "M 0 10 C 26 10 46 25 100 50 C 46 75 26 90 0 90 Z",
                "#B8C4CF", "#334155", 2, dimensional ? "#F8FAFC" : null, "diagonal-down", dimensional),
            BezierShape(E(family, style, 4), "body-right", 80, 60, 43, 43,
                "M 100 10 C 74 10 54 25 0 50 C 54 75 74 90 100 90 Z",
                "#B8C4CF", "#334155", 2, dimensional ? "#F8FAFC" : null, "diagonal-up", dimensional),
            MaterialShape(E(family, style, 5), "flange-right", "core.rectangle", 124, 69, 10, 30, "#94A3B8", "#DDE4EA", "#334155", 2, 2, dimensional, "horizontal"),
            MaterialShape(E(family, style, 6), "pipe-right", "core.rectangle", 132, 78, 32, 12, "#AAB8C5", "#F8FAFC", "#475569", 1, 3, dimensional, "vertical"),
            FlatShape(E(family, style, 7), "stem", "core.rectangle", 79, 43, 5, 24, "#64748B", "#334155", 1, 1),
            BezierShape(E(family, style, 8), "actuator", 52, 9, 60, 37,
                "M 4 50 C 7 18 25 5 50 5 C 75 5 93 18 96 50 C 93 82 75 95 50 95 C 25 95 7 82 4 50 Z",
                "#AEBCC8", "#334155", 2, dimensional ? "#F8FAFC" : null, "vertical", dimensional),
            FlatShape(E(family, style, 9), "actuator-cap", "core.rectangle", 68, 4, 28, 7, "#475569", "#334155", 1, 2),
            MaterialShape(E(family, style, 12), "bonnet", "core.ellipse", 71, 42, 21, 14,
                "#AAB8C5", "#E4EBEF", "#334155", 1.5, 0, dimensional, "vertical"),
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
                FlatShape(E(family, style, 2), "liquid", "core.rectangle", 23, 77, 62, 65, "#AAB2B8", "#6B7280", 1, 10),
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
                FlatShape(E(family, style, 2), "liquid", "core.rectangle", 24, 50, 120, 28, "#AAB2B8", "#6B7280", 1, 14),
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
            return Dynamo(sequence, "process.blower.centrifugal", "Soprador centrífugo", "compressor", style, 176, 132,
            [
                FlatShape(E(family, style, 1), "outlet-pipe", "core.rectangle", 114, 16, 48, 20, "#D8E0E8", "#263746", 2, 3),
                FlatShape(E(family, style, 2), "outlet-neck", "core.rectangle", 101, 29, 25, 39, "#C5CFD9", "#263746", 2, 4),
                FlatShape(E(family, style, 3), "outlet-flange", "core.rectangle", 154, 12, 10, 28, "#EEF2F6", "#263746", 2, 2),
                FlatShape(E(family, style, 4), "inlet-pipe", "core.rectangle", 4, 66, 54, 20, "#D8E0E8", "#263746", 2, 3),
                FlatShape(E(family, style, 5), "inlet-flange", "core.rectangle", 38, 59, 12, 34, "#EEF2F6", "#263746", 2, 2),
                FlatShape(E(family, style, 6), "casing", "core.ellipse", 43, 30, 94, 94, "#D8E0E8", "#263746", 3),
                FlatShape(E(family, style, 7), "casing-rim", "core.ellipse", 50, 37, 80, 80, "#F8FAFC", "#546879", 2),
                FlatShape(E(family, style, 8), "impeller-recess", "core.ellipse", 60, 47, 60, 60, "#263746", "#17232D", 2),
                RotorBlade(E(family, style, 9), "impeller-blade-1", 90, 77, 11, 26, 0, "#AEBBC7", "#263746", 1),
                RotorBlade(E(family, style, 10), "impeller-blade-2", 90, 77, 11, 26, 60, "#AEBBC7", "#263746", 1),
                RotorBlade(E(family, style, 11), "impeller-blade-3", 90, 77, 11, 26, 120, "#AEBBC7", "#263746", 1),
                RotorBlade(E(family, style, 12), "impeller-blade-4", 90, 77, 11, 26, 180, "#AEBBC7", "#263746", 1),
                RotorBlade(E(family, style, 13), "impeller-blade-5", 90, 77, 11, 26, 240, "#AEBBC7", "#263746", 1),
                RotorBlade(E(family, style, 14), "impeller-blade-6", 90, 77, 11, 26, 300, "#AEBBC7", "#263746", 1),
                FlatShape(E(family, style, 15), "hub", "core.ellipse", 78, 65, 24, 24, "#F8FAFC", "#263746", 2),
                FlatShape(E(family, style, 16), "hub-cap", "core.ellipse", 85, 72, 10, 10, "#64748B", "#263746", 1),
                FlatShape(E(family, style, 17), "base-left-foot", "core.rectangle", 57, 114, 17, 9, "#7B8996", "#263746", 1, 2),
                FlatShape(E(family, style, 18), "base-right-foot", "core.rectangle", 106, 114, 17, 9, "#7B8996", "#263746", 1, 2),
                FlatShape(E(family, style, 19), "base", "core.rectangle", 45, 122, 90, 8, "#445565", "#263746", 1, 2),
                Polygon(E(family, style, 20), "outlet-flow-arrow", 135, 18, 12, 14, [(0d, 0d), (12d, 7d), (0d, 14d)], "#1877A8", "#125575", 1),
                Text(E(family, style, 21), "label", "B", 81, 68, 18, 18, 10, "#17232D"),
                StateLamp(E(family, style, 22), "running", 7, 7, "#16A34A", "running", "{equipmentPath}.Running"),
                StateLamp(E(family, style, 23), "fault", 158, 7, "#DC2626", "fault", "{equipmentPath}.Fault")
            ],
            parameters: BlowerParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        if (dimensional)
        {
            return Dynamo(sequence, "process.blower.centrifugal", "Soprador centrífugo", "compressor", style, 196, 146,
            [
                MaterialShape(E(family, style, 1), "outlet-pipe", "core.rectangle", 123, 17, 58, 22, "#91A7B9", "#F8FAFC", "#30485A", 2, 5, true, "vertical", true),
                MaterialShape(E(family, style, 2), "outlet-neck", "core.rectangle", 106, 30, 31, 45, "#7892A7", "#E4ECF2", "#30485A", 2, 6, true, "horizontal", true),
                MaterialShape(E(family, style, 3), "outlet-flange", "core.rectangle", 174, 13, 12, 31, "#B6C5D1", "#F8FAFC", "#30485A", 2, 3, true, "horizontal"),
                MaterialShape(E(family, style, 4), "inlet-pipe", "core.rectangle", 4, 70, 65, 22, "#91A7B9", "#F8FAFC", "#30485A", 2, 5, true, "vertical", true),
                MaterialShape(E(family, style, 5), "inlet-flange", "core.rectangle", 48, 62, 15, 38, "#B6C5D1", "#F8FAFC", "#30485A", 2, 3, true, "horizontal"),
                MaterialShape(E(family, style, 6), "volute-case", "core.ellipse", 44, 31, 102, 102, "#7892A7", "#E5EDF3", "#30485A", 3, 0, true, "diagonal-down", true),
                MaterialShape(E(family, style, 7), "case-cover", "core.ellipse", 51, 38, 88, 88, "#B5C6D3", "#F8FAFC", "#597184", 2, 0, true, "diagonal-up"),
                MaterialShape(E(family, style, 8), "impeller-recess", "core.ellipse", 62, 49, 66, 66, "#354E61", "#7892A7", "#30485A", 2, 0, true, "diagonal-down"),
                RotorBlade(E(family, style, 9), "impeller-blade-1", 95, 82, 12, 29, 0, "#AFC4D2", "#243B4A", 1),
                RotorBlade(E(family, style, 10), "impeller-blade-2", 95, 82, 12, 29, 60, "#AFC4D2", "#243B4A", 1),
                RotorBlade(E(family, style, 11), "impeller-blade-3", 95, 82, 12, 29, 120, "#AFC4D2", "#243B4A", 1),
                RotorBlade(E(family, style, 12), "impeller-blade-4", 95, 82, 12, 29, 180, "#AFC4D2", "#243B4A", 1),
                RotorBlade(E(family, style, 13), "impeller-blade-5", 95, 82, 12, 29, 240, "#AFC4D2", "#243B4A", 1),
                RotorBlade(E(family, style, 14), "impeller-blade-6", 95, 82, 12, 29, 300, "#AFC4D2", "#243B4A", 1),
                MaterialShape(E(family, style, 15), "hub", "core.ellipse", 79, 66, 32, 32, "#CBD9E3", "#FFFFFF", "#30485A", 2, 0, true, "diagonal-down", true),
                FlatShape(E(family, style, 16), "hub-cap", "core.ellipse", 89, 76, 12, 12, "#547084", "#243B4A", 1),
                FlatShape(E(family, style, 17), "bolt-1", "core.ellipse", 88, 35, 5, 5, "#F8FAFC", "#526B7D", 1),
                FlatShape(E(family, style, 18), "bolt-2", "core.ellipse", 125, 53, 5, 5, "#F8FAFC", "#526B7D", 1),
                FlatShape(E(family, style, 19), "bolt-3", "core.ellipse", 126, 99, 5, 5, "#F8FAFC", "#526B7D", 1),
                FlatShape(E(family, style, 20), "bolt-4", "core.ellipse", 89, 121, 5, 5, "#F8FAFC", "#526B7D", 1),
                FlatShape(E(family, style, 21), "bolt-5", "core.ellipse", 54, 99, 5, 5, "#F8FAFC", "#526B7D", 1),
                FlatShape(E(family, style, 22), "bolt-6", "core.ellipse", 53, 55, 5, 5, "#F8FAFC", "#526B7D", 1),
                FlatShape(E(family, style, 23), "foot-left", "core.rectangle", 62, 125, 18, 9, "#667F91", "#30485A", 1, 2),
                FlatShape(E(family, style, 24), "foot-right", "core.rectangle", 112, 125, 18, 9, "#667F91", "#30485A", 1, 2),
                FlatShape(E(family, style, 25), "base", "core.rectangle", 49, 134, 96, 8, "#435B6D", "#30485A", 1, 2),
                Polygon(E(family, style, 26), "outlet-flow-arrow", 145, 22, 15, 14, [(0d, 0d), (15d, 7d), (0d, 14d)], "#1687B4", "#125575", 1),
                Text(E(family, style, 27), "label", "B", 84, 73, 22, 20, 11, "#243B4A"),
                StateLamp(E(family, style, 28), "running", 7, 7, "#22C55E", "running", "{equipmentPath}.Running"),
                StateLamp(E(family, style, 29), "fault", 177, 7, "#EF4444", "fault", "{equipmentPath}.Fault")
            ],
            parameters: BlowerParameters());
        }

        return Dynamo(sequence, "process.blower.centrifugal", "Soprador centrífugo", "compressor", style, 196, 146,
        [
            FlatShape(E(family, style, 1), "outlet-pipe", "core.rectangle", 123, 17, 58, 22, "#A8B4BF", "#273746", 2, 4),
            FlatShape(E(family, style, 2), "outlet-neck", "core.rectangle", 106, 30, 31, 45, "#8799A8", "#273746", 2, 4),
            FlatShape(E(family, style, 3), "outlet-flange", "core.rectangle", 174, 13, 12, 31, "#D3DCE4", "#273746", 2, 2),
            FlatShape(E(family, style, 4), "inlet-pipe", "core.rectangle", 4, 70, 65, 22, "#A8B4BF", "#273746", 2, 4),
            FlatShape(E(family, style, 5), "inlet-flange", "core.rectangle", 48, 62, 15, 38, "#D3DCE4", "#273746", 2, 2),
            FlatShape(E(family, style, 6), "volute-case", "core.ellipse", 44, 31, 102, 102, "#8999A7", "#273746", 3),
            FlatShape(E(family, style, 7), "case-cover", "core.ellipse", 51, 38, 88, 88, "#D3DCE4", "#526575", 2),
            FlatShape(E(family, style, 8), "impeller-recess", "core.ellipse", 62, 49, 66, 66, "#405363", "#273746", 2),
            RotorBlade(E(family, style, 9), "impeller-blade-1", 95, 82, 12, 29, 0, "#A8B4BF", "#273746", 1),
            RotorBlade(E(family, style, 10), "impeller-blade-2", 95, 82, 12, 29, 60, "#A8B4BF", "#273746", 1),
            RotorBlade(E(family, style, 11), "impeller-blade-3", 95, 82, 12, 29, 120, "#A8B4BF", "#273746", 1),
            RotorBlade(E(family, style, 12), "impeller-blade-4", 95, 82, 12, 29, 180, "#A8B4BF", "#273746", 1),
            RotorBlade(E(family, style, 13), "impeller-blade-5", 95, 82, 12, 29, 240, "#A8B4BF", "#273746", 1),
            RotorBlade(E(family, style, 14), "impeller-blade-6", 95, 82, 12, 29, 300, "#A8B4BF", "#273746", 1),
            FlatShape(E(family, style, 15), "hub", "core.ellipse", 79, 66, 32, 32, "#EEF2F6", "#273746", 2),
            FlatShape(E(family, style, 16), "hub-cap", "core.ellipse", 89, 76, 12, 12, "#657A8A", "#273746", 1),
            FlatShape(E(family, style, 17), "bolt-1", "core.ellipse", 88, 35, 5, 5, "#F8FAFC", "#526575", 1),
            FlatShape(E(family, style, 18), "bolt-2", "core.ellipse", 125, 53, 5, 5, "#F8FAFC", "#526575", 1),
            FlatShape(E(family, style, 19), "bolt-3", "core.ellipse", 126, 99, 5, 5, "#F8FAFC", "#526575", 1),
            FlatShape(E(family, style, 20), "bolt-4", "core.ellipse", 89, 121, 5, 5, "#F8FAFC", "#526575", 1),
            FlatShape(E(family, style, 21), "bolt-5", "core.ellipse", 54, 99, 5, 5, "#F8FAFC", "#526575", 1),
            FlatShape(E(family, style, 22), "bolt-6", "core.ellipse", 53, 55, 5, 5, "#F8FAFC", "#526575", 1),
            FlatShape(E(family, style, 23), "foot-left", "core.rectangle", 62, 125, 18, 9, "#647789", "#273746", 1, 2),
            FlatShape(E(family, style, 24), "foot-right", "core.rectangle", 112, 125, 18, 9, "#647789", "#273746", 1, 2),
            FlatShape(E(family, style, 25), "base", "core.rectangle", 49, 134, 96, 8, "#445767", "#273746", 1, 2),
            Polygon(E(family, style, 26), "outlet-flow-arrow", 145, 22, 15, 14, [(0d, 0d), (15d, 7d), (0d, 14d)], "#13799D", "#125575", 1),
            Text(E(family, style, 27), "label", "B", 84, 73, 22, 20, 11, "#243B4A"),
            StateLamp(E(family, style, 28), "running", 7, 7, "#22C55E", "running", "{equipmentPath}.Running"),
            StateLamp(E(family, style, 29), "fault", 177, 7, "#EF4444", "fault", "{equipmentPath}.Fault")
        ],
        parameters: BlowerParameters());
    }

    private static VisualElementEngineeringDto RotorBlade(
        int sequence,
        string key,
        double centerX,
        double centerY,
        double innerRadius,
        double outerRadius,
        double angleDegrees,
        string fill,
        string stroke,
        double strokeWidth)
    {
        var angle = angleDegrees * Math.PI / 180d;
        var radialX = Math.Cos(angle);
        var radialY = Math.Sin(angle);
        var tangentX = -radialY;
        var tangentY = radialX;
        var points = new[]
        {
            (centerX + innerRadius * radialX - 4 * tangentX, centerY + innerRadius * radialY - 4 * tangentY),
            (centerX + outerRadius * radialX - tangentX, centerY + outerRadius * radialY - tangentY),
            (centerX + outerRadius * radialX + 4 * tangentX, centerY + outerRadius * radialY + 4 * tangentY),
            (centerX + innerRadius * radialX + 4 * tangentX, centerY + innerRadius * radialY + 4 * tangentY)
        };
        var minX = points.Min(point => point.Item1);
        var maxX = points.Max(point => point.Item1);
        var minY = points.Min(point => point.Item2);
        var maxY = points.Max(point => point.Item2);
        var localPoints = points.Select(point => (point.Item1 - minX, point.Item2 - minY)).ToArray();
        return Polygon(sequence, key, minX, minY, maxX - minX, maxY - minY, localPoints, fill, stroke, strokeWidth);
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

    private static DynamoEngineeringDto AdditionalFamily(
        VisualStyle style,
        int family,
        string familyKey,
        string name,
        string category,
        int width,
        int height)
    {
        var sequence = DefinitionSequence(family, style);
        var highPerformance = style == VisualStyle.HighPerformance;
        var dimensional = style == VisualStyle.DimensionalFront;
        var shell = highPerformance ? "#AEB7BE" : dimensional ? "#AFC0CC" : "#91A5B5";
        var light = highPerformance ? "#D5DBDF" : dimensional ? "#E7EEF3" : "#C7D4DE";
        var dark = highPerformance ? "#58636B" : "#34495A";
        var accent = highPerformance ? "#7F8A91" : "#4785A6";
        var shapes = new List<VisualElementEngineeringDto>();
        var centerX = width / 2d;
        var centerY = height / 2d;

        bool PrimaryMass(string key) =>
            key is "crankcase" or "compressor-housing" or "body-ring" or "body" or "shell" or
                "filter-body" or "vessel" or "tank" or "interrupter" or "stator" or "core" or "operating-box" ||
            key.StartsWith("support-", StringComparison.Ordinal);

        void Rect(string key, double x, double y, double w, double h, string fill, double radius = 0, double stroke = 2) =>
            shapes.Add(MaterialShape(E(family, style, shapes.Count + 1), key, "core.rectangle", x, y, w, h,
                fill, highPerformance ? fill : light, dark, stroke, radius, dimensional, "diagonal-down",
                dimensional && PrimaryMass(key)));
        void Ellipse(string key, double x, double y, double w, double h, string fill, double stroke = 2) =>
            shapes.Add(MaterialShape(E(family, style, shapes.Count + 1), key, "core.ellipse", x, y, w, h,
                fill, highPerformance ? fill : light, dark, stroke, 0, dimensional, "diagonal-down",
                dimensional && PrimaryMass(key)));
        void Bar(string key, double x, double y, double w, double h, string fill, double rotation = 0) =>
            shapes.Add(FlatShape(E(family, style, shapes.Count + 1), key, "core.rectangle", x, y, w, h, fill, dark, 1.4, 1.5, rotation));
        void Label(string text, double x, double y, double w, double h, double size = 13) =>
            shapes.Add(Text(E(family, style, shapes.Count + 1), "equipment-label", text, x, y, w, h, size, "#17232D"));
        void Lamp(string key, double x, double y, string parameter, string target, string color) =>
            shapes.Add(StateLamp(E(family, style, shapes.Count + 1), key, x, y, color, parameter, target));
        void Triangle(string key, double x, double y, double w, double h, bool left, string fill) =>
            shapes.Add(Polygon(E(family, style, shapes.Count + 1), key, x, y, w, h,
                left ? [(0d, 0d), (w, h / 2), (0d, h)] : [(w, 0d), (0d, h / 2), (w, h)], fill, dark, 2,
                dimensional ? light : null, "diagonal-down", dimensional));

        switch (familyKey)
        {
            case "process.compressor.reciprocating":
                Rect("base", 14, height - 20, width - 28, 9, dark, 2);
                Rect("crankcase", 25, 62, 52, 32, shell, 7, 2.5);
                Rect("cylinder-left", 34, 24, 20, 42, light, 4);
                Rect("cylinder-right", 62, 24, 20, 42, shell, 4);
                Ellipse("head-left", 31, 17, 26, 14, accent);
                Ellipse("head-right", 59, 17, 26, 14, accent);
                Rect("discharge", 79, 29, 39, 9, shell, 2);
                Rect("inlet", 6, 39, 30, 9, shell, 2);
                for (var index = 0; index < (highPerformance ? 2 : 4); index++)
                    Bar($"cooling-fin-{index + 1}", 38 + index * (highPerformance ? 18 : 9), 29, 2, 24, dark);
                Label("C", 41, 69, 25, 18, 12);
                Lamp("running", 5, 5, "running", "{equipmentPath}.Running", "#D92D20");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                break;
            case "process.compressor.screw":
                Rect("base", 12, height - 18, width - 24, 8, dark, 2);
                Rect("compressor-housing", 29, 25, 88, 62, shell, 15, 2.5);
                Ellipse("rotor-left", 42, 36, 34, 38, light);
                Ellipse("rotor-right", 68, 36, 34, 38, highPerformance ? "#8C969D" : "#A5B9C8");
                Rect("inlet", 6, 46, 31, 10, shell, 2);
                Rect("outlet", 108, 38, 45, 10, shell, 2);
                Label("SC", 59, 79, 28, 15, 10);
                Lamp("running", 5, 5, "running", "{equipmentPath}.Running", "#D92D20");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                break;
            case "process.valve.butterfly":
                Rect("pipe-left", 3, 51, 46, 10, shell, 2);
                Rect("pipe-right", width - 48, 51, 45, 10, shell, 2);
                Rect("flange-left", 30, 43, 8, 26, light, 1);
                Rect("flange-right", width - 38, 43, 8, 26, light, 1);
                Ellipse("body-ring", 39, 30, 64, 54, highPerformance ? "#E0E4E7" : shell, 3);
                Ellipse("disc", 48, 35, 46, 44, accent, 2.5);
                Bar("disc-edge", 68, 32, 6, 50, dark, -22);
                Bar("shaft", 69, 16, 5, 19, dark);
                Rect("actuator", 56, 4, 31, 15, light, 4);
                Lamp("open", 5, 5, "open", "{equipmentPath}.Open", "#16A34A");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                break;
            case "process.valve.ball":
                Rect("pipe-left", 3, 51, 45, 10, shell, 2);
                Rect("pipe-right", width - 48, 51, 45, 10, shell, 2);
                Rect("flange-left", 30, 43, 8, 26, light, 1);
                Rect("flange-right", width - 38, 43, 8, 26, light, 1);
                Ellipse("body", centerX - 30, 33, 60, 46, shell, 2.5);
                Ellipse("ball", centerX - 18, 39, 36, 34, highPerformance ? "#727D84" : accent, 2);
                Bar("bore", centerX - 16.5, 54, 33, 5, "#F5F7F8");
                Bar("stem", centerX - 2.5, 18, 5, 23, dark);
                Bar("handle", centerX - 17, 13, 34, 6, accent, -20);
                Lamp("open", 5, 5, "open", "{equipmentPath}.Open", "#16A34A");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                break;
            case "process.valve.gate":
                Rect("pipe-left", 3, 83, 45, 10, shell, 2);
                Rect("pipe-right", width - 48, 83, 45, 10, shell, 2);
                Rect("flange-left", 30, 75, 8, 26, light, 1);
                Rect("flange-right", width - 38, 75, 8, 26, light, 1);
                Triangle("body-left", centerX - 31, 69, 31, 38, true, shell);
                Triangle("body-right", centerX, 69, 31, 38, false, shell);
                Bar("stem", centerX - 2.5, 34, 5, 38, dark);
                Ellipse("handwheel", centerX - 19, 4, 38, 32, highPerformance ? "#D5DBDF" : accent, 2);
                Ellipse("handwheel-hub", centerX - 4, 16, 8, 8, light, 1);
                Lamp("open", 5, 5, "open", "{equipmentPath}.Open", "#16A34A");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                break;
            case "process.exchanger.shell-tube":
                Rect("shell", 25, 35, 111, 56, shell, 22, 2.5);
                Ellipse("head-left", 17, 35, 28, 56, light);
                Ellipse("head-right", 119, 35, 28, 56, light);
                for (var index = 0; index < (highPerformance ? 3 : 5); index++)
                    Bar($"tube-{index + 1}", 45, 48 + index * (highPerformance ? 14 : 8), 71, 2, dark);
                Rect("nozzle-hot-in", 49, 15, 10, 24, accent, 2);
                Rect("nozzle-hot-out", 100, 86, 10, 25, accent, 2);
                Rect("nozzle-cold-in", 51, 87, 9, 25, shell, 2);
                Rect("nozzle-cold-out", 101, 14, 9, 25, shell, 2);
                Label("E", 70, 55, 24, 18, 12);
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                break;
            case "process.filter.strainer":
                Rect("pipe-left", 3, 35, 48, 10, shell, 2);
                Rect("pipe-right", 94, 35, 45, 10, shell, 2);
                Rect("filter-body", 39, 20, 61, 36, shell, 6, 2.5);
                Triangle("basket", 54, 52, 43, 47, false, light);
                for (var index = 0; index < (highPerformance ? 2 : 4); index++)
                    Bar($"basket-slot-{index + 1}", 62 + index * (highPerformance ? 14 : 7), 65, 2, 24, dark, -22);
                Ellipse("cap", 51, 91, 49, 12, accent);
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                break;
            case "process.mixer.agitator":
                Rect("vessel", 26, 65, 79, 88, highPerformance ? "#D5DBDF" : shell, 18, 2.5);
                Ellipse("tank-top", 26, 58, 79, 23, light, 2);
                Rect("liquid", 33, 98, 65, 47, highPerformance ? "#AEB7BE" : "#74B6CC", 13, 1);
                Rect("motor", 47, 15, 39, 27, accent, 5);
                Rect("shaft", 64, 41, 5, 82, dark);
                Bar("impeller", 43, 117, 49, 6, dark);
                Bar("blade-left", 45, 111, 5, 24, dark, -28);
                Bar("blade-right", 85, 111, 5, 24, dark, 28);
                Label("MX", 51, 21, 31, 14, 10);
                Lamp("running", 5, 5, "running", "{equipmentPath}.Running", "#D92D20");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                break;
            case "electrical.transformer.power":
                Rect("base", 20, height - 18, width - 40, 8, dark, 2);
                Rect("tank", 39, 43, 72, 83, shell, 6, 2.5);
                Rect("cover", 34, 36, 82, 12, light, 2);
                for (var index = 0; index < (highPerformance ? 3 : 5); index++)
                    Rect($"radiator-{index + 1}", 18 + index * (highPerformance ? 8 : 4), 59, 4, 52, accent, 1, 1);
                for (var index = 0; index < (highPerformance ? 3 : 5); index++)
                    Rect($"radiator-r-{index + 1}", 112 + index * (highPerformance ? 8 : 4), 59, 4, 52, accent, 1, 1);
                Rect("bushing-left", 53, 10, 11, 29, light, 3);
                Rect("bushing-right", 84, 10, 11, 29, light, 3);
                Label("T", 59, 72, 31, 22, 15);
                Lamp("fault", width - 22, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                break;
            case "electrical.breaker":
                Rect("base", 17, height - 18, width - 34, 8, dark, 2);
                Rect("support-left", 33, 70, 10, 61, shell, 2);
                Rect("support-right", 88, 70, 10, 61, shell, 2);
                Rect("interrupter", 38, 37, 56, 50, light, 7, 2.5);
                Bar("contact-left", 62, 17, 7, 25, dark);
                Bar("contact-right", 73, 17, 7, 25, dark);
                Ellipse("terminal-left", 56, 5, 18, 16, accent);
                Ellipse("terminal-right", 71, 5, 18, 16, accent);
                Label("52", 49, 50, 35, 18, 12);
                Lamp("closed", 6, 5, "closed", "{equipmentPath}.Closed", "#16A34A");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                break;
            case "electrical.disconnector":
            case "electrical.earthing-switch":
                Rect("support-left", 25, 64, 10, 49, shell, 2);
                Rect("support-right", width - 36, 64, 10, 49, shell, 2);
                Ellipse("insulator-left-top", 20, 53, 20, 15, light);
                Ellipse("insulator-right-top", width - 41, 53, 20, 15, light);
                Ellipse("contact-left", 26, 39, 16, 16, accent);
                Ellipse("contact-right", width - 42, 39, 16, 16, accent);
                Bar("blade", 34, 41, width - 61, 8, highPerformance ? "#646E75" : "#738D9F", familyKey.EndsWith("earthing-switch", StringComparison.Ordinal) ? 24 : -18);
                Rect("base", 13, 113, width - 26, 8, dark, 2);
                if (familyKey.EndsWith("earthing-switch", StringComparison.Ordinal))
                {
                    Bar("earth-lead", centerX - 3, 80, 6, 36, accent);
                    Triangle("ground-1", centerX - 15, 94, 30, 12, true, accent);
                    Triangle("ground-2", centerX - 10, 103, 20, 8, true, accent);
                }
                else
                {
                    Rect("operating-box", centerX - 17, 75, 34, 24, light, 3);
                    Label("89", centerX - 15, 78, 30, 15, 10);
                }
                Lamp("closed", 5, 5, "closed", "{equipmentPath}.Closed", "#16A34A");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                break;
            case "electrical.generator":
                Rect("base", 23, height - 19, width - 43, 8, dark, 2);
                Ellipse("stator", 24, 20, 90, 76, shell, 3);
                Ellipse("rotor", 41, 34, 56, 48, light, 2);
                Ellipse("hub", 59, 49, 20, 18, accent, 1);
                Rect("shaft", 107, 53, 34, 9, dark, 2);
                for (var index = 0; index < (highPerformance ? 3 : 5); index++)
                    Bar($"stator-slot-{index + 1}", 40 + index * (highPerformance ? 20 : 12), 27, 3, 9, dark);
                Label("G", 55, 48, 31, 20, 13);
                Lamp("running", 5, 5, "running", "{equipmentPath}.Running", "#D92D20");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                break;
            case "electrical.current-transformer":
                Rect("primary-conductor", 51, 3, 10, height - 6, dark, 2);
                Ellipse("core", 22, 43, 68, 68, highPerformance ? "#C6CDD2" : shell, 3);
                Ellipse("core-window", 39, 60, 34, 34, "#F5F7F8", 2);
                Rect("secondary-left", 13, 92, 23, 6, accent, 2);
                Rect("secondary-right", 76, 92, 23, 6, accent, 2);
                Label("TC", 39, 114, 34, 21, 10);
                Lamp("fault", 5, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                break;
        }

        return Dynamo(sequence, familyKey, name, category, style, width, height, shapes,
            Parameters(EquipmentPathParameter(),
                TagParameter("running"), TagParameter("fault"), TagParameter("open"), TagParameter("closed"), CommandKeyParameter("commandKey")));
    }

    private static void AddDiscreteStateColorMap(
        List<VisualElementEngineeringDto> elements,
        string familyKey,
        VisualStyle style,
        string targetKey)
    {
        var index = elements.FindIndex(element => element.Key == targetKey);
        if (index < 0 && familyKey == "process.motor.vfd")
            index = elements.FindIndex(element => element.Key == "motor-body");
        if (index < 0 && familyKey == "process.blower.centrifugal")
            index = elements.FindIndex(element => element.Key == "volute-case");
        if (index < 0) return;
        var element = elements[index];
        // Some family targets intentionally fall back to the same visual part
        // (for example, both VFD aliases resolve to motor-body). Add the color
        // map only once when aliases land on that shared element.
        if (element.PropertyMaps?.Any(map => map.PropertyKey == "fillColor") == true)
            return;
        var properties = element.Properties is null ? new Dictionary<string, JsonElement>() : new Dictionary<string, JsonElement>(element.Properties);
        var stopped = DefaultStoppedColor(style);
        properties["fillColor"] = JsonSerializer.SerializeToElement(stopped);
        var source = new VisualValueSourceEngineeringDto(
            VisualValueSourceKind.Tag,
            VisualExpressionValueType.Number,
            Target: "{equipmentPath}.State");
        var stateMap = new VisualPropertyMapEngineeringDto(
            "fillColor",
            source,
            [
                new(JsonSerializer.SerializeToElement(stopped), Minimum: 0, Maximum: 1),
                new(JsonSerializer.SerializeToElement(DefaultRunningColor(style)), Minimum: 1, Maximum: 2),
                new(JsonSerializer.SerializeToElement(DefaultFaultColor(style)), Minimum: 2, Maximum: 3)
            ],
            JsonSerializer.SerializeToElement(stopped));
        var metadata = element.Metadata is null ? new Dictionary<string, string>() : new Dictionary<string, string>(element.Metadata);
        metadata["dynamoStateColorParameter"] = "state";
        metadata["dynamoStateColorProfile"] = "stopped,running,fault";
        metadata["dynamoStateColorFamily"] = familyKey;
        elements[index] = element with
        {
            Properties = properties,
            PropertyMaps = [.. element.PropertyMaps ?? [], stateMap],
            Metadata = metadata
        };
    }

    /// <summary>
    /// Centers each definition in its own logical viewBox and keeps a consistent
    /// margin. Families use different canvas proportions, so fitting the full
    /// assembly as a unit prevents clipping without distorting its components.
    /// </summary>
    private static void FitArtworkToCanvas(
        List<VisualElementEngineeringDto> elements,
        double canvasWidth,
        double canvasHeight,
        VisualStyle style)
    {
        var bounds = new List<(int Index, double X, double Y, double Width, double Height)>();
        for (var index = 0; index < elements.Count; index++)
        {
            var properties = elements[index].Properties;
            if (properties is null || !TryNumber(properties, "x", out var x) || !TryNumber(properties, "y", out var y) ||
                !TryNumber(properties, "width", out var width) || !TryNumber(properties, "height", out var height) || width <= 0 || height <= 0)
                continue;

            var rotation = TryNumber(properties, "rotation", out var angle) ? angle * Math.PI / 180 : 0;
            var rotatedWidth = Math.Abs(width * Math.Cos(rotation)) + Math.Abs(height * Math.Sin(rotation));
            var rotatedHeight = Math.Abs(width * Math.Sin(rotation)) + Math.Abs(height * Math.Cos(rotation));
            var centerX = x + width / 2;
            var centerY = y + height / 2;
            bounds.Add((index, centerX - rotatedWidth / 2, centerY - rotatedHeight / 2, rotatedWidth, rotatedHeight));
        }

        if (bounds.Count == 0) return;
        var left = bounds.Min(item => item.X);
        var top = bounds.Min(item => item.Y);
        var right = bounds.Max(item => item.X + item.Width);
        var bottom = bounds.Max(item => item.Y + item.Height);
        var artworkWidth = Math.Max(1, right - left);
        var artworkHeight = Math.Max(1, bottom - top);
        var targetFill = style switch
        {
            VisualStyle.HighPerformance => 0.92,
            VisualStyle.DimensionalFront => 0.88,
            _ => 0.90
        };
        var scale = Math.Min(canvasWidth * targetFill / artworkWidth, canvasHeight * targetFill / artworkHeight);
        var maximumScale = style == VisualStyle.DimensionalFront ? 1.12 : 1.18;
        scale = Math.Min(scale, maximumScale);
        foreach (var item in bounds)
        {
            var element = elements[item.Index];
            var properties = new Dictionary<string, JsonElement>(element.Properties!);
            var x = properties["x"].GetDouble();
            var y = properties["y"].GetDouble();
            var width = properties["width"].GetDouble();
            var height = properties["height"].GetDouble();
            var centerX = x + width / 2;
            var centerY = y + height / 2;
            var nextWidth = width * scale;
            var nextHeight = height * scale;
            properties["x"] = JsonSerializer.SerializeToElement(canvasWidth / 2 + (centerX - canvasWidth / 2) * scale - nextWidth / 2);
            properties["y"] = JsonSerializer.SerializeToElement(canvasHeight / 2 + (centerY - canvasHeight / 2) * scale - nextHeight / 2);
            properties["width"] = JsonSerializer.SerializeToElement(nextWidth);
            properties["height"] = JsonSerializer.SerializeToElement(nextHeight);

            foreach (var property in new[] { "fontSize", "strokeWidth", "cornerRadius", "shadowOffsetX", "shadowOffsetY", "shadowBlur" })
            {
                if (TryNumber(properties, property, out var value))
                    properties[property] = JsonSerializer.SerializeToElement(value * scale);
            }

            if (properties.TryGetValue("points", out var points) && points.ValueKind == JsonValueKind.Array)
            {
                var scaledPoints = points.EnumerateArray().Select(point => new Dictionary<string, double>
                {
                    ["x"] = point.TryGetProperty("x", out var pointX) && pointX.TryGetDouble(out var px) ? px * scale : 0,
                    ["y"] = point.TryGetProperty("y", out var pointY) && pointY.TryGetDouble(out var py) ? py * scale : 0
                }).ToArray();
                properties["points"] = JsonSerializer.SerializeToElement(scaledPoints);
            }

            elements[item.Index] = element with { Properties = properties };
        }
    }

    private static bool TryNumber(IReadOnlyDictionary<string, JsonElement> properties, string key, out double value)
    {
        if (properties.TryGetValue(key, out var json) && json.ValueKind == JsonValueKind.Number && json.TryGetDouble(out value))
            return true;
        value = 0;
        return false;
    }

    private static string DefaultStoppedColor(VisualStyle style) => style == VisualStyle.HighPerformance ? "#8FBF98" : "#16A34A";
    private static string DefaultRunningColor(VisualStyle style) => style == VisualStyle.HighPerformance ? "#D98282" : "#DC2626";
    private static string DefaultFaultColor(VisualStyle style) => style == VisualStyle.HighPerformance ? "#D8B95F" : "#EAB308";

    private static DynamoParameterDefinitionEngineeringDto StringParameter(string key, string defaultValue) =>
        new(key, DynamoParameterKind.String, DefaultValue: JsonSerializer.SerializeToElement(defaultValue));

    private static DynamoEngineeringDto Dynamo(
        int sequence,
        string familyKey,
        string familyName,
        string category,
        VisualStyle style,
        int width,
        int height,
        IReadOnlyCollection<VisualElementEngineeringDto> elements,
        IReadOnlyCollection<DynamoParameterDefinitionEngineeringDto>? parameters = null)
    {
        var originalElements = elements.ToArray();
        var details = VisualEnhancements(familyKey, sequence, style, width, height);
        var firstBoundElement = Array.FindIndex(originalElements, element => element.Bindings?.Count > 0);
        if (firstBoundElement < 0) firstBoundElement = originalElements.Length;
        var refinedElements = originalElements
            .Take(firstBoundElement)
            .Concat(details)
            .Concat(originalElements.Skip(firstBoundElement))
            .ToArray();

        var visualElements = refinedElements.ToList();
        FitArtworkToCanvas(visualElements, width, height, style);
        visualElements = visualElements
            .Select(element => ApplyArtworkFinish(element, style))
            .ToList();
        var stateColorTargets = StateColorTargets(familyKey);
        if (stateColorTargets is not null)
        {
            foreach (var stateColorTarget in stateColorTargets)
                AddDiscreteStateColorMap(visualElements, familyKey, style, stateColorTarget);
        }
        var publicParameters = (parameters ?? Array.Empty<DynamoParameterDefinitionEngineeringDto>()).ToList();
        if (stateColorTargets is not null)
        {
            AddParameterIfMissing(publicParameters, TagParameter("state"));
            AddParameterIfMissing(publicParameters, StringParameter("stoppedColor", DefaultStoppedColor(style)));
            AddParameterIfMissing(publicParameters, StringParameter("runningColor", DefaultRunningColor(style)));
            AddParameterIfMissing(publicParameters, StringParameter("faultColor", DefaultFaultColor(style)));
        }

        return new(
            DefinitionId(sequence),
            VariantKey(familyKey, style),
            VariantName(familyName, style),
            TemplateKey: null,
            Properties: new Dictionary<string, string>
            {
                ["category"] = category,
                ["defaultWidth"] = width.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["defaultHeight"] = height.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["libraryVersion"] = Version,
                ["visualStyle"] = StyleKey(style),
                ["visualFinish"] = FinishProfile(style).Key,
                ["visualReferenceProfile"] = "pid-inspired-native-vector-v1",
                ["artworkContract"] = "C-DYNAMO-ARTWORK-02"
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
                ["performanceProfile"] = style == VisualStyle.HighPerformance ? "high-performance" : "rich",
                ["visualFinish"] = FinishProfile(style).Key,
                ["visualReferenceProfile"] = "pid-inspired-native-vector-v1",
                ["visualReferencePolicy"] = "original-editable-geometry; third-party SVGs are not embedded",
                ["artworkContract"] = "C-DYNAMO-ARTWORK-02",
                ["stateTagProfile"] = stateColorTargets is null ? "none" : "0=stopped;1=running;2=fault",
                ["stateColorsEditable"] = stateColorTargets is null ? "false" : "true"
            },
            Parameters: publicParameters,
            Elements: visualElements);
    }

    private sealed record ArtworkFinish(
        string Key,
        string Deepest,
        string Dark,
        string Mid,
        string Shell,
        string Light,
        string Highlight,
        string Outline,
        string SoftOutline);

    private static ArtworkFinish FinishProfile(VisualStyle style) => style switch
    {
        VisualStyle.Detailed2D => new(
            "industrial-steel-2d-v5",
            "#24333E", "#3D5362", "#718795", "#A9BAC4", "#D7E2E8", "#F7F9FA", "#263B49", "#6D7D87"),
        VisualStyle.DimensionalFront => new(
            "soft-machined-steel-v5",
            "#203747", "#405C6C", "#7993A4", "#B5C9D4", "#E4EDF2", "#FCFDFD", "#294253", "#617887"),
        _ => new(
            "high-performance-neutral-v5",
            "#2F373C", "#49545B", "#7B858B", "#A8B1B6", "#D1D6D9", "#ECEFF0", "#343F45", "#687278")
    };

    private static VisualElementEngineeringDto ApplyArtworkFinish(
        VisualElementEngineeringDto element,
        VisualStyle style)
    {
        if (element.Properties is null) return element;

        var finish = FinishProfile(style);
        var properties = new Dictionary<string, JsonElement>(element.Properties);
        foreach (var property in new[] { "fillColor", "fillSecondaryColor", "strokeColor" })
        {
            if (!properties.TryGetValue(property, out var value) ||
                value.ValueKind != JsonValueKind.String ||
                !TryParseArtworkColor(value.GetString(), out var red, out var green, out var blue) ||
                !IsNeutralArtworkColor(red, green, blue))
                continue;

            var luminance = 0.2126 * red + 0.7152 * green + 0.0722 * blue;
            var normalized = property == "strokeColor"
                ? luminance < 128 ? finish.Outline : finish.SoftOutline
                : InterpolateArtworkMetal(finish, luminance);
            properties[property] = JsonSerializer.SerializeToElement(normalized);
        }

        // Only the dimensional illustration style uses soft elevation. The IEC-like 2D
        // and high-performance variants stay crisp and flat for dense operating screens.
        if (style == VisualStyle.DimensionalFront &&
            (element.Type is "core.rectangle" or "core.ellipse" or "core.polygon" or "core.bezier" or "core.arc") &&
            IsPrimaryDimensionalMass(element.Key) &&
            !properties.ContainsKey("shadowEnabled") &&
            properties.TryGetValue("width", out var shapeWidth) && shapeWidth.TryGetDouble(out var shapeWidthValue) &&
            properties.TryGetValue("height", out var shapeHeight) && shapeHeight.TryGetDouble(out var shapeHeightValue) &&
            shapeWidthValue * shapeHeightValue >= 650)
        {
            properties["shadowEnabled"] = JsonSerializer.SerializeToElement(true);
            properties["shadowColor"] = JsonSerializer.SerializeToElement("#1F34401F");
            properties["shadowOffsetX"] = JsonSerializer.SerializeToElement(1d);
            properties["shadowOffsetY"] = JsonSerializer.SerializeToElement(1.5d);
            properties["shadowBlur"] = JsonSerializer.SerializeToElement(1.5d);
        }

        if (properties.TryGetValue("strokeWidth", out var strokeWidth) &&
            strokeWidth.ValueKind == JsonValueKind.Number &&
            strokeWidth.TryGetDouble(out var width) && width > 0)
        {
            // Keep intended visual hierarchy while avoiding inconsistent fractional hairlines
            // and unusually heavy borders across the 72 library drawings.
            var (minimumStroke, maximumStroke) = style switch
            {
                VisualStyle.HighPerformance => (0.75d, 2d),
                VisualStyle.DimensionalFront => (0.75d, 2d),
                _ => (0.75d, 2d)
            };
            var polishedWidth = Math.Round(Math.Clamp(width, minimumStroke, maximumStroke) * 2, MidpointRounding.AwayFromZero) / 2;
            properties["strokeWidth"] = JsonSerializer.SerializeToElement(polishedWidth);
        }

        return element with { Properties = properties };
    }

    private static bool IsPrimaryDimensionalMass(string key) =>
        key is "casing" or "body" or "motor" or "motor-body" or "tank" or "shell" or "vessel" or
            "crankcase" or "compressor-housing" or "body-ring" or "filter-body" or "interrupter" or
            "stator" or "core" or "outer-case" or "operating-box" ||
        key.StartsWith("body-", StringComparison.Ordinal) ||
        key.StartsWith("support-", StringComparison.Ordinal);

    private static string InterpolateArtworkMetal(ArtworkFinish finish, double luminance)
    {
        var stops = new[]
        {
            (0d, finish.Deepest), (52d, finish.Dark), (104d, finish.Mid),
            (156d, finish.Shell), (208d, finish.Light), (255d, finish.Highlight)
        };
        var upper = Array.FindIndex(stops, stop => stop.Item1 >= luminance);
        if (upper <= 0) return upper == 0 ? stops[0].Item2 : stops[^1].Item2;

        var (lowLuminance, lowColor) = stops[upper - 1];
        var (highLuminance, highColor) = stops[upper];
        if (!TryParseArtworkColor(lowColor, out var lowRed, out var lowGreen, out var lowBlue) ||
            !TryParseArtworkColor(highColor, out var highRed, out var highGreen, out var highBlue))
            return lowColor;

        var ratio = (luminance - lowLuminance) / (highLuminance - lowLuminance);
        var red = (int)Math.Round(lowRed + (highRed - lowRed) * ratio);
        var green = (int)Math.Round(lowGreen + (highGreen - lowGreen) * ratio);
        var blue = (int)Math.Round(lowBlue + (highBlue - lowBlue) * ratio);
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    private static bool TryParseArtworkColor(
        string? value,
        out int red,
        out int green,
        out int blue)
    {
        red = green = blue = 0;
        if (value is null || value.Length != 7 || value[0] != '#') return false;
        if (!int.TryParse(value.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out red) ||
            !int.TryParse(value.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out green) ||
            !int.TryParse(value.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out blue))
            return false;
        return true;
    }

    private static bool IsNeutralArtworkColor(int red, int green, int blue) =>
        Math.Max(red, Math.Max(green, blue)) - Math.Min(red, Math.Min(green, blue)) <= 64;

    private static void AddParameterIfMissing(
        List<DynamoParameterDefinitionEngineeringDto> parameters,
        DynamoParameterDefinitionEngineeringDto parameter)
    {
        if (parameters.All(candidate => !candidate.Key.Equals(parameter.Key, StringComparison.OrdinalIgnoreCase)))
            parameters.Add(parameter);
    }

    private static IReadOnlyList<string>? StateColorTargets(string familyKey) => familyKey switch
    {
        "dynamo.pump.standard" => ["casing"],
        "process.pump.submersible" => ["body"],
        "process.motor.standard" => ["body"],
        "process.motor.vfd" => ["motor", "motor-body"],
        "process.blower.centrifugal" => ["casing", "volute-case"],
        "process.valve.onoff" or "process.valve.control" or "process.valve.gate" => ["body-left", "body-right"],
        "process.valve.ball" => ["ball"],
        "process.valve.butterfly" => ["disc"],
        "process.filter.strainer" => ["filter-body"],
        "process.compressor.reciprocating" => ["crankcase"],
        "process.compressor.screw" => ["compressor-housing"],
        "process.mixer.agitator" => ["vessel"],
        "electrical.transformer.power" => ["tank"],
        "electrical.breaker" => ["interrupter"],
        "electrical.disconnector" => ["blade", "operating-box"],
        "electrical.earthing-switch" => ["blade"],
        "electrical.generator" => ["stator"],
        _ => null
    };

    private static IReadOnlyCollection<VisualElementEngineeringDto> VisualEnhancements(
        string familyKey,
        int sequence,
        VisualStyle style,
        int width,
        int height)
    {
        var details = new List<VisualElementEngineeringDto>();
        // High-performance variants are intentionally sparse: the equipment
        // silhouette and state binding carry the information, not decorative
        // fasteners or machining marks.
        if (style == VisualStyle.HighPerformance)
            return details;

        var localSequence = 70;
        var centerX = width / 2d;

        void Dot(string key, double x, double y, double size = 5, string fill = "#DCE7EF", string stroke = "#526879") =>
            details.Add(FlatShape(1000 + sequence * 100 + localSequence++, $"detail-{key}", "core.ellipse",
                x, y, size, size, fill, stroke, 1));

        void Bar(string key, double x, double y, double barWidth, double barHeight, string fill = "#75899A", double rotation = 0) =>
            details.Add(FlatShape(1000 + sequence * 100 + localSequence++, $"detail-{key}", "core.rectangle",
                x, y, barWidth, barHeight, fill, "#526879", 0.6, 0.8, rotation));

        void RadialBolts(string key, double centerX, double centerY, double radius, int count, double size = 5)
        {
            for (var index = 0; index < count; index++)
            {
                var angle = (index * 360d / count - 90) * Math.PI / 180d;
                Dot($"{key}-{index + 1}", centerX + radius * Math.Cos(angle) - size / 2,
                    centerY + radius * Math.Sin(angle) - size / 2, size);
            }
        }

        switch (familyKey)
        {
            case "dynamo.pump.standard":
                RadialBolts("casing-bolt", style == VisualStyle.HighPerformance ? 59 : 75,
                    style == VisualStyle.HighPerformance ? 48 : 59,
                    style == VisualStyle.HighPerformance ? 28 : 36, 6,
                    style == VisualStyle.HighPerformance ? 4 : 5);
                break;

            case "process.pump.submersible":
                var bodyLeft = style == VisualStyle.HighPerformance ? 27 : 37;
                var bodyWidth = style == VisualStyle.HighPerformance ? 40 : 38;
                var upperVentY = style == VisualStyle.HighPerformance ? 35 : 43;
                var lowerVentY = style == VisualStyle.HighPerformance ? 83 : 105;
                for (var index = 0; index < 4; index++)
                {
                    Bar($"upper-cooling-slot-{index + 1}", bodyLeft + index * (bodyWidth / 4d), upperVentY, 2.2, 9, "#64798B");
                    Bar($"lower-cooling-slot-{index + 1}", bodyLeft + index * (bodyWidth / 4d), lowerVentY, 2.2, 9, "#64798B");
                }
                break;

            case "process.motor.standard":
                // Keep family-specific details aligned with the redesigned side elevation.
                // The central frame carries the cooling ribs; the end bells/cowl remain visually clean.
                for (var index = 0; index < 3; index++)
                {
                    Bar($"cooling-rib-left-{index + 1}", 47 + index * 4, 33, 1.8, 31, "#73889A");
                    Bar($"cooling-rib-right-{index + 1}", 92 + index * 4, 33, 1.8, 31, "#73889A");
                }
                Bar("fan-cowl-vent-upper", 29, 37, 13, 1.5, "#667B8B");
                Bar("fan-cowl-vent-middle", 27, 48, 16, 1.5, "#667B8B");
                Bar("fan-cowl-vent-lower", 29, 59, 13, 1.5, "#667B8B");
                break;

            case "process.motor.vfd":
                var vfdStart = style == VisualStyle.HighPerformance ? 96 : 142;
                var vfdWidth = style == VisualStyle.HighPerformance ? 26 : 32;
                var vfdY = style == VisualStyle.HighPerformance ? 59 : 74;
                for (var index = 0; index < 3; index++)
                    Dot($"drive-status-{index + 1}", vfdStart + index * 8, vfdY, 4,
                        index == 0 ? "#22C55E" : "#94A3B8", "#334155");
                for (var index = 0; index < 3; index++)
                    Bar($"drive-vent-{index + 1}", vfdStart, vfdY + 7 + index * 3, vfdWidth, 1.4, "#718496");
                break;

            case "process.valve.onoff":
            case "process.valve.control":
                if (style == VisualStyle.HighPerformance)
                {
                    Dot("actuator-fastener-left", width * 0.43, style == VisualStyle.HighPerformance && familyKey == "process.valve.control" ? 20 : 16, 4);
                    Dot("actuator-fastener-right", width * 0.58, style == VisualStyle.HighPerformance && familyKey == "process.valve.control" ? 20 : 16, 4);
                    Bar("position-indicator", width * 0.47, style == VisualStyle.HighPerformance && familyKey == "process.valve.control" ? 37 : 24,
                        width * 0.08, 2, "#1877A8");
                }
                else
                {
                    foreach (var x in new[] { 31d, width - 38d })
                    foreach (var y in familyKey == "process.valve.control" ? new[] { 72d, 91d } : new[] { 49d, 67d })
                        Dot($"flange-bolt-{x:0}-{y:0}", x, y, 4.5);
                }
                break;

            case "process.tank.vertical":
                var tankLeft = style == VisualStyle.HighPerformance ? 27d : 33d;
                var tankWidth = style == VisualStyle.HighPerformance ? 54d : 62d;
                foreach (var y in style == VisualStyle.HighPerformance ? new[] { 42d, 69d, 132d } : new[] { 50d, 77d, 151d })
                    Bar($"shell-weld-{y:0}", tankLeft, y, tankWidth, 1.5, "#8295A5");
                var glassX = style == VisualStyle.HighPerformance ? 94d : 108d;
                var glassY = style == VisualStyle.HighPerformance ? 88d : 84d;
                Bar("level-sight-glass", glassX, glassY, 4, style == VisualStyle.HighPerformance ? 38 : 43, "#1877A8");
                Dot("sight-glass-upper", glassX - 1, glassY - 3, 6, "#DCE7EF", "#526879");
                Dot("sight-glass-lower", glassX - 1, glassY + (style == VisualStyle.HighPerformance ? 37 : 42), 6, "#DCE7EF", "#526879");
                break;

            case "process.tank.horizontal":
                var seamTop = style == VisualStyle.HighPerformance ? 28d : 34d;
                var seamHeight = style == VisualStyle.HighPerformance ? 48d : 52d;
                foreach (var x in style == VisualStyle.HighPerformance ? new[] { 55d, 113d } : new[] { 65d, 126d })
                    Bar($"shell-seam-{x:0}", x, seamTop, 1.8, seamHeight, "#8295A5");
                break;

            case "process.instrument.indicator":
                var gaugeX = style == VisualStyle.HighPerformance ? 14d : 16d;
                var gaugeY = style == VisualStyle.HighPerformance ? 8d : 10d;
                var gaugeDiameter = style == VisualStyle.HighPerformance ? 68d : 88d;
                var gaugeCenterX = gaugeX + gaugeDiameter / 2;
                var gaugeCenterY = gaugeY + gaugeDiameter / 2;
                var radius = gaugeDiameter / 2 - 11;
                var angles = style == VisualStyle.HighPerformance
                    ? Enumerable.Range(0, 7).Select(index => -150d + index * 30).ToArray()
                    : new[] { -160d, -115d, -70d, -25d };
                foreach (var angleDegrees in angles)
                {
                    var angle = angleDegrees * Math.PI / 180d;
                    var x = gaugeCenterX + radius * Math.Cos(angle);
                    var y = gaugeCenterY + radius * Math.Sin(angle);
                    Bar($"scale-tick-{angleDegrees:0}", x - 1.2, y - 4, 2.4, 8, "#475569", angleDegrees + 90);
                }
                break;

            case "process.compressor.reciprocating":
                RadialBolts("crankcase-fastener", 51, 78, 22, style == VisualStyle.HighPerformance ? 4 : 6,
                    style == VisualStyle.HighPerformance ? 3 : 4);
                for (var index = 0; index < 5; index++)
                    Bar($"cylinder-fin-{index + 1}", 37 + index * 10, 25, 2, 12,
                        style == VisualStyle.HighPerformance ? "#66747D" : "#64798B");
                Dot("crosshead-pin", 51, 76, 7, "#DCE5EB", "#526879");
                Bar("connecting-rod", 53, 79, 3, 15, "#586D7D", 28);
                break;

            case "process.compressor.screw":
                RadialBolts("housing-fastener", 73, 56, 39, style == VisualStyle.HighPerformance ? 4 : 8,
                    style == VisualStyle.HighPerformance ? 3 : 4);
                Bar("rotor-highlight-left", 48, 40, 3, 27, "#F0F4F6", -12);
                Bar("rotor-highlight-right", 83, 40, 3, 27, "#DCE5EB", 12);
                Bar("oil-sight-glass", 112, 70, 5, 10, "#4B9BB4");
                break;

            case "process.valve.butterfly":
            case "process.valve.ball":
                foreach (var flangeX in new[] { 32d, width - 38d })
                foreach (var boltY in new[] { 47d, 64d })
                    Dot($"flange-fastener-{flangeX:0}-{boltY:0}", flangeX, boltY, 4,
                        "#E7EEF3", "#506575");
                Dot("stem-bearing", centerX - 3, 31, 6, "#DCE5EB", "#526879");
                Bar("actuator-indicator", centerX - 12, 8, 24, 2, "#F0F4F6");
                break;

            case "process.valve.gate":
                foreach (var flangeX in new[] { 32d, width - 36d })
                foreach (var boltY in new[] { 78d, 94d })
                    Dot($"flange-fastener-{flangeX:0}-{boltY:0}", flangeX, boltY, 4,
                        "#E7EEF3", "#506575");
                Bar("handwheel-spoke-top", centerX - 1, 6, 2, 9, "#E7EEF3");
                Bar("handwheel-spoke-bottom", centerX - 1, 25, 2, 9, "#E7EEF3");
                Bar("handwheel-spoke-left", centerX - 14, 19, 9, 2, "#E7EEF3");
                Bar("handwheel-spoke-right", centerX + 5, 19, 9, 2, "#E7EEF3");
                break;

            case "process.exchanger.shell-tube":
                for (var index = 0; index < 3; index++)
                    Bar($"saddle-support-{index + 1}", 51 + index * 38, 91, 8, 19, "#526575");
                foreach (var boltY in new[] { 43d, 81d })
                foreach (var boltX in new[] { 28d, 132d })
                    Dot($"channel-cover-bolt-{boltX:0}-{boltY:0}", boltX, boltY, 4,
                        "#F0F4F6", "#526879");
                break;

            case "process.filter.strainer":
                foreach (var flangeX in new[] { 30d, width - 37d })
                foreach (var boltY in new[] { 29d, 46d })
                    Dot($"flange-fastener-{flangeX:0}-{boltY:0}", flangeX, boltY, 4,
                        "#E7EEF3", "#506575");
                Bar("drain-neck", 72, 91, 7, 15, "#526575");
                Dot("drain-plug", 70, 102, 11, "#B6C4CE", "#526879");
                break;

            case "process.mixer.agitator":
                for (var index = 0; index < 3; index++)
                    Bar($"vessel-baffle-{index + 1}", 37 + index * 25, 91, 2, 45,
                        style == VisualStyle.HighPerformance ? "#77838B" : "#8295A5");
                foreach (var boltX in new[] { 40d, 60d, 80d, 100d })
                    Dot($"cover-bolt-{boltX:0}", boltX, 66, 4, "#F0F4F6", "#526879");
                Dot("gearbox-hub", centerX - 4, 23, 8, "#DCE5EB", "#526879");
                break;

            case "electrical.transformer.power":
                for (var index = 0; index < 5; index++)
                {
                    Bar($"left-radiator-channel-{index + 1}", 19 + index * 4, 64, 1.5, 43, "#526575");
                    Bar($"right-radiator-channel-{index + 1}", 113 + index * 4, 64, 1.5, 43, "#526575");
                }
                Dot("oil-level-window", 102, 54, 8, "#4B9BB4", "#526879");
                Bar("nameplate", 55, 104, 39, 10, "#E7EEF3");
                break;

            case "electrical.breaker":
                for (var index = 0; index < 5; index++)
                {
                    Bar($"left-post-rib-{index + 1}", 34, 76 + index * 10, 8, 2, "#F0F4F6");
                    Bar($"right-post-rib-{index + 1}", 89, 76 + index * 10, 8, 2, "#F0F4F6");
                }
                foreach (var terminalX in new[] { 58d, 78d })
                    Dot($"terminal-fastener-{terminalX:0}", terminalX, 10, 5, "#F0F4F6", "#526879");
                break;

            case "electrical.disconnector":
            case "electrical.earthing-switch":
                for (var index = 0; index < 4; index++)
                {
                    Bar($"left-insulator-rib-{index + 1}", 21, 68 + index * 9, 18, 2, "#F0F4F6");
                    Bar($"right-insulator-rib-{index + 1}", width - 42, 68 + index * 9, 18, 2, "#F0F4F6");
                }
                Dot("blade-pivot", 31, 42, 8, "#DCE5EB", "#526879");
                Dot("contact-jaw", width - 37, 39, 10, "#B6C4CE", "#526879");
                break;

            case "electrical.generator":
                RadialBolts("end-shield-fastener", 69, 58, 37, style == VisualStyle.HighPerformance ? 4 : 8,
                    style == VisualStyle.HighPerformance ? 3 : 4);
                for (var index = 0; index < 5; index++)
                    Bar($"stator-vent-{index + 1}", 36 + index * 12, 83, 5, 2, "#526575");
                break;

            case "electrical.current-transformer":
                for (var index = 0; index < 5; index++)
                    Bar($"winding-band-{index + 1}", 28, 53 + index * 8, 56, 2,
                        style == VisualStyle.HighPerformance ? "#77838B" : "#8295A5");
                foreach (var terminalX in new[] { 20d, 84d })
                    Dot($"secondary-terminal-{terminalX:0}", terminalX, 88, 8, "#E7EEF3", "#526879");
                break;
        }

        return details;
    }

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

    private static VisualElementEngineeringDto BezierShape(
        int sequence,
        string key,
        double x,
        double y,
        double width,
        double height,
        string bezierPath,
        string fill,
        string stroke,
        double strokeWidth,
        string? secondaryFill = null,
        string gradientDirection = "vertical",
        bool shadow = false)
    {
        var properties = Properties(
            ("x", x), ("y", y), ("width", width), ("height", height),
            ("rotation", 0d),
            ("bezierPath", bezierPath),
            ("fillStyle", secondaryFill is null ? "solid" : "gradient"),
            ("fillColor", fill),
            ("strokeColor", stroke), ("strokeWidth", strokeWidth));

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

        return new(key, "core.bezier", Properties: properties, Id: ElementId(sequence));
    }

    private static VisualElementEngineeringDto ArcShape(
        int sequence,
        string key,
        double x,
        double y,
        double width,
        double height,
        double startAngle,
        double endAngle,
        string arcStyle,
        string fill,
        string stroke,
        double strokeWidth,
        string? secondaryFill = null,
        string gradientDirection = "vertical")
    {
        var properties = Properties(
            ("x", x), ("y", y), ("width", width), ("height", height),
            ("rotation", 0d),
            ("arcStartAngle", startAngle), ("arcEndAngle", endAngle),
            ("arcStyle", arcStyle),
            ("fillStyle", secondaryFill is null ? "solid" : "gradient"),
            ("fillColor", fill),
            ("strokeColor", stroke), ("strokeWidth", strokeWidth));

        if (secondaryFill is not null)
        {
            properties["fillSecondaryColor"] = JsonSerializer.SerializeToElement(secondaryFill);
            properties["gradientDirection"] = JsonSerializer.SerializeToElement(gradientDirection);
        }

        return new(key, "core.arc", Properties: properties, Id: ElementId(sequence));
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
                ("text", text), ("fontSize", fontSize), ("fontWeight", 600),
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
        string target)
    {
        var semanticColor = parameterKey switch
        {
            "running" when color.Equals("#16A34A", StringComparison.OrdinalIgnoreCase) => "#C97B7B",
            "running" => "#D92D20",
            "fault" when color.Equals("#DC2626", StringComparison.OrdinalIgnoreCase) => "#D8B95F",
            "fault" when color.Equals("#EF4444", StringComparison.OrdinalIgnoreCase) => "#EAB308",
            _ => color
        };
        return new(
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
                ("fillColor", semanticColor), ("strokeColor", "#111827"), ("strokeWidth", 1),
                ("visible", false)),
            Id: ElementId(sequence));
    }

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
        familySequence + (style switch
        {
            VisualStyle.Detailed2D => 0,
            VisualStyle.DimensionalFront => 10,
            VisualStyle.HighPerformance => 20,
            _ => throw new ArgumentOutOfRangeException(nameof(style))
        });

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
