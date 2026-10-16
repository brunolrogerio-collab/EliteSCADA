using System.Collections.ObjectModel;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.Intelligence;

namespace Scada.Api.Licensing;

public sealed record AssetProcessIntelligencePerformanceV1BQueryRequest(
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid AssignmentId,
    Guid ProfileId,
    string RuleId,
    IReadOnlyList<Guid>? SignalIds,
    AssetProcessIntelligenceAnalyticsWindow? Window);

public sealed record AssetProcessIntelligencePerformanceV1BRule(
    string Id,
    string Version,
    string Kind,
    string Formula,
    IReadOnlyList<string> InputRoles,
    IReadOnlyList<AssetProcessIntelligencePerformanceV1BUnitSupport> InputUnits,
    string OutputUnit,
    IReadOnlyList<AssetProcessIntelligencePerformanceV1BOutputUnitVariant> OutputUnitVariants,
    string TemporalRequirement,
    int MinimumQualifiedTuples,
    string Bounds,
    IReadOnlyList<string> UnavailableStates);

public sealed record AssetProcessIntelligencePerformanceV1BUnitSupport(
    string Role,
    string CanonicalUnit,
    IReadOnlyList<int> UnitCatalogVersions,
    IReadOnlyList<string> AcceptedUnitCodes);

public sealed record AssetProcessIntelligencePerformanceV1BOutputUnitVariant(
    string ProductionRole,
    string CanonicalInputUnit,
    string OutputUnit);

public static class AssetProcessIntelligencePerformanceV1BCatalog
{
    public const int ContractVersion = 1;
    public const int CatalogVersion = 1;
    public const string RuleVersion = "1.0.0";
    public const int QueryPageSize = 200;
    public const int MaximumRawRowsAcrossInputs = 50000;
    public static readonly TimeSpan MaximumWindow = TimeSpan.FromDays(31);

    private static readonly IReadOnlyList<AssetProcessIntelligencePerformanceV1BRule> Catalog =
        Array.AsReadOnly<AssetProcessIntelligencePerformanceV1BRule>(
        [
            new(
                "specific-active-energy-at-recorded-production-rate.v1",
                RuleVersion,
                "derived",
                "active_input_power_W / net_good_production_rate_{volume|mass|item}_per_second / 3,600,000 = kWh per product unit",
                Array.AsReadOnly<string>(["equipment.active_input_power", "process.net_good_production_volume_rate|process.net_good_production_mass_rate|process.net_good_production_item_rate"]),
                Array.AsReadOnly<AssetProcessIntelligencePerformanceV1BUnitSupport>([
                    new("equipment.active_input_power", "W", Array.AsReadOnly([2]), Array.AsReadOnly<string>(["unit.power.watt", "unit.power.kilowatt"])),
                    new("process.net_good_production_volume_rate", "m³/s", Array.AsReadOnly([2]), Array.AsReadOnly<string>(["unit.volumetric_flow.cubic_metre_per_second"])),
                    new("process.net_good_production_mass_rate", "kg/s", Array.AsReadOnly([2]), Array.AsReadOnly<string>(["unit.mass_flow.kilogram_per_second", "unit.mass_flow.kilogram_per_hour"])),
                    new("process.net_good_production_item_rate", "item/s", Array.AsReadOnly([2]), Array.AsReadOnly<string>(["unit.item_rate.item_per_second", "unit.item_rate.item_per_hour"]))]),
                "role-specific; select exactly the output matching the selected net-good production-rate role",
                Array.AsReadOnly<AssetProcessIntelligencePerformanceV1BOutputUnitVariant>([
                    new("process.net_good_production_volume_rate", "m³/s", "kWh/m³"),
                    new("process.net_good_production_mass_rate", "kg/s", "kWh/kg"),
                    new("process.net_good_production_item_rate", "item/s", "kWh/item")]),
                "Exact equality of distinct synchronized source timestamps; no interpolation, nearest-time join, carry-forward or assumed cadence.",
                2,
                "Active power must be >= 0 W; recorded production rate must be > 0 in its explicit canonical unit/s.",
                Array.AsReadOnly<string>(["fewer_than_two_qualified_tuples", "source_timestamp_missing", "source_timestamp_evidence_missing", "source_timestamp_not_synchronized", "source_timestamp_outside_requested_window", "ambiguous_source_timestamp", "no_exact_source_timestamp_pair", "value_not_finite_numeric", "unit_conversion_non_finite", "nonpositive_production_rate", "negative_power", "non_finite_arithmetic"])),
            new(
                "recorded-differential-pressure-power.v1",
                RuleVersion,
                "derived",
                "flow_m3_per_second * direct_differential_pressure_Pa = pressure-only W",
                Array.AsReadOnly<string>(["pump.discharge_volumetric_flow", "pump.direct_differential_pressure"]),
                Array.AsReadOnly<AssetProcessIntelligencePerformanceV1BUnitSupport>([
                    new("pump.discharge_volumetric_flow", "m³/s", Array.AsReadOnly<int>([1, 2]), Array.AsReadOnly<string>(["unit.volumetric_flow.cubic_metre_per_hour", "unit.volumetric_flow.cubic_metre_per_second"])),
                    new("pump.direct_differential_pressure", "Pa", Array.AsReadOnly<int>([1, 2]), Array.AsReadOnly<string>(["unit.pressure.bar", "unit.pressure.pascal", "unit.pressure.kilopascal"]))]),
                "W",
                Array.Empty<AssetProcessIntelligencePerformanceV1BOutputUnitVariant>(),
                "Exact equality of distinct synchronized source timestamps; no interpolation, nearest-time join, carry-forward or assumed cadence.",
                2,
                "Pump-to-process flow must be > 0 m³/s; direct outlet-minus-inlet differential pressure must be >= 0 Pa.",
                Array.AsReadOnly<string>(["fewer_than_two_qualified_tuples", "source_timestamp_missing", "source_timestamp_evidence_missing", "source_timestamp_not_synchronized", "source_timestamp_outside_requested_window", "ambiguous_source_timestamp", "no_exact_source_timestamp_pair", "value_not_finite_numeric", "unit_conversion_non_finite", "flow_not_forward_positive", "negative_differential_pressure", "non_finite_arithmetic"])),
            new(
                "recorded-pressure-power-input-ratio.v1",
                RuleVersion,
                "derived",
                "100 * flow_m3_per_second * direct_differential_pressure_Pa / measured_total_active_input_power_W",
                Array.AsReadOnly<string>(["pump.discharge_volumetric_flow", "pump.direct_differential_pressure", "equipment.active_input_power"]),
                Array.AsReadOnly<AssetProcessIntelligencePerformanceV1BUnitSupport>([
                    new("pump.discharge_volumetric_flow", "m³/s", Array.AsReadOnly<int>([1, 2]), Array.AsReadOnly<string>(["unit.volumetric_flow.cubic_metre_per_hour", "unit.volumetric_flow.cubic_metre_per_second"])),
                    new("pump.direct_differential_pressure", "Pa", Array.AsReadOnly<int>([1, 2]), Array.AsReadOnly<string>(["unit.pressure.bar", "unit.pressure.pascal", "unit.pressure.kilopascal"])),
                    new("equipment.active_input_power", "W", Array.AsReadOnly<int>([2]), Array.AsReadOnly<string>(["unit.power.watt", "unit.power.kilowatt"]))]),
                "% pressure-only power / measured electrical input",
                Array.Empty<AssetProcessIntelligencePerformanceV1BOutputUnitVariant>(),
                "Exact equality of distinct synchronized source timestamps; no interpolation, nearest-time join, carry-forward or assumed cadence.",
                2,
                "Pump-to-process flow must be > 0 m³/s; differential pressure >= 0 Pa; measured active input power > 0 W. Finite results above 100% are retained and flagged.",
                Array.AsReadOnly<string>(["fewer_than_two_qualified_tuples", "source_timestamp_missing", "source_timestamp_evidence_missing", "source_timestamp_not_synchronized", "source_timestamp_outside_requested_window", "ambiguous_source_timestamp", "no_exact_source_timestamp_pair", "value_not_finite_numeric", "unit_conversion_non_finite", "flow_not_forward_positive", "negative_differential_pressure", "nonpositive_input_power", "non_finite_arithmetic"]))
        ]);

    public static IReadOnlyList<AssetProcessIntelligencePerformanceV1BRule> Rules => Catalog;

    public static AssetProcessIntelligencePerformanceV1BRule? FindRule(string? id) =>
        Catalog.SingleOrDefault(rule => string.Equals(rule.Id, id, StringComparison.Ordinal));

    public static object Describe() => new
    {
        contractVersion = ContractVersion,
        catalogVersion = CatalogVersion,
        ruleVersion = RuleVersion,
        dataset = HistoricalDatasets.HistorianSamplesV2,
        datasetContractVersion = 2,
        maximumWindowDays = (int)MaximumWindow.TotalDays,
        maximumRawRowsAcrossSelectedTags = MaximumRawRowsAcrossInputs,
        pageSize = QueryPageSize,
        ordering = "observed Historian timestamp ascending per TAG; tuple joining uses only explicit synchronized source timestamps",
        completeness = "unknown_without_expected_cadence",
        physicalSimultaneity = "unverified; equal source timestamps do not prove physical co-acquisition",
        processRegimeEquivalence = "unverified",
        statuses = new[] { AssetProcessIntelligencePerformanceV1BCalculator.AvailableStatus, AssetProcessIntelligencePerformanceV1BCalculator.InsufficientDataStatus },
        rules = Catalog
    };
}

public sealed record AssetProcessIntelligencePerformanceV1BSignal(
    Guid SignalId,
    string SignalKey,
    Guid TagId,
    string TagPath,
    TagDataType TagDataType,
    int SemanticCatalogVersion,
    string RoleCode,
    string? CurrentBasis,
    string? SignalKind,
    string? FrequencyReference,
    string? PressureReference,
    string? FlowDirection,
    string ControlledUnitCode,
    int UnitCatalogVersion,
    string CanonicalUnit);

public sealed record AssetProcessIntelligencePerformanceV1BInputEvidence(
    Guid SignalId,
    int RawObservationCount,
    int EligibleSynchronizedCount,
    int PairedCount,
    int QualifiedCount,
    IReadOnlyDictionary<string, int> QualityCounts,
    IReadOnlyDictionary<string, int> RejectionCounts);

public sealed record AssetProcessIntelligencePerformanceV1BValueEvidence(
    Guid SignalId,
    DateTimeOffset ObservedTimestampUtc,
    double CanonicalValue);

public sealed record AssetProcessIntelligencePerformanceV1BTuple(
    DateTimeOffset SourceTimestampUtc,
    IReadOnlyList<AssetProcessIntelligencePerformanceV1BValueEvidence> Inputs,
    string Status,
    double? Value,
    string? ReasonCode,
    bool AboveOneHundredPercent);

public sealed record AssetProcessIntelligencePerformanceV1BSummary(
    string Status,
    string Unit,
    IReadOnlyList<string> ReasonCodes,
    int QualifiedTupleCount,
    double? Minimum,
    double? Maximum,
    double? ArithmeticMean,
    string MeanSemantics);

public sealed record AssetProcessIntelligencePerformanceV1BProvenance(
    string Dataset,
    int DatasetContractVersion,
    string TimestampPairing,
    string AcquisitionAlignment,
    string OperatingRegimeEquivalence,
    string ExpectedCadence,
    string Completeness,
    string MeasurementInterpretation);

public sealed record AssetProcessIntelligencePerformanceV1BResponse(
    int ContractVersion,
    int CatalogVersion,
    string RuleId,
    string RuleVersion,
    string ProjectKey,
    long ActiveRevision,
    AssetProcessIntelligenceScopeKind ScopeKind,
    Guid ScopeId,
    Guid AssignmentId,
    Guid EquipmentId,
    Guid ProfileId,
    AssetProcessIntelligenceAnalyticsWindow Window,
    IReadOnlyList<AssetProcessIntelligencePerformanceV1BSignal> Signals,
    IReadOnlyList<AssetProcessIntelligencePerformanceV1BInputEvidence> Inputs,
    IReadOnlyList<AssetProcessIntelligencePerformanceV1BTuple> Tuples,
    int TotalRawObservationCount,
    AssetProcessIntelligencePerformanceV1BSummary Summary,
    AssetProcessIntelligencePerformanceV1BProvenance Provenance);

internal sealed record AssetProcessIntelligencePerformanceV1BRawObservation(
    DateTimeOffset ObservedTimestampUtc,
    string? Quality,
    HistoricalQueryValue? Value,
    DateTimeOffset? SourceTimestampUtc,
    string? SourceTimestampEvidence);

internal sealed record AssetProcessIntelligencePerformanceV1BPreparedSignal(
    AssetProcessIntelligenceSignalEngineeringDto Signal,
    TagDefinition Tag,
    AssetProcessIntelligenceSignalSemanticBindingEngineeringDto Binding,
    ControlledEngineeringUnitDefinition Unit,
    AssetProcessIntelligencePerformanceV1BSignal Description,
    AssetProcessIntelligenceAnalyticsService.PreparedPlan Plan);

internal sealed record AssetProcessIntelligencePerformanceV1BCalculation(
    IReadOnlyList<AssetProcessIntelligencePerformanceV1BInputEvidence> Inputs,
    IReadOnlyList<AssetProcessIntelligencePerformanceV1BTuple> Tuples,
    AssetProcessIntelligencePerformanceV1BSummary Summary);

internal static class AssetProcessIntelligencePerformanceV1BCalculator
{
    public const string AvailableStatus = "available";
    public const string InsufficientDataStatus = "insufficient_data";

    public static AssetProcessIntelligencePerformanceV1BCalculation Calculate(
        AssetProcessIntelligencePerformanceV1BRule rule,
        AssetProcessIntelligenceAnalyticsWindow window,
        IReadOnlyList<AssetProcessIntelligencePerformanceV1BPreparedSignal> signals,
        IReadOnlyDictionary<Guid, IReadOnlyList<AssetProcessIntelligencePerformanceV1BRawObservation>> observations)
    {
        var evaluated = signals.Select(signal => EvaluateInput(signal, observations[signal.Signal.Id], window)).ToArray();
        var exactTimes = evaluated.Length == 0
            ? new HashSet<DateTimeOffset>()
            : evaluated.Skip(1).Aggregate(
                evaluated[0].Accepted.Keys.ToHashSet(),
                (current, next) => { current.IntersectWith(next.Accepted.Keys); return current; });
        foreach (var input in evaluated)
        {
            var unpaired = input.Accepted.Keys.Count(timestamp => !exactTimes.Contains(timestamp));
            if (unpaired > 0) Increment(input.Rejections, "no_exact_source_timestamp_pair", unpaired);
        }
        var tuples = new List<AssetProcessIntelligencePerformanceV1BTuple>(exactTimes.Count);
        foreach (var sourceTimestamp in exactTimes.Order())
        {
            var aligned = evaluated.Select(input => input.Accepted[sourceTimestamp]).ToArray();
            var canonicalByRole = signals.Select((signal, index) => (signal.Binding.RoleCode, aligned[index].CanonicalValue))
                .ToDictionary(item => item.RoleCode, item => item.CanonicalValue, StringComparer.Ordinal);
            var values = aligned.Select((sample, index) => new AssetProcessIntelligencePerformanceV1BValueEvidence(
                signals[index].Signal.Id, sample.ObservedTimestampUtc, sample.CanonicalValue)).ToArray();
            var formula = CalculateTuple(rule.Id, canonicalByRole);
            tuples.Add(new(sourceTimestamp, Array.AsReadOnly(values), formula.Value.HasValue ? "qualified" : "rejected",
                formula.Value, formula.ReasonCode, formula.AboveOneHundredPercent));
            for (var index = 0; index < evaluated.Length; index++)
            {
                var input = evaluated[index];
                input.PairedCount++;
                if (formula.Value.HasValue) input.QualifiedCount++;
                else if (formula.ReasonCode is not null) Increment(input.Rejections, formula.ReasonCode);
            }
        }

        var qualified = tuples.Where(tuple => tuple.Value.HasValue).Select(tuple => tuple.Value!.Value).ToArray();
        var reasons = tuples.Where(tuple => tuple.ReasonCode is not null)
            .Select(tuple => tuple.ReasonCode!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        reasons.AddRange(evaluated.SelectMany(input => input.Rejections.Keys));
        if (qualified.Length < rule.MinimumQualifiedTuples)
        {
            reasons.Add("fewer_than_two_qualified_tuples");
            return new(
                Array.AsReadOnly(evaluated.Select((input, index) => input.ToEvidence(signals[index].Signal.Id)).ToArray()),
                Array.AsReadOnly(tuples.ToArray()),
                new(InsufficientDataStatus, OutputUnit(rule, signals), Array.AsReadOnly(reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()),
                    qualified.Length, null, null, null, "arithmetic mean of qualified pointwise formula results; not time-weighted"));
        }

        var mean = FiniteMean(qualified);
        if (!mean.HasValue)
            reasons.Add("non_finite_arithmetic_mean");
        var overOneHundred = tuples.Any(tuple => tuple.AboveOneHundredPercent);
        if (overOneHundred) reasons.Add("result_above_100_percent_observed_not_clipped");
        return new(
            Array.AsReadOnly(evaluated.Select((input, index) => input.ToEvidence(signals[index].Signal.Id)).ToArray()),
            Array.AsReadOnly(tuples.ToArray()),
            new(AvailableStatus, OutputUnit(rule, signals), Array.AsReadOnly(reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()),
                qualified.Length, qualified.Min(), qualified.Max(), mean,
                "arithmetic mean of qualified pointwise formula results; not time-weighted"));
    }

    private static string OutputUnit(
        AssetProcessIntelligencePerformanceV1BRule rule,
        IReadOnlyList<AssetProcessIntelligencePerformanceV1BPreparedSignal> signals)
    {
        if (rule.Id != "specific-active-energy-at-recorded-production-rate.v1") return rule.OutputUnit;
        var productionRole = signals.Single(signal => signal.Binding.RoleCode.StartsWith(
            "process.net_good_production_", StringComparison.Ordinal)).Binding.RoleCode;
        return rule.OutputUnitVariants.Single(variant => variant.ProductionRole == productionRole).OutputUnit;
    }

    private static InputEvaluation EvaluateInput(
        AssetProcessIntelligencePerformanceV1BPreparedSignal signal,
        IReadOnlyList<AssetProcessIntelligencePerformanceV1BRawObservation> rows,
        AssetProcessIntelligenceAnalyticsWindow window)
    {
        var result = new InputEvaluation(rows.Count);
        foreach (var row in rows)
        {
            Increment(result.QualityCounts, row.Quality ?? "Missing");
            if (!string.Equals(row.Quality, TagQuality.Good.ToString(), StringComparison.Ordinal))
            {
                Increment(result.Rejections, row.Quality is null ? "quality_missing" : "quality_not_good");
                continue;
            }
            if (!TryFiniteNumeric(row.Value, out var numeric))
            {
                Increment(result.Rejections, "value_not_finite_numeric");
                continue;
            }
            if (!row.SourceTimestampUtc.HasValue)
            {
                Increment(result.Rejections, "source_timestamp_missing");
                continue;
            }
            if (!string.Equals(row.SourceTimestampEvidence, TimestampEvidenceState.Synchronized.ToString(), StringComparison.Ordinal))
            {
                Increment(result.Rejections, row.SourceTimestampEvidence is null
                    ? "source_timestamp_evidence_missing" : "source_timestamp_not_synchronized");
                continue;
            }
            var timestamp = row.SourceTimestampUtc.Value;
            if (timestamp.Offset != TimeSpan.Zero || timestamp < window.FromUtc || timestamp > window.ToUtc)
            {
                Increment(result.Rejections, "source_timestamp_outside_requested_window");
                continue;
            }
            if (!ControlledEngineeringSemanticsResolver.TryConvertToCanonical(numeric, signal.Tag.ControlledEngineeringUnit, out var canonical))
            {
                Increment(result.Rejections, "unit_conversion_non_finite");
                continue;
            }
            result.ValidCandidates.TryAdd(timestamp, new AcceptedSample(row.ObservedTimestampUtc, canonical));
        }

        var duplicateTimestamps = rows
            .Where(row => row.SourceTimestampUtc is { } timestamp && timestamp.Offset == TimeSpan.Zero &&
                          timestamp >= window.FromUtc && timestamp <= window.ToUtc)
            .GroupBy(row => row.SourceTimestampUtc!.Value)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
        foreach (var duplicate in duplicateTimestamps)
        {
            result.ValidCandidates.Remove(duplicate);
            Increment(result.Rejections, "ambiguous_source_timestamp", rows.Count(row => row.SourceTimestampUtc == duplicate));
        }
        result.Accepted = result.ValidCandidates;
        result.EligibleCount = result.Accepted.Count;
        return result;
    }

    private static (double? Value, string? ReasonCode, bool AboveOneHundredPercent) CalculateTuple(
        string ruleId,
        IReadOnlyDictionary<string, double> values)
    {
        double result;
        if (ruleId == "specific-active-energy-at-recorded-production-rate.v1")
        {
            var power = values["equipment.active_input_power"];
            var rate = values.Single(pair => pair.Key.StartsWith("process.net_good_production_", StringComparison.Ordinal)).Value;
            if (power < 0) return (null, "negative_power", false);
            if (rate <= 0) return (null, "nonpositive_production_rate", false);
            result = power / rate / 3_600_000d;
        }
        else
        {
            var flow = values["pump.discharge_volumetric_flow"];
            var pressure = values["pump.direct_differential_pressure"];
            if (flow <= 0) return (null, "flow_not_forward_positive", false);
            if (pressure < 0) return (null, "negative_differential_pressure", false);
            var pressurePower = flow * pressure;
            if (!double.IsFinite(pressurePower)) return (null, "non_finite_arithmetic", false);
            if (ruleId == "recorded-differential-pressure-power.v1") result = pressurePower;
            else
            {
                var inputPower = values["equipment.active_input_power"];
                if (inputPower <= 0) return (null, "nonpositive_input_power", false);
                result = 100d * pressurePower / inputPower;
            }
        }
        if (!double.IsFinite(result)) return (null, "non_finite_arithmetic", false);
        return (result, null, ruleId == "recorded-pressure-power-input-ratio.v1" && result > 100d);
    }

    private static bool TryFiniteNumeric(HistoricalQueryValue? value, out double numeric)
    {
        numeric = default;
        if (value is null || value.Kind is not (HistoricalValueKind.Int16 or HistoricalValueKind.Int32 or
            HistoricalValueKind.Int64 or HistoricalValueKind.Float or HistoricalValueKind.Double or HistoricalValueKind.Number) ||
            value.Value is null || !double.TryParse(value.Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out numeric)) return false;
        return double.IsFinite(numeric);
    }

    private static double? FiniteMean(IReadOnlyList<double> values)
    {
        var scale = values.Max(value => Math.Abs(value));
        if (scale == 0) return 0d;
        var normalizedMean = values.Sum(value => value / scale) / values.Count;
        var mean = normalizedMean * scale;
        return double.IsFinite(mean) ? mean : null;
    }

    private static void Increment(IDictionary<string, int> counts, string code, int amount = 1) =>
        counts[code] = (counts.TryGetValue(code, out var current) ? current : 0) + amount;

    private sealed record AcceptedSample(DateTimeOffset ObservedTimestampUtc, double CanonicalValue);

    private sealed class InputEvaluation(int rawCount)
    {
        public int RawObservationCount { get; } = rawCount;
        public int EligibleCount { get; set; }
        public int PairedCount { get; set; }
        public int QualifiedCount { get; set; }
        public Dictionary<DateTimeOffset, AcceptedSample> ValidCandidates { get; } = [];
        public Dictionary<DateTimeOffset, AcceptedSample> Accepted { get; set; } = [];
        public SortedDictionary<string, int> QualityCounts { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, int> Rejections { get; } = new(StringComparer.Ordinal);

        public AssetProcessIntelligencePerformanceV1BInputEvidence ToEvidence(Guid signalId) => new(
            signalId,
            RawObservationCount,
            EligibleCount,
            PairedCount,
            QualifiedCount,
            new ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(QualityCounts, StringComparer.Ordinal)),
            new ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(Rejections, StringComparer.Ordinal)));
    }
}
