using System.Globalization;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Dnp3;

public sealed record Dnp3ParsedDataSourceSettings(
    Dnp3TcpConnectionOptions Connection,
    Dnp3AssociationOptions Association);

public sealed record Dnp3DataSourceSettingsParseResult(
    Dnp3ParsedDataSourceSettings? Value,
    IReadOnlyCollection<DriverEngineeringIssue> Issues)
{
    public bool Succeeded => Value is not null && Issues.All(issue => issue.Severity != DriverEngineeringIssueSeverity.Error);
}

/// <summary>
/// Pure parser/validator for DNP3 Data Source settings declared by
/// <see cref="Dnp3DriverDescriptorProvider"/>. It intentionally does not parse
/// TAG bindings from ad-hoc metadata; canonical rich DNP3 binding integration
/// remains a Coordinator-owned Engineering contract decision.
/// </summary>
public static class Dnp3DataSourceSettingsParser
{
    private static readonly HashSet<string> KnownKeys = Dnp3DriverDescriptorProvider.SharedDescriptor
        .ConfigurationSchema
        .DataSourceFields
        .Select(field => field.Key)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static Dnp3DataSourceSettingsParseResult Parse(IReadOnlyDictionary<string, string>? settings)
    {
        var issues = new List<DriverEngineeringIssue>();
        var map = Normalize(settings, issues);

        var transport = Required(map, "transport", issues);
        if (transport is not null && !transport.Equals("tcp", StringComparison.OrdinalIgnoreCase))
            AddError(issues, "DNP3_TRANSPORT_UNSUPPORTED", "Only DNP3 TCP transport is supported in the current driver contract.", "transport");

        var host = Required(map, "host", issues);
        var port = ParseInt(map, "port", 20000, 1, 65535, issues);
        var masterAddress = ParseRequiredUShort(map, "masterAddress", 0, Dnp3TcpConnectionOptions.MaxIndividualLinkAddress, issues);
        var outstationAddress = ParseRequiredUShort(map, "outstationAddress", 0, Dnp3TcpConnectionOptions.MaxIndividualLinkAddress, issues);
        var connectTimeout = ParseDuration(map, "connectTimeout", TimeSpan.FromSeconds(5), issues)!.Value;

        var responseTimeout = ParseDuration(map, "responseTimeout", TimeSpan.FromSeconds(5), issues)!.Value;
        var reconnectMinDelay = ParseDuration(map, "reconnectMinDelay", TimeSpan.FromSeconds(1), issues)!.Value;
        var reconnectMaxDelay = ParseDuration(map, "reconnectMaxDelay", TimeSpan.FromSeconds(30), issues)!.Value;
        var keepAliveTimeout = ParseOptionalDuration(map, "keepAliveTimeout", TimeSpan.FromSeconds(60), issues);
        var integrityPollInterval = ParseOptionalDuration(map, "integrityPollInterval", TimeSpan.FromMinutes(15), issues);
        var class1PollInterval = ParseOptionalDuration(map, "class1PollInterval", null, issues);
        var class2PollInterval = ParseOptionalDuration(map, "class2PollInterval", null, issues);
        var class3PollInterval = ParseOptionalDuration(map, "class3PollInterval", null, issues);

        var startupIntegrityClasses = ComposeClasses(
            ParseBool(map, "startupIntegrityClass0", true, issues),
            ParseBool(map, "startupIntegrityClass1", true, issues),
            ParseBool(map, "startupIntegrityClass2", true, issues),
            ParseBool(map, "startupIntegrityClass3", true, issues));

        var disableUnsolicited = ComposeEventClasses(
            ParseBool(map, "disableUnsolicitedClass1OnStartup", true, issues),
            ParseBool(map, "disableUnsolicitedClass2OnStartup", true, issues),
            ParseBool(map, "disableUnsolicitedClass3OnStartup", true, issues));

        var enableUnsolicited = ComposeEventClasses(
            ParseBool(map, "enableUnsolicitedClass1AfterIntegrity", true, issues),
            ParseBool(map, "enableUnsolicitedClass2AfterIntegrity", true, issues),
            ParseBool(map, "enableUnsolicitedClass3AfterIntegrity", true, issues));

        var eventScanOnAvailable = ComposeEventClasses(
            ParseBool(map, "eventScanClass1OnEventsAvailable", true, issues),
            ParseBool(map, "eventScanClass2OnEventsAvailable", true, issues),
            ParseBool(map, "eventScanClass3OnEventsAvailable", true, issues));

        var integrityOnOverflow = ParseBool(map, "integrityOnEventBufferOverflow", true, issues);
        var timeSyncMode = ParseTimeSyncMode(map, issues);
        var maxQueuedUserRequests = ParseInt(map, "maxQueuedUserRequests", 16, 1, 1024, issues);

        if (issues.Any(issue => issue.Severity == DriverEngineeringIssueSeverity.Error) ||
            host is null || masterAddress is null || outstationAddress is null)
            return new Dnp3DataSourceSettingsParseResult(null, issues);

        var connection = new Dnp3TcpConnectionOptions
        {
            Host = host,
            Port = port,
            MasterAddress = masterAddress.Value,
            OutstationAddress = outstationAddress.Value,
            ConnectTimeout = connectTimeout
        };

        var association = new Dnp3AssociationOptions
        {
            StartupIntegrityClasses = startupIntegrityClasses,
            DisableUnsolicitedClassesOnStartup = disableUnsolicited,
            EnableUnsolicitedClassesAfterIntegrity = enableUnsolicited,
            EventScanOnEventsAvailable = eventScanOnAvailable,
            ResponseTimeout = responseTimeout,
            ReconnectMinDelay = reconnectMinDelay,
            ReconnectMaxDelay = reconnectMaxDelay,
            KeepAliveTimeout = keepAliveTimeout,
            IntegrityPollInterval = integrityPollInterval,
            Class1PollInterval = class1PollInterval,
            Class2PollInterval = class2PollInterval,
            Class3PollInterval = class3PollInterval,
            IntegrityOnEventBufferOverflow = integrityOnOverflow,
            TimeSyncMode = timeSyncMode,
            MaxQueuedUserRequests = maxQueuedUserRequests
        };

        try
        {
            connection.Validate();
        }
        catch (ArgumentException ex)
        {
            AddError(issues, "DNP3_CONNECTION_INVALID", Sanitize(ex.Message), MapParameterToField(ex.ParamName));
        }

        try
        {
            association.Validate();
        }
        catch (ArgumentException ex)
        {
            AddError(issues, "DNP3_ASSOCIATION_INVALID", Sanitize(ex.Message), MapParameterToField(ex.ParamName));
        }

        return issues.Any(issue => issue.Severity == DriverEngineeringIssueSeverity.Error)
            ? new Dnp3DataSourceSettingsParseResult(null, issues)
            : new Dnp3DataSourceSettingsParseResult(new Dnp3ParsedDataSourceSettings(connection, association), issues);
    }

    private static Dictionary<string, string> Normalize(
        IReadOnlyDictionary<string, string>? settings,
        List<DriverEngineeringIssue> issues)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (settings is null) return map;

        foreach (var pair in settings)
        {
            if (!map.TryAdd(pair.Key, pair.Value))
            {
                AddError(issues, "DNP3_SETTING_DUPLICATE", $"Setting '{pair.Key}' duplicates another key differing only by case.", pair.Key);
                continue;
            }

            if (!KnownKeys.Contains(pair.Key))
            {
                issues.Add(new DriverEngineeringIssue(
                    "DNP3_SETTING_UNKNOWN",
                    DriverEngineeringIssueSeverity.Warning,
                    $"Unknown DNP3 setting '{pair.Key}' is ignored by contract version 1.",
                    pair.Key));
            }
        }

        return map;
    }

    private static string? Required(
        IReadOnlyDictionary<string, string> map,
        string key,
        List<DriverEngineeringIssue> issues)
    {
        if (!map.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            AddError(issues, "DNP3_SETTING_REQUIRED", $"DNP3 setting '{key}' is required.", key);
            return null;
        }

        return value;
    }

    private static ushort? ParseRequiredUShort(
        IReadOnlyDictionary<string, string> map,
        string key,
        ushort minimum,
        ushort maximum,
        List<DriverEngineeringIssue> issues)
    {
        var raw = Required(map, key, issues);
        if (raw is null) return null;

        if (ushort.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= minimum && parsed <= maximum)
            return parsed;

        AddError(issues, "DNP3_SETTING_INVALID", $"Setting '{key}' must be a decimal integer from {minimum} to {maximum}; received '{raw}'.", key);
        return null;
    }

    private static int ParseInt(
        IReadOnlyDictionary<string, string> map,
        string key,
        int fallback,
        int minimum,
        int maximum,
        List<DriverEngineeringIssue> issues)
    {
        if (!map.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return fallback;
        if (int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= minimum && parsed <= maximum)
            return parsed;

        AddError(issues, "DNP3_SETTING_INVALID", $"Setting '{key}' must be a decimal integer from {minimum} to {maximum}; received '{raw}'.", key);
        return fallback;
    }

    private static bool ParseBool(
        IReadOnlyDictionary<string, string> map,
        string key,
        bool fallback,
        List<DriverEngineeringIssue> issues)
    {
        if (!map.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return fallback;
        if (bool.TryParse(raw, out var parsed)) return parsed;

        AddError(issues, "DNP3_SETTING_INVALID", $"Setting '{key}' must be 'true' or 'false'; received '{raw}'.", key);
        return fallback;
    }

    private static TimeSpan? ParseDuration(
        IReadOnlyDictionary<string, string> map,
        string key,
        TimeSpan? fallback,
        List<DriverEngineeringIssue> issues)
    {
        if (!map.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return fallback;

        if (TimeSpan.TryParseExact(raw, "c", CultureInfo.InvariantCulture, out var parsed) && parsed > TimeSpan.Zero)
            return parsed;

        AddError(issues, "DNP3_SETTING_INVALID", $"Setting '{key}' must be a positive invariant TimeSpan such as '00:00:05'; received '{raw}'.", key);
        return fallback;
    }

    private static TimeSpan? ParseOptionalDuration(
        IReadOnlyDictionary<string, string> map,
        string key,
        TimeSpan? fallback,
        List<DriverEngineeringIssue> issues)
    {
        if (!map.TryGetValue(key, out var raw)) return fallback;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return ParseDuration(map, key, fallback, issues);
    }

    private static Dnp3TimeSyncMode ParseTimeSyncMode(
        IReadOnlyDictionary<string, string> map,
        List<DriverEngineeringIssue> issues)
    {
        if (!map.TryGetValue("timeSyncMode", out var raw) || string.IsNullOrWhiteSpace(raw))
            return Dnp3TimeSyncMode.Disabled;

        if (raw.Equals("disabled", StringComparison.OrdinalIgnoreCase)) return Dnp3TimeSyncMode.Disabled;
        if (raw.Equals("lan", StringComparison.OrdinalIgnoreCase)) return Dnp3TimeSyncMode.Lan;
        if (raw.Equals("nonLan", StringComparison.OrdinalIgnoreCase)) return Dnp3TimeSyncMode.NonLan;

        AddError(issues, "DNP3_SETTING_INVALID", $"Setting 'timeSyncMode' must be disabled, lan or nonLan; received '{raw}'.", "timeSyncMode");
        return Dnp3TimeSyncMode.Disabled;
    }

    private static Dnp3ClassSet ComposeClasses(bool class0, bool class1, bool class2, bool class3)
    {
        var result = Dnp3ClassSet.None;
        if (class0) result |= Dnp3ClassSet.Class0;
        if (class1) result |= Dnp3ClassSet.Class1;
        if (class2) result |= Dnp3ClassSet.Class2;
        if (class3) result |= Dnp3ClassSet.Class3;
        return result;
    }

    private static Dnp3ClassSet ComposeEventClasses(bool class1, bool class2, bool class3) =>
        ComposeClasses(false, class1, class2, class3);

    private static string? MapParameterToField(string? parameterName) => parameterName switch
    {
        nameof(Dnp3TcpConnectionOptions.Host) => "host",
        nameof(Dnp3TcpConnectionOptions.Port) => "port",
        nameof(Dnp3TcpConnectionOptions.MasterAddress) => "masterAddress",
        nameof(Dnp3TcpConnectionOptions.OutstationAddress) => "outstationAddress",
        nameof(Dnp3TcpConnectionOptions.ConnectTimeout) => "connectTimeout",
        nameof(Dnp3AssociationOptions.StartupIntegrityClasses) => "startupIntegrityClass0",
        nameof(Dnp3AssociationOptions.ResponseTimeout) => "responseTimeout",
        nameof(Dnp3AssociationOptions.ReconnectMinDelay) => "reconnectMinDelay",
        nameof(Dnp3AssociationOptions.ReconnectMaxDelay) => "reconnectMaxDelay",
        nameof(Dnp3AssociationOptions.KeepAliveTimeout) => "keepAliveTimeout",
        nameof(Dnp3AssociationOptions.IntegrityPollInterval) => "integrityPollInterval",
        nameof(Dnp3AssociationOptions.Class1PollInterval) => "class1PollInterval",
        nameof(Dnp3AssociationOptions.Class2PollInterval) => "class2PollInterval",
        nameof(Dnp3AssociationOptions.Class3PollInterval) => "class3PollInterval",
        nameof(Dnp3AssociationOptions.MaxQueuedUserRequests) => "maxQueuedUserRequests",
        _ => parameterName
    };

    private static string Sanitize(string message)
    {
        var value = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length <= 512 ? value : value[..512];
    }

    private static void AddError(
        List<DriverEngineeringIssue> issues,
        string code,
        string message,
        string? fieldKey) =>
        issues.Add(new DriverEngineeringIssue(code, DriverEngineeringIssueSeverity.Error, message, fieldKey));
}
