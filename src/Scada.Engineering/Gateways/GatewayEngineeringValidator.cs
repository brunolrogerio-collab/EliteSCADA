using Scada.Core.Tags;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.Gateways;

public static class GatewayEngineeringValidator
{
    public const int MinimumOnChangeIntervalMilliseconds = 100;
    public const int MinimumPeriodicIntervalMilliseconds = 100;
    public const int MaximumIntervalMilliseconds = 86_400_000;

    public static IReadOnlyCollection<ImportIssue> Validate(GatewayRouteEngineeringDto route)
    {
        var issues = new List<ImportIssue>();
        var key = string.IsNullOrWhiteSpace(route.Key) ? route.Name : route.Key;

        if (string.IsNullOrWhiteSpace(route.Key))
            issues.Add(Error("GATEWAY_KEY_REQUIRED", "Gateway route key is required.", key));
        if (route.Key?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("GATEWAY_KEY_WHITESPACE", "Gateway route key cannot contain whitespace.", key));
        if (string.IsNullOrWhiteSpace(route.Name))
            issues.Add(Error("GATEWAY_NAME_REQUIRED", "Gateway route name is required.", key));

        if (route.SourceTagId is null && string.IsNullOrWhiteSpace(route.SourceTagPath))
            issues.Add(Error("GATEWAY_SOURCE_TAG_REQUIRED", "Gateway route must reference a source TAG by stable ID or path.", key));
        if (route.DestinationTagId is null && string.IsNullOrWhiteSpace(route.DestinationTagPath))
            issues.Add(Error("GATEWAY_DESTINATION_TAG_REQUIRED", "Gateway route must reference a destination TAG by stable ID or path.", key));
        if (route.SourceTagPath?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("GATEWAY_SOURCE_TAG_PATH_WHITESPACE", "Gateway source TAG path cannot contain whitespace.", key));
        if (route.DestinationTagPath?.Any(char.IsWhiteSpace) == true)
            issues.Add(Error("GATEWAY_DESTINATION_TAG_PATH_WHITESPACE", "Gateway destination TAG path cannot contain whitespace.", key));

        if (!Enum.IsDefined(route.TransferMode))
            issues.Add(Error("GATEWAY_TRANSFER_MODE_INVALID", $"Gateway transfer mode '{route.TransferMode}' is not supported.", key));
        if (!Enum.IsDefined(route.QualityPolicy))
            issues.Add(Error("GATEWAY_QUALITY_POLICY_INVALID", $"Gateway quality policy '{route.QualityPolicy}' is not supported.", key));
        if (!Enum.IsDefined(route.ConversionPolicy))
            issues.Add(Error("GATEWAY_CONVERSION_POLICY_INVALID", $"Gateway conversion policy '{route.ConversionPolicy}' is not supported.", key));
        if (!Enum.IsDefined(route.InitialTransferPolicy))
            issues.Add(Error("GATEWAY_INITIAL_TRANSFER_POLICY_INVALID", $"Gateway initial-transfer policy '{route.InitialTransferPolicy}' is not supported.", key));

        ValidateFinite(route.Gain, "GATEWAY_GAIN_INVALID", "Gateway gain must be a finite number.", key, issues);
        ValidateFinite(route.Offset, "GATEWAY_OFFSET_INVALID", "Gateway offset must be a finite number.", key, issues);
        ValidateFinite(route.Deadband, "GATEWAY_DEADBAND_INVALID", "Gateway deadband must be a finite number.", key, issues);
        if (route.Deadband < 0)
            issues.Add(Error("GATEWAY_DEADBAND_NEGATIVE", "Gateway deadband cannot be negative.", key));

        switch (route.TransferMode)
        {
            case GatewayTransferMode.OnChange:
                if (route.PeriodMilliseconds is not null)
                    issues.Add(Error("GATEWAY_ON_CHANGE_PERIOD_NOT_ALLOWED", "OnChange routes cannot define PeriodMilliseconds.", key));
                if (route.MinimumIntervalMilliseconds is < MinimumOnChangeIntervalMilliseconds or > MaximumIntervalMilliseconds)
                    issues.Add(Error(
                        "GATEWAY_ON_CHANGE_INTERVAL_OUT_OF_RANGE",
                        $"OnChange MinimumIntervalMilliseconds must be between {MinimumOnChangeIntervalMilliseconds} and {MaximumIntervalMilliseconds} when configured.",
                        key));
                break;

            case GatewayTransferMode.Periodic:
                if (route.PeriodMilliseconds is null)
                    issues.Add(Error("GATEWAY_PERIOD_REQUIRED", "Periodic routes require PeriodMilliseconds.", key));
                else if (route.PeriodMilliseconds is < MinimumPeriodicIntervalMilliseconds or > MaximumIntervalMilliseconds)
                    issues.Add(Error(
                        "GATEWAY_PERIOD_OUT_OF_RANGE",
                        $"Periodic PeriodMilliseconds must be between {MinimumPeriodicIntervalMilliseconds} and {MaximumIntervalMilliseconds}.",
                        key));
                if (route.MinimumIntervalMilliseconds is not null)
                    issues.Add(Error("GATEWAY_PERIODIC_MINIMUM_INTERVAL_NOT_ALLOWED", "Periodic routes use PeriodMilliseconds and cannot also define MinimumIntervalMilliseconds.", key));
                if (route.Deadband is not null)
                    issues.Add(Error("GATEWAY_PERIODIC_DEADBAND_NOT_ALLOWED", "Periodic routes cannot define OnChange deadband semantics.", key));
                break;
        }

        if (route.ConversionPolicy == GatewayConversionPolicy.Exact && (route.Gain is not null || route.Offset is not null))
        {
            issues.Add(Error(
                "GATEWAY_TRANSFORM_REQUIRES_NUMERIC_CONVERSION",
                "Gain/offset transforms require the explicit CheckedNumeric conversion policy.",
                key));
        }

        return issues;
    }

    public static bool IsNumeric(TagDataType dataType) => dataType is
        TagDataType.Int16 or
        TagDataType.Int32 or
        TagDataType.Int64 or
        TagDataType.Float or
        TagDataType.Double;

    private static void ValidateFinite(
        double? value,
        string code,
        string message,
        string key,
        List<ImportIssue> issues)
    {
        if (value is not null && (double.IsNaN(value.Value) || double.IsInfinity(value.Value)))
            issues.Add(Error(code, message, key));
    }

    internal static ImportIssue Error(string code, string message, string key) =>
        new(code, message, ImportEntityKind.Gateway, key, true);
}