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
                StateLamp(E(family, style, 8), "fault", 107, 5, "#DC2626", "fault", "{equipmentPath}.Fault"),
                FlatShape(E(family, style, 15), "wear-ring", "core.ellipse", 44, 32, 33, 33,
                    "#00000000", "#66737C", 1.2),
                BezierShape(E(family, style, 16), "volute-tongue", 72, 23, 19, 22,
                    "M 6 88 C 30 64 56 44 95 30 L 95 62 C 66 70 41 84 16 98 Z",
                    "#AEB7BE", "#56636C", 1.2)
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
            StateLamp(E(family, style, 14), "fault", 137, 5, "#EF4444", "fault", "{equipmentPath}.Fault"),
            FlatShape(E(family, style, 16), "wear-ring", "core.ellipse", 58, 42, 35, 35,
                "#00000000", dimensional ? "#5C7484" : "#64748B", 1.3),
            BezierShape(E(family, style, 17), "volute-tongue", 91, 28, 23, 26,
                "M 6 88 C 30 64 56 44 95 30 L 95 62 C 66 70 41 84 16 98 Z",
                dimensional ? "#C6D2DA" : "#B6C5CF", "#526575", 1.2,
                dimensional ? "#F8FAFC" : null, "diagonal-down", dimensional)
        ],
        parameters: PumpParameters());
    }

    private static DynamoEngineeringDto SubmersiblePump(VisualStyle style)
    {
        const int family = 2;
        var sequence = DefinitionSequence(family, style);
        if (style == VisualStyle.HighPerformance)
        {
            return Dynamo(sequence, "process.pump.submersible", "Bomba submersível", "pump", style, 104, 136,
            [
                // Vertical motor over a lower hydraulic section. The discharge leaves the
                // pump housing, not the motor body, so the silhouette reads as submersible machinery.
                BezierShape(E(family, style, 1), "body", 30, 22, 44, 72,
                    "M 14 4 C 26 1 74 1 86 4 L 86 82 C 76 94 24 94 14 82 Z",
                    "#C5CDD3", "#374151", 2),
                FlatShape(E(family, style, 2), "intake", "core.rectangle", 29, 101, 46, 18, "#9CA3AF", "#374151", 2, 4),
                FlatShape(E(family, style, 3), "outlet", "core.rectangle", 78, 79, 22, 13, "#A7B0B7", "#374151", 2, 2),
                Text(E(family, style, 4), "label", "BS", 38, 51, 28, 22, 11, "#111827"),
                StateLamp(E(family, style, 5), "running", 6, 5, "#16A34A", "running", "{equipmentPath}.Running"),
                StateLamp(E(family, style, 6), "fault", 80, 5, "#DC2626", "fault", "{equipmentPath}.Fault"),
                FlatShape(E(family, style, 7), "top-cap", "core.rectangle", 35, 16, 34, 10, "#B7C0C6", "#374151", 1.5, 3),
                FlatShape(E(family, style, 8), "outlet-neck", "core.rectangle", 69, 72, 15, 22, "#A7B0B7", "#374151", 1.5, 3),
                FlatShape(E(family, style, 9), "outlet-flange", "core.rectangle", 96, 75, 6, 21, "#B7C0C6", "#374151", 1.5, 1),
                FlatShape(E(family, style, 10), "cable-gland", "core.rectangle", 44, 7, 10, 11, "#6B7280", "#374151", 1, 2),
                BezierShape(E(family, style, 11), "pump-housing", 25, 84, 54, 29,
                    "M 8 18 C 20 5 38 2 58 5 C 78 8 92 24 94 48 C 90 72 74 91 50 96 C 28 94 12 78 6 58 Z",
                    "#AEB7BE", "#374151", 1.5),
                FlatShape(E(family, style, 12), "cable", "core.rectangle", 47, 0, 5, 9, "#374151", "#111827", 1, 2)
            ],
            parameters: PumpParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.pump.submersible", "Bomba submersível", "pump", style, 122, 166,
        [
            BezierShape(E(family, style, 1), "body", 36, 31, 48, 84,
                "M 14 4 C 26 1 74 1 86 4 L 86 82 C 76 94 24 94 14 82 Z",
                "#AEBCC8", "#334155", 3, dimensional ? "#F8FAFC" : null, "horizontal", dimensional),
            MaterialShape(E(family, style, 2), "top-cap", "core.rectangle", 40, 23, 40, 15, "#CBD5E1", "#FFFFFF", "#334155", 2, 5, dimensional, "vertical"),
            FlatShape(E(family, style, 3), "cable-gland", "core.rectangle", 49, 11, 11, 14, "#64748B", "#334155", 1.5, 3),
            FlatShape(E(family, style, 4), "cable", "core.rectangle", 52, 1, 5, 13, "#374151", "#111827", 1, 2),
            MaterialShape(E(family, style, 5), "outlet-neck", "core.rectangle", 78, 91, 18, 25, "#B8C4CF", "#F8FAFC", "#334155", 2, 4, dimensional, "horizontal"),
            MaterialShape(E(family, style, 6), "outlet", "core.rectangle", 92, 86, 23, 16, "#B8C4CF", "#F8FAFC", "#334155", 2, 2, dimensional, "vertical"),
            MaterialShape(E(family, style, 7), "intake", "core.rectangle", 34, 130, 50, 20, "#94A3B8", "#DDE4EA", "#334155", 2, 4, dimensional, "vertical"),
            FlatShape(E(family, style, 8), "grille-1", "core.rectangle", 40, 133, 3, 14, "#475569", "#334155", 0),
            FlatShape(E(family, style, 9), "grille-2", "core.rectangle", 50, 133, 3, 14, "#475569", "#334155", 0),
            FlatShape(E(family, style, 10), "grille-3", "core.rectangle", 61, 133, 3, 14, "#475569", "#334155", 0),
            FlatShape(E(family, style, 11), "grille-4", "core.rectangle", 72, 133, 3, 14, "#475569", "#334155", 0),
            Text(E(family, style, 12), "label", "BS", 46, 67, 28, 22, 11, "#1F2937"),
            StateLamp(E(family, style, 13), "running", 6, 5, "#22C55E", "running", "{equipmentPath}.Running"),
            StateLamp(E(family, style, 14), "fault", 98, 5, "#EF4444", "fault", "{equipmentPath}.Fault"),
            BezierShape(E(family, style, 15), "pump-housing", 29, 103, 61, 34,
                "M 8 18 C 20 5 38 2 58 5 C 78 8 92 24 94 48 C 90 72 74 91 50 96 C 28 94 12 78 6 58 Z",
                "#94A3B8", "#334155", 2, dimensional ? "#DDE4EA" : null, "diagonal-down", dimensional),
            FlatShape(E(family, style, 16), "outlet-flange", "core.rectangle", 111, 83, 6, 22, "#AAB8C5", "#475569", 1.5, 1)
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
                BezierShape(E(family, style, 1), "body", 25, 20, 61, 50,
                    "M 10 5 C 18 2 82 2 90 5 L 98 18 C 100 26 100 74 98 82 L 90 95 C 82 98 18 98 10 95 L 2 82 C 0 74 0 26 2 18 Z",
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
                StateLamp(E(family, style, 7), "fault", 82, 4, "#DC2626", "fault", "{equipmentPath}.Fault"),
                FlatShape(E(family, style, 22), "fan-cowl-ring", "core.ellipse", 11, 22, 30, 46,
                    "#00000000", "#66737C", 1.2),
                FlatShape(E(family, style, 23), "terminal-cover", "core.rectangle", 51, 4, 20, 5,
                    "#A7B0B7", "#374151", 1, 2),
                FlatShape(E(family, style, 24), "cable-gland", "core.ellipse", 57, 1, 8, 7,
                    "#7B878F", "#374151", 1)
            ],
            parameters: MotorParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.motor.standard", "Motor padrão", "motor", style, 150, 102,
        [
            BezierShape(E(family, style, 1), "body", 38, 21, 78, 57,
                "M 10 5 C 18 2 82 2 90 5 L 98 18 C 100 26 100 74 98 82 L 90 95 C 82 98 18 98 10 95 L 2 82 C 0 74 0 26 2 18 Z",
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
            FlatShape(E(family, style, 21), "fan-cowl-ring", "core.ellipse", 18, 24, 38, 51,
                "#00000000", dimensional ? "#5C7484" : "#64748B", 1.3),
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
                BezierShape(E(family, style, 1), "motor", 12, 20, 66, 52,
                    "M 10 5 C 18 2 82 2 90 5 L 98 18 C 100 26 100 74 98 82 L 90 95 C 82 98 18 98 10 95 L 2 82 C 0 74 0 26 2 18 Z",
                    "#C5CDD3", "#374151", 2),
                FlatShape(E(family, style, 2), "shaft", "core.rectangle", 76, 42, 12, 8, "#9CA3AF", "#374151", 1, 2),
                FlatShape(E(family, style, 3), "vfd", "core.rectangle", 99, 10, 32, 48, "#D1D5DB", "#374151", 2, 4),
                Text(E(family, style, 4), "motor-label", "M", 28, 35, 26, 22, 12, "#111827"),
                Text(E(family, style, 5), "vfd-label", "VFD", 94, 37, 31, 18, 9, "#111827"),
                StateLamp(E(family, style, 6), "running", 4, 4, "#16A34A", "running", "{equipmentPath}.Running"),
                StateLamp(E(family, style, 7), "fault", 114, 4, "#DC2626", "fault", "{equipmentPath}.Fault"),
                FlatShape(E(family, style, 8), "motor-end", "core.ellipse", 5, 25, 20, 42, "#AEB7BE", "#374151", 1.5),
                FlatShape(E(family, style, 9), "terminal", "core.rectangle", 35, 9, 25, 15, "#D1D5DB", "#374151", 1.5, 3),
                FlatShape(E(family, style, 10), "foot-left", "core.rectangle", 25, 69, 15, 9, "#7D898F", "#374151", 1, 2),
                FlatShape(E(family, style, 11), "foot-right", "core.rectangle", 58, 69, 15, 9, "#7D898F", "#374151", 1, 2),
                FlatShape(E(family, style, 12), "motor-base", "core.rectangle", 18, 77, 64, 6, "#5F6A70", "#374151", 1, 2),
                BezierShape(E(family, style, 13), "control-cable", 76, 18, 27, 24,
                    "M 3 25 C 30 6 66 4 97 26 L 97 39 C 65 18 30 18 3 35 Z",
                    "#58636B", "#374151", 0.8),
                FlatShape(E(family, style, 14), "vfd-cable-gland", "core.ellipse", 96, 20, 8, 8, "#8A969E", "#374151", 1),
                FlatShape(E(family, style, 15), "vfd-screen", "core.rectangle", 104, 18, 22, 12, "#374151", "#1F2937", 1, 2),
                FlatShape(E(family, style, 16), "vfd-mounting-rail", "core.rectangle", 96, 59, 38, 5, "#68737A", "#374151", 1, 1)
            ],
            parameters: VfdMotorParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.motor.vfd", "Motor com inversor", "motor", style, 196, 112,
        [
            BezierShape(E(family, style, 1), "motor-body", 24, 29, 78, 57,
                "M 10 5 C 18 2 82 2 90 5 L 98 18 C 100 26 100 74 98 82 L 90 95 C 82 98 18 98 10 95 L 2 82 C 0 74 0 26 2 18 Z",
                "#AEBCC8", "#334155", 3, dimensional ? "#F8FAFC" : null, "vertical", dimensional),
            MaterialShape(E(family, style, 2), "motor-end", "core.ellipse", 12, 34, 25, 47, "#94A3B8", "#DDE4EA", "#334155", 2, 0, dimensional, "horizontal"),
            FlatShape(E(family, style, 3), "shaft", "core.rectangle", 99, 51, 14, 9, "#94A3B8", "#475569", 1, 2),
            FlatShape(E(family, style, 4), "motor-base", "core.rectangle", 31, 91, 78, 7, "#475569", "#334155", 1, 2),
            MaterialShape(E(family, style, 5), "vfd", "core.rectangle", 133, 14, 51, 80, "#D8E0E8", "#FFFFFF", "#334155", 2, 5, dimensional, "horizontal", dimensional),
            FlatShape(E(family, style, 6), "vfd-screen", "core.rectangle", 142, 27, 32, 18, "#334155", "#0F172A", 1, 2),
            FlatShape(E(family, style, 7), "vfd-key-1", "core.rectangle", 144, 52, 8, 7, "#94A3B8", "#475569", 1, 1),
            FlatShape(E(family, style, 8), "vfd-key-2", "core.rectangle", 156, 52, 8, 7, "#94A3B8", "#475569", 1, 1),
            FlatShape(E(family, style, 9), "vfd-key-3", "core.rectangle", 168, 52, 8, 7, "#94A3B8", "#475569", 1, 1),
            Text(E(family, style, 10), "motor-label", "M", 49, 45, 24, 20, 11, "#1F2937"),
            Text(E(family, style, 11), "vfd-label", "VFD", 142, 66, 32, 16, 9, "#1F2937"),
            StateLamp(E(family, style, 12), "running", 4, 4, "#22C55E", "running", "{equipmentPath}.Running"),
            StateLamp(E(family, style, 13), "fault", 173, 4, "#EF4444", "fault", "{equipmentPath}.Fault"),
            MaterialShape(E(family, style, 14), "terminal", "core.rectangle", 52, 11, 34, 20,
                "#CBD5E1", "#F8FAFC", "#334155", 1.5, 4, dimensional, "vertical"),
            FlatShape(E(family, style, 15), "foot-left", "core.rectangle", 42, 82, 18, 11, "#64748B", "#334155", 1, 2),
            FlatShape(E(family, style, 16), "foot-right", "core.rectangle", 86, 82, 18, 11, "#64748B", "#334155", 1, 2),
            BezierShape(E(family, style, 17), "control-cable", 102, 24, 39, 27,
                "M 3 20 C 30 5 65 5 97 27 L 97 39 C 65 18 30 18 3 32 Z",
                "#526575", "#334155", 0.8),
            FlatShape(E(family, style, 18), "vfd-cable-gland", "core.ellipse", 128, 22, 10, 10, "#8798A6", "#334155", 1),
            FlatShape(E(family, style, 19), "vfd-mounting-rail", "core.rectangle", 130, 95, 57, 5, "#64748B", "#334155", 1, 1)
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
                    "M 0 35 L 22 26 C 42 18 68 14 100 20 L 100 80 C 68 86 42 82 22 74 L 0 65 Z",
                    "#C5CDD3", "#374151", 2),
                BezierShape(E(family, style, 3), "body-right", 63, 31, 34, 34,
                    "M 100 35 L 78 26 C 58 18 32 14 0 20 L 0 80 C 32 86 58 82 78 74 L 100 65 Z",
                    "#C5CDD3", "#374151", 2),
                FlatShape(E(family, style, 4), "pipe-right", "core.rectangle", 94, 44, 31, 9, "#9CA3AF", "#4B5563", 1, 2),
                FlatShape(E(family, style, 5), "stem", "core.rectangle", 61, 20, 5, 17, "#6B7280", "#374151", 1),
                BezierShape(E(family, style, 6), "actuator", 49, 4, 29, 19,
                    "M 8 18 C 8 7 22 3 50 3 C 78 3 92 7 92 18 L 92 82 C 92 93 78 97 50 97 C 22 97 8 93 8 82 Z",
                    "#D1D5DB", "#374151", 2),
                FlatShape(E(family, style, 9), "bonnet", "core.ellipse", 56, 18, 15, 10, "#AEB7BE", "#374151", 1),
                StateLamp(E(family, style, 7), "open", 5, 5, "#16A34A", "open", "{equipmentPath}.Open"),
                StateLamp(E(family, style, 8), "fault", 104, 5, "#DC2626", "fault", "{equipmentPath}.Fault"),
                FlatShape(E(family, style, 10), "flange-left", "core.rectangle", 28, 37, 7, 23, "#B7C0C6", "#374151", 1.5, 1),
                FlatShape(E(family, style, 11), "flange-right", "core.rectangle", 94, 37, 7, 23, "#B7C0C6", "#374151", 1.5, 1),
                FlatShape(E(family, style, 12), "seat-ring", "core.ellipse", 59, 36, 10, 25, "#AEB7BE", "#374151", 1.2),
                FlatShape(E(family, style, 13), "closure-member", "core.ellipse", 61, 41, 6, 15, "#7E8990", "#374151", 1)
            ],
            parameters: OnOffValveParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.valve.onoff", "Válvula abre/fecha", "valve", style, 164, 112,
        [
            MaterialShape(E(family, style, 1), "pipe-left", "core.rectangle", 2, 54, 43, 12, "#AAB8C5", "#F8FAFC", "#475569", 1, 3, dimensional, "vertical"),
            MaterialShape(E(family, style, 2), "flange-left", "core.rectangle", 29, 46, 10, 28, "#94A3B8", "#DDE4EA", "#334155", 2, 2, dimensional, "horizontal"),
            BezierShape(E(family, style, 3), "body-left", 39, 38, 43, 43,
                "M 0 35 L 22 26 C 42 18 68 14 100 20 L 100 80 C 68 86 42 82 22 74 L 0 65 Z",
                "#B8C4CF", "#334155", 2, dimensional ? "#F8FAFC" : null, "diagonal-down", dimensional),
            BezierShape(E(family, style, 4), "body-right", 80, 38, 43, 43,
                "M 100 35 L 78 26 C 58 18 32 14 0 20 L 0 80 C 32 86 58 82 78 74 L 100 65 Z",
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
            StateLamp(E(family, style, 11), "fault", 140, 5, "#EF4444", "fault", "{equipmentPath}.Fault"),
            MaterialShape(E(family, style, 13), "seat-ring", "core.ellipse", 76, 45, 13, 30,
                "#AAB8C5", "#E4EBEF", "#334155", 1.2, 0, dimensional, "vertical"),
            MaterialShape(E(family, style, 14), "closure-member", "core.ellipse", 79, 51, 7, 18,
                "#8798A5", "#DDE4EA", "#334155", 1, 0, dimensional, "vertical")
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
                    "M 0 42 C 25 38 50 24 100 12 L 100 88 C 50 76 25 62 0 58 Z",
                    "#C5CDD3", "#374151", 2),
                BezierShape(E(family, style, 3), "body-right", 63, 47, 34, 34,
                    "M 100 42 C 75 38 50 24 0 12 L 0 88 C 50 76 75 62 100 58 Z",
                    "#C5CDD3", "#374151", 2),
                FlatShape(E(family, style, 4), "pipe-right", "core.rectangle", 94, 60, 31, 9, "#9CA3AF", "#4B5563", 1, 2),
                FlatShape(E(family, style, 5), "stem", "core.rectangle", 61, 29, 5, 24, "#6B7280", "#374151", 1),
                BezierShape(E(family, style, 6), "actuator", 43, 4, 42, 28,
                    "M 4 50 C 7 18 25 5 50 5 C 75 5 93 18 96 50 C 93 82 75 95 50 95 C 25 95 7 82 4 50 Z",
                    "#D1D5DB", "#374151", 2),
                Text(E(family, style, 7), "label", "%", 51, 8, 26, 20, 10, "#111827"),
                FlatShape(E(family, style, 9), "bonnet", "core.ellipse", 55, 26, 18, 11, "#AEB7BE", "#374151", 1),
                StateLamp(E(family, style, 8), "fault", 104, 5, "#DC2626", "fault", "{equipmentPath}.Fault"),
                FlatShape(E(family, style, 10), "flange-left", "core.rectangle", 28, 53, 7, 23, "#B7C0C6", "#374151", 1.5, 1),
                FlatShape(E(family, style, 11), "flange-right", "core.rectangle", 94, 53, 7, 23, "#B7C0C6", "#374151", 1.5, 1),
                FlatShape(E(family, style, 12), "seat-ring", "core.ellipse", 59, 52, 10, 27, "#AEB7BE", "#374151", 1.2),
                FlatShape(E(family, style, 13), "yoke-left", "core.rectangle", 51, 29, 4, 22, "#89959D", "#374151", 1, 1),
                FlatShape(E(family, style, 14), "yoke-right", "core.rectangle", 73, 29, 4, 22, "#89959D", "#374151", 1, 1),
                BezierShape(E(family, style, 15), "plug", 59, 58, 10, 14,
                    "M 10 10 C 28 3 72 3 90 10 L 80 90 C 60 97 40 97 20 90 Z",
                    "#7E8990", "#374151", 1)
            ],
            parameters: ControlValveParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.valve.control", "Válvula de controle", "valve", style, 166, 136,
        [
            MaterialShape(E(family, style, 1), "pipe-left", "core.rectangle", 2, 78, 44, 12, "#AAB8C5", "#F8FAFC", "#475569", 1, 3, dimensional, "vertical"),
            MaterialShape(E(family, style, 2), "flange-left", "core.rectangle", 30, 69, 10, 30, "#94A3B8", "#DDE4EA", "#334155", 2, 2, dimensional, "horizontal"),
            BezierShape(E(family, style, 3), "body-left", 39, 60, 43, 43,
                "M 0 42 C 25 38 50 24 100 12 L 100 88 C 50 76 25 62 0 58 Z",
                "#B8C4CF", "#334155", 2, dimensional ? "#F8FAFC" : null, "diagonal-down", dimensional),
            BezierShape(E(family, style, 4), "body-right", 80, 60, 43, 43,
                "M 100 42 C 75 38 50 24 0 12 L 0 88 C 50 76 75 62 100 58 Z",
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
            StateLamp(E(family, style, 11), "fault", 140, 5, "#EF4444", "fault", "{equipmentPath}.Fault"),
            MaterialShape(E(family, style, 13), "seat-ring", "core.ellipse", 75, 67, 14, 31,
                "#AAB8C5", "#E4EBEF", "#334155", 1.2, 0, dimensional, "vertical"),
            FlatShape(E(family, style, 14), "yoke-left", "core.rectangle", 64, 43, 4, 24, "#738391", "#334155", 1, 1),
            FlatShape(E(family, style, 15), "yoke-right", "core.rectangle", 96, 43, 4, 24, "#738391", "#334155", 1, 1),
            BezierShape(E(family, style, 16), "plug", 77, 76, 10, 15,
                "M 10 10 C 28 3 72 3 90 10 L 80 90 C 60 97 40 97 20 90 Z",
                "#8798A5", "#334155", 1,
                dimensional ? "#E8EEF2" : null, "vertical", dimensional)
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
                BezierShape(E(family, style, 1), "vessel", 18, 8, 72, 140,
                    "M 12 12 C 20 4 32 1 50 1 C 68 1 80 4 88 12 L 88 88 C 80 96 68 99 50 99 C 32 99 20 96 12 88 Z",
                    "#D1D5DB", "#475569", 2),
                FlatShape(E(family, style, 2), "liquid", "core.rectangle", 24, 77, 60, 62, "#AAB2B8", "#6B7280", 1, 8),
                ArcShape(E(family, style, 9), "top-head-seam", 26, 12, 56, 21, 180, 360, "arc",
                    "#00000000", "#77838B", 1),
                ArcShape(E(family, style, 10), "bottom-head-seam", 26, 123, 56, 20, 0, 180, "arc",
                    "#00000000", "#77838B", 1),
                FlatShape(E(family, style, 3), "nozzle", "core.rectangle", 48, 2, 12, 10, "#9CA3AF", "#475569", 1, 2),
                FlatShape(E(family, style, 4), "leg-left", "core.rectangle", 29, 144, 10, 10, "#6B7280", "#475569", 1, 2),
                FlatShape(E(family, style, 5), "leg-right", "core.rectangle", 69, 144, 10, 10, "#6B7280", "#475569", 1, 2),
                Text(E(family, style, 6), "label", "TK", 39, 30, 30, 24, 11, "#111827"),
                StateLamp(E(family, style, 7), "high", 84, 10, "#D97706", "high", "{equipmentPath}.High"),
                StateLamp(E(family, style, 8), "fault", 84, 132, "#DC2626", "fault", "{equipmentPath}.Fault"),
                FlatShape(E(family, style, 11), "nozzle-flange", "core.rectangle", 44, 0, 20, 4, "#AEB7BE", "#475569", 1, 1),
                FlatShape(E(family, style, 12), "liquid-line", "core.rectangle", 24, 76, 60, 2, "#6F7A82", "#6F7A82", 0, 1),
                FlatShape(E(family, style, 13), "side-nozzle", "core.rectangle", 86, 66, 16, 10, "#9CA3AF", "#475569", 1, 2),
                FlatShape(E(family, style, 14), "side-nozzle-flange", "core.rectangle", 99, 63, 5, 16, "#B7C0C6", "#475569", 1, 1),
                FlatShape(E(family, style, 15), "foot-left", "core.rectangle", 25, 152, 18, 5, "#5B646B", "#475569", 1, 1),
                FlatShape(E(family, style, 16), "foot-right", "core.rectangle", 65, 152, 18, 5, "#5B646B", "#475569", 1, 1)
            ],
            parameters: TankParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.tank.vertical", "Tanque vertical", "tank", style, 128, 186,
        [
            BezierShape(E(family, style, 1), "vessel", 26, 15, 76, 153,
                "M 10 11 C 19 4 31 1 50 1 C 69 1 81 4 90 11 L 90 89 C 81 96 69 99 50 99 C 31 99 19 96 10 89 Z",
                "#C3CDD6", "#475569", 3, dimensional ? "#F8FAFC" : null, "horizontal", dimensional),
            ArcShape(E(family, style, 2), "top-head", 31, 18, 66, 27, 180, 360, "arc",
                "#00000000", dimensional ? "#617887" : "#71808A", 1.5),
            ArcShape(E(family, style, 3), "bottom-head", 31, 139, 66, 27, 0, 180, "arc",
                "#00000000", dimensional ? "#617887" : "#71808A", 1.5),
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
            StateLamp(E(family, style, 14), "fault", 104, 147, "#EF4444", "fault", "{equipmentPath}.Fault"),
            FlatShape(E(family, style, 15), "top-nozzle-flange", "core.rectangle", 52, 0, 24, 5, "#AAB8C5", "#475569", 1, 1),
            FlatShape(E(family, style, 16), "side-nozzle-flange", "core.rectangle", 117, 59, 5, 20, "#AAB8C5", "#475569", 1, 1)
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
                BezierShape(E(family, style, 1), "vessel", 18, 18, 132, 66,
                    "M 18 3 C 8 8 2 21 2 50 C 2 79 8 92 18 97 L 82 97 C 92 92 98 79 98 50 C 98 21 92 8 82 3 Z",
                    "#D1D5DB", "#475569", 2),
                FlatShape(E(family, style, 2), "liquid", "core.rectangle", 29, 51, 110, 26, "#AAB2B8", "#6B7280", 1, 11),
                ArcShape(E(family, style, 8), "head-left-seam", 18, 20, 35, 62, 90, 270, "arc",
                    "#00000000", "#77838B", 1),
                ArcShape(E(family, style, 9), "head-right-seam", 115, 20, 35, 62, 270, 450, "arc",
                    "#00000000", "#77838B", 1),
                FlatShape(E(family, style, 3), "leg-left", "core.rectangle", 38, 79, 18, 13, "#6B7280", "#475569", 1, 3),
                FlatShape(E(family, style, 4), "leg-right", "core.rectangle", 112, 79, 18, 13, "#6B7280", "#475569", 1, 3),
                Text(E(family, style, 5), "label", "TK", 68, 28, 32, 24, 11, "#111827"),
                StateLamp(E(family, style, 6), "high", 144, 10, "#D97706", "high", "{equipmentPath}.High"),
                StateLamp(E(family, style, 7), "fault", 144, 74, "#DC2626", "fault", "{equipmentPath}.Fault"),
                FlatShape(E(family, style, 10), "top-nozzle", "core.rectangle", 78, 8, 13, 14, "#AEB7BE", "#475569", 1, 2),
                FlatShape(E(family, style, 11), "top-nozzle-flange", "core.rectangle", 74, 5, 21, 4, "#C7CFD4", "#475569", 1, 1),
                FlatShape(E(family, style, 12), "side-nozzle", "core.rectangle", 146, 45, 17, 10, "#AEB7BE", "#475569", 1, 2),
                FlatShape(E(family, style, 13), "side-nozzle-flange", "core.rectangle", 160, 42, 5, 16, "#C7CFD4", "#475569", 1, 1),
                FlatShape(E(family, style, 14), "liquid-line", "core.rectangle", 29, 50, 110, 2, "#6F7A82", "#6F7A82", 0, 1)
            ],
            parameters: TankParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.tank.horizontal", "Tanque horizontal", "tank", style, 196, 120,
        [
            BezierShape(E(family, style, 1), "vessel", 17, 26, 171, 68,
                "M 12 3 C 4 10 1 23 1 50 C 1 77 4 90 12 97 L 88 97 C 96 90 99 77 99 50 C 99 23 96 10 88 3 Z",
                "#C3CDD6", "#475569", 3, dimensional ? "#F8FAFC" : null, "vertical", dimensional),
            ArcShape(E(family, style, 2), "head-left", 18, 27, 38, 66, 90, 270, "arc",
                "#00000000", dimensional ? "#617887" : "#71808A", 1.5),
            ArcShape(E(family, style, 3), "head-right", 149, 27, 38, 66, 270, 450, "arc",
                "#00000000", dimensional ? "#617887" : "#71808A", 1.5),
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
            StateLamp(E(family, style, 14), "fault", 171, 88, "#EF4444", "fault", "{equipmentPath}.Fault"),
            FlatShape(E(family, style, 15), "top-nozzle-flange", "core.rectangle", 87, 7, 24, 5, "#AAB8C5", "#475569", 1, 1),
            FlatShape(E(family, style, 16), "side-nozzle-flange", "core.rectangle", 190, 49, 5, 22, "#AAB8C5", "#475569", 1, 1)
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
                BezierShape(E(family, style, 6), "casing", 43, 30, 94, 94,
                    "M 8 58 C 7 29 25 8 53 6 C 78 4 95 20 96 43 C 98 65 84 84 63 94 C 42 99 20 94 11 78 C 8 72 7 65 8 58 Z",
                    "#D8E0E8", "#263746", 3),
                FlatShape(E(family, style, 7), "casing-rim", "core.ellipse", 50, 37, 80, 80, "#F8FAFC", "#546879", 2),
                FlatShape(E(family, style, 8), "impeller-recess", "core.ellipse", 60, 47, 60, 60, "#263746", "#17232D", 2),
                RotorBlade(E(family, style, 9), "impeller-blade-1", 90, 77, 8, 21, 0, "#AEBBC7", "#263746", 1),
                RotorBlade(E(family, style, 10), "impeller-blade-2", 90, 77, 8, 21, 60, "#AEBBC7", "#263746", 1),
                RotorBlade(E(family, style, 11), "impeller-blade-3", 90, 77, 8, 21, 120, "#AEBBC7", "#263746", 1),
                RotorBlade(E(family, style, 12), "impeller-blade-4", 90, 77, 8, 21, 180, "#AEBBC7", "#263746", 1),
                RotorBlade(E(family, style, 13), "impeller-blade-5", 90, 77, 8, 21, 240, "#AEBBC7", "#263746", 1),
                RotorBlade(E(family, style, 14), "impeller-blade-6", 90, 77, 8, 21, 300, "#AEBBC7", "#263746", 1),
                FlatShape(E(family, style, 15), "hub", "core.ellipse", 78, 65, 24, 24, "#F8FAFC", "#263746", 2),
                FlatShape(E(family, style, 16), "hub-cap", "core.ellipse", 85, 72, 10, 10, "#64748B", "#263746", 1),
                FlatShape(E(family, style, 17), "base-left-foot", "core.rectangle", 57, 114, 17, 9, "#7B8996", "#263746", 1, 2),
                FlatShape(E(family, style, 18), "base-right-foot", "core.rectangle", 106, 114, 17, 9, "#7B8996", "#263746", 1, 2),
                FlatShape(E(family, style, 19), "base", "core.rectangle", 45, 122, 90, 8, "#445565", "#263746", 1, 2),
                Polygon(E(family, style, 20), "outlet-flow-arrow", 135, 18, 12, 14, [(0d, 0d), (12d, 7d), (0d, 14d)], "#1877A8", "#125575", 1),
                Text(E(family, style, 21), "label", "B", 81, 68, 18, 18, 10, "#17232D"),
                StateLamp(E(family, style, 22), "running", 7, 7, "#16A34A", "running", "{equipmentPath}.Running"),
                StateLamp(E(family, style, 23), "fault", 158, 7, "#DC2626", "fault", "{equipmentPath}.Fault"),
                FlatShape(E(family, style, 24), "impeller-eye-ring", "core.ellipse", 69, 56, 42, 42, "#00000000", "#596872", 1.5)
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
                BezierShape(E(family, style, 6), "volute-case", 44, 31, 102, 102,
                    "M 8 58 C 7 29 25 8 53 6 C 78 4 95 20 96 43 C 98 65 84 84 63 94 C 42 99 20 94 11 78 C 8 72 7 65 8 58 Z",
                    "#7892A7", "#30485A", 3, "#E5EDF3", "diagonal-down", true),
                MaterialShape(E(family, style, 7), "case-cover", "core.ellipse", 51, 38, 88, 88, "#B5C6D3", "#F8FAFC", "#597184", 2, 0, true, "diagonal-up"),
                MaterialShape(E(family, style, 8), "impeller-recess", "core.ellipse", 62, 49, 66, 66, "#354E61", "#7892A7", "#30485A", 2, 0, true, "diagonal-down"),
                RotorBlade(E(family, style, 9), "impeller-blade-1", 95, 82, 9, 23, 0, "#AFC4D2", "#243B4A", 1),
                RotorBlade(E(family, style, 10), "impeller-blade-2", 95, 82, 9, 23, 60, "#AFC4D2", "#243B4A", 1),
                RotorBlade(E(family, style, 11), "impeller-blade-3", 95, 82, 9, 23, 120, "#AFC4D2", "#243B4A", 1),
                RotorBlade(E(family, style, 12), "impeller-blade-4", 95, 82, 9, 23, 180, "#AFC4D2", "#243B4A", 1),
                RotorBlade(E(family, style, 13), "impeller-blade-5", 95, 82, 9, 23, 240, "#AFC4D2", "#243B4A", 1),
                RotorBlade(E(family, style, 14), "impeller-blade-6", 95, 82, 9, 23, 300, "#AFC4D2", "#243B4A", 1),
                MaterialShape(E(family, style, 15), "hub", "core.ellipse", 79, 66, 32, 32, "#CBD9E3", "#FFFFFF", "#30485A", 2, 0, true, "diagonal-down", true),
                FlatShape(E(family, style, 16), "hub-cap", "core.ellipse", 89, 76, 12, 12, "#547084", "#243B4A", 1),
                FlatShape(E(family, style, 17), "bolt-1", "core.ellipse", 88.9, 35.9, 3.2, 3.2, "#F8FAFC", "#526B7D", 1),
                FlatShape(E(family, style, 18), "bolt-2", "core.ellipse", 125.9, 53.9, 3.2, 3.2, "#F8FAFC", "#526B7D", 1),
                FlatShape(E(family, style, 19), "bolt-3", "core.ellipse", 126.9, 99.9, 3.2, 3.2, "#F8FAFC", "#526B7D", 1),
                FlatShape(E(family, style, 20), "bolt-4", "core.ellipse", 89.9, 121.9, 3.2, 3.2, "#F8FAFC", "#526B7D", 1),
                FlatShape(E(family, style, 21), "bolt-5", "core.ellipse", 54.9, 99.9, 3.2, 3.2, "#F8FAFC", "#526B7D", 1),
                FlatShape(E(family, style, 22), "bolt-6", "core.ellipse", 53.9, 55.9, 3.2, 3.2, "#F8FAFC", "#526B7D", 1),
                FlatShape(E(family, style, 23), "foot-left", "core.rectangle", 62, 125, 18, 9, "#667F91", "#30485A", 1, 2),
                FlatShape(E(family, style, 24), "foot-right", "core.rectangle", 112, 125, 18, 9, "#667F91", "#30485A", 1, 2),
                FlatShape(E(family, style, 25), "base", "core.rectangle", 49, 134, 96, 8, "#435B6D", "#30485A", 1, 2),
                Polygon(E(family, style, 26), "outlet-flow-arrow", 145, 22, 15, 14, [(0d, 0d), (15d, 7d), (0d, 14d)], "#1687B4", "#125575", 1),
                Text(E(family, style, 27), "label", "B", 84, 73, 22, 20, 11, "#243B4A"),
                StateLamp(E(family, style, 28), "running", 7, 7, "#22C55E", "running", "{equipmentPath}.Running"),
                StateLamp(E(family, style, 29), "fault", 177, 7, "#EF4444", "fault", "{equipmentPath}.Fault"),
                FlatShape(E(family, style, 30), "impeller-eye-ring", "core.ellipse", 74, 61, 42, 42, "#00000000", "#526B7D", 1.5)
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
            BezierShape(E(family, style, 6), "volute-case", 44, 31, 102, 102,
                "M 8 58 C 7 29 25 8 53 6 C 78 4 95 20 96 43 C 98 65 84 84 63 94 C 42 99 20 94 11 78 C 8 72 7 65 8 58 Z",
                "#8999A7", "#273746", 3),
            FlatShape(E(family, style, 7), "case-cover", "core.ellipse", 51, 38, 88, 88, "#D3DCE4", "#526575", 2),
            FlatShape(E(family, style, 8), "impeller-recess", "core.ellipse", 62, 49, 66, 66, "#405363", "#273746", 2),
            RotorBlade(E(family, style, 9), "impeller-blade-1", 95, 82, 9, 23, 0, "#A8B4BF", "#273746", 1),
            RotorBlade(E(family, style, 10), "impeller-blade-2", 95, 82, 9, 23, 60, "#A8B4BF", "#273746", 1),
            RotorBlade(E(family, style, 11), "impeller-blade-3", 95, 82, 9, 23, 120, "#A8B4BF", "#273746", 1),
            RotorBlade(E(family, style, 12), "impeller-blade-4", 95, 82, 9, 23, 180, "#A8B4BF", "#273746", 1),
            RotorBlade(E(family, style, 13), "impeller-blade-5", 95, 82, 9, 23, 240, "#A8B4BF", "#273746", 1),
            RotorBlade(E(family, style, 14), "impeller-blade-6", 95, 82, 9, 23, 300, "#A8B4BF", "#273746", 1),
            FlatShape(E(family, style, 15), "hub", "core.ellipse", 79, 66, 32, 32, "#EEF2F6", "#273746", 2),
            FlatShape(E(family, style, 16), "hub-cap", "core.ellipse", 89, 76, 12, 12, "#657A8A", "#273746", 1),
            FlatShape(E(family, style, 17), "bolt-1", "core.ellipse", 88.9, 35.9, 3.2, 3.2, "#F8FAFC", "#526575", 1),
            FlatShape(E(family, style, 18), "bolt-2", "core.ellipse", 125.9, 53.9, 3.2, 3.2, "#F8FAFC", "#526575", 1),
            FlatShape(E(family, style, 19), "bolt-3", "core.ellipse", 126.9, 99.9, 3.2, 3.2, "#F8FAFC", "#526575", 1),
            FlatShape(E(family, style, 20), "bolt-4", "core.ellipse", 89.9, 121.9, 3.2, 3.2, "#F8FAFC", "#526575", 1),
            FlatShape(E(family, style, 21), "bolt-5", "core.ellipse", 54.9, 99.9, 3.2, 3.2, "#F8FAFC", "#526575", 1),
            FlatShape(E(family, style, 22), "bolt-6", "core.ellipse", 53.9, 55.9, 3.2, 3.2, "#F8FAFC", "#526575", 1),
            FlatShape(E(family, style, 23), "foot-left", "core.rectangle", 62, 125, 18, 9, "#647789", "#273746", 1, 2),
            FlatShape(E(family, style, 24), "foot-right", "core.rectangle", 112, 125, 18, 9, "#647789", "#273746", 1, 2),
            FlatShape(E(family, style, 25), "base", "core.rectangle", 49, 134, 96, 8, "#445767", "#273746", 1, 2),
            Polygon(E(family, style, 26), "outlet-flow-arrow", 145, 22, 15, 14, [(0d, 0d), (15d, 7d), (0d, 14d)], "#13799D", "#125575", 1),
            Text(E(family, style, 27), "label", "B", 84, 73, 22, 20, 11, "#243B4A"),
            StateLamp(E(family, style, 28), "running", 7, 7, "#22C55E", "running", "{equipmentPath}.Running"),
            StateLamp(E(family, style, 29), "fault", 177, 7, "#EF4444", "fault", "{equipmentPath}.Fault"),
            FlatShape(E(family, style, 30), "impeller-eye-ring", "core.ellipse", 74, 61, 42, 42, "#00000000", "#526575", 1.5)
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
                ArcShape(E(family, style, 7), "scale-arc", 25, 19, 46, 46, 210, 510, "arc",
                    "#00000000", "#68747C", 1.5),
                Text(E(family, style, 4), "label", "PI", 32, 30, 32, 22, 11, "#111827"),
                FlatShape(E(family, style, 5), "connection", "core.rectangle", 34, 96, 28, 8, "#9CA3AF", "#374151", 1, 2),
                StateLamp(E(family, style, 6), "fault", 72, 5, "#DC2626", "fault", "{equipmentPath}.Fault"),
                FlatShape(E(family, style, 8), "needle", "core.rectangle", 47, 27, 2, 20, "#5E6A72", "#374151", 1, 1, 35),
                FlatShape(E(family, style, 9), "hub", "core.ellipse", 44, 38, 8, 8, "#5E6A72", "#374151", 1)
            ],
            parameters: IndicatorParameters());
        }

        var dimensional = style == VisualStyle.DimensionalFront;
        return Dynamo(sequence, "process.instrument.indicator", "Indicador de processo", "instrument", style, 124, 146,
        [
            MaterialShape(E(family, style, 1), "outer-case", "core.ellipse", 16, 10, 88, 88, "#AEBCC8", "#F8FAFC", "#334155", 3, 0, dimensional, "diagonal-down", dimensional),
            MaterialShape(E(family, style, 2), "face", "core.ellipse", 24, 18, 72, 72, "#F8FAFC", "#FFFFFF", "#64748B", 2, 0, dimensional, "vertical"),
            ArcShape(E(family, style, 13), "scale-arc", 30, 24, 60, 60, 210, 510, "arc",
                "#00000000", dimensional ? "#526979" : "#64748B", 1.5),
            FlatShape(E(family, style, 3), "tick-1", "core.rectangle", 58, 23, 2, 7, "#475569", "#475569", 0),
            FlatShape(E(family, style, 4), "tick-2", "core.rectangle", 81, 33, 2, 7, "#475569", "#475569", 0, 0, 45),
            FlatShape(E(family, style, 5), "tick-3", "core.rectangle", 89, 55, 2, 7, "#475569", "#475569", 0, 0, 90),
            FlatShape(E(family, style, 6), "tick-4", "core.rectangle", 36, 33, 2, 7, "#475569", "#475569", 0, 0, -45),
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
        void Bezier(string key, double x, double y, double w, double h, string path, string fill, double stroke = 2) =>
            shapes.Add(BezierShape(E(family, style, shapes.Count + 1), key, x, y, w, h, path,
                fill, dark, stroke, highPerformance ? null : light, "diagonal-down",
                dimensional && PrimaryMass(key)));
        void Arc(string key, double x, double y, double w, double h, double start, double end, string strokeFill, double stroke = 1.5) =>
            shapes.Add(ArcShape(E(family, style, shapes.Count + 1), key, x, y, w, h, start, end, "arc",
                "#00000000", strokeFill, stroke));
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
                Rect("base", 20, height - 18, width - 40, 8, dark, 2);
                Bezier("crankcase", 39, 64, 63, 30,
                    "M 7 12 C 18 4 32 2 50 2 C 68 2 82 4 93 12 L 93 88 C 82 96 68 98 50 98 C 32 98 18 96 7 88 Z",
                    shell, 2.5);
                Bar("cylinder-left", 45, 27, 18, 39, light, -5);
                Bar("cylinder-right", 75, 27, 18, 39, shell, 5);
                Rect("head-left", 40, 17, 27, 11, light, 2, 1.5);
                Rect("head-right", 72, 17, 27, 11, light, 2, 1.5);
                Rect("discharge", 91, 22, 37, 8, shell, 2, 1.5);
                Rect("inlet", 20, 42, 27, 8, shell, 2, 1.5);
                for (var index = 0; index < 2; index++)
                {
                    var finWidth = highPerformance ? 1.1 : 1.4;
                    Bar($"cooling-fin-left-{index + 1}", 49 + index * 7, 31, finWidth, 24, dark, -5);
                    Bar($"cooling-fin-right-{index + 1}", 79 + index * 7, 31, finWidth, 24, dark, 5);
                }
                Label("C", 53, 70, 25, 18, 12);
                Lamp("running", 5, 5, "running", "{equipmentPath}.Running", "#D92D20");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                Ellipse("flywheel", 96, 66, 27, 27, light, 1.5);
                Ellipse("flywheel-hub", 105, 75, 9, 9, dark, 1);
                Rect("manifold", 50, 50, 45, 6, dark, 2, 1);
                break;
            case "process.compressor.screw":
                Rect("base", 18, height - 17, width - 36, 8, dark, 2);
                Bezier("compressor-housing", 34, 27, 91, 59,
                    "M 8 9 C 20 3 80 3 92 9 L 92 91 C 80 97 20 97 8 91 C 3 76 2 24 8 9 Z",
                    shell, 2.5);
                Rect("rotor-left", 48, 42, 59, 9, light, 4, 1.2);
                Rect("rotor-right", 48, 58, 59, 9, highPerformance ? "#8C969D" : "#A5B9C8", 4, 1.2);
                Rect("inlet", 11, 39, 29, 10, shell, 2, 1.5);
                Rect("outlet", 119, 47, 36, 10, shell, 2, 1.5);
                Label("SC", 64, 73, 28, 15, 10);
                Lamp("running", 5, 5, "running", "{equipmentPath}.Running", "#D92D20");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                Rect("inlet-flange", 35, 33, 7, 22, light, 2, 1.5);
                Rect("outlet-flange", 117, 41, 7, 22, light, 2, 1.5);
                Rect("end-cover", 111, 34, 9, 45, light, 3, 1.5);
                for (var index = 0; index < (highPerformance ? 2 : 3); index++)
                {
                    Bar($"helix-upper-{index + 1}", 60 + index * 18, 40, 2, 14, dark, 28);
                    Bar($"helix-lower-{index + 1}", 60 + index * 18, 56, 2, 14, dark, -28);
                }
                break;
            case "process.valve.butterfly":
                Rect("pipe-left", 3, 51, 46, 10, shell, 2);
                Rect("pipe-right", width - 48, 51, 45, 10, shell, 2);
                Rect("flange-left", 30, 43, 8, 26, light, 1);
                Rect("flange-right", width - 38, 43, 8, 26, light, 1);
                Ellipse("body-ring", 39, 30, 64, 54, highPerformance ? "#E0E4E7" : shell, 3);
                Ellipse("disc", 54, 36, 34, 42, accent, 2.2);
                Bar("disc-edge", 68, 35, 5, 44, dark, -18);
                Bar("shaft", 69, 16, 5, 21, dark);
                Bezier("actuator", 56, 4, 31, 15,
                    "M 8 18 C 8 7 22 3 50 3 C 78 3 92 7 92 18 L 92 82 C 92 93 78 97 50 97 C 22 97 8 93 8 82 Z",
                    light, 2);
                Lamp("open", 5, 5, "open", "{equipmentPath}.Open", "#16A34A");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                shapes.Add(FlatShape(E(family, style, shapes.Count + 1), "seat-ring", "core.ellipse",
                    45, 33, 52, 48, "#00000000", dark, 1.4));
                Rect("actuator-coupling", 64, 15, 15, 8, light, 3, 1.2);
                break;
            case "process.valve.ball":
                Rect("pipe-left", 3, 51, 45, 10, shell, 2);
                Rect("pipe-right", width - 48, 51, 45, 10, shell, 2);
                Rect("flange-left", 30, 43, 8, 26, light, 1);
                Rect("flange-right", width - 38, 43, 8, 26, light, 1);
                Bezier("body", centerX - 30, 33, 60, 46,
                    "M 8 20 C 22 7 36 3 50 3 C 64 3 78 7 92 20 L 92 80 C 78 93 64 97 50 97 C 36 97 22 93 8 80 Z",
                    shell, 2.5);
                Ellipse("ball", centerX - 16, 40, 32, 32, highPerformance ? "#727D84" : accent, 2);
                Bar("bore", centerX - 14, 54, 28, 4, "#F5F7F8");
                Bar("stem", centerX - 2.5, 18, 5, 24, dark);
                Bar("handle", centerX - 18, 12, 36, 6, accent, -18);
                Lamp("open", 5, 5, "open", "{equipmentPath}.Open", "#16A34A");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                Rect("seat-left", centerX - 22, 42, 5, 28, light, 2, 1.2);
                Rect("seat-right", centerX + 17, 42, 5, 28, light, 2, 1.2);
                Ellipse("packing-gland", centerX - 7, 33, 14, 10, light, 1.2);
                break;
            case "process.valve.gate":
                Rect("pipe-left", 3, 83, 45, 10, shell, 2);
                Rect("pipe-right", width - 48, 83, 45, 10, shell, 2);
                Rect("flange-left", 30, 75, 8, 26, light, 1);
                Rect("flange-right", width - 38, 75, 8, 26, light, 1);
                Bezier("body-left", centerX - 31, 69, 31, 38,
                    "M 0 26 C 20 12 52 9 100 22 L 100 78 C 52 91 20 88 0 74 Z", shell, 2);
                Bezier("body-right", centerX, 69, 31, 38,
                    "M 100 26 C 80 12 48 9 0 22 L 0 78 C 48 91 80 88 100 74 Z", shell, 2);
                Bar("stem", centerX - 2.5, 34, 5, 38, dark);
                Ellipse("handwheel", centerX - 19, 4, 38, 32, highPerformance ? "#D5DBDF" : accent, 2);
                Ellipse("handwheel-hub", centerX - 4, 16, 8, 8, light, 1);
                Lamp("open", 5, 5, "open", "{equipmentPath}.Open", "#16A34A");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                Rect("bonnet", centerX - 14, 58, 28, 17, light, 4, 1.5);
                Ellipse("handwheel-inner", centerX - 11, 10, 22, 20, "#F5F7F8", 1);
                Bezier("gate-plate", centerX - 11, 76, 22, 24,
                    "M 8 5 L 92 5 L 76 95 L 24 95 Z", light, 1.3);
                Ellipse("packing-gland", centerX - 8, 50, 16, 11, light, 1.2);
                break;
            case "process.exchanger.shell-tube":
                Bezier("shell", 17, 35, 130, 56,
                    "M 10 4 C 4 12 2 25 2 50 C 2 75 4 88 10 96 L 90 96 C 96 88 98 75 98 50 C 98 25 96 12 90 4 Z",
                    shell, 2.5);
                Arc("head-left", 18, 36, 28, 54, 90, 270, dark);
                Arc("head-right", 118, 36, 28, 54, 270, 450, dark);
                for (var index = 0; index < 5; index++)
                    Bar($"tube-{index + 1}", 45, 49 + index * 8, 71, highPerformance ? 0.9 : 1.2, dark);
                Rect("nozzle-hot-in", 49, 15, 10, 24, accent, 2);
                Rect("nozzle-hot-out", 100, 86, 10, 25, accent, 2);
                Rect("nozzle-cold-in", 51, 87, 9, 25, shell, 2);
                Rect("nozzle-cold-out", 101, 14, 9, 25, shell, 2);
                Label("E", 70, 55, 24, 18, 12);
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                Rect("nozzle-hot-in-flange", 45, 12, 18, 4, light, 1, 1);
                Rect("nozzle-cold-out-flange", 97, 11, 17, 4, light, 1, 1);
                Rect("nozzle-cold-in-flange", 47, 109, 17, 4, light, 1, 1);
                Rect("nozzle-hot-out-flange", 96, 108, 18, 4, light, 1, 1);
                Bar("tube-sheet-left", 42, 41, 2.5, 44, dark);
                Bar("tube-sheet-right", 116, 41, 2.5, 44, dark);
                Bar("baffle-upper", 68, 42, 2, 29, dark);
                Bar("baffle-lower", 92, 57, 2, 29, dark);
                if (highPerformance)
                {
                    Rect("saddle-left", 49, 89, 18, 16, dark, 3, 1);
                    Rect("saddle-right", 105, 89, 18, 16, dark, 3, 1);
                }
                break;
            case "process.filter.strainer":
                Rect("pipe-left", 3, 35, 45, 10, shell, 2);
                Rect("pipe-right", 98, 35, 41, 10, shell, 2);
                Bezier("filter-body", 39, 22, 62, 34,
                    "M 8 12 C 20 4 80 4 92 12 L 92 88 C 80 96 20 96 8 88 Z",
                    shell, 2.5);
                Bezier("basket", 57, 48, 43, 53,
                    "M 18 2 C 31 4 45 10 58 20 L 97 73 C 88 87 74 96 59 98 L 9 39 C 3 28 6 13 18 2 Z",
                    light, 2);
                for (var index = 0; index < 4; index++)
                    Bar($"basket-slot-{index + 1}", 67 + index * 7, 65, highPerformance ? 1 : 1.4, 23, dark, -32);
                Ellipse("cap", 76, 94, 29, 10, accent);
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                Bar("basket-neck", 65, 43, 9, 25, shell, -36);
                Rect("flange-left", 34, 27, 7, 26, light, 2, 1.5);
                Rect("flange-right", 98, 27, 7, 26, light, 2, 1.5);
                break;
            case "process.mixer.agitator":
                Bezier("vessel", 26, 58, 79, 95,
                    "M 10 10 C 20 3 32 1 50 1 C 68 1 80 3 90 10 L 90 90 C 80 97 68 99 50 99 C 32 99 20 97 10 90 Z",
                    highPerformance ? "#D5DBDF" : shell, 2.5);
                Arc("tank-top", 27, 59, 77, 22, 180, 360, dark);
                Rect("liquid", 33, 98, 65, 47, highPerformance ? "#AEB7BE" : "#74B6CC", 13, 1);
                Bezier("motor", 47, 13, 39, 25,
                    "M 10 8 C 22 3 78 3 90 8 L 90 92 C 78 97 22 97 10 92 C 4 75 4 25 10 8 Z",
                    accent, 2);
                Rect("shaft", 64, 48, 5, 75, dark);
                Bar("impeller", 43, 117, 49, 6, dark);
                Bar("blade-left", 45, 111, 5, 24, dark, -28);
                Bar("blade-right", 85, 111, 5, 24, dark, 28);
                Label("MX", 51, 21, 31, 14, 10);
                Lamp("running", 5, 5, "running", "{equipmentPath}.Running", "#D92D20");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                Rect("gearbox", 54, 36, 25, 16, light, 4, 1.5);
                Ellipse("motor-end-left", 43, 17, 11, 17, light, 1.2);
                Ellipse("motor-end-right", 79, 17, 11, 17, light, 1.2);
                Rect("motor-terminal", 57, 5, 20, 10, light, 3, 1.2);
                Ellipse("coupling", 61, 45, 11, 9, dark, 1);
                Ellipse("impeller-hub", 61, 113, 10, 10, dark, 1);
                break;
            case "electrical.transformer.power":
                Rect("base", 20, height - 18, width - 40, 8, dark, 2);
                Bezier("tank", 42, 46, 66, 78,
                    "M 7 3 L 93 3 C 97 3 99 7 99 11 L 99 89 C 99 95 96 97 91 97 L 9 97 C 4 97 1 94 1 89 L 1 11 C 1 6 3 3 7 3 Z",
                    shell, 2.5);
                Bezier("cover", 37, 37, 76, 13,
                    "M 5 35 C 17 10 31 4 50 4 C 69 4 83 10 95 35 L 95 88 L 5 88 Z",
                    light, 2);
                for (var index = 0; index < 5; index++)
                    Rect($"radiator-{index + 1}", 18 + index * 4, 61, highPerformance ? 2.5 : 4, 49, accent, 1, 1);
                for (var index = 0; index < 5; index++)
                    Rect($"radiator-r-{index + 1}", 112 + index * 4, 61, highPerformance ? 2.5 : 4, 49, accent, 1, 1);
                Bezier("bushing-left", 51, 10, 13, 30,
                    "M 38 2 L 62 2 L 76 98 L 24 98 Z", light, 1.5);
                Bezier("bushing-right", 84, 10, 13, 30,
                    "M 38 2 L 62 2 L 76 98 L 24 98 Z", light, 1.5);
                Label("T", 59, 72, 31, 22, 15);
                Lamp("fault", width - 22, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                Bezier("conservator", 103, 24, 34, 16,
                    "M 8 8 C 20 2 80 2 92 8 L 92 92 C 80 98 20 98 8 92 Z", light, 1.5);
                Bar("conservator-neck", 108, 37, 5, 12, dark);
                Bezier("bushing-center", 68, 8, 13, 32,
                    "M 38 2 L 62 2 L 76 98 L 24 98 Z", light, 1.5);
                break;
            case "electrical.breaker":
                Rect("base", 18, height - 18, width - 36, 8, dark, 2);
                Rect("support-left", 34, 78, 9, 52, shell, 2);
                Rect("support-right", 88, 78, 9, 52, shell, 2);
                Bezier("interrupter", 39, 48, 55, 30,
                    "M 7 12 C 18 3 82 3 93 12 L 93 88 C 82 97 18 97 7 88 Z",
                    light, 2.5);
                Bar("contact-left", 53, 22, 6, 29, dark);
                Bar("contact-right", 77, 22, 6, 29, dark);
                Rect("terminal-left", 47, 12, 17, 8, accent, 2, 1.5);
                Rect("terminal-right", 71, 12, 17, 8, accent, 2, 1.5);
                Label("52", 51, 86, 30, 16, 10);
                Lamp("closed", 6, 5, "closed", "{equipmentPath}.Closed", "#16A34A");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                Rect("mechanism-box", 49, 80, 34, 28, shell, 3, 1.5);
                Bar("linkage-left", 43, 69, 11, 4, dark, 28);
                Bar("linkage-right", 81, 69, 11, 4, dark, -28);
                Ellipse("interrupter-cap-left", 35, 51, 12, 24, light, 1.2);
                Ellipse("interrupter-cap-right", 87, 51, 12, 24, light, 1.2);
                break;
            case "electrical.disconnector":
            case "electrical.earthing-switch":
            {
                var earthingSwitch = familyKey.EndsWith("earthing-switch", StringComparison.Ordinal);
                Bezier("support-left",
                    earthingSwitch ? 28 : 25,
                    earthingSwitch ? 78 : 64,
                    10,
                    earthingSwitch ? 35 : 49,
                    "M 30 2 C 42 8 58 8 70 2 L 86 98 L 14 98 Z", shell, 2);
                Bezier("support-right", width - 36, 64, 10, 49,
                    "M 30 2 C 42 8 58 8 70 2 L 86 98 L 14 98 Z", shell, 2);
                Ellipse("insulator-left-top",
                    earthingSwitch ? 23 : 20,
                    earthingSwitch ? 68 : 53,
                    earthingSwitch ? 18 : 20,
                    earthingSwitch ? 13 : 15,
                    light);
                Ellipse("insulator-right-top", width - 41, 53, 20, 15, light);
                Rect("contact-left",
                    earthingSwitch ? 28 : 27,
                    earthingSwitch ? 60 : 41,
                    14, 8, accent, 2, 1.2);
                Rect("contact-right", width - 40, 41, 14, 8, accent, 2, 1.2);
                Bar("blade",
                    earthingSwitch ? 35 : 34,
                    earthingSwitch ? 55 : 41,
                    width - 61,
                    8,
                    highPerformance ? "#646E75" : "#738D9F",
                    earthingSwitch ? -28 : -18);
                Rect("base", 13, 113, width - 26, 8, dark, 2);
                if (earthingSwitch)
                {
                    Bar("earth-lead", 31, 84, 5, 30, accent);
                    Bar("ground-1", 18, 98, 31, 3, accent);
                    Bar("ground-2", 23, 104, 21, 3, accent);
                }
                else
                {
                    Rect("operating-box", centerX - 17, 75, 34, 24, light, 3);
                    Label("89", centerX - 15, 78, 30, 15, 10);
                }
                Lamp("closed", 5, 5, "closed", "{equipmentPath}.Closed", "#16A34A");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                if (earthingSwitch)
                    Bar("ground-3", 27, 110, 13, 3, accent);
                break;
            }
            case "electrical.generator":
                Rect("base", 22, height - 17, width - 38, 7, dark, 2);
                Bezier("stator", 36, 30, 78, 52,
                    "M 10 5 C 22 2 78 2 90 5 L 96 18 L 96 82 L 90 95 C 78 98 22 98 10 95 L 4 82 L 4 18 Z",
                    shell, 3);
                Ellipse("rotor", 27, 35, 28, 42, light, 2);
                Ellipse("hub", 36, 49, 11, 14, accent, 1);
                Rect("shaft", 111, 51, 31, 8, dark, 2);
                for (var index = 0; index < 5; index++)
                    Bar($"stator-slot-{index + 1}", 52 + index * 12, 36, highPerformance ? 1.3 : 2, 39, dark);
                Label("G", 61, 47, 26, 18, 11);
                Lamp("running", 5, 5, "running", "{equipmentPath}.Running", "#D92D20");
                Lamp("fault", width - 23, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                Ellipse("end-bell-right", 104, 35, 19, 42, light, 1.5);
                Rect("terminal-box", 61, 16, 29, 15, light, 4, 1.5);
                Rect("foot-left", 48, 80, 18, 12, shell, 2, 1.2);
                Rect("foot-right", 91, 80, 18, 12, shell, 2, 1.2);
                break;
            case "electrical.current-transformer":
                Rect("primary-conductor", 5, 68, width - 10, 9, dark, 2, 1.5);
                Ellipse("core", 24, 38, 64, 64, highPerformance ? "#C6CDD2" : shell, 3);
                Ellipse("core-window", 41, 55, 30, 30, "#F5F7F8", 2);
                Rect("secondary-left", 79, 96, 9, 7, accent, 2, 1);
                Rect("secondary-right", 91, 96, 9, 7, accent, 2, 1);
                Label("TC", 39, 108, 34, 18, 9);
                Lamp("fault", 5, 5, "fault", "{equipmentPath}.Fault", "#EAB308");
                Rect("base", 24, height - 13, 64, 7, dark, 2, 1.5);
                Rect("terminal-box", 74, 87, 31, 22, light, 3, 1.5);
                Bar("terminal-box-neck", 73, 82, 7, 10, shell);
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
        var neutralPreviewFill = JsonSerializer.SerializeToElement(FinishProfile(style).Shell);
        // Library/Editor previews without a live state sample stay neutral. Runtime still
        // maps 0/1/2 to the same stopped/running/fault colors and public parameters.
        properties["fillColor"] = neutralPreviewFill;
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
            neutralPreviewFill);
        var metadata = element.Metadata is null ? new Dictionary<string, string>() : new Dictionary<string, string>(element.Metadata);
        metadata["dynamoStateColorParameter"] = "state";
        metadata["dynamoStateColorProfile"] = "stopped,running,fault";
        metadata["dynamoStateColorFamily"] = familyKey;
        metadata["dynamoStateColorPreview"] = "neutral-unbound";
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
        VisualStyle style,
        string familyKey)
    {
        var bounds = new List<(int Index, double X, double Y, double Width, double Height)>();
        for (var index = 0; index < elements.Count; index++)
        {
            var candidate = elements[index];
            if (candidate.Type == "core.text" && !PreserveIndustrialText(familyKey, candidate))
                continue;

            var properties = candidate.Properties;
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
        FitArtworkToCanvas(visualElements, width, height, style, familyKey);
        visualElements = visualElements
            .Select(element => ApplyIndustrialVisualGrammar(
                ApplyArtworkFinish(element, style),
                familyKey,
                style))
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
                ["visualGrammar"] = "industrial-orthographic-v1",
                ["visualGrammarGroup"] = VisualGrammarGroup(familyKey),
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
                ["visualGrammar"] = "industrial-orthographic-v1",
                ["visualGrammarGroup"] = VisualGrammarGroup(familyKey),
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

    private static VisualElementEngineeringDto ApplyIndustrialVisualGrammar(
        VisualElementEngineeringDto element,
        string familyKey,
        VisualStyle style)
    {
        if (element.Properties is null) return element;

        var properties = new Dictionary<string, JsonElement>(element.Properties);
        var metadata = element.Metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(element.Metadata);

        metadata["visualGrammar"] = "industrial-orthographic-v1";
        var visualRole = IndustrialVisualRole(familyKey, element.Key, element.Type);
        metadata["visualRole"] = visualRole;

        var finish = FinishProfile(style);
        var preserveSemanticColor = PreserveSemanticArtworkColor(familyKey, element.Key);
        if (!preserveSemanticColor &&
            properties.TryGetValue("fillColor", out var authoredFill) &&
            authoredFill.ValueKind == JsonValueKind.String)
        {
            var normalizedFill = visualRole switch
            {
                "primary-mass" => finish.Shell,
                "support-structure" => finish.Dark,
                "auxiliary-enclosure" => finish.Light,
                "process-connection" => finish.Mid,
                "fastener-detail" => finish.Mid,
                "functional-internal"
                    when TryParseArtworkColor(authoredFill.GetString(), out _, out _, out _) => finish.Mid,
                "instrument-detail"
                    when TryParseArtworkColor(authoredFill.GetString(), out _, out _, out _) => finish.Dark,
                _ when TryParseArtworkColor(authoredFill.GetString(), out var red, out var green, out var blue) &&
                    !IsNeutralArtworkColor(red, green, blue) => finish.Mid,
                _ => null
            };

            if (normalizedFill is not null)
                properties["fillColor"] = JsonSerializer.SerializeToElement(normalizedFill);
        }

        if (!preserveSemanticColor &&
            properties.TryGetValue("strokeColor", out var authoredStroke) &&
            authoredStroke.ValueKind == JsonValueKind.String &&
            visualRole is "primary-mass" or "support-structure" or "auxiliary-enclosure" or "process-connection" or "fastener-detail" or "functional-internal")
        {
            properties["strokeColor"] = JsonSerializer.SerializeToElement(
                visualRole == "primary-mass" ? finish.Outline : finish.SoftOutline);
        }

        if (style == VisualStyle.DimensionalFront &&
            visualRole == "primary-mass" &&
            properties.TryGetValue("fillStyle", out var primaryFillStyle) &&
            primaryFillStyle.ValueKind == JsonValueKind.String &&
            primaryFillStyle.GetString() == "gradient")
        {
            properties["fillSecondaryColor"] = JsonSerializer.SerializeToElement(finish.Highlight);
        }

        // Embedded alphabetic equipment labels made the catalog read like mixed iconography.
        // Preserve the element identity but keep only instrumentation IDs and the standardized
        // ANSI device numbers used by substation equipment.
        if (element.Type == "core.text" &&
            properties.ContainsKey("text"))
        {
            var preserveText = PreserveIndustrialText(familyKey, element);

            if (!preserveText && element.Key is "label" or "equipment-label" or "motor-label" or "vfd-label")
                properties["text"] = JsonSerializer.SerializeToElement(string.Empty);

            if (preserveText && properties.TryGetValue("fontSize", out var fontSize) &&
                fontSize.TryGetDouble(out var fontSizeValue))
            {
                var maximum = familyKey == "process.instrument.indicator" ? 10d : 8d;
                properties["fontSize"] = JsonSerializer.SerializeToElement(Math.Min(fontSizeValue, maximum));
                properties["fontWeight"] = JsonSerializer.SerializeToElement(600);
            }
        }

        if (element.Type == "core.rectangle" &&
            properties.TryGetValue("cornerRadius", out var radius) &&
            radius.TryGetDouble(out var radiusValue))
        {
            var maximumRadius = element.Key switch
            {
                var key when key.Contains("pipe", StringComparison.Ordinal) ||
                    key.Contains("shaft", StringComparison.Ordinal) ||
                    key.Contains("stem", StringComparison.Ordinal) => 2d,
                var key when key.Contains("base", StringComparison.Ordinal) ||
                    key.Contains("foot", StringComparison.Ordinal) => 2d,
                var key when key.Contains("vfd", StringComparison.Ordinal) ||
                    key.Contains("actuator", StringComparison.Ordinal) ||
                    key.Contains("terminal", StringComparison.Ordinal) => 4d,
                _ => 6d
            };
            properties["cornerRadius"] = JsonSerializer.SerializeToElement(Math.Min(radiusValue, maximumRadius));
        }

        // Dimensional Front uses gradients only on masses that actually describe volume.
        // Small brackets, flanges, pipes and details stay flat so the catalog reads as one
        // industrial drawing system instead of many independently shaded icons.
        if (style == VisualStyle.DimensionalFront &&
            properties.TryGetValue("width", out var widthJson) && widthJson.TryGetDouble(out var width) &&
            properties.TryGetValue("height", out var heightJson) && heightJson.TryGetDouble(out var height) &&
            width * height < 700 &&
            !IsPrimaryDimensionalMass(element.Key) &&
            properties.TryGetValue("fillStyle", out var fillStyle) &&
            fillStyle.ValueKind == JsonValueKind.String &&
            fillStyle.GetString() == "gradient")
        {
            properties["fillStyle"] = JsonSerializer.SerializeToElement("solid");
            properties.Remove("fillSecondaryColor");
            properties.Remove("gradientDirection");
        }

        if (properties.TryGetValue("strokeWidth", out var strokeWidthJson) &&
            strokeWidthJson.TryGetDouble(out var strokeWidth) &&
            strokeWidth > 0)
        {
            var role = metadata["visualRole"];
            var normalizedStroke = role switch
            {
                "primary-mass" => Math.Clamp(strokeWidth, 1.5, 2),
                "support-structure" => style == VisualStyle.HighPerformance ? 1d : 1.5d,
                "auxiliary-enclosure" => style == VisualStyle.HighPerformance ? 1d : 1.5d,
                "fastener-detail" => 1d,
                "process-connection" => Math.Clamp(strokeWidth, 1, 1.5),
                "functional-internal" => style == VisualStyle.HighPerformance ? 1d : 1.5d,
                "instrument-detail" => 1d,
                _ => Math.Clamp(strokeWidth, 1, 1.5)
            };
            normalizedStroke = Math.Round(normalizedStroke * 2, MidpointRounding.AwayFromZero) / 2;
            properties["strokeWidth"] = JsonSerializer.SerializeToElement(normalizedStroke);
        }

        return element with { Properties = properties, Metadata = metadata };
    }

    private static bool PreserveIndustrialText(string familyKey, VisualElementEngineeringDto element) =>
        element.Type == "core.text" &&
        (familyKey == "process.instrument.indicator" ||
         (element.Key == "equipment-label" &&
            familyKey is "electrical.breaker" or "electrical.disconnector"));

    private static bool PreserveSemanticArtworkColor(string familyKey, string key) =>
        key is "running" or "fault" or "open" or "closed" or "high" or
            "liquid" or "liquid-line" or "needle" ||
        (familyKey == "process.instrument.indicator" && key is "face" or "inner");

    private static string IndustrialVisualRole(string familyKey, string key, string type)
    {
        if (IsPrimaryDimensionalMass(key) ||
            key is "volute-case" or "motor-end" or "case-cover" or "rotor" or "outer-case")
            return "primary-mass";
        if (key.StartsWith("detail-", StringComparison.Ordinal) &&
            (key.Contains("bolt", StringComparison.Ordinal) ||
             key.Contains("fastener", StringComparison.Ordinal)))
            return "fastener-detail";
        if (key == "base" ||
            key.EndsWith("-base", StringComparison.Ordinal) ||
            key.StartsWith("foot-", StringComparison.Ordinal) ||
            key.StartsWith("leg-", StringComparison.Ordinal) ||
            key.StartsWith("saddle-", StringComparison.Ordinal) ||
            key.Contains("mounting-rail", StringComparison.Ordinal))
            return "support-structure";
        if (key is "vfd" or "actuator" or "terminal" or "terminal-box" or "mechanism-box" or
            "gearbox" or "bonnet" or "terminal-cover")
            return "auxiliary-enclosure";
        if (key.Contains("pipe", StringComparison.Ordinal) ||
            key.Contains("flange", StringComparison.Ordinal) ||
            key.Contains("nozzle", StringComparison.Ordinal) ||
            key.Contains("inlet", StringComparison.Ordinal) ||
            key.Contains("outlet", StringComparison.Ordinal))
            return "process-connection";
        if (familyKey == "process.instrument.indicator" &&
            (key is "scale-arc" or "needle" or "hub" ||
             key.StartsWith("tick-", StringComparison.Ordinal)))
            return "instrument-detail";
        if (IsFunctionalInternalKey(key))
            return "functional-internal";
        if (type == "core.text") return "annotation";
        return "secondary-detail";
    }

    private static bool IsFunctionalInternalKey(string key) =>
        key is "wear-ring" or "volute-tongue" or "impeller" or "impeller-eye-ring" or
            "seat-ring" or "closure-member" or "plug" or "gate-plate" or "packing-gland" or
            "bore" or "disc" or "disc-edge" or "ball" or "core-window" ||
        key is "rotor-left" or "rotor-right" ||
        key.StartsWith("tube-sheet-", StringComparison.Ordinal) ||
        key.StartsWith("baffle-", StringComparison.Ordinal) ||
        key.StartsWith("winding-band-", StringComparison.Ordinal) ||
        key.StartsWith("ground-", StringComparison.Ordinal);

    private static string VisualGrammarGroup(string familyKey) => familyKey switch
    {
        "dynamo.pump.standard" or "process.pump.submersible" or "process.motor.standard" or
        "process.motor.vfd" or "process.blower.centrifugal" or "process.compressor.reciprocating" or
        "process.compressor.screw" or "process.mixer.agitator" or "electrical.generator" => "rotating-machinery",
        "process.valve.onoff" or "process.valve.control" or "process.valve.butterfly" or
        "process.valve.ball" or "process.valve.gate" => "inline-valve",
        "process.tank.vertical" or "process.tank.horizontal" or "process.exchanger.shell-tube" or
        "process.filter.strainer" => "static-process-equipment",
        "process.instrument.indicator" => "instrumentation",
        _ => "substation-electrical"
    };

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

        void Dot(string key, double x, double y, double size = 5, string fill = "#DCE7EF", string stroke = "#526879")
        {
            var fastener = key.Contains("bolt", StringComparison.Ordinal) ||
                key.Contains("fastener", StringComparison.Ordinal);
            var actualSize = fastener ? Math.Min(size, 3.2) : size;
            var actualFill = fastener ? "#8EA0AD" : fill;
            details.Add(FlatShape(1000 + sequence * 100 + localSequence++, $"detail-{key}", "core.ellipse",
                x + (size - actualSize) / 2, y + (size - actualSize) / 2,
                actualSize, actualSize, actualFill, stroke, fastener ? 0.8 : 1));
        }

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
                RadialBolts("casing-bolt", 75, 59, 36, 4, 4);
                break;

            case "process.pump.submersible":
                var bodyLeft = style == VisualStyle.HighPerformance ? 27 : 37;
                var bodyWidth = style == VisualStyle.HighPerformance ? 40 : 38;
                var upperVentY = style == VisualStyle.HighPerformance ? 35 : 43;
                var lowerVentY = style == VisualStyle.HighPerformance ? 83 : 105;
                for (var index = 0; index < 3; index++)
                {
                    Bar($"upper-cooling-slot-{index + 1}", bodyLeft + index * (bodyWidth / 3d), upperVentY, 2.2, 9, "#64798B");
                    Bar($"lower-cooling-slot-{index + 1}", bodyLeft + index * (bodyWidth / 3d), lowerVentY, 2.2, 9, "#64798B");
                }
                break;

            case "process.motor.standard":
                // Keep family-specific details aligned with the redesigned side elevation.
                // The central frame carries the cooling ribs; the end bells/cowl remain visually clean.
                for (var index = 0; index < 2; index++)
                {
                    Bar($"cooling-rib-left-{index + 1}", 49 + index * 6, 33, 1.8, 31, "#73889A");
                    Bar($"cooling-rib-right-{index + 1}", 92 + index * 6, 33, 1.8, 31, "#73889A");
                }
                localSequence += 2; // reserve historical cooling-rib-left/right-3 slots
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
                // The authored face already contains the primary scale ticks.
                // Additional ticks doubled the visual weight and made the gauge look icon-like.
                break;

            case "process.compressor.reciprocating":
                RadialBolts("crankcase-fastener", 70, 79, 24, 4, 3.2);
                localSequence += 7; // reserve 2 fastener + 5 cylinder-fin historical slots
                Dot("crosshead-pin", 70, 76, 6, "#DCE5EB", "#526879");
                Bar("connecting-rod", 81, 76, 3, 18, "#586D7D", -52);
                Bar("discharge-manifold-seam", 94, 24, 28, 1.5, "#8295A5");
                break;

            case "process.compressor.screw":
                RadialBolts("housing-fastener", 80, 56, 42, 4, 3.2);
                localSequence += 6; // reserve 4 fastener + 2 rotor-highlight historical slots
                Bar("oil-sight-glass", 112, 67, 4, 9, "#4B9BB4");
                Bar("housing-seam", 113, 37, 1.5, 38, "#8295A5");
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
                Bar("saddle-support-1", 52, 91, 9, 19, "#526575");
                localSequence++; // reserve historical saddle-support-2 slot
                Bar("saddle-support-3", 108, 91, 9, 19, "#526575");
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
                Bar("drain-neck", 86, 92, 6, 13, "#526575", -18);
                Dot("drain-plug", 84, 101, 8, "#B6C4CE", "#526879");
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
                for (var index = 0; index < 4; index++)
                {
                    Bar($"left-radiator-channel-{index + 1}", 20 + index * 5, 64, 1.5, 43, "#526575");
                    Bar($"right-radiator-channel-{index + 1}", 114 + index * 5, 64, 1.5, 43, "#526575");
                }
                localSequence += 2; // reserve historical left/right-radiator-channel-5 slots
                Dot("oil-level-window", 102, 54, 8, "#4B9BB4", "#526879");
                Bar("nameplate", 55, 104, 39, 10, "#E7EEF3");
                foreach (var bushingX in new[] { 52d, 68d, 83d })
                {
                    Bar($"bushing-rib-upper-{bushingX:0}", bushingX - 2, 18, 15, 2, "#8295A5");
                    Bar($"bushing-rib-lower-{bushingX:0}", bushingX - 2, 27, 15, 2, "#8295A5");
                }
                break;

            case "electrical.breaker":
                for (var index = 0; index < 4; index++)
                {
                    Bar($"left-post-rib-{index + 1}", 34, 78 + index * 12, 8, 2, "#F0F4F6");
                    Bar($"right-post-rib-{index + 1}", 89, 78 + index * 12, 8, 2, "#F0F4F6");
                }
                localSequence += 2; // reserve historical left/right-post-rib-5 slots
                foreach (var terminalX in new[] { 58d, 78d })
                    Dot($"terminal-fastener-{terminalX:0}", terminalX, 10, 5, "#F0F4F6", "#526879");
                break;

            case "electrical.disconnector":
            case "electrical.earthing-switch":
            {
                var earthingSwitch = familyKey.EndsWith("earthing-switch", StringComparison.Ordinal);
                for (var index = 0; index < 4; index++)
                {
                    Bar($"left-insulator-rib-{index + 1}",
                        earthingSwitch ? 24 : 21,
                        (earthingSwitch ? 83 : 68) + index * (earthingSwitch ? 7 : 9),
                        earthingSwitch ? 16 : 18,
                        2,
                        "#F0F4F6");
                    Bar($"right-insulator-rib-{index + 1}", width - 42, 68 + index * 9, 18, 2, "#F0F4F6");
                }
                Dot("blade-pivot", earthingSwitch ? 30 : 31, earthingSwitch ? 60 : 42, 6, "#DCE5EB", "#526879");
                Bar("contact-jaw", width - 39, 40, 10, 4, "#B6C4CE");
                break;
            }

            case "electrical.generator":
                RadialBolts("end-shield-fastener", 41, 56, 15, 4, 3.2);
                localSequence += 4; // reserve historical end-shield-fastener-5..8 slots
                for (var index = 0; index < 4; index++)
                    Bar($"stator-vent-{index + 1}", 58 + index * 13, 37, 2, 37, "#526575");
                break;

            case "electrical.current-transformer":
                Bar("winding-band-1", 30, 49, 15, 1.4, "#8295A5");
                Bar("winding-band-2", 30, 84, 15, 1.4, "#8295A5");
                Bar("winding-band-3", 68, 49, 15, 1.4, "#8295A5");
                Bar("winding-band-4", 68, 84, 15, 1.4, "#8295A5");
                localSequence++; // reserve historical winding-band-5 slot
                Dot("secondary-terminal-20", 81, 98, 4, "#E7EEF3", "#526879");
                Dot("secondary-terminal-84", 93, 98, 4, "#E7EEF3", "#526879");
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
