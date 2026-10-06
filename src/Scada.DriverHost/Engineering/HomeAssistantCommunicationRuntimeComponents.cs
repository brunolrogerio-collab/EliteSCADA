using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.HomeAssistant;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record HomeAssistantCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    HomeAssistantConnectionSettings Connection,
    string AccessTokenSecretReference,
    IReadOnlyCollection<HomeAssistantPoint> Points) : ICommunicationDriverRuntimePlan
{
    public string DriverType => HomeAssistantContract.DriverType;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(x => x.Tag).ToArray();
}

public sealed class HomeAssistantCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    public string DriverType => HomeAssistantContract.DriverType;

    public CommunicationDriverRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);
        var issues = new List<EngineeringDriverIssue>();

        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
            return Result(null, Error(
                "HA_DRIVER_TYPE_MISMATCH",
                $"Data source '{dataSource.Key}' is not '{DriverType}'.",
                dataSource.Key));

        HomeAssistantConnectionSettings connection;
        try
        {
            connection = ParseConnection(dataSource.Settings);
            connection.Validate();
        }
        catch (Exception ex)
        {
            return Result(null, Error(
                "HA_CONNECTION_INVALID",
                Sanitize(ex.Message),
                dataSource.Key));
        }

        string? accessTokenReference = null;
        dataSource.SecretReferences?.TryGetValue(
            HomeAssistantEngineeringProvider.AccessTokenReferenceKey,
            out accessTokenReference);
        if (string.IsNullOrWhiteSpace(accessTokenReference))
        {
            return Result(null, Error(
                "HA_ACCESS_TOKEN_REFERENCE_REQUIRED",
                "Home Assistant Data Source requires an access token secret reference.",
                dataSource.Key));
        }

        var sourceTags = package.Tags.Where(tag =>
            dataSource.Id.HasValue && tag.DataSourceId == dataSource.Id ||
            (!tag.DataSourceId.HasValue &&
             string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))).ToArray();

        var points = new List<HomeAssistantPoint>();
        foreach (var dto in sourceTags)
        {
            var binding = dto.CommunicationBinding;
            if (binding is null)
            {
                issues.Add(Error(
                    "HA_BINDING_REQUIRED",
                    $"Home Assistant TAG '{dto.Path}' requires CommunicationBinding.",
                    dataSource.Key,
                    dto.Path));
                continue;
            }

            try
            {
                binding.Validate();
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                issues.Add(Error(
                    "HA_BINDING_INVALID",
                    $"Home Assistant TAG '{dto.Path}' has invalid binding: {Sanitize(ex.Message)}",
                    dataSource.Key,
                    dto.Path));
                continue;
            }

            if (!string.Equals(binding.SchemaId, HomeAssistantContract.SchemaId, StringComparison.Ordinal) ||
                binding.SchemaVersion != HomeAssistantContract.SchemaVersion)
            {
                issues.Add(Error(
                    "HA_BINDING_SCHEMA",
                    $"Home Assistant TAG '{dto.Path}' uses unsupported binding schema.",
                    dataSource.Key,
                    dto.Path));
                continue;
            }

            if (!TryParsePortableAddress(binding.PortableAddress, out var entityId, out var field))
            {
                issues.Add(Error(
                    "HA_ADDRESS_INVALID",
                    $"Home Assistant TAG '{dto.Path}' address must be entity:<entity_id>:state or entity:<entity_id>:attribute:<name>.",
                    dataSource.Key,
                    dto.Path));
                continue;
            }

            var writeMode = ResolveWriteMode(entityId, field, dto.ReadOnly);
            if (!dto.ReadOnly && writeMode == HomeAssistantWriteMode.None)
            {
                issues.Add(Error(
                    "HA_WRITE_PROFILE_UNSUPPORTED",
                    $"Writable Home Assistant TAG '{dto.Path}' is outside the bounded service-action profiles.",
                    dataSource.Key,
                    dto.Path));
                continue;
            }

            if (!ValidateType(writeMode, dto.DataType))
            {
                issues.Add(Error(
                    "HA_WRITE_TYPE_INVALID",
                    $"Home Assistant TAG '{dto.Path}' data type is incompatible with its bounded write profile.",
                    dataSource.Key,
                    dto.Path));
                continue;
            }

            points.Add(new HomeAssistantPoint(
                BuildCanonicalTag(dto),
                entityId,
                field,
                writeMode));
        }

        if (issues.Any(x => x.IsError))
            return new CommunicationDriverRuntimePlanningResult(null, issues);

        if (points.Count == 0)
        {
            issues.Add(new EngineeringDriverIssue(
                "HA_NO_TAGS",
                $"Home Assistant data source '{dataSource.Key}' has no mapped TAGs.",
                dataSource.Key,
                IsError: false));
        }

        return new CommunicationDriverRuntimePlanningResult(
            new HomeAssistantCommunicationRuntimePlan(
                dataSource.Key,
                dataSource.Name,
                connection,
                accessTokenReference!,
                points),
            issues);
    }

    public static bool TryParsePortableAddress(
        string address,
        out string entityId,
        out string field)
    {
        entityId = string.Empty;
        field = string.Empty;
        if (string.IsNullOrWhiteSpace(address)) return false;

        var parts = address.Split(':');
        if (parts.Length == 3 &&
            string.Equals(parts[0], "entity", StringComparison.Ordinal) &&
            string.Equals(parts[2], "state", StringComparison.Ordinal))
        {
            entityId = parts[1];
            field = "state";
            return IsEntityId(entityId);
        }

        if (parts.Length == 4 &&
            string.Equals(parts[0], "entity", StringComparison.Ordinal) &&
            string.Equals(parts[2], "attribute", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(parts[3]))
        {
            entityId = parts[1];
            field = parts[3];
            return IsEntityId(entityId);
        }

        return false;
    }

    private static HomeAssistantWriteMode ResolveWriteMode(
        string entityId,
        string field,
        bool readOnly)
    {
        if (readOnly) return HomeAssistantWriteMode.None;
        var separator = entityId.IndexOf('.');
        if (separator <= 0) return HomeAssistantWriteMode.None;
        var domain = entityId[..separator];

        return (domain, field) switch
        {
            ("switch", "state") => HomeAssistantWriteMode.SwitchState,
            ("light", "state") => HomeAssistantWriteMode.LightState,
            ("light", "brightness") => HomeAssistantWriteMode.LightBrightnessPercent,
            ("light", "rgb_color") => HomeAssistantWriteMode.LightRgb,
            ("cover", "current_position") => HomeAssistantWriteMode.CoverPosition,
            _ => HomeAssistantWriteMode.None
        };
    }

    private static bool ValidateType(HomeAssistantWriteMode mode, TagDataType type) =>
        mode switch
        {
            HomeAssistantWriteMode.None => true,
            HomeAssistantWriteMode.SwitchState or HomeAssistantWriteMode.LightState =>
                type == TagDataType.Boolean,
            HomeAssistantWriteMode.LightBrightnessPercent or HomeAssistantWriteMode.CoverPosition =>
                type is TagDataType.Double or TagDataType.Float or TagDataType.Int16 or TagDataType.Int32 or TagDataType.Int64,
            HomeAssistantWriteMode.LightRgb => type is TagDataType.String or TagDataType.Enum,
            _ => false
        };

    private static HomeAssistantConnectionSettings ParseConnection(
        IReadOnlyDictionary<string, string>? settings)
    {
        var values = settings ?? new Dictionary<string, string>();
        var host = Require(values, "host");
        var tls = GetBool(values, "tls", false);
        var port = GetInt(values, "port", tls ? 443 : 8123);
        return new HomeAssistantConnectionSettings(
            host,
            port,
            tls,
            Get(values, "basePath") ?? string.Empty,
            TimeSpan.FromMilliseconds(GetInt(values, "connectTimeoutMilliseconds", 10000)),
            TimeSpan.FromMilliseconds(GetInt(values, "requestTimeoutMilliseconds", 10000)),
            TimeSpan.FromMilliseconds(GetInt(values, "reconnectMinimumMilliseconds", 1000)),
            TimeSpan.FromMilliseconds(GetInt(values, "reconnectMaximumMilliseconds", 30000)));
    }

    private static TagDefinition BuildCanonicalTag(TagEngineeringDto dto)
    {
        var metadata = dto.Metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(dto.Metadata, StringComparer.OrdinalIgnoreCase);
        var access = dto.AccessPolicy is null ? null : new TagAccessPolicy(
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
            dto.CommunicationBinding,
            dto.DataSourceId);
    }

    private static bool IsEntityId(string entityId)
    {
        var separator = entityId.IndexOf('.');
        return separator > 0 &&
               separator < entityId.Length - 1 &&
               entityId.IndexOf('.', separator + 1) < 0 &&
               entityId.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '.');
    }

    private static string Require(IReadOnlyDictionary<string, string> values, string key) =>
        Get(values, key) ?? throw new ArgumentException($"Home Assistant setting '{key}' is required.");

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        Get(values, key) is { } value &&
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static bool GetBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        Get(values, key) is { } value && bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;

    private static string Sanitize(string message)
    {
        var clean = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length <= 256 ? clean : clean[..256];
    }

    private static CommunicationDriverRuntimePlanningResult Result(
        ICommunicationDriverRuntimePlan? plan,
        params EngineeringDriverIssue[] issues) =>
        new(plan, issues);

    private static EngineeringDriverIssue Error(
        string code,
        string message,
        string source,
        string? tag = null) =>
        new(code, message, source, tag, IsError: true);
}

public sealed class HomeAssistantCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly Func<HomeAssistantConnectionSettings, IHomeAssistantClient>? _clientFactory;

    public HomeAssistantCommunicationRuntimeFactory(
        Func<HomeAssistantConnectionSettings, IHomeAssistantClient>? clientFactory = null) =>
        _clientFactory = clientFactory;

    public string DriverType => HomeAssistantContract.DriverType;

    public ICommunicationDriver Create(
        ICommunicationDriverRuntimePlan plan,
        CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();

        if (plan is not HomeAssistantCommunicationRuntimePlan ha)
        {
            throw new ArgumentException(
                $"Home Assistant runtime factory requires {nameof(HomeAssistantCommunicationRuntimePlan)}.",
                nameof(plan));
        }

        if (services.ProtectedMaterialResolver is null)
        {
            throw new InvalidOperationException(
                $"Home Assistant data source '{ha.DataSourceKey}' requires protected material resolution.");
        }

        async ValueTask<HomeAssistantResolvedCredential> Resolve(CancellationToken cancellationToken)
        {
            var request = HomeAssistantProtectedMaterial.CreateAccessTokenRequest(
                services.ProjectKey,
                ha.DataSourceKey,
                ha.AccessTokenSecretReference);
            await using var lease = await services.ProtectedMaterialResolver
                .ResolveAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (lease.Material.IsEmpty)
                throw new InvalidOperationException("Home Assistant protected access token is empty.");
            return new HomeAssistantResolvedCredential(lease.Material);
        }

        return new HomeAssistantDriver(
            ha.DataSourceKey,
            ha.Name,
            ha.Connection,
            services.Cache,
            services.Registry,
            ha.Points,
            _clientFactory ?? (settings => new HomeAssistantWebSocketClient(settings)),
            Resolve);
    }
}
