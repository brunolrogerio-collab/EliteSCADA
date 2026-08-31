using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Dnp3.StepFunction;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record Dnp3CommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    Dnp3TcpConnectionOptions Connection,
    Dnp3AssociationOptions Association,
    IReadOnlyCollection<Dnp3Point> Points) : ICommunicationDriverRuntimePlan
{
    public string DriverType => Dnp3DriverDescriptorProvider.DriverType;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(static point => point.Tag).ToArray();
}

/// <summary>
/// Coordinator-owned DNP3 convergence adapter. The protocol worker owns DNP3
/// semantics; schema-v15 CommunicationBinding remains the canonical host envelope.
/// </summary>
public sealed class Dnp3CommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    private const int BindingSchemaVersion = 1;

    private static readonly HashSet<string> BindingKeys = Dnp3DriverDescriptorProvider.SharedDescriptor
        .ConfigurationSchema.TagBindingFields
        .Select(static field => field.Key)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public string DriverType => Dnp3DriverDescriptorProvider.DriverType;

    public CommunicationDriverRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);

        var issues = new List<EngineeringDriverIssue>();
        if (string.IsNullOrWhiteSpace(dataSource.Key))
        {
            issues.Add(Error("DNP3_DATASOURCE_KEY_REQUIRED", "DNP3 data source key is required.", dataSource.Key ?? string.Empty));
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        }
        if (string.IsNullOrWhiteSpace(dataSource.Name))
            issues.Add(Error("DNP3_DATASOURCE_NAME_REQUIRED", $"DNP3 data source '{dataSource.Key}' requires a name.", dataSource.Key));
        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error(
                "DNP3_DRIVER_TYPE_INVALID",
                $"Data source '{dataSource.Key}' uses driver '{dataSource.Driver}' instead of '{DriverType}'.",
                dataSource.Key));
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        }

        if (dataSource.SecretReferences is { Count: > 0 })
        {
            issues.Add(Error(
                "DNP3_PROTECTED_MATERIAL_UNSUPPORTED",
                $"DNP3 data source '{dataSource.Key}' cannot declare protected material in the current TCP profile. Secure Authentication/TLS are not implemented.",
                dataSource.Key));
        }

        var parsed = Dnp3DataSourceSettingsParser.Parse(dataSource.Settings);
        foreach (var issue in parsed.Issues)
        {
            issues.Add(new EngineeringDriverIssue(
                issue.Code,
                issue.Message,
                dataSource.Key,
                IsError: issue.Severity == DriverEngineeringIssueSeverity.Error));
        }

        var points = package.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .Select(tag => BuildPoint(package.SchemaVersion, dataSource.Key, tag, issues))
            .Where(static point => point is not null)
            .Cast<Dnp3Point>()
            .ToArray();

        if (points.Length == 0)
        {
            issues.Add(Error(
                "DNP3_DATASOURCE_NO_TAGS",
                $"DNP3 data source '{dataSource.Key}' requires at least one configured TAG.",
                dataSource.Key));
        }

        foreach (var duplicate in points
                     .GroupBy(static point => (point.Binding.PointKind, point.Binding.Index))
                     .Where(static group => group.Count() > 1))
        {
            issues.Add(Error(
                "DNP3_PHYSICAL_IDENTITY_DUPLICATE",
                $"DNP3 data source '{dataSource.Key}' contains duplicate physical identity '{new Dnp3PortableAddress(duplicate.Key.PointKind, duplicate.Key.Index)}'.",
                dataSource.Key));
        }

        foreach (var duplicate in points.GroupBy(static point => point.Tag.Id).Where(static group => group.Count() > 1))
        {
            issues.Add(Error(
                "DNP3_TAG_ID_DUPLICATE",
                $"DNP3 data source '{dataSource.Key}' contains duplicate stable TAG id '{duplicate.Key}'.",
                dataSource.Key));
        }

        if (issues.Any(static issue => issue.IsError) || parsed.Value is null || points.Length == 0)
            return new CommunicationDriverRuntimePlanningResult(null, issues);

        return new CommunicationDriverRuntimePlanningResult(
            new Dnp3CommunicationRuntimePlan(
                dataSource.Key,
                dataSource.Name,
                parsed.Value.Connection,
                parsed.Value.Association,
                points),
            issues);
    }

    private static Dnp3Point? BuildPoint(
        int packageSchemaVersion,
        string dataSourceKey,
        TagEngineeringDto dto,
        ICollection<EngineeringDriverIssue> issues)
    {
        if (!dto.Id.HasValue || dto.Id.Value == Guid.Empty)
        {
            issues.Add(Error(
                "DNP3_TAG_STABLE_ID_REQUIRED",
                $"DNP3 TAG '{dto.Path}' requires a non-empty stable Id before runtime activation.",
                dataSourceKey,
                dto.Path));
            return null;
        }

        Dnp3PortableAddress address;
        IReadOnlyDictionary<string, string> settings;
        var communicationBinding = dto.CommunicationBinding;
        if (communicationBinding is null)
        {
            if (packageSchemaVersion >= 15)
            {
                issues.Add(new EngineeringDriverIssue(
                    "DNP3_TAG_LEGACY_BINDING",
                    $"DNP3 TAG '{dto.Path}' uses legacy Address/Metadata without CommunicationBinding; it remains activatable only for backward-compatible migration.",
                    dataSourceKey,
                    dto.Path,
                    IsError: false));
            }

            if (!Dnp3PortableAddress.TryParse(dto.Address, out address))
            {
                issues.Add(Error(
                    "DNP3_TAG_ADDRESS_INVALID",
                    $"DNP3 TAG '{dto.Path}' requires a canonical portable Address such as 'dnp3:analogInput:0'.",
                    dataSourceKey,
                    dto.Path));
                return null;
            }
            settings = LegacyBindingSettings(dto.Metadata);
        }
        else
        {
            var valid = true;
            try
            {
                communicationBinding.Validate();
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or NotSupportedException)
            {
                issues.Add(Error(
                    "DNP3_TAG_BINDING_INVALID",
                    $"DNP3 TAG '{dto.Path}' has an invalid CommunicationBinding: {ex.Message}",
                    dataSourceKey,
                    dto.Path));
                return null;
            }

            if (!communicationBinding.SchemaId.Equals(Dnp3DriverDescriptorProvider.ConfigurationSchemaId, StringComparison.OrdinalIgnoreCase))
            {
                valid = false;
                issues.Add(Error(
                    "DNP3_TAG_BINDING_SCHEMA_MISMATCH",
                    $"DNP3 TAG '{dto.Path}' binding schema must be '{Dnp3DriverDescriptorProvider.ConfigurationSchemaId}', received '{communicationBinding.SchemaId}'.",
                    dataSourceKey,
                    dto.Path));
            }
            if (communicationBinding.SchemaVersion != BindingSchemaVersion)
            {
                valid = false;
                issues.Add(Error(
                    "DNP3_TAG_BINDING_SCHEMA_VERSION_UNSUPPORTED",
                    $"DNP3 TAG '{dto.Path}' binding schema version must be {BindingSchemaVersion}, received {communicationBinding.SchemaVersion}.",
                    dataSourceKey,
                    dto.Path));
            }
            if (communicationBinding.ValueTransform is { IsIdentity: false })
            {
                valid = false;
                issues.Add(Error(
                    "DNP3_TAG_BINDING_TRANSFORM_UNSUPPORTED",
                    $"DNP3 TAG '{dto.Path}' cannot use byte/word physical transforms because DNP3 values are already protocol-typed.",
                    dataSourceKey,
                    dto.Path));
            }
            foreach (var key in communicationBinding.EffectiveSettings.Keys.Where(key => !BindingKeys.Contains(key)))
            {
                valid = false;
                issues.Add(Error(
                    "DNP3_TAG_BINDING_SETTING_UNSUPPORTED",
                    $"DNP3 TAG '{dto.Path}' contains unsupported binding setting '{key}'.",
                    dataSourceKey,
                    dto.Path));
            }

            if (!Dnp3PortableAddress.TryParse(communicationBinding.PortableAddress, out address))
            {
                issues.Add(Error(
                    "DNP3_TAG_ADDRESS_INVALID",
                    $"DNP3 TAG '{dto.Path}' portable address '{communicationBinding.PortableAddress}' is not canonical.",
                    dataSourceKey,
                    dto.Path));
                return null;
            }
            if (!string.IsNullOrWhiteSpace(dto.Address) &&
                !string.Equals(dto.Address, communicationBinding.PortableAddress, StringComparison.Ordinal))
            {
                valid = false;
                issues.Add(Error(
                    "DNP3_TAG_BINDING_ADDRESS_MISMATCH",
                    $"DNP3 TAG '{dto.Path}' Address must exactly match CommunicationBinding.PortableAddress.",
                    dataSourceKey,
                    dto.Path));
            }

            settings = communicationBinding.EffectiveSettings;
            if (settings.TryGetValue("pointKind", out var configuredKind) &&
                !string.Equals(configuredKind, Dnp3VariationRules.GetPointKindToken(address.PointKind), StringComparison.Ordinal))
            {
                valid = false;
                issues.Add(Error(
                    "DNP3_TAG_BINDING_IDENTITY_MISMATCH",
                    $"DNP3 TAG '{dto.Path}' pointKind setting does not match its portable address.",
                    dataSourceKey,
                    dto.Path));
            }
            if (settings.TryGetValue("index", out var configuredIndex) &&
                (!ushort.TryParse(configuredIndex, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedIndex) || parsedIndex != address.Index))
            {
                valid = false;
                issues.Add(Error(
                    "DNP3_TAG_BINDING_IDENTITY_MISMATCH",
                    $"DNP3 TAG '{dto.Path}' index setting does not match its portable address.",
                    dataSourceKey,
                    dto.Path));
            }

            if (!valid) return null;
        }

        try
        {
            var staticVariation = ParseVariation(settings, "staticVariation");
            var eventVariation = ParseVariation(settings, "eventVariation");
            var expectedEventClass = ParseEventClass(settings);
            var writable = ParseBoolean(settings, "writable", !dto.ReadOnly);

            if (writable == dto.ReadOnly)
                throw new ArgumentException("DNP3 binding writable setting must be the inverse of the canonical TAG ReadOnly value.");

            var pointBinding = new Dnp3PointBinding(
                address.PointKind,
                address.Index,
                dto.DataType,
                staticVariation,
                eventVariation,
                expectedEventClass,
                writable);

            Dnp3BinaryCommandProfile? binaryCommand = null;
            Dnp3AnalogCommandProfile? analogCommand = null;
            if (writable && address.PointKind == Dnp3PointKind.BinaryOutputStatus)
            {
                binaryCommand = new Dnp3BinaryCommandProfile
                {
                    Mode = ParseCommandMode(settings),
                    TrueOperation = ParseBinaryOperation(settings, "binaryTrueOperation", Dnp3BinaryOperation.LatchOn),
                    FalseOperation = ParseBinaryOperation(settings, "binaryFalseOperation", Dnp3BinaryOperation.LatchOff),
                    TripCloseCode = ParseTripCloseCode(settings),
                    Count = ParseByte(settings, "commandCount", 1),
                    OnTime = ParseNonNegativeDuration(settings, "commandOnTime", TimeSpan.Zero),
                    OffTime = ParseNonNegativeDuration(settings, "commandOffTime", TimeSpan.Zero)
                };
            }
            else if (writable && address.PointKind == Dnp3PointKind.AnalogOutputStatus)
            {
                analogCommand = new Dnp3AnalogCommandProfile(
                    ParseCommandMode(settings),
                    ParseAnalogVariation(settings, dto.DataType));
            }

            var point = new Dnp3Point(
                BuildCanonicalTag(dto),
                pointBinding,
                binaryCommand,
                analogCommand);
            point.Validate();
            return point;
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or FormatException or InvalidOperationException)
        {
            issues.Add(Error(
                "DNP3_TAG_CONFIGURATION_INVALID",
                $"DNP3 TAG '{dto.Path}' configuration is invalid: {ex.Message}",
                dataSourceKey,
                dto.Path));
            return null;
        }
    }

    private static Dnp3ObjectVariation? ParseVariation(IReadOnlyDictionary<string, string> settings, string key)
    {
        if (!settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return null;
        var value = raw.Trim();
        var marker = value.IndexOf('V');
        if (value.Length < 4 || value[0] != 'G' || marker <= 1 || marker == value.Length - 1 ||
            !byte.TryParse(value.AsSpan(1, marker - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var group) ||
            !byte.TryParse(value.AsSpan(marker + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var variation))
            throw new FormatException($"Binding setting '{key}' must use canonical GxVy syntax.");
        return new Dnp3ObjectVariation(group, variation);
    }

    private static Dnp3EventClass? ParseEventClass(IReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue("expectedEventClass", out var raw) || string.IsNullOrWhiteSpace(raw)) return null;
        return raw switch
        {
            "class1" => Dnp3EventClass.Class1,
            "class2" => Dnp3EventClass.Class2,
            "class3" => Dnp3EventClass.Class3,
            _ => throw new FormatException("expectedEventClass must be class1, class2 or class3.")
        };
    }

    private static bool ParseBoolean(IReadOnlyDictionary<string, string> settings, string key, bool fallback)
    {
        if (!settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return fallback;
        if (bool.TryParse(raw, out var parsed)) return parsed;
        throw new FormatException($"Binding setting '{key}' must be true or false.");
    }

    private static Dnp3CommandMode ParseCommandMode(IReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue("commandMode", out var raw) || string.IsNullOrWhiteSpace(raw))
            return Dnp3CommandMode.SelectBeforeOperate;
        return raw switch
        {
            "selectBeforeOperate" => Dnp3CommandMode.SelectBeforeOperate,
            "directOperate" => Dnp3CommandMode.DirectOperate,
            _ => throw new FormatException("commandMode must be selectBeforeOperate or directOperate.")
        };
    }

    private static Dnp3BinaryOperation ParseBinaryOperation(
        IReadOnlyDictionary<string, string> settings,
        string key,
        Dnp3BinaryOperation fallback)
    {
        if (!settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return fallback;
        return raw switch
        {
            "latchOn" => Dnp3BinaryOperation.LatchOn,
            "latchOff" => Dnp3BinaryOperation.LatchOff,
            "pulseOn" => Dnp3BinaryOperation.PulseOn,
            "pulseOff" => Dnp3BinaryOperation.PulseOff,
            _ => throw new FormatException($"Binding setting '{key}' has an unsupported binary operation.")
        };
    }

    private static Dnp3TripCloseCode ParseTripCloseCode(IReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue("tripCloseCode", out var raw) || string.IsNullOrWhiteSpace(raw)) return Dnp3TripCloseCode.None;
        return raw switch
        {
            "none" => Dnp3TripCloseCode.None,
            "trip" => Dnp3TripCloseCode.Trip,
            "close" => Dnp3TripCloseCode.Close,
            _ => throw new FormatException("tripCloseCode must be none, trip or close.")
        };
    }

    private static byte ParseByte(IReadOnlyDictionary<string, string> settings, string key, byte fallback)
    {
        if (!settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return fallback;
        if (byte.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0) return parsed;
        throw new FormatException($"Binding setting '{key}' must be an integer from 1 to {byte.MaxValue}.");
    }

    private static TimeSpan ParseNonNegativeDuration(
        IReadOnlyDictionary<string, string> settings,
        string key,
        TimeSpan fallback)
    {
        if (!settings.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return fallback;
        if (TimeSpan.TryParseExact(raw, "c", CultureInfo.InvariantCulture, out var parsed) && parsed >= TimeSpan.Zero) return parsed;
        throw new FormatException($"Binding setting '{key}' must be a non-negative invariant TimeSpan.");
    }

    private static Dnp3AnalogOutputVariation ParseAnalogVariation(
        IReadOnlyDictionary<string, string> settings,
        TagDataType dataType)
    {
        if (settings.TryGetValue("analogCommandVariation", out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            return raw switch
            {
                "int32" => Dnp3AnalogOutputVariation.Int32,
                "int16" => Dnp3AnalogOutputVariation.Int16,
                "float32" => Dnp3AnalogOutputVariation.Float32,
                "float64" => Dnp3AnalogOutputVariation.Float64,
                _ => throw new FormatException("analogCommandVariation must be int32, int16, float32 or float64.")
            };
        }

        return dataType switch
        {
            TagDataType.Int32 => Dnp3AnalogOutputVariation.Int32,
            TagDataType.Int16 => Dnp3AnalogOutputVariation.Int16,
            TagDataType.Float => Dnp3AnalogOutputVariation.Float32,
            TagDataType.Double => Dnp3AnalogOutputVariation.Float64,
            _ => throw new ArgumentException($"Canonical TAG type {dataType} cannot be used for a DNP3 analog output command.")
        };
    }

    private static IReadOnlyDictionary<string, string> LegacyBindingSettings(IReadOnlyDictionary<string, string>? metadata)
    {
        var source = metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in BindingKeys)
        {
            if (source.TryGetValue(key, out var value) || source.TryGetValue($"dnp3.{key}", out value))
                settings[key] = value;
        }
        return settings;
    }

    private static TagDefinition BuildCanonicalTag(TagEngineeringDto dto)
    {
        var metadata = dto.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);
        if (dto.CommunicationBinding is not null)
        {
            foreach (var key in BindingKeys)
            {
                metadata.Remove(key);
                metadata.Remove($"dnp3.{key}");
            }
        }
        if (!string.IsNullOrWhiteSpace(dto.Address)) metadata["address"] = dto.Address;
        if (dto.ScaleMinimum.HasValue) metadata["scale.minimum"] = dto.ScaleMinimum.Value.ToString(CultureInfo.InvariantCulture);
        if (dto.ScaleMaximum.HasValue) metadata["scale.maximum"] = dto.ScaleMaximum.Value.ToString(CultureInfo.InvariantCulture);
        if (dto.Historian is not null)
        {
            metadata["historian.enabled"] = dto.Historian.Enabled.ToString(CultureInfo.InvariantCulture);
            metadata["historian.strategy"] = dto.Historian.Strategy;
            Set(metadata, "historian.deadband", dto.Historian.Deadband);
            Set(metadata, "historian.periodMs", dto.Historian.PeriodMilliseconds);
            Set(metadata, "historian.maxPeriodMs", dto.Historian.MaximumPeriodMilliseconds);
        }

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

    private static void Set(Dictionary<string, string> metadata, string key, double? value)
    {
        if (value.HasValue) metadata[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static void Set(Dictionary<string, string> metadata, string key, int? value)
    {
        if (value.HasValue) metadata[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static EngineeringDriverIssue Error(
        string code,
        string message,
        string dataSourceKey,
        string? tagPath = null) =>
        new(code, message, dataSourceKey, tagPath, IsError: true);
}

public sealed class Dnp3CommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly IDnp3MasterSessionFactory _sessionFactory;

    public Dnp3CommunicationRuntimeFactory(IDnp3MasterSessionFactory? sessionFactory = null)
    {
        _sessionFactory = sessionFactory ?? new StepFunctionDnp3MasterSessionFactory();
    }

    public string DriverType => Dnp3DriverDescriptorProvider.DriverType;

    public ICommunicationDriver Create(
        ICommunicationDriverRuntimePlan plan,
        CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();

        if (plan is not Dnp3CommunicationRuntimePlan dnp3Plan)
            throw new ArgumentException($"DNP3 runtime factory requires {nameof(Dnp3CommunicationRuntimePlan)}.", nameof(plan));
        if (!dnp3Plan.DriverType.Equals(DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"DNP3 runtime plan declares unexpected DriverType '{dnp3Plan.DriverType}'.", nameof(plan));
        if (dnp3Plan.Points.Count == 0)
            throw new ArgumentException("DNP3 runtime plan must contain at least one point.", nameof(plan));

        dnp3Plan.Connection.Validate();
        dnp3Plan.Association.Validate();
        foreach (var point in dnp3Plan.Points) point.Validate();

        var session = _sessionFactory.Create(dnp3Plan.Connection)
            ?? throw new InvalidOperationException("DNP3 session factory returned null.");
        var driver = new Dnp3Driver(
            dnp3Plan.DataSourceKey,
            dnp3Plan.Name,
            services.Cache,
            services.Registry,
            dnp3Plan.Points,
            session,
            dnp3Plan.Association);
        return new Dnp3CoordinatorRuntimeDriver(driver, session, dnp3Plan.DataSourceKey);
    }

    private sealed class Dnp3CoordinatorRuntimeDriver :
        ICommunicationDriver,
        ICommunicationDiagnosticsSource,
        ICommunicationDriverReadinessSource
    {
        private readonly Dnp3Driver _inner;
        private readonly IDnp3MasterSession _session;
        private readonly string _dataSourceKey;

        public Dnp3CoordinatorRuntimeDriver(
            Dnp3Driver inner,
            IDnp3MasterSession session,
            string dataSourceKey)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _dataSourceKey = dataSourceKey;
        }

        public string DriverId => _inner.DriverId;
        public string Name => _inner.Name;
        public DriverCapabilities Capabilities => _inner.Capabilities;
        public DriverStatus Status => _inner.Status;
        public IReadOnlyCollection<TagDefinition> Tags => _inner.Tags;

        public Task StartAsync(CancellationToken cancellationToken = default) => _inner.StartAsync(cancellationToken);
        public Task StopAsync(CancellationToken cancellationToken = default) => _inner.StopAsync(cancellationToken);
        public ValueTask<TagValue?> ReadAsync(Guid tagId, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(tagId, cancellationToken);
        public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) =>
            _inner.WriteAsync(tagId, value, cancellationToken);
        public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics() => _inner.GetCommunicationDiagnostics();

        public CommunicationDriverReadinessSnapshot GetCommunicationReadiness()
        {
            var observedAt = DateTimeOffset.UtcNow;
            var session = _session.GetDiagnostics();
            var evidence = Dnp3ReadinessPolicy.Evaluate(session);
            var state = evidence.IsReady
                ? CommunicationDriverReadinessState.Ready
                : _inner.Status.State switch
                {
                    DriverState.Faulted => CommunicationDriverReadinessState.Faulted,
                    DriverState.Stopped or DriverState.Stopping => CommunicationDriverReadinessState.Stopped,
                    DriverState.Starting or DriverState.Running => CommunicationDriverReadinessState.Starting,
                    _ => CommunicationDriverReadinessState.NotStarted
                };

            return new CommunicationDriverReadinessSnapshot(
                _dataSourceKey,
                Dnp3DriverDescriptorProvider.DriverType,
                state,
                observedAt,
                evidence.IsReady ? null : evidence.Reason,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sessionState"] = session.State.ToString(),
                    ["startupIntegrityScans"] = session.StartupIntegrityScans.ToString(CultureInfo.InvariantCulture)
                });
        }

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
