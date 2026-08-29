using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Bacnet;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

/// <summary>
/// BACnet-specific canonical Engineering compiler kept isolated from the current
/// monolithic EngineeringDriverCompiler so parallel protocol branches do not all
/// edit the same dispatch switch. The coordinator can compose protocol planners
/// during integration without changing BACnet persistence semantics.
/// </summary>
public sealed record BacnetIpRuntimePlan(
    string DataSourceKey,
    string Name,
    BacnetSessionOptions SessionOptions,
    TimeSpan ScanRate,
    IReadOnlyCollection<BacnetPoint> Points);

public sealed record BacnetEngineeringPlanningResult(
    BacnetIpRuntimePlan? Plan,
    IReadOnlyCollection<EngineeringDriverIssue> Issues)
{
    public bool CanActivate => Plan is not null && Issues.All(x => !x.IsError);
}

public static class BacnetEngineeringRuntimePlanner
{
    public static BacnetEngineeringPlanningResult Plan(EngineeringPackage package, DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);
        var issues = new List<EngineeringDriverIssue>();

        if (!dataSource.Driver.Equals(BacnetDriverDescriptor.DriverType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("BACNET_DRIVER_TYPE_INVALID", $"Data source '{dataSource.Key}' is not a BACnet/IP data source.", dataSource.Key));
            return new(null, issues);
        }

        var settings = dataSource.Settings ?? new Dictionary<string, string>();
        var deviceInstance = ParseUInt(settings, "deviceInstance", 0, BacnetBinding.MaximumDeviceInstance, dataSource.Key, issues);
        var localPort = ParseInt(settings, "localPort", 47808, 1, 65535, dataSource.Key, issues);
        var scanMs = ParseInt(settings, "scanIntervalMilliseconds", 1000, 50, 600000, dataSource.Key, issues);
        var timeoutMs = ParseInt(settings, "requestTimeoutMilliseconds", 3000, 100, 60000, dataSource.Key, issues);
        var discoveryMs = ParseInt(settings, "discoveryWindowMilliseconds", 1500, 100, 30000, dataSource.Key, issues);
        var bbmdAddress = Get(settings, "bbmdAddress");
        var foreignTtl = ParseOptionalInt(settings, "foreignDeviceTtlSeconds", 30, short.MaxValue, dataSource.Key, issues);
        if (foreignTtl.HasValue && string.IsNullOrWhiteSpace(bbmdAddress))
            issues.Add(Error("BACNET_BBMD_REQUIRED_FOR_FDR", "BACnet Foreign Device Registration requires setting 'bbmdAddress'.", dataSource.Key));

        var sourceTags = package.Tags
            .Where(x => string.Equals(x.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceTags.Length == 0)
            issues.Add(new EngineeringDriverIssue("BACNET_DATASOURCE_NO_TAGS", $"Enabled BACnet/IP data source '{dataSource.Key}' has no associated TAGs.", dataSource.Key, IsError: false));

        var points = new List<BacnetPoint>();
        foreach (var dto in sourceTags)
        {
            if (!BacnetBinding.TryParse(dto.Address, out var parsed, out var parseError) || parsed is null)
            {
                issues.Add(Error("BACNET_TAG_ADDRESS_INVALID", parseError ?? "BACnet TAG address is invalid.", dataSource.Key, dto.Path));
                continue;
            }
            if (parsed.DeviceInstance != deviceInstance)
            {
                issues.Add(Error(
                    "BACNET_TAG_DEVICE_MISMATCH",
                    $"BACnet TAG '{dto.Path}' targets Device Instance {parsed.DeviceInstance}, but data source '{dataSource.Key}' targets {deviceInstance}.",
                    dataSource.Key,
                    dto.Path));
                continue;
            }

            var metadata = dto.Metadata ?? new Dictionary<string, string>();
            var useCov = ParseBool(metadata, "bacnet.useCov", parsed.UseCov, dataSource.Key, dto.Path, issues);
            var priority = ParseOptionalByte(metadata, "bacnet.writePriority", 1, 16, dataSource.Key, dto.Path, issues) ?? parsed.WritePriority;
            var binding = parsed with { UseCov = useCov, WritePriority = priority };
            var point = new BacnetPoint(BuildTagDefinition(dto), binding, Writable: !dto.ReadOnly);
            try
            {
                point.Validate();
                points.Add(point);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                issues.Add(Error("BACNET_TAG_CONFIGURATION_INVALID", ex.Message, dataSource.Key, dto.Path));
            }
        }

        if (issues.Any(x => x.IsError)) return new(null, issues);

        var sessionOptions = new BacnetSessionOptions(
            localPort,
            TimeSpan.FromMilliseconds(timeoutMs),
            Retries: 2,
            TimeSpan.FromMilliseconds(discoveryMs),
            bbmdAddress,
            foreignTtl);
        try
        {
            sessionOptions.Validate();
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            issues.Add(Error("BACNET_DATASOURCE_CONFIGURATION_INVALID", ex.Message, dataSource.Key));
            return new(null, issues);
        }

        return new(
            new BacnetIpRuntimePlan(dataSource.Key, dataSource.Name, sessionOptions, TimeSpan.FromMilliseconds(scanMs), points),
            issues);
    }

    private static TagDefinition BuildTagDefinition(TagEngineeringDto dto)
    {
        var metadata = dto.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(dto.Address)) metadata["address"] = dto.Address;
        var access = dto.AccessPolicy is null
            ? null
            : new TagAccessPolicy(
                dto.AccessPolicy.ReadRoles?.ToArray(),
                dto.AccessPolicy.WriteRoles?.ToArray(),
                dto.AccessPolicy.ConfigureRoles?.ToArray());
        return new TagDefinition(
            dto.Id ?? Guid.NewGuid(),
            dto.Name,
            dto.Path,
            dto.DataType,
            dto.Source,
            dto.EngineeringUnit,
            dto.Description,
            dto.ReadOnly,
            metadata,
            access,
            dto.AddressSelector);
    }

    private static uint ParseUInt(IReadOnlyDictionary<string, string> settings, string key, uint min, uint max, string dataSourceKey, List<EngineeringDriverIssue> issues)
    {
        var raw = Get(settings, key);
        if (!uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
        {
            issues.Add(Error("BACNET_SETTING_INVALID", $"BACnet setting '{key}' is required from {min} to {max}.", dataSourceKey));
            return min;
        }
        return value;
    }

    private static int ParseInt(IReadOnlyDictionary<string, string> settings, string key, int defaultValue, int min, int max, string dataSourceKey, List<EngineeringDriverIssue> issues)
    {
        var raw = Get(settings, key);
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
        {
            issues.Add(Error("BACNET_SETTING_INVALID", $"BACnet setting '{key}' must be from {min} to {max}.", dataSourceKey));
            return defaultValue;
        }
        return value;
    }

    private static int? ParseOptionalInt(IReadOnlyDictionary<string, string> settings, string key, int min, int max, string dataSourceKey, List<EngineeringDriverIssue> issues)
    {
        var raw = Get(settings, key);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
        {
            issues.Add(Error("BACNET_SETTING_INVALID", $"BACnet setting '{key}' must be from {min} to {max}.", dataSourceKey));
            return null;
        }
        return value;
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string> metadata, string key, bool defaultValue, string dataSourceKey, string tagPath, List<EngineeringDriverIssue> issues)
    {
        var raw = Get(metadata, key);
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        if (bool.TryParse(raw, out var value)) return value;
        issues.Add(Error("BACNET_TAG_METADATA_INVALID", $"BACnet metadata '{key}' for TAG '{tagPath}' must be true or false.", dataSourceKey, tagPath));
        return defaultValue;
    }

    private static byte? ParseOptionalByte(IReadOnlyDictionary<string, string> metadata, string key, byte min, byte max, string dataSourceKey, string tagPath, List<EngineeringDriverIssue> issues)
    {
        var raw = Get(metadata, key);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (byte.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max) return value;
        issues.Add(Error("BACNET_TAG_METADATA_INVALID", $"BACnet metadata '{key}' for TAG '{tagPath}' must be from {min} to {max}.", dataSourceKey, tagPath));
        return null;
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key)
        => values.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;

    private static EngineeringDriverIssue Error(string code, string message, string dataSourceKey, string? tagPath = null)
        => new(code, message, dataSourceKey, tagPath, IsError: true);
}
