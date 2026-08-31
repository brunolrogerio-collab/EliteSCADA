using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record S7IsoCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    S7IsoConnectionOptions Options,
    IReadOnlyCollection<S7IsoPoint> Points) : ICommunicationDriverRuntimePlan
{
    public const string DriverTypeKey = "siemens.s7.iso";
    public string DriverType => DriverTypeKey;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(static point => point.Tag).ToArray();
}

/// <summary>
/// Coordinator-owned Siemens S7 ISO-on-TCP convergence adapter. Siemens parsing,
/// point-shape validation and byte/word ordering remain protocol-owned; schema-v15
/// CommunicationBinding is the host-owned canonical envelope.
/// </summary>
public sealed class S7IsoCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    public string DriverType => S7IsoCommunicationRuntimePlan.DriverTypeKey;

    public CommunicationDriverRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);

        var issues = new List<EngineeringDriverIssue>();
        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error(
                "S7_DATASOURCE_DRIVER_MISMATCH",
                $"Data source '{dataSource.Key}' declares driver '{dataSource.Driver}', not '{DriverType}'.",
                dataSource.Key));
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        }

        if (string.IsNullOrWhiteSpace(dataSource.Key))
            issues.Add(Error("S7_DATASOURCE_KEY_REQUIRED", "Siemens S7 data source key is required.", dataSource.Key));
        if (string.IsNullOrWhiteSpace(dataSource.Name))
            issues.Add(Error("S7_DATASOURCE_NAME_REQUIRED", "Siemens S7 data source name is required.", dataSource.Key));

        if (dataSource.SecretReferences is { Count: > 0 })
        {
            issues.Add(Error(
                "S7_PROTECTED_MATERIAL_UNSUPPORTED",
                $"Siemens S7 data source '{dataSource.Key}' cannot declare protected material in the current ISO-on-TCP profile.",
                dataSource.Key));
        }

        S7IsoConnectionOptions? options = null;
        var settings = dataSource.Settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _ = S7IsoRuntimeConfiguration.TryCreateOptions(settings, out options, out var configurationIssues);
        foreach (var issue in configurationIssues)
        {
            issues.Add(new EngineeringDriverIssue(
                issue.Code,
                issue.Message,
                dataSource.Key,
                IsError: issue.Severity == DriverEngineeringIssueSeverity.Error));
        }

        var sourceTags = package.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static tag => tag.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceTags.Length == 0)
        {
            issues.Add(Error(
                "S7_DATASOURCE_NO_TAGS",
                $"Enabled Siemens S7 data source '{dataSource.Key}' has no associated TAGs and cannot create an S7 runtime.",
                dataSource.Key));
        }

        var points = new List<S7IsoPoint>();
        foreach (var dto in sourceTags)
        {
            var point = BuildPoint(package.SchemaVersion, dataSource.Key, dto, issues);
            if (point is not null) points.Add(point);
        }

        foreach (var duplicate in points.GroupBy(static point => point.Tag.Id).Where(static group => group.Count() > 1))
        {
            issues.Add(Error(
                "S7_TAG_ID_DUPLICATE",
                $"Siemens S7 data source '{dataSource.Key}' contains duplicate stable TAG ID '{duplicate.Key}'.",
                dataSource.Key));
        }

        foreach (var duplicate in points
                     .GroupBy(static point => PhysicalIdentity(point), StringComparer.Ordinal)
                     .Where(static group => group.Count() > 1))
        {
            issues.Add(Error(
                "S7_PHYSICAL_IDENTITY_DUPLICATE",
                $"Siemens S7 data source '{dataSource.Key}' contains duplicate physical binding '{duplicate.Key}'.",
                dataSource.Key));
        }

        if (options is null || points.Count == 0 || issues.Any(static issue => issue.IsError))
            return new CommunicationDriverRuntimePlanningResult(null, issues);

        return new CommunicationDriverRuntimePlanningResult(
            new S7IsoCommunicationRuntimePlan(dataSource.Key, dataSource.Name, options, points.ToArray()),
            issues);
    }

    private static S7IsoPoint? BuildPoint(
        int packageSchemaVersion,
        string dataSourceKey,
        TagEngineeringDto dto,
        ICollection<EngineeringDriverIssue> issues)
    {
        if (!dto.Id.HasValue || dto.Id.Value == Guid.Empty)
        {
            issues.Add(Error(
                "S7_TAG_STABLE_ID_REQUIRED",
                $"Siemens S7 TAG '{dto.Path}' requires a stable non-empty ID before runtime activation.",
                dataSourceKey,
                dto.Path));
            return null;
        }

        if (dto.AddressSelector is not null)
        {
            issues.Add(Error(
                "S7_TAG_ADDRESS_SELECTOR_UNSUPPORTED",
                $"Siemens S7 TAG '{dto.Path}' cannot use generic AddressSelector; BOOL bit identity belongs to the S7 binding.",
                dataSourceKey,
                dto.Path));
            return null;
        }

        S7IsoTagBinding? binding;
        if (dto.CommunicationBinding is { } communicationBinding)
        {
            try
            {
                communicationBinding.Validate();
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or NotSupportedException)
            {
                issues.Add(Error(
                    "S7_TAG_BINDING_INVALID",
                    $"Siemens S7 TAG '{dto.Path}' has an invalid CommunicationBinding: {ex.Message}",
                    dataSourceKey,
                    dto.Path));
                return null;
            }

            if (!string.Equals(
                    communicationBinding.SchemaId,
                    S7IsoCommunicationBindingProjection.SchemaId,
                    StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error(
                    "S7_TAG_BINDING_SCHEMA_MISMATCH",
                    $"Siemens S7 TAG '{dto.Path}' binding schema must be '{S7IsoCommunicationBindingProjection.SchemaId}', received '{communicationBinding.SchemaId}'.",
                    dataSourceKey,
                    dto.Path));
                return null;
            }
            if (communicationBinding.SchemaVersion != S7IsoCommunicationBindingProjection.SchemaVersion)
            {
                issues.Add(Error(
                    "S7_TAG_BINDING_SCHEMA_VERSION_UNSUPPORTED",
                    $"Siemens S7 TAG '{dto.Path}' binding schema version must be {S7IsoCommunicationBindingProjection.SchemaVersion}, received {communicationBinding.SchemaVersion}.",
                    dataSourceKey,
                    dto.Path));
                return null;
            }
            if (!string.IsNullOrWhiteSpace(dto.Address) &&
                !string.Equals(dto.Address, communicationBinding.PortableAddress, StringComparison.Ordinal))
            {
                issues.Add(Error(
                    "S7_TAG_BINDING_ADDRESS_MISMATCH",
                    $"Siemens S7 TAG '{dto.Path}' Address must exactly match CommunicationBinding.PortableAddress.",
                    dataSourceKey,
                    dto.Path));
                return null;
            }

            var transform = communicationBinding.ValueTransform;
            var byteSwap = transform?.ByteSwap ?? false;
            var wordSwap = transform?.WordSwap ?? false;
            if (!S7IsoCommunicationBindingProjection.TryMaterializeCanonical(
                    communicationBinding.PortableAddress,
                    communicationBinding.EffectiveSettings,
                    byteSwap,
                    wordSwap,
                    out binding,
                    out var bindingError))
            {
                issues.Add(Error(
                    "S7_TAG_BINDING_INVALID",
                    $"Siemens S7 TAG '{dto.Path}' cannot materialize its canonical binding: {bindingError}",
                    dataSourceKey,
                    dto.Path));
                return null;
            }
        }
        else
        {
            if (packageSchemaVersion >= 15)
            {
                issues.Add(new EngineeringDriverIssue(
                    "S7_TAG_LEGACY_BINDING",
                    $"Siemens S7 TAG '{dto.Path}' uses legacy Address without CommunicationBinding; it remains activatable only for migration compatibility.",
                    dataSourceKey,
                    dto.Path,
                    IsError: false));
            }

            if (!S7IsoTagBinding.TryParsePortableAddress(dto.Address, out binding, out var bindingError))
            {
                issues.Add(Error(
                    "S7_TAG_BINDING_INVALID",
                    $"Siemens S7 TAG '{dto.Path}' has an invalid legacy binding: {bindingError}",
                    dataSourceKey,
                    dto.Path));
                return null;
            }
        }

        try
        {
            var tag = BuildCanonicalTag(dto);
            var point = binding!.ToPoint(tag);
            point.Validate();
            return point;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            issues.Add(Error(
                "S7_TAG_RUNTIME_INVALID",
                $"Siemens S7 TAG '{dto.Path}' cannot be compiled for runtime: {ex.Message}",
                dataSourceKey,
                dto.Path));
            return null;
        }
    }

    private static TagDefinition BuildCanonicalTag(TagEngineeringDto dto)
    {
        var metadata = dto.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);
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

    private static string PhysicalIdentity(S7IsoPoint point) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{point.Area}:{point.DbNumber}:{point.ByteOffset}:{point.BitOffset}:{point.ValueType}");

    private static EngineeringDriverIssue Error(
        string code,
        string message,
        string dataSourceKey,
        string? tagPath = null) =>
        new(code, message, dataSourceKey, tagPath, IsError: true);

    private static void Set(Dictionary<string, string> target, string key, double? value)
    {
        if (value.HasValue) target[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static void Set(Dictionary<string, string> target, string key, int? value)
    {
        if (value.HasValue) target[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }
}

public sealed class S7IsoCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    public string DriverType => S7IsoCommunicationRuntimePlan.DriverTypeKey;

    public ICommunicationDriver Create(
        ICommunicationDriverRuntimePlan plan,
        CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();

        if (plan is not S7IsoCommunicationRuntimePlan s7Plan)
            throw new ArgumentException($"S7 runtime factory requires {nameof(S7IsoCommunicationRuntimePlan)}.", nameof(plan));
        if (!string.Equals(s7Plan.DriverType, DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"S7 runtime plan DriverType '{s7Plan.DriverType}' does not match '{DriverType}'.", nameof(plan));
        if (s7Plan.Points.Count == 0)
            throw new ArgumentException("S7 runtime plan requires at least one point.", nameof(plan));

        var driver = new S7IsoDriver(
            s7Plan.DataSourceKey,
            s7Plan.Name,
            s7Plan.Options,
            services.Cache,
            services.Registry,
            s7Plan.Points);
        return new S7IsoCoordinatorRuntimeDriver(driver, s7Plan.DataSourceKey);
    }

    private sealed class S7IsoCoordinatorRuntimeDriver :
        ICommunicationDriver,
        ICommunicationDiagnosticsSource,
        ICommunicationDriverReadinessSource
    {
        private readonly S7IsoDriver _inner;
        private readonly string _dataSourceKey;

        public S7IsoCoordinatorRuntimeDriver(S7IsoDriver inner, string dataSourceKey)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
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
            var snapshot = _inner.GetS7IsoRuntimeReadiness();
            var state = snapshot.State switch
            {
                S7IsoRuntimeReadinessState.Ready => CommunicationDriverReadinessState.Ready,
                S7IsoRuntimeReadinessState.Starting => CommunicationDriverReadinessState.Starting,
                S7IsoRuntimeReadinessState.Faulted => CommunicationDriverReadinessState.Faulted,
                S7IsoRuntimeReadinessState.Stopped => CommunicationDriverReadinessState.Stopped,
                _ => CommunicationDriverReadinessState.NotStarted
            };

            return new CommunicationDriverReadinessSnapshot(
                _dataSourceKey,
                S7IsoCommunicationRuntimePlan.DriverTypeKey,
                state,
                snapshot.CapturedAt,
                snapshot.LastError,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["initialAcquisitionCompleted"] = snapshot.InitialAcquisitionCompleted ? "true" : "false",
                    ["initialAcquisitionAttempts"] = snapshot.InitialAcquisitionAttempts.ToString(CultureInfo.InvariantCulture),
                    ["negotiatedPduSize"] = snapshot.NegotiatedPduSizeAtReady?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
                });
        }

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
