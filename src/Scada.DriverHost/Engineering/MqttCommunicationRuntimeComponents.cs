using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record MqttCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    MqttConnectionSettings Connection,
    string? Username,
    string? PasswordSecretReference,
    IReadOnlyCollection<MqttPoint> Points) : ICommunicationDriverRuntimePlan
{
    public string DriverType => MqttDriverDescriptorProvider.DriverType;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(point => point.Tag).ToArray();
}

/// <summary>
/// Coordinator adapter over the audited MQTT compiler. Engineering v15
/// CommunicationBinding is canonical when present; legacy Address/Metadata is
/// accepted only as backward-compatible input when the binding is absent.
/// </summary>
public sealed class MqttCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    private const int BindingSchemaVersion = 1;
    private readonly MqttEngineeringCompiler _compiler = new();

    public string DriverType => MqttDriverDescriptorProvider.DriverType;

    public CommunicationDriverRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);

        if (!dataSource.Driver.Equals(DriverType, StringComparison.OrdinalIgnoreCase))
        {
            return new CommunicationDriverRuntimePlanningResult(
                null,
                [new EngineeringDriverIssue(
                    "MQTT_DRIVER_TYPE_MISMATCH",
                    $"Data source '{dataSource.Key}' declares driver '{dataSource.Driver}', not '{DriverType}'.",
                    dataSource.Key)]);
        }

        var issues = new List<EngineeringDriverIssue>();
        var normalizedTags = package.Tags
            .Select(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase)
                ? NormalizeTag(package.SchemaVersion, dataSource.Key, tag, issues)
                : tag)
            .ToArray();

        if (issues.Any(issue => issue.IsError))
            return new CommunicationDriverRuntimePlanningResult(null, issues);

        var normalizedPackage = package with
        {
            DataSources = [dataSource],
            Tags = normalizedTags
        };
        var compilation = _compiler.Compile(normalizedPackage);
        issues.AddRange(compilation.Issues);

        var workerPlan = compilation.Plans.SingleOrDefault(plan =>
            plan.DataSourceKey.Equals(dataSource.Key, StringComparison.OrdinalIgnoreCase));
        if (workerPlan is null || issues.Any(issue => issue.IsError))
            return new CommunicationDriverRuntimePlanningResult(null, issues);

        var originals = package.Tags
            .Where(tag => string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(tag => tag.Path, StringComparer.OrdinalIgnoreCase);
        var points = workerPlan.Points
            .Select(point =>
            {
                if (!originals.TryGetValue(point.Tag.Path, out var original))
                    throw new InvalidOperationException($"MQTT compiled TAG '{point.Tag.Path}' has no canonical Engineering source.");
                return point with { Tag = BuildCanonicalTag(original) };
            })
            .ToArray();

        return new CommunicationDriverRuntimePlanningResult(
            new MqttCommunicationRuntimePlan(
                dataSource.Key,
                workerPlan.Name,
                workerPlan.Connection,
                workerPlan.Username,
                workerPlan.PasswordSecretReference,
                points),
            issues);
    }

    private static TagEngineeringDto NormalizeTag(
        int packageSchemaVersion,
        string dataSourceKey,
        TagEngineeringDto tag,
        List<EngineeringDriverIssue> issues)
    {
        var binding = tag.CommunicationBinding;
        if (binding is null)
        {
            if (packageSchemaVersion >= 15)
            {
                issues.Add(new EngineeringDriverIssue(
                    "MQTT_TAG_LEGACY_BINDING",
                    $"MQTT TAG '{tag.Path}' uses legacy Address/Metadata input without CommunicationBinding; it remains activatable for backward compatibility.",
                    dataSourceKey,
                    tag.Path,
                    IsError: false));
            }
            return tag;
        }

        try
        {
            binding.Validate();
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            issues.Add(Error(
                "MQTT_TAG_BINDING_INVALID",
                $"MQTT TAG '{tag.Path}' has an invalid CommunicationBinding: {ex.Message}",
                dataSourceKey,
                tag.Path));
            return tag;
        }

        if (!binding.SchemaId.Equals(MqttDriverDescriptorProvider.SchemaId, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error(
                "MQTT_TAG_BINDING_SCHEMA_MISMATCH",
                $"MQTT TAG '{tag.Path}' binding schema must be '{MqttDriverDescriptorProvider.SchemaId}', received '{binding.SchemaId}'.",
                dataSourceKey,
                tag.Path));
        }
        if (binding.SchemaVersion != BindingSchemaVersion)
        {
            issues.Add(Error(
                "MQTT_TAG_BINDING_SCHEMA_VERSION_UNSUPPORTED",
                $"MQTT TAG '{tag.Path}' binding schema version must be {BindingSchemaVersion}, received {binding.SchemaVersion}.",
                dataSourceKey,
                tag.Path));
        }
        if (binding.ValueTransform is not null)
        {
            issues.Add(Error(
                "MQTT_TAG_BINDING_TRANSFORM_UNSUPPORTED",
                $"MQTT TAG '{tag.Path}' cannot use byte/word physical transforms; payload decoding is defined by MQTT payload settings.",
                dataSourceKey,
                tag.Path));
        }
        if (!string.IsNullOrWhiteSpace(tag.Address) &&
            !string.Equals(tag.Address, binding.PortableAddress, StringComparison.Ordinal))
        {
            issues.Add(Error(
                "MQTT_TAG_BINDING_ADDRESS_MISMATCH",
                $"MQTT TAG '{tag.Path}' Address must exactly match CommunicationBinding.PortableAddress.",
                dataSourceKey,
                tag.Path));
        }

        var metadata = CaseInsensitive(tag.Metadata);
        foreach (var key in metadata.Keys.Where(key => key.StartsWith("mqtt.", StringComparison.OrdinalIgnoreCase)).ToArray())
            metadata.Remove(key);
        foreach (var setting in binding.EffectiveSettings)
            metadata[setting.Key] = setting.Value;

        return tag with
        {
            Address = binding.PortableAddress,
            Metadata = metadata
        };
    }

    private static TagDefinition BuildCanonicalTag(TagEngineeringDto dto)
    {
        var metadata = CaseInsensitive(dto.Metadata);
        if (dto.CommunicationBinding is not null)
        {
            foreach (var key in metadata.Keys.Where(key => key.StartsWith("mqtt.", StringComparison.OrdinalIgnoreCase)).ToArray())
                metadata.Remove(key);
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
            dto.AddressSelector,
            dto.CommunicationBinding);
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

    private static void Set(Dictionary<string, string> metadata, string key, double? value)
    {
        if (value.HasValue) metadata[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static void Set(Dictionary<string, string> metadata, string key, int? value)
    {
        if (value.HasValue) metadata[key] = value.Value.ToString(CultureInfo.InvariantCulture);
    }
}

public sealed class MqttCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly Func<IMqttClientTransport> _transportFactory;

    public MqttCommunicationRuntimeFactory(Func<IMqttClientTransport>? transportFactory = null)
    {
        _transportFactory = transportFactory ?? (() => new MqttNetClientTransport());
    }

    public string DriverType => MqttDriverDescriptorProvider.DriverType;

    public ICommunicationDriver Create(
        ICommunicationDriverRuntimePlan plan,
        CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();

        if (plan is not MqttCommunicationRuntimePlan mqttPlan)
            throw new ArgumentException($"MQTT runtime factory requires {nameof(MqttCommunicationRuntimePlan)}.", nameof(plan));
        if (!mqttPlan.DriverType.Equals(DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"MQTT runtime plan declares unexpected DriverType '{mqttPlan.DriverType}'.", nameof(plan));
        if (string.IsNullOrWhiteSpace(mqttPlan.DataSourceKey))
            throw new ArgumentException("MQTT runtime plan requires a data source key.", nameof(plan));
        if (string.IsNullOrWhiteSpace(mqttPlan.Name))
            throw new ArgumentException("MQTT runtime plan requires a display name.", nameof(plan));

        mqttPlan.Connection.Validate();
        if (mqttPlan.Points.Count == 0)
            throw new ArgumentException("MQTT runtime plan must contain at least one point.", nameof(plan));
        foreach (var point in mqttPlan.Points) point.Validate();
        if (mqttPlan.Points.Select(point => point.Tag.Id).Distinct().Count() != mqttPlan.Points.Count)
            throw new ArgumentException("MQTT runtime plan contains duplicate TAG IDs.", nameof(plan));

        if (mqttPlan.PasswordSecretReference is not null && services.ProtectedMaterialResolver is null)
        {
            throw new InvalidOperationException(
                $"MQTT data source '{mqttPlan.DataSourceKey}' references protected credentials, but the host did not provide a protected-material resolver.");
        }

        var transport = _transportFactory()
            ?? throw new InvalidOperationException("MQTT transport factory returned null.");

        MqttCredentialResolver credentials = async cancellationToken =>
        {
            if (mqttPlan.PasswordSecretReference is null)
                return new MqttResolvedCredentials(mqttPlan.Username, ReadOnlyMemory<byte>.Empty);

            var resolver = services.ProtectedMaterialResolver
                ?? throw new InvalidOperationException("MQTT protected-material resolver is unavailable.");
            var request = new CommunicationDriverProtectedMaterialRequest(
                services.ProjectKey,
                mqttPlan.DataSourceKey,
                DriverType,
                "mqtt.password",
                mqttPlan.PasswordSecretReference);
            request.Validate();

            await using var lease = await resolver.ResolveAsync(request, cancellationToken);
            var resolved = new MqttResolvedCredentials(mqttPlan.Username, lease.Material);
            ValidateResolvedCredentials(mqttPlan, resolved);
            return resolved;
        };

        return new MqttDriver(
            mqttPlan.DataSourceKey,
            mqttPlan.Name,
            mqttPlan.Connection,
            services.Cache,
            services.Registry,
            mqttPlan.Points,
            transport,
            credentials);
    }

    private static void ValidateResolvedCredentials(
        MqttCommunicationRuntimePlan plan,
        MqttResolvedCredentials resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);

        if (!string.Equals(plan.Username, resolved.Username, StringComparison.Ordinal))
        {
            resolved.Dispose();
            throw new MqttTransportException(
                $"Resolved MQTT username does not match canonical Engineering for data source '{plan.DataSourceKey}'.",
                isPermanent: true);
        }
        if (plan.PasswordSecretReference is not null && resolved.Password.IsEmpty)
        {
            resolved.Dispose();
            throw new MqttTransportException(
                $"Protected MQTT credential reference for data source '{plan.DataSourceKey}' resolved to empty material.",
                isPermanent: true);
        }
        if (resolved.Username is null && !resolved.Password.IsEmpty)
        {
            resolved.Dispose();
            throw new MqttTransportException(
                $"MQTT data source '{plan.DataSourceKey}' resolved password material without a username.",
                isPermanent: true);
        }
    }
}
