using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.Intelligence;

/// <summary>Closed, repository-owned unit catalog. Codes are stable identities; labels remain on the TAG.</summary>
public static class ControlledEngineeringUnitCatalog
{
    public const int CurrentCatalogVersion = 1;

    private static readonly IReadOnlyDictionary<string, ControlledEngineeringUnitDefinition> Definitions =
        new Dictionary<string, ControlledEngineeringUnitDefinition>(StringComparer.Ordinal)
        {
            ["unit.ratio.percent"] = new("unit.ratio.percent", "ratio", "1", 0.01d),
            ["unit.electric_current.ampere"] = new("unit.electric_current.ampere", "electric_current", "A", 1d),
            ["unit.frequency.hertz"] = new("unit.frequency.hertz", "frequency", "Hz", 1d),
            ["unit.pressure.bar"] = new("unit.pressure.bar", "pressure", "Pa", 100000d),
            ["unit.volumetric_flow.cubic_metre_per_hour"] = new("unit.volumetric_flow.cubic_metre_per_hour", "volumetric_flow", "m³/s", 1d / 3600d)
        };

    public static IReadOnlyCollection<ControlledEngineeringUnitDefinition> All => Definitions.Values.ToArray();

    public static bool TryResolve(
        ControlledEngineeringUnitReference? reference,
        out ControlledEngineeringUnitDefinition definition)
    {
        definition = default!;
        return reference is not null &&
               reference.CatalogVersion == CurrentCatalogVersion &&
               !string.IsNullOrWhiteSpace(reference.Code) &&
               Definitions.TryGetValue(reference.Code, out definition!);
    }

    public static IReadOnlyCollection<AssetProcessIntelligenceEngineeringIssue> ValidateReference(
        ControlledEngineeringUnitReference? reference,
        AssetProcessIntelligenceIssueEntity entity = AssetProcessIntelligenceIssueEntity.Profile,
        Guid? resourceId = null)
    {
        if (reference is null)
            return [new("ENGINEERING_UNIT_REFERENCE_REQUIRED", "A controlled unit reference is required for a semantic signal.", entity, resourceId)];
        if (reference.CatalogVersion != CurrentCatalogVersion)
            return [new("ENGINEERING_UNIT_CATALOG_VERSION_UNSUPPORTED", $"Unit catalog version '{reference.CatalogVersion}' is unsupported; expected {CurrentCatalogVersion}.", entity, resourceId)];
        if (string.IsNullOrWhiteSpace(reference.Code) || !Definitions.ContainsKey(reference.Code))
            return [new("ENGINEERING_UNIT_CODE_UNSUPPORTED", "The controlled unit code is missing or is not present in unit catalog version 1.", entity, resourceId)];
        return Array.Empty<AssetProcessIntelligenceEngineeringIssue>();
    }

    /// <summary>Converts a finite value to the catalog's canonical unit using invariant binary coefficients.</summary>
    public static bool TryConvertToCanonical(
        double value,
        ControlledEngineeringUnitReference? reference,
        out double canonicalValue)
    {
        canonicalValue = default;
        if (!double.IsFinite(value) || !TryResolve(reference, out var definition))
            return false;
        canonicalValue = value * definition.ScaleToCanonical;
        return double.IsFinite(canonicalValue);
    }
}

public sealed record ControlledEngineeringUnitDefinition(
    string Code,
    string Dimension,
    string CanonicalUnit,
    double ScaleToCanonical);

/// <summary>Closed v1 role catalog. It validates annotation completeness only; it does not calculate process KPIs.</summary>
public static class AssetProcessIntelligenceSemanticCatalog
{
    public const int CurrentCatalogVersion = 1;

    public static IReadOnlyCollection<AssetProcessIntelligenceEngineeringIssue> ValidateBinding(
        AssetProcessIntelligenceSignalSemanticBindingEngineeringDto? binding,
        Guid signalId)
    {
        if (binding is null) return Array.Empty<AssetProcessIntelligenceEngineeringIssue>();
        var issues = new List<AssetProcessIntelligenceEngineeringIssue>();
        if (binding.CatalogVersion != CurrentCatalogVersion)
        {
            issues.Add(new(
                "INTELLIGENCE_SEMANTIC_CATALOG_VERSION_UNSUPPORTED",
                $"Signal semantic catalog version '{binding.CatalogVersion}' is unsupported; expected {CurrentCatalogVersion}.",
                AssetProcessIntelligenceIssueEntity.Profile,
                signalId));
            return issues;
        }

        string? dimension = binding.RoleCode switch
        {
            "pump.electrical_current" => "electric_current",
            "pump.drive_frequency" => "frequency",
            "pump.discharge_pressure" => "pressure",
            "pump.discharge_volumetric_flow" => "volumetric_flow",
            _ => null
        };
        if (dimension is null)
        {
            issues.Add(new(
                "INTELLIGENCE_SEMANTIC_ROLE_UNSUPPORTED",
                "The semantic role is missing or is not present in role catalog version 1.",
                AssetProcessIntelligenceIssueEntity.Profile,
                signalId));
            return issues;
        }

        ValidateChoice(binding.CurrentBasis, binding.RoleCode == "pump.electrical_current"
            ? ["line_rms", "phase_rms", "dc_bus"] : null, "currentBasis", signalId, issues);
        ValidateChoice(binding.SignalKind, binding.RoleCode == "pump.drive_frequency"
            ? ["measured_output", "command_setpoint"] : null, "signalKind", signalId, issues);
        ValidateChoice(binding.FrequencyReference, binding.RoleCode == "pump.drive_frequency"
            ? ["drive_electrical", "shaft_rotational"] : null, "frequencyReference", signalId, issues);
        ValidateChoice(binding.PressureReference, binding.RoleCode == "pump.discharge_pressure"
            ? ["gauge", "absolute"] : null, "pressureReference", signalId, issues);
        ValidateChoice(binding.FlowDirection, binding.RoleCode == "pump.discharge_volumetric_flow"
            ? ["pump_to_process", "process_to_pump", "bidirectional_signed", "bidirectional_magnitude"] : null, "flowDirection", signalId, issues);
        return issues;
    }

    public static string? DimensionForRole(string? roleCode) => roleCode switch
    {
        "pump.electrical_current" => "electric_current",
        "pump.drive_frequency" => "frequency",
        "pump.discharge_pressure" => "pressure",
        "pump.discharge_volumetric_flow" => "volumetric_flow",
        _ => null
    };

    private static void ValidateChoice(
        string? value,
        IReadOnlyCollection<string>? allowed,
        string field,
        Guid signalId,
        List<AssetProcessIntelligenceEngineeringIssue> issues)
    {
        if (allowed is null)
        {
            if (value is not null)
                issues.Add(new("INTELLIGENCE_SEMANTIC_QUALIFIER_NOT_APPLICABLE", $"Qualifier '{field}' is not valid for this role.", AssetProcessIntelligenceIssueEntity.Profile, signalId));
            return;
        }
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new("INTELLIGENCE_SEMANTIC_QUALIFIER_INCOMPLETE", $"Required semantic qualifier '{field}' is missing.", AssetProcessIntelligenceIssueEntity.Profile, signalId));
            return;
        }
        if (!allowed.Contains(value, StringComparer.Ordinal))
            issues.Add(new("INTELLIGENCE_SEMANTIC_QUALIFIER_UNSUPPORTED", $"Qualifier '{field}' has an unsupported value.", AssetProcessIntelligenceIssueEntity.Profile, signalId));
    }
}
