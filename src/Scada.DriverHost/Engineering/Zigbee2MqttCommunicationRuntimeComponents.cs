using System.Globalization;
using Scada.Core.Tags;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Mqtt;
using Scada.Drivers.Zigbee2Mqtt;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record Zigbee2MqttCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    Guid DataSourceId,
    Zigbee2MqttConnectionSettings Connection,
    string? Username,
    string? PasswordSecretReference,
    IReadOnlyCollection<Zigbee2MqttPoint> Points) : ICommunicationDriverRuntimePlan
{
    public string DriverType => Zigbee2MqttContract.DriverType;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(point => point.Tag).ToArray();
}

public sealed class Zigbee2MqttCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    public string DriverType => Zigbee2MqttContract.DriverType;

    public CommunicationDriverRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);
        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
            return Failure("Z2M_DRIVER_TYPE_MISMATCH", $"Data Source '{dataSource.Key}' does not declare DriverType '{DriverType}'.", dataSource.Key);
        if (dataSource.Id is null || dataSource.Id == Guid.Empty)
            return Failure("Z2M_DATASOURCE_ID_REQUIRED", "Zigbee2MQTT requires a canonical DataSourceId to preserve IEEE device identity.", dataSource.Key);

        Zigbee2MqttConnectionSettings connection;
        try { connection = Zigbee2MqttConnectionSettings.Parse(dataSource.Key, dataSource.Id, dataSource.Settings); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException)
        {
            return Failure("Z2M_CONNECTION_INVALID", SafeFailure(ex), dataSource.Key);
        }

        var settings = dataSource.Settings ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (settings.Keys.Any(key => key.Equals("password", StringComparison.OrdinalIgnoreCase) || key.Equals("passwordValue", StringComparison.OrdinalIgnoreCase)))
            return Failure("Z2M_PLAINTEXT_CREDENTIAL_FORBIDDEN", "MQTT passwords must be referenced through Data Source protected SecretReferences, never stored in settings.", dataSource.Key);

        var secretReferences = dataSource.SecretReferences ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? passwordReference = Get(secretReferences, Zigbee2MqttContract.PasswordSecretReferenceKey);
        var username = Get(settings, "username");
        if (passwordReference is not null && string.IsNullOrWhiteSpace(username))
            return Failure("Z2M_USERNAME_REQUIRED", "A protected MQTT password reference requires a broker username.", dataSource.Key);

        var sourceTags = package.Tags.Where(tag =>
            (tag.DataSourceId.HasValue && tag.DataSourceId == dataSource.Id) ||
            (!tag.DataSourceId.HasValue && string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))).ToArray();
        var issues = new List<EngineeringDriverIssue>();
        var points = new List<Zigbee2MqttPoint>();
        var addresses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dto in sourceTags)
        {
            if (dto.CommunicationBinding is null)
            {
                issues.Add(Error("Z2M_BINDING_REQUIRED", $"Zigbee2MQTT TAG '{dto.Path}' requires a versioned CommunicationBinding.", dataSource.Key, dto.Path));
                continue;
            }

            try
            {
                var binding = dto.CommunicationBinding;
                binding.Validate();
                if (!string.IsNullOrWhiteSpace(dto.Address) && !string.Equals(dto.Address, binding.PortableAddress, StringComparison.Ordinal))
                    throw new ArgumentException("TAG Address must exactly match CommunicationBinding.PortableAddress.");
                if (!addresses.Add(binding.PortableAddress))
                    throw new ArgumentException("Data Source contains duplicate or ambiguous IEEE/endpoint/property bindings.");

                var point = Zigbee2MqttEngineeringProvider.CreatePoint(
                    binding,
                    dto.DataType,
                    dto.EngineeringUnit,
                    dto.ReadOnly,
                    dataSource.Id.Value);
                var ieee = point.IeeeAddress;
                var expectedIdentity = Zigbee2MqttIdentity.StablePointIdentity(dataSource.Id.Value, ieee, point.Endpoint, point.Property);
                if (!binding.EffectiveSettings.TryGetValue("z2m.identity", out var identity) || !string.Equals(identity, expectedIdentity, StringComparison.Ordinal))
                    throw new ArgumentException("TAG binding identity must be DataSourceId + IEEE address + endpoint/property.");
                if (!binding.EffectiveSettings.TryGetValue("z2m.dataSourceKey", out var sourceKey) || !string.Equals(sourceKey, dataSource.Key, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("TAG binding does not belong to this canonical Data Source.");

                var tag = BuildCanonicalTag(dto, dataSource.Id.Value);
                points.Add(point with { Tag = tag });
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidOperationException)
            {
                issues.Add(Error("Z2M_TAG_BINDING_INVALID", $"Zigbee2MQTT TAG '{dto.Path}' binding is invalid: {SafeFailure(ex)}", dataSource.Key, dto.Path));
            }
        }

        if (issues.Any(issue => issue.IsError))
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        if (points.Count == 0)
            issues.Add(new EngineeringDriverIssue(
                "Z2M_NO_TAGS",
                $"Data Source '{dataSource.Key}' has no selected scalar TAG bindings; bridge health remains available.",
                dataSource.Key,
                IsError: false));

        return new CommunicationDriverRuntimePlanningResult(
            new Zigbee2MqttCommunicationRuntimePlan(
                dataSource.Key,
                dataSource.Name,
                dataSource.Id.Value,
                connection,
                username,
                passwordReference,
                points),
            issues);
    }

    private static TagDefinition BuildCanonicalTag(TagEngineeringDto dto, Guid dataSourceId)
    {
        var metadata = dto.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(dto.Address)) metadata["address"] = dto.Address;
        if (dto.ScaleMinimum.HasValue) metadata["scale.minimum"] = dto.ScaleMinimum.Value.ToString("R", CultureInfo.InvariantCulture);
        if (dto.ScaleMaximum.HasValue) metadata["scale.maximum"] = dto.ScaleMaximum.Value.ToString("R", CultureInfo.InvariantCulture);
        if (dto.Historian is not null)
        {
            metadata["historian.enabled"] = dto.Historian.Enabled.ToString(CultureInfo.InvariantCulture);
            metadata["historian.strategy"] = dto.Historian.Strategy;
            if (dto.Historian.Deadband.HasValue) metadata["historian.deadband"] = dto.Historian.Deadband.Value.ToString("R", CultureInfo.InvariantCulture);
            if (dto.Historian.PeriodMilliseconds.HasValue) metadata["historian.periodMs"] = dto.Historian.PeriodMilliseconds.Value.ToString(CultureInfo.InvariantCulture);
            if (dto.Historian.MaximumPeriodMilliseconds.HasValue) metadata["historian.maxPeriodMs"] = dto.Historian.MaximumPeriodMilliseconds.Value.ToString(CultureInfo.InvariantCulture);
        }

        var access = dto.AccessPolicy is null
            ? null
            : new TagAccessPolicy(dto.AccessPolicy.ReadRoles?.ToArray(), dto.AccessPolicy.WriteRoles?.ToArray(), dto.AccessPolicy.ConfigureRoles?.ToArray());
        return new TagDefinition(
            dto.Id ?? Guid.NewGuid(), dto.Name, dto.Path, dto.DataType, dto.Source, dto.EngineeringUnit,
            dto.Description, dto.ReadOnly, metadata, access, dto.AddressSelector, dto.CommunicationBinding, dataSourceId);
    }

    private static CommunicationDriverRuntimePlanningResult Failure(string code, string message, string dataSourceKey) =>
        new(null, [Error(code, message, dataSourceKey)]);

    private static EngineeringDriverIssue Error(string code, string message, string dataSourceKey, string? tagPath = null) =>
        new(code, message, dataSourceKey, tagPath, IsError: true);

    private static string SafeFailure(Exception exception) => exception switch
    {
        NotSupportedException => "the selected binding schema or feature is outside the supported v1 contract",
        ArgumentException => "settings, type, unit, range, access, or stable identity failed validation",
        InvalidOperationException => "the selected expose no longer matches the canonical binding",
        _ => "the Zigbee2MQTT configuration failed validation"
    };

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : values.FirstOrDefault(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
}

public sealed class Zigbee2MqttCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly Func<IMqttClientTransport> _transportFactory;

    public Zigbee2MqttCommunicationRuntimeFactory(Func<IMqttClientTransport>? transportFactory = null) =>
        _transportFactory = transportFactory ?? (() => new MqttNetClientTransport());

    public string DriverType => Zigbee2MqttContract.DriverType;

    public ICommunicationDriver Create(ICommunicationDriverRuntimePlan plan, CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        if (plan is not Zigbee2MqttCommunicationRuntimePlan z2mPlan)
            throw new ArgumentException($"Zigbee2MQTT runtime factory requires {nameof(Zigbee2MqttCommunicationRuntimePlan)}.", nameof(plan));
        if (!string.Equals(z2mPlan.DriverType, DriverType, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Zigbee2MQTT runtime plan DriverType does not match its factory.", nameof(plan));

        Zigbee2MqttCredentialResolver resolver = async cancellationToken =>
        {
            if (z2mPlan.PasswordSecretReference is null)
            {
                if (z2mPlan.Username is null) return MqttResolvedCredentials.None;
                return new MqttResolvedCredentials(z2mPlan.Username);
            }

            var protectedMaterial = services.ProtectedMaterialResolver
                ?? throw new InvalidOperationException("Zigbee2MQTT protected-material resolver is unavailable.");
            var request = new CommunicationDriverProtectedMaterialRequest(
                services.ProjectKey,
                z2mPlan.DataSourceKey,
                DriverType,
                Zigbee2MqttContract.PasswordPurpose,
                z2mPlan.PasswordSecretReference);
            request.Validate();
            await using var lease = await protectedMaterial.ResolveAsync(request, cancellationToken).ConfigureAwait(false);
            if (lease.Material.IsEmpty) throw new InvalidOperationException("Zigbee2MQTT protected password resolved to empty material.");
            if (string.IsNullOrWhiteSpace(z2mPlan.Username)) throw new InvalidOperationException("Zigbee2MQTT protected password requires a username.");
            return new MqttResolvedCredentials(z2mPlan.Username, lease.Material);
        };

        return new Zigbee2MqttDriver(
            z2mPlan.DataSourceKey,
            z2mPlan.Name,
            z2mPlan.DataSourceId,
            z2mPlan.Connection,
            services.Cache,
            services.Registry,
            z2mPlan.Points,
            _transportFactory,
            resolver,
            () => services.CanOwnExternalEffects);
    }
}
