using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Iec60870;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record Iec104CommunicationPoint(
    TagDefinition Tag,
    Iec104PortablePointAddress Address,
    Iec104TypeId MonitoredTypeId,
    Iec104TypeId? CommandTypeId = null,
    Iec104CommandMode? CommandMode = null,
    byte CommandQualifier = 0);

public sealed record Iec104CommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    string Host,
    int Port,
    Iec104SessionOptions SessionOptions,
    TimeZoneInfo StationTimeZone,
    IReadOnlyCollection<ushort> CommonAddresses,
    byte OriginatorAddress,
    IReadOnlyCollection<Iec104CommunicationPoint> Points) : ICommunicationDriverRuntimePlan
{
    public string DriverType => Iec104EngineeringConnectionTester.DriverType;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(point => point.Tag).ToArray();
}

/// <summary>
/// Coordinator-owned IEC-104 adapter. CommunicationBinding is canonical for
/// schema v15 packages; legacy Address/Metadata remains an explicit migration
/// path only. Common Address + IOA is the stable protocol identity.
/// </summary>
public sealed class Iec104CommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    public const string BindingSchemaId = "elite.iec60870.5.104.point";
    public const int BindingSchemaVersion = 1;

    private static readonly HashSet<string> AllowedDataSourceSettings = new(StringComparer.OrdinalIgnoreCase)
    {
        "host",
        "port",
        "commonAddresses",
        "stationTimeZone",
        "originatorAddress",
        "t0Seconds",
        "t1Seconds",
        "t2Seconds",
        "t3Seconds",
        "k",
        "w"
    };

    private static readonly HashSet<string> AllowedBindingSettings = new(StringComparer.OrdinalIgnoreCase)
    {
        "iec104.typeId",
        "iec104.commandTypeId",
        "iec104.commandMode",
        "iec104.qualifier"
    };

    public string DriverType => Iec104EngineeringConnectionTester.DriverType;

    public CommunicationDriverRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);

        var issues = new List<EngineeringDriverIssue>();
        if (!dataSource.Driver.Equals(DriverType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error(
                "IEC104_DRIVER_TYPE_MISMATCH",
                $"Data source '{dataSource.Key}' declares driver '{dataSource.Driver}', not '{DriverType}'.",
                dataSource.Key));
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        }

        if (dataSource.SecretReferences is { Count: > 0 })
        {
            issues.Add(Error(
                "IEC104_PROTECTED_MATERIAL_UNSUPPORTED",
                $"IEC-104 data source '{dataSource.Key}' contains protected-material references, but the current first-release runtime is plain TCP and has no TLS/IEC 62351 credential contract.",
                dataSource.Key));
        }

        var settings = CaseInsensitive(dataSource.Settings);
        foreach (var key in settings.Keys.Where(key => !AllowedDataSourceSettings.Contains(key)))
        {
            issues.Add(Error(
                "IEC104_DATASOURCE_SETTING_UNSUPPORTED",
                $"IEC-104 data source '{dataSource.Key}' contains unsupported setting '{key}'. The runtime fails closed rather than silently ignoring security or transport configuration.",
                dataSource.Key));
        }

        var host = Required(settings, "host", dataSource.Key, issues);
        var port = Integer(settings, "port", 2404, 1, 65535, dataSource.Key, issues);
        var commonAddresses = CommonAddresses(settings, dataSource.Key, issues);
        var stationTimeZone = StationTimeZone(settings, dataSource.Key, issues);
        var originatorAddress = checked((byte)Integer(settings, "originatorAddress", 0, 0, byte.MaxValue, dataSource.Key, issues));
        var sessionOptions = new Iec104SessionOptions
        {
            T0 = Seconds(settings, "t0Seconds", 30, dataSource.Key, issues),
            T1 = Seconds(settings, "t1Seconds", 15, dataSource.Key, issues),
            T2 = Seconds(settings, "t2Seconds", 10, dataSource.Key, issues),
            T3 = Seconds(settings, "t3Seconds", 20, dataSource.Key, issues),
            K = Integer(settings, "k", 12, 1, 32767, dataSource.Key, issues),
            W = Integer(settings, "w", 8, 1, 32767, dataSource.Key, issues)
        };
        try
        {
            sessionOptions.Validate();
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            issues.Add(Error("IEC104_APCI_CONFIGURATION_INVALID", ex.Message, dataSource.Key));
        }

        var points = package.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .Select(tag => BuildPoint(package.SchemaVersion, dataSource.Key, tag, commonAddresses, issues))
            .Where(point => point is not null)
            .Cast<Iec104CommunicationPoint>()
            .ToArray();

        if (points.Length == 0)
        {
            issues.Add(Error(
                "IEC104_DATASOURCE_NO_POINTS",
                $"IEC-104 data source '{dataSource.Key}' requires at least one configured TAG.",
                dataSource.Key));
        }

        foreach (var duplicate in points.GroupBy(point => point.Tag.Id).Where(group => group.Count() > 1))
        {
            issues.Add(Error(
                "IEC104_TAG_ID_DUPLICATE",
                $"IEC-104 data source '{dataSource.Key}' contains duplicate stable TAG id '{duplicate.Key}'.",
                dataSource.Key));
        }
        foreach (var duplicate in points.GroupBy(point => point.Address).Where(group => group.Count() > 1))
        {
            issues.Add(Error(
                "IEC104_POINT_ADDRESS_DUPLICATE",
                $"IEC-104 data source '{dataSource.Key}' maps more than one TAG to portable point address '{duplicate.Key}'.",
                dataSource.Key));
        }

        if (issues.Any(issue => issue.IsError) ||
            host is null ||
            stationTimeZone is null ||
            commonAddresses.Count == 0 ||
            points.Length == 0)
        {
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        }

        return new CommunicationDriverRuntimePlanningResult(
            new Iec104CommunicationRuntimePlan(
                dataSource.Key,
                dataSource.Name,
                host,
                port,
                sessionOptions,
                stationTimeZone,
                commonAddresses,
                originatorAddress,
                points),
            issues);
    }

    private static Iec104CommunicationPoint? BuildPoint(
        int packageSchemaVersion,
        string dataSourceKey,
        TagEngineeringDto tag,
        IReadOnlyCollection<ushort> commonAddresses,
        List<EngineeringDriverIssue> issues)
    {
        if (!tag.Id.HasValue || tag.Id.Value == Guid.Empty)
        {
            issues.Add(Error(
                "IEC104_TAG_STABLE_ID_REQUIRED",
                $"IEC-104 TAG '{tag.Path}' requires a non-empty stable Id before runtime activation.",
                dataSourceKey,
                tag.Path));
            return null;
        }

        var binding = tag.CommunicationBinding;
        IReadOnlyDictionary<string, string> effectiveSettings;
        string? portableAddress;
        if (binding is null)
        {
            portableAddress = tag.Address;
            effectiveSettings = CaseInsensitive(tag.Metadata);
            if (packageSchemaVersion >= 15)
            {
                issues.Add(new EngineeringDriverIssue(
                    "IEC104_TAG_LEGACY_BINDING",
                    $"IEC-104 TAG '{tag.Path}' uses legacy Address/Metadata without CommunicationBinding; it remains activatable only for backward-compatible migration.",
                    dataSourceKey,
                    tag.Path,
                    IsError: false));
            }
        }
        else
        {
            try
            {
                binding.Validate();
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                issues.Add(Error(
                    "IEC104_TAG_BINDING_INVALID",
                    $"IEC-104 TAG '{tag.Path}' has an invalid CommunicationBinding: {ex.Message}",
                    dataSourceKey,
                    tag.Path));
                return null;
            }

            if (!binding.SchemaId.Equals(BindingSchemaId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error(
                    "IEC104_TAG_BINDING_SCHEMA_MISMATCH",
                    $"IEC-104 TAG '{tag.Path}' binding schema must be '{BindingSchemaId}', received '{binding.SchemaId}'.",
                    dataSourceKey,
                    tag.Path));
            }
            if (binding.SchemaVersion != BindingSchemaVersion)
            {
                issues.Add(Error(
                    "IEC104_TAG_BINDING_SCHEMA_VERSION_UNSUPPORTED",
                    $"IEC-104 TAG '{tag.Path}' binding schema version must be {BindingSchemaVersion}, received {binding.SchemaVersion}.",
                    dataSourceKey,
                    tag.Path));
            }
            if (binding.ValueTransform is not null)
            {
                issues.Add(Error(
                    "IEC104_TAG_BINDING_TRANSFORM_UNSUPPORTED",
                    $"IEC-104 TAG '{tag.Path}' cannot use byte/word transforms; standard IEC-104 information-object encodings define their own wire representation.",
                    dataSourceKey,
                    tag.Path));
            }
            if (!string.IsNullOrWhiteSpace(tag.Address) &&
                !string.Equals(tag.Address, binding.PortableAddress, StringComparison.Ordinal))
            {
                issues.Add(Error(
                    "IEC104_TAG_BINDING_ADDRESS_MISMATCH",
                    $"IEC-104 TAG '{tag.Path}' Address must exactly match CommunicationBinding.PortableAddress.",
                    dataSourceKey,
                    tag.Path));
            }

            portableAddress = binding.PortableAddress;
            effectiveSettings = CaseInsensitive(binding.EffectiveSettings);
        }

        if (!Iec104PortablePointAddress.TryParse(portableAddress, out var address))
        {
            issues.Add(Error(
                "IEC104_TAG_ADDRESS_INVALID",
                $"IEC-104 TAG '{tag.Path}' must use portable address 'ca=<0..65535>;ioa=<0..16777215>'.",
                dataSourceKey,
                tag.Path));
            return null;
        }
        if (!string.Equals(portableAddress, address.ToString(), StringComparison.Ordinal))
        {
            issues.Add(Error(
                "IEC104_TAG_ADDRESS_NONCANONICAL",
                $"IEC-104 TAG '{tag.Path}' portable address must be canonical '{address}'.",
                dataSourceKey,
                tag.Path));
        }
        if (!commonAddresses.Contains(address.CommonAddress))
        {
            issues.Add(Error(
                "IEC104_TAG_COMMON_ADDRESS_NOT_CONFIGURED",
                $"IEC-104 TAG '{tag.Path}' uses Common Address {address.CommonAddress}, which is not listed in Data Source commonAddresses.",
                dataSourceKey,
                tag.Path));
        }
        if (tag.AddressSelector is not null)
        {
            issues.Add(Error(
                "IEC104_TAG_SELECTOR_UNSUPPORTED",
                $"IEC-104 TAG '{tag.Path}' cannot currently use AddressSelector; IEC-104 semantic Type ID decode is already typed and no private selector behavior is inferred.",
                dataSourceKey,
                tag.Path));
        }

        foreach (var key in effectiveSettings.Keys
                     .Where(key => key.StartsWith("iec104.", StringComparison.OrdinalIgnoreCase) && !AllowedBindingSettings.Contains(key)))
        {
            issues.Add(Error(
                "IEC104_TAG_BINDING_SETTING_UNSUPPORTED",
                $"IEC-104 TAG '{tag.Path}' contains unsupported binding setting '{key}'.",
                dataSourceKey,
                tag.Path));
        }

        if (!TryTypeId(effectiveSettings, "iec104.typeId", monitoredOnly: true, out var monitoredTypeId))
        {
            issues.Add(Error(
                "IEC104_TAG_TYPE_ID_REQUIRED",
                $"IEC-104 TAG '{tag.Path}' requires binding setting 'iec104.typeId' with one supported monitored Type ID.",
                dataSourceKey,
                tag.Path));
            return null;
        }

        var expectedDataType = DataTypeForMonitored(monitoredTypeId);
        if (tag.DataType != expectedDataType)
        {
            issues.Add(Error(
                "IEC104_TAG_TYPE_MISMATCH",
                $"IEC-104 TAG '{tag.Path}' Type ID {monitoredTypeId} requires canonical data type {expectedDataType}, received {tag.DataType}.",
                dataSourceKey,
                tag.Path));
        }

        Iec104TypeId? commandTypeId = null;
        Iec104CommandMode? commandMode = null;
        byte commandQualifier = 0;
        if (!tag.ReadOnly)
        {
            if (!TryTypeId(effectiveSettings, "iec104.commandTypeId", monitoredOnly: false, out var parsedCommand) || !IsCommandType(parsedCommand))
            {
                issues.Add(Error(
                    "IEC104_TAG_COMMAND_TYPE_REQUIRED",
                    $"Writable IEC-104 TAG '{tag.Path}' requires binding setting 'iec104.commandTypeId' with CScNa1, CDcNa1, CSeNa1, CSeNb1 or CSeNc1.",
                    dataSourceKey,
                    tag.Path));
            }
            else
            {
                commandTypeId = parsedCommand;
                var commandDataType = DataTypeForCommand(parsedCommand);
                if (tag.DataType != commandDataType)
                {
                    issues.Add(Error(
                        "IEC104_TAG_COMMAND_TYPE_MISMATCH",
                        $"Writable IEC-104 TAG '{tag.Path}' command Type ID {parsedCommand} requires canonical data type {commandDataType}, received {tag.DataType}.",
                        dataSourceKey,
                        tag.Path));
                }
            }

            if (!effectiveSettings.TryGetValue("iec104.commandMode", out var modeRaw) || !TryCommandMode(modeRaw, out var parsedMode))
            {
                issues.Add(Error(
                    "IEC104_TAG_COMMAND_MODE_REQUIRED",
                    $"Writable IEC-104 TAG '{tag.Path}' requires explicit 'iec104.commandMode' of 'direct' or 'sbo'.",
                    dataSourceKey,
                    tag.Path));
            }
            else
            {
                commandMode = parsedMode;
            }

            if (effectiveSettings.TryGetValue("iec104.qualifier", out var qualifierRaw) &&
                (!byte.TryParse(qualifierRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out commandQualifier) || commandQualifier > 31))
            {
                issues.Add(Error(
                    "IEC104_TAG_COMMAND_QUALIFIER_INVALID",
                    $"IEC-104 TAG '{tag.Path}' command qualifier must be an integer in the range 0..31.",
                    dataSourceKey,
                    tag.Path));
            }
        }
        else if (effectiveSettings.ContainsKey("iec104.commandTypeId") ||
                 effectiveSettings.ContainsKey("iec104.commandMode") ||
                 effectiveSettings.ContainsKey("iec104.qualifier"))
        {
            issues.Add(new EngineeringDriverIssue(
                "IEC104_TAG_READONLY_COMMAND_SETTINGS_IGNORED",
                $"Read-only IEC-104 TAG '{tag.Path}' contains command settings; they are not part of runtime behavior.",
                dataSourceKey,
                tag.Path,
                IsError: false));
        }

        var canonicalTag = BuildCanonicalTag(tag, address);
        return new Iec104CommunicationPoint(
            canonicalTag,
            address,
            monitoredTypeId,
            commandTypeId,
            commandMode,
            commandQualifier);
    }

    private static TagDefinition BuildCanonicalTag(TagEngineeringDto dto, Iec104PortablePointAddress address)
    {
        var metadata = CaseInsensitive(dto.Metadata);
        if (dto.CommunicationBinding is not null)
        {
            foreach (var key in metadata.Keys.Where(key => key.StartsWith("iec104.", StringComparison.OrdinalIgnoreCase)).ToArray())
                metadata.Remove(key);
        }
        metadata["address"] = address.ToString();

        var access = dto.AccessPolicy is null
            ? null
            : new TagAccessPolicy(
                dto.AccessPolicy.ReadRoles?.ToArray(),
                dto.AccessPolicy.WriteRoles?.ToArray(),
                dto.AccessPolicy.ConfigureRoles?.ToArray());

        return new TagDefinition(
            dto.Id!.Value,
            dto.Name,
            dto.Path,
            dto.DataType,
            dto.Source,
            dto.EngineeringUnit,
            dto.Description,
            dto.ReadOnly,
            metadata,
            access,
            dto.AddressSelector,
            dto.CommunicationBinding);
    }

    private static bool TryTypeId(
        IReadOnlyDictionary<string, string> settings,
        string key,
        bool monitoredOnly,
        out Iec104TypeId typeId)
    {
        typeId = default;
        if (!settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return false;

        if (byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
            typeId = (Iec104TypeId)numeric;
        else if (!Enum.TryParse(raw, ignoreCase: true, out typeId))
            return false;

        return monitoredOnly ? Iec104InformationObjectDecoder.IsSupported(typeId) : Enum.IsDefined(typeId);
    }

    private static bool IsCommandType(Iec104TypeId typeId) => typeId is
        Iec104TypeId.CScNa1 or
        Iec104TypeId.CDcNa1 or
        Iec104TypeId.CSeNa1 or
        Iec104TypeId.CSeNb1 or
        Iec104TypeId.CSeNc1;

    private static TagDataType DataTypeForMonitored(Iec104TypeId typeId) => typeId switch
    {
        Iec104TypeId.MSpNa1 or Iec104TypeId.MSpTb1 => TagDataType.Boolean,
        Iec104TypeId.MDpNa1 or Iec104TypeId.MDpTb1 => TagDataType.Enum,
        Iec104TypeId.MBoNa1 or Iec104TypeId.MBoTb1 => TagDataType.Int32,
        Iec104TypeId.MMeNa1 or Iec104TypeId.MMeTd1 => TagDataType.Float,
        Iec104TypeId.MMeNb1 or Iec104TypeId.MMeTe1 => TagDataType.Int16,
        Iec104TypeId.MMeNc1 or Iec104TypeId.MMeTf1 => TagDataType.Float,
        _ => throw new ArgumentOutOfRangeException(nameof(typeId), typeId, "Unsupported IEC-104 monitored Type ID.")
    };

    private static TagDataType DataTypeForCommand(Iec104TypeId typeId) => typeId switch
    {
        Iec104TypeId.CScNa1 => TagDataType.Boolean,
        Iec104TypeId.CDcNa1 => TagDataType.Enum,
        Iec104TypeId.CSeNa1 => TagDataType.Float,
        Iec104TypeId.CSeNb1 => TagDataType.Int16,
        Iec104TypeId.CSeNc1 => TagDataType.Float,
        _ => throw new ArgumentOutOfRangeException(nameof(typeId), typeId, "Unsupported IEC-104 command Type ID.")
    };

    private static bool TryCommandMode(string raw, out Iec104CommandMode mode)
    {
        if (raw.Equals("direct", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("directOperate", StringComparison.OrdinalIgnoreCase))
        {
            mode = Iec104CommandMode.DirectOperate;
            return true;
        }
        if (raw.Equals("sbo", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("selectBeforeOperate", StringComparison.OrdinalIgnoreCase))
        {
            mode = Iec104CommandMode.SelectBeforeOperate;
            return true;
        }

        mode = default;
        return false;
    }

    private static string? Required(
        IReadOnlyDictionary<string, string> settings,
        string key,
        string dataSourceKey,
        ICollection<EngineeringDriverIssue> issues)
    {
        if (settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value.Trim();
        issues.Add(Error("IEC104_SETTING_REQUIRED", $"IEC-104 setting '{key}' is required.", dataSourceKey));
        return null;
    }

    private static int Integer(
        IReadOnlyDictionary<string, string> settings,
        string key,
        int defaultValue,
        int minimum,
        int maximum,
        string dataSourceKey,
        ICollection<EngineeringDriverIssue> issues)
    {
        if (!settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return defaultValue;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= minimum && value <= maximum)
            return value;
        issues.Add(Error("IEC104_SETTING_INTEGER_INVALID", $"IEC-104 setting '{key}' must be an integer in the range {minimum}..{maximum}.", dataSourceKey));
        return defaultValue;
    }

    private static TimeSpan Seconds(
        IReadOnlyDictionary<string, string> settings,
        string key,
        double defaultValue,
        string dataSourceKey,
        ICollection<EngineeringDriverIssue> issues)
    {
        if (!settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return TimeSpan.FromSeconds(defaultValue);
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value is >= 0.1 and <= 86400)
            return TimeSpan.FromSeconds(value);
        issues.Add(Error("IEC104_SETTING_DURATION_INVALID", $"IEC-104 setting '{key}' must be seconds in the range 0.1..86400.", dataSourceKey));
        return TimeSpan.FromSeconds(defaultValue);
    }

    private static IReadOnlyCollection<ushort> CommonAddresses(
        IReadOnlyDictionary<string, string> settings,
        string dataSourceKey,
        ICollection<EngineeringDriverIssue> issues)
    {
        var raw = Required(settings, "commonAddresses", dataSourceKey, issues);
        if (raw is null) return Array.Empty<ushort>();

        var result = new SortedSet<ushort>();
        foreach (var token in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!ushort.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var address))
            {
                issues.Add(Error("IEC104_COMMON_ADDRESS_INVALID", $"IEC-104 Common Address '{token}' is not a 16-bit unsigned integer.", dataSourceKey));
                continue;
            }
            result.Add(address);
        }
        if (result.Count == 0)
            issues.Add(Error("IEC104_COMMON_ADDRESS_REQUIRED", "IEC-104 commonAddresses must contain at least one valid address.", dataSourceKey));
        return result.ToArray();
    }

    private static TimeZoneInfo? StationTimeZone(
        IReadOnlyDictionary<string, string> settings,
        string dataSourceKey,
        ICollection<EngineeringDriverIssue> issues)
    {
        var id = Required(settings, "stationTimeZone", dataSourceKey, issues);
        if (id is null) return null;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            issues.Add(Error("IEC104_TIME_ZONE_INVALID", $"IEC-104 stationTimeZone '{id}' was not found on this runtime.", dataSourceKey));
        }
        catch (InvalidTimeZoneException)
        {
            issues.Add(Error("IEC104_TIME_ZONE_INVALID", $"IEC-104 stationTimeZone '{id}' is invalid on this runtime.", dataSourceKey));
        }
        return null;
    }

    private static Dictionary<string, string> CaseInsensitive(IReadOnlyDictionary<string, string>? source) =>
        source is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);

    private static EngineeringDriverIssue Error(
        string code,
        string message,
        string dataSourceKey,
        string? tagPath = null) =>
        new(code, message, dataSourceKey, tagPath, IsError: true);
}

public sealed class Iec104CommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly Func<IIec104ClientAdapter> _adapterFactory;

    public Iec104CommunicationRuntimeFactory(Func<IIec104ClientAdapter>? adapterFactory = null)
    {
        _adapterFactory = adapterFactory ?? (static () => new Iec104TcpClientAdapter());
    }

    public string DriverType => Iec104EngineeringConnectionTester.DriverType;

    public ICommunicationDriver Create(
        ICommunicationDriverRuntimePlan plan,
        CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();

        if (plan is not Iec104CommunicationRuntimePlan iecPlan)
            throw new ArgumentException($"IEC-104 runtime factory requires {nameof(Iec104CommunicationRuntimePlan)}.", nameof(plan));
        if (!iecPlan.DriverType.Equals(DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"IEC-104 runtime plan declares unexpected DriverType '{iecPlan.DriverType}'.", nameof(plan));
        if (iecPlan.Points.Count == 0)
            throw new ArgumentException("IEC-104 runtime plan requires at least one point.", nameof(plan));

        return new Iec104HostCommunicationDriver(
            iecPlan,
            services.Cache,
            services.Registry,
            _adapterFactory);
    }
}

internal sealed class Iec104HostCommunicationDriver : ICommunicationDriver, ICommunicationDriverReadinessSource
{
    private readonly Iec104CommunicationRuntimePlan _plan;
    private readonly ICurrentTagCache _cache;
    private readonly ITagRegistry _registry;
    private readonly Iec104ManagedClient _client;
    private readonly IReadOnlyDictionary<Iec104PortablePointAddress, Iec104CommunicationPoint> _pointsByAddress;
    private readonly IReadOnlyDictionary<Guid, Iec104CommunicationPoint> _pointsByTagId;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private long _updatesPublished;
    private bool _disposed;

    public Iec104HostCommunicationDriver(
        Iec104CommunicationRuntimePlan plan,
        ICurrentTagCache cache,
        ITagRegistry registry,
        Func<IIec104ClientAdapter> adapterFactory)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        ArgumentNullException.ThrowIfNull(adapterFactory);

        _pointsByAddress = plan.Points.ToDictionary(point => point.Address);
        _pointsByTagId = plan.Points.ToDictionary(point => point.Tag.Id);
        _client = new Iec104ManagedClient(
            adapterFactory,
            plan.Host,
            plan.Port,
            plan.SessionOptions,
            plan.StationTimeZone,
            plan.CommonAddresses,
            originatorAddress: plan.OriginatorAddress);

        Status = new DriverStatus(plan.DataSourceKey, plan.Name, DriverState.Stopped, DateTimeOffset.UtcNow);
    }

    public string DriverId => _plan.DataSourceKey;
    public string Name => _plan.Name;
    public DriverStatus Status { get; private set; }
    public IReadOnlyCollection<TagDefinition> Tags => _plan.Tags;

    public DriverCapabilities Capabilities
    {
        get
        {
            var capabilities = DriverCapabilities.Read |
                               DriverCapabilities.Subscribe |
                               DriverCapabilities.Diagnostics |
                               DriverCapabilities.SourceTimestamp;
            if (_plan.Points.Any(point => !point.Tag.ReadOnly))
                capabilities |= DriverCapabilities.Write;
            return capabilities;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_runTask is { IsCompleted: false }) return;

            foreach (var point in _plan.Points)
                _registry.Upsert(point.Tag);

            var cts = new CancellationTokenSource();
            _cts = cts;
            Status = new DriverStatus(DriverId, Name, DriverState.Starting, DateTimeOffset.UtcNow);
            _runTask = RunAsync(cts.Token);
            Status = new DriverStatus(DriverId, Name, DriverState.Running, DateTimeOffset.UtcNow);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (!_pointsByTagId.ContainsKey(tagId))
            throw new KeyNotFoundException($"IEC-104 TAG '{tagId}' is not owned by data source '{DriverId}'.");
        _cache.TryGet(tagId, out var value);
        return ValueTask.FromResult(value);
    }

    public async ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!_pointsByTagId.TryGetValue(tagId, out var point))
            throw new KeyNotFoundException($"IEC-104 TAG '{tagId}' is not owned by data source '{DriverId}'.");
        if (point.Tag.ReadOnly || !point.CommandTypeId.HasValue || !point.CommandMode.HasValue)
            throw new InvalidOperationException($"IEC-104 TAG '{point.Tag.Path}' has no ordinary-write command profile.");

        var transaction = CreateTransaction(point, value);
        var result = await _client.ExecuteCommandAsync(transaction, cancellationToken).ConfigureAwait(false);
        switch (result.Outcome)
        {
            case Iec104CommandOutcome.Completed:
                return;
            case Iec104CommandOutcome.Cancelled when cancellationToken.IsCancellationRequested:
                throw new OperationCanceledException(cancellationToken);
            case Iec104CommandOutcome.TimedOut:
                throw new TimeoutException(result.Detail ?? $"IEC-104 command for TAG '{point.Tag.Path}' timed out.");
            case Iec104CommandOutcome.Ambiguous:
                throw new IOException(result.Detail ?? $"IEC-104 command for TAG '{point.Tag.Path}' has an ambiguous outcome and will not be replayed.");
            default:
                throw new InvalidOperationException(result.Detail ?? $"IEC-104 command for TAG '{point.Tag.Path}' was not completed ({result.Outcome}).");
        }
    }

    public CommunicationDriverReadinessSnapshot GetCommunicationReadiness()
    {
        var readiness = _client.GetReadiness();
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sessionState"] = readiness.SessionState.ToString(),
            ["transportConnected"] = readiness.IsTransportConnected ? "true" : "false",
            ["dataTransferStarted"] = readiness.IsDataTransferStarted ? "true" : "false",
            ["startupGeneralInterrogationCompleted"] = readiness.StartupGeneralInterrogationCompleted ? "true" : "false",
            ["startupGeneralInterrogationRejected"] = readiness.StartupGeneralInterrogationRejected ? "true" : "false",
            ["reconnectAttempt"] = readiness.ReconnectAttempt.ToString(CultureInfo.InvariantCulture)
        };

        return new CommunicationDriverReadinessSnapshot(
            DriverId,
            Iec104EngineeringConnectionTester.DriverType,
            readiness.State switch
            {
                Iec104ReadinessState.NotStarted => CommunicationDriverReadinessState.NotStarted,
                Iec104ReadinessState.Starting => CommunicationDriverReadinessState.Starting,
                Iec104ReadinessState.Ready => CommunicationDriverReadinessState.Ready,
                Iec104ReadinessState.Faulted => CommunicationDriverReadinessState.Faulted,
                Iec104ReadinessState.Stopped => CommunicationDriverReadinessState.Stopped,
                _ => throw new ArgumentOutOfRangeException(nameof(readiness.State), readiness.State, "Unsupported IEC-104 readiness state.")
            },
            readiness.CapturedAt,
            readiness.LastFailure,
            details);
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            await StopCoreAsync().ConfigureAwait(false);
            _disposed = true;
            _lifecycleGate.Dispose();
        }
        finally
        {
            if (!_disposed)
                _lifecycleGate.Release();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.RunAsync(PublishObservedPointAsync, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Status = new DriverStatus(
                DriverId,
                Name,
                DriverState.Faulted,
                DateTimeOffset.UtcNow,
                ex.GetType().Name,
                Interlocked.Read(ref _updatesPublished));
        }
    }

    private async ValueTask PublishObservedPointAsync(
        Iec104DecodedPoint observed,
        CancellationToken cancellationToken)
    {
        var address = new Iec104PortablePointAddress(
            observed.CommonAddress,
            observed.InformationObjectAddress.Value);
        if (!_pointsByAddress.TryGetValue(address, out var point))
            return;

        var timestamp = DateTimeOffset.UtcNow;
        object? value;
        TagQuality quality;
        if (observed.TypeId != point.MonitoredTypeId)
        {
            value = null;
            quality = TagQuality.BadConfiguration;
        }
        else
        {
            value = CanonicalValue(observed);
            quality = observed.Quality;
        }

        var sample = new TagValue(
            point.Tag.Id,
            value,
            timestamp,
            quality,
            DriverId)
        {
            SourceTimestamp = observed.SourceTimestamp,
            ServerTimestamp = null
        };

        await _cache.UpdateAsync(point.Tag, sample, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _updatesPublished);
        Status = Status with { UpdatesPublished = Interlocked.Read(ref _updatesPublished), Timestamp = timestamp };
    }

    private static object CanonicalValue(Iec104DecodedPoint observed) => observed.TypeId switch
    {
        Iec104TypeId.MDpNa1 or Iec104TypeId.MDpTb1 => (int)(Iec104DoublePointState)observed.Value,
        _ => observed.Value
    };

    private Iec104CommandTransaction CreateTransaction(Iec104CommunicationPoint point, object? value)
    {
        var mode = point.CommandMode!.Value;
        var address = point.Address;
        return point.CommandTypeId!.Value switch
        {
            Iec104TypeId.CScNa1 when value is bool boolean =>
                Iec104CommandTransaction.Single(address.CommonAddress, address.InformationObjectAddress, boolean, mode, point.CommandQualifier, _plan.OriginatorAddress),
            Iec104TypeId.CDcNa1 when value is int integer && integer is 1 or 2 =>
                Iec104CommandTransaction.Double(address.CommonAddress, address.InformationObjectAddress, (Iec104DoublePointState)integer, mode, point.CommandQualifier, _plan.OriginatorAddress),
            Iec104TypeId.CSeNa1 when value is float normalized =>
                Iec104CommandTransaction.NormalizedSetpoint(address.CommonAddress, address.InformationObjectAddress, normalized, mode, point.CommandQualifier, _plan.OriginatorAddress),
            Iec104TypeId.CSeNb1 when value is short scaled =>
                Iec104CommandTransaction.ScaledSetpoint(address.CommonAddress, address.InformationObjectAddress, scaled, mode, point.CommandQualifier, _plan.OriginatorAddress),
            Iec104TypeId.CSeNc1 when value is float shortFloat =>
                Iec104CommandTransaction.ShortFloatSetpoint(address.CommonAddress, address.InformationObjectAddress, shortFloat, mode, point.CommandQualifier, _plan.OriginatorAddress),
            _ => throw new ArgumentException(
                $"Value type is incompatible with IEC-104 command profile {point.CommandTypeId} for TAG '{point.Tag.Path}'.",
                nameof(value))
        };
    }

    private async Task StopCoreAsync()
    {
        var cts = _cts;
        if (cts is null)
        {
            Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
            return;
        }

        Status = new DriverStatus(DriverId, Name, DriverState.Stopping, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
        await cts.CancelAsync().ConfigureAwait(false);
        if (_runTask is not null)
            await _runTask.ConfigureAwait(false);
        cts.Dispose();
        _cts = null;
        _runTask = null;
        Status = new DriverStatus(DriverId, Name, DriverState.Stopped, DateTimeOffset.UtcNow, UpdatesPublished: Interlocked.Read(ref _updatesPublished));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Iec104HostCommunicationDriver));
    }
}
