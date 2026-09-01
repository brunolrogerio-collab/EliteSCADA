using System.Globalization;
using System.IO.BACnet;

namespace Scada.Drivers.Bacnet;

public sealed record BacnetRuntimeConfiguration(
    uint DeviceInstance,
    BacnetSessionOptions SessionOptions,
    TimeSpan ScanRate);

/// <summary>
/// Protocol-owned parsing authority used by the converged runtime planner. It
/// keeps BACnet network/configuration semantics below the shared host boundary.
/// </summary>
public static class BacnetRuntimeConfigurationParser
{
    public static bool TryCreate(
        IReadOnlyDictionary<string, string> settings,
        out BacnetRuntimeConfiguration? configuration,
        out IReadOnlyCollection<string> errors)
    {
        ArgumentNullException.ThrowIfNull(settings);
        configuration = null;
        var issues = new List<string>();

        var deviceInstance = ParseRequiredUInt(settings, "deviceInstance", 0, BacnetBinding.MaximumDeviceInstance, issues);
        var localPort = ParseInt(settings, "localPort", BacnetClient.DEFAULT_UDP_PORT, 1, 65535, issues);
        var scanMs = ParseInt(settings, "scanIntervalMilliseconds", 1000, 50, 600000, issues);
        var timeoutMs = ParseInt(settings, "requestTimeoutMilliseconds", 3000, 100, 60000, issues);
        var discoveryMs = ParseInt(settings, "discoveryWindowMilliseconds", 1500, 100, 30000, issues);
        var bbmdAddress = Get(settings, "bbmdAddress");
        var targetAddress = Get(settings, "targetAddress");
        var localEndpointIp = Get(settings, "localEndpointIp");
        var foreignTtl = ParseOptionalInt(settings, "foreignDeviceTtlSeconds", 30, short.MaxValue, issues);
        if (foreignTtl.HasValue && string.IsNullOrWhiteSpace(bbmdAddress))
            issues.Add("BACnet Foreign Device Registration requires setting 'bbmdAddress'.");

        BacnetSessionOptions? options = null;
        if (issues.Count == 0)
        {
            options = new BacnetSessionOptions(
                LocalPort: localPort,
                RequestTimeout: TimeSpan.FromMilliseconds(timeoutMs),
                Retries: 2,
                DiscoveryWindow: TimeSpan.FromMilliseconds(discoveryMs),
                BbmdAddress: bbmdAddress,
                ForeignDeviceTtlSeconds: foreignTtl,
                TargetAddress: targetAddress,
                LocalEndpointIp: localEndpointIp);
            try
            {
                options.Validate();
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                issues.Add(ex.Message);
            }
        }

        if (issues.Count == 0 && options is not null)
            configuration = new BacnetRuntimeConfiguration(deviceInstance, options, TimeSpan.FromMilliseconds(scanMs));

        errors = issues;
        return configuration is not null;
    }

    private static uint ParseRequiredUInt(
        IReadOnlyDictionary<string, string> settings,
        string key,
        uint minimum,
        uint maximum,
        List<string> errors)
    {
        var raw = Get(settings, key);
        if (uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) &&
            value >= minimum && value <= maximum)
            return value;
        errors.Add($"BACnet setting '{key}' is required from {minimum} to {maximum}.");
        return minimum;
    }

    private static int ParseInt(
        IReadOnlyDictionary<string, string> settings,
        string key,
        int defaultValue,
        int minimum,
        int maximum,
        List<string> errors)
    {
        var raw = Get(settings, key);
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
            value >= minimum && value <= maximum)
            return value;
        errors.Add($"BACnet setting '{key}' must be from {minimum} to {maximum}.");
        return defaultValue;
    }

    private static int? ParseOptionalInt(
        IReadOnlyDictionary<string, string> settings,
        string key,
        int minimum,
        int maximum,
        List<string> errors)
    {
        var raw = Get(settings, key);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
            value >= minimum && value <= maximum)
            return value;
        errors.Add($"BACnet setting '{key}' must be from {minimum} to {maximum}.");
        return null;
    }

    private static string? Get(IReadOnlyDictionary<string, string> settings, string key)
    {
        foreach (var item in settings)
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) return item.Value;
        return null;
    }
}
