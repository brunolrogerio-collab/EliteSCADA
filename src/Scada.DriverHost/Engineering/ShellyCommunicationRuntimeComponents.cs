using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Shelly;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record ShellyCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    ShellyConnectionSettings Connection,
    string? PasswordSecretReference,
    IReadOnlyCollection<ShellyPoint> Points) : ICommunicationDriverRuntimePlan
{
    public string DriverType => ShellyRpcContract.DriverType;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(x => x.Tag).ToArray();
}

public sealed class ShellyCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    public string DriverType => ShellyRpcContract.DriverType;

    public CommunicationDriverRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);
        var issues = new List<EngineeringDriverIssue>();

        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
            return Result(null, Error("SHELLY_DRIVER_TYPE_MISMATCH", $"Data source '{dataSource.Key}' is not '{DriverType}'.", dataSource.Key));

        ShellyConnectionSettings connection;
        try
        {
            connection = ParseConnection(dataSource.Settings);
            connection.Validate();
        }
        catch (Exception ex)
        {
            return Result(null, Error("SHELLY_CONNECTION_INVALID", ex.Message, dataSource.Key));
        }

        var sourceTags = package.Tags.Where(tag =>
            dataSource.Id.HasValue && tag.DataSourceId == dataSource.Id ||
            (!tag.DataSourceId.HasValue && string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))).ToArray();

        var points = new List<ShellyPoint>();
        foreach (var dto in sourceTags)
        {
            var binding = dto.CommunicationBinding;
            if (binding is null)
            {
                issues.Add(Error("SHELLY_BINDING_REQUIRED", $"Shelly TAG '{dto.Path}' requires CommunicationBinding.", dataSource.Key, dto.Path));
                continue;
            }

            try { binding.Validate(); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                issues.Add(Error("SHELLY_BINDING_INVALID", $"Shelly TAG '{dto.Path}' has invalid binding: {ex.Message}", dataSource.Key, dto.Path));
                continue;
            }

            if (!string.Equals(binding.SchemaId, ShellyRpcContract.SchemaId, StringComparison.Ordinal) ||
                binding.SchemaVersion != ShellyRpcContract.SchemaVersion)
            {
                issues.Add(Error("SHELLY_BINDING_SCHEMA", $"Shelly TAG '{dto.Path}' uses unsupported binding schema.", dataSource.Key, dto.Path));
                continue;
            }

            var separator = binding.PortableAddress.IndexOf('.');
            if (separator <= 0 || separator == binding.PortableAddress.Length - 1)
            {
                issues.Add(Error("SHELLY_ADDRESS_INVALID", $"Shelly TAG '{dto.Path}' address must be <component>.<field>.", dataSource.Key, dto.Path));
                continue;
            }

            var component = binding.PortableAddress[..separator];
            var field = binding.PortableAddress[(separator + 1)..];
            binding.EffectiveSettings.TryGetValue("writeMethod", out var writeMethod);
            binding.EffectiveSettings.TryGetValue("writeParameter", out var writeParameter);
            if (!dto.ReadOnly && (string.IsNullOrWhiteSpace(writeMethod) || string.IsNullOrWhiteSpace(writeParameter)))
            {
                issues.Add(Error("SHELLY_WRITE_MAPPING_REQUIRED", $"Writable Shelly TAG '{dto.Path}' requires writeMethod/writeParameter.", dataSource.Key, dto.Path));
                continue;
            }

            points.Add(new ShellyPoint(BuildCanonicalTag(dto), component, field, writeMethod, writeParameter));
        }

        if (issues.Any(x => x.IsError))
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        if (points.Count == 0)
            issues.Add(new EngineeringDriverIssue("SHELLY_NO_TAGS", $"Shelly data source '{dataSource.Key}' has no mapped TAGs.", dataSource.Key, IsError: false));

        dataSource.SecretReferences?.TryGetValue("password", out var passwordReference);
        return new CommunicationDriverRuntimePlanningResult(
            new ShellyCommunicationRuntimePlan(dataSource.Key, dataSource.Name, connection, passwordReference, points),
            issues);
    }

    private static ShellyConnectionSettings ParseConnection(IReadOnlyDictionary<string, string>? settings)
    {
        var values = settings ?? new Dictionary<string, string>();
        var host = Require(values, "host");
        var tls = GetBool(values, "tls", false);
        var port = GetInt(values, "port", tls ? 443 : 80);
        var username = Get(values, "username") ?? "admin";
        return new ShellyConnectionSettings(
            host,
            port,
            tls,
            username,
            TimeSpan.FromMilliseconds(GetInt(values, "requestTimeoutMilliseconds", 5000)),
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
            dto.Id ?? Guid.NewGuid(), dto.Name, dto.Path, dto.DataType, dto.Source,
            dto.EngineeringUnit, dto.Description, dto.ReadOnly, metadata, access,
            dto.AddressSelector, dto.CommunicationBinding, dto.DataSourceId);
    }

    private static string Require(IReadOnlyDictionary<string, string> values, string key) =>
        Get(values, key) ?? throw new ArgumentException($"Shelly setting '{key}' is required.");

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static int GetInt(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        Get(values, key) is { } value && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : fallback;

    private static bool GetBool(IReadOnlyDictionary<string, string> values, string key, bool fallback) =>
        Get(values, key) is { } value && bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static CommunicationDriverRuntimePlanningResult Result(ICommunicationDriverRuntimePlan? plan, params EngineeringDriverIssue[] issues) =>
        new(plan, issues);

    private static EngineeringDriverIssue Error(string code, string message, string source, string? tag = null) =>
        new(code, message, source, tag, IsError: true);
}

public sealed class ShellyCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly Func<ShellyConnectionSettings, string, IShellyRpcClient>? _clientFactory;

    public ShellyCommunicationRuntimeFactory(Func<ShellyConnectionSettings, string, IShellyRpcClient>? clientFactory = null) =>
        _clientFactory = clientFactory;

    public string DriverType => ShellyRpcContract.DriverType;

    public ICommunicationDriver Create(ICommunicationDriverRuntimePlan plan, CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        if (plan is not ShellyCommunicationRuntimePlan shelly)
            throw new ArgumentException($"Shelly runtime factory requires {nameof(ShellyCommunicationRuntimePlan)}.", nameof(plan));

        if (shelly.PasswordSecretReference is not null && services.ProtectedMaterialResolver is null)
            throw new InvalidOperationException($"Shelly data source '{shelly.DataSourceKey}' references protected credentials but no resolver is available.");

        var source = $"elitescada-{Guid.NewGuid():N}"[..28];
        var client = _clientFactory?.Invoke(shelly.Connection, source)
            ?? new ShellyRpcClient(shelly.Connection, source);

        async ValueTask<ShellyResolvedCredential> Resolve(CancellationToken cancellationToken)
        {
            if (shelly.PasswordSecretReference is null)
                return new ShellyResolvedCredential(ReadOnlyMemory<byte>.Empty);

            var request = new CommunicationDriverProtectedMaterialRequest(
                services.ProjectKey,
                shelly.DataSourceKey,
                DriverType,
                ShellyRpcContract.PasswordPurpose,
                shelly.PasswordSecretReference);
            request.Validate();
            await using var lease = await services.ProtectedMaterialResolver!.ResolveAsync(request, cancellationToken).ConfigureAwait(false);
            return new ShellyResolvedCredential(lease.Material);
        }

        return new ShellyDriver(
            shelly.DataSourceKey,
            shelly.Name,
            shelly.Connection,
            services.Cache,
            shelly.Points,
            client,
            Resolve);
    }
}
