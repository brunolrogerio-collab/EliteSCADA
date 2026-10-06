using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Drivers.ESPHome;
using Scada.Drivers.ESPHome.Protocol;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Engineering;

public sealed record EspHomeCommunicationRuntimePlan(
    string DataSourceKey,
    string Name,
    EspHomeConnectionSettings Connection,
    string? EncryptionKeyReference,
    IReadOnlyCollection<EspHomePoint> Points) : ICommunicationDriverRuntimePlan
{
    public string DriverType => EspHomeNativeContract.DriverType;
    public IReadOnlyCollection<TagDefinition> Tags => Points.Select(x => x.Tag).ToArray();
}

public sealed class EspHomeCommunicationRuntimePlanner : ICommunicationDriverRuntimePlanner
{
    public string DriverType => EspHomeNativeContract.DriverType;

    public CommunicationDriverRuntimePlanningResult Plan(
        EngineeringPackage package,
        DataSourceEngineeringDto dataSource)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(dataSource);
        var issues = new List<EngineeringDriverIssue>();

        if (!string.Equals(dataSource.Driver, DriverType, StringComparison.OrdinalIgnoreCase))
            return Result(null, Error("ESPHOME_DRIVER_TYPE_MISMATCH", $"Data source '{dataSource.Key}' is not '{DriverType}'.", dataSource.Key));

        EspHomeConnectionSettings connection;
        string? encryptionKeyReference = null;
        dataSource.SecretReferences?.TryGetValue("encryptionKey", out encryptionKeyReference);
        try
        {
            connection = EspHomeEngineeringProvider.ParseConnection(dataSource.Settings ?? new Dictionary<string, string>());
            EspHomeSecurityPolicy.Validate(connection.EncryptionMode, encryptionKeyReference);
        }
        catch (Exception ex)
        {
            return Result(null, Error("ESPHOME_CONNECTION_INVALID", ex.Message, dataSource.Key));
        }

        var sourceTags = package.Tags.Where(tag =>
            dataSource.Id.HasValue && tag.DataSourceId == dataSource.Id ||
            (!tag.DataSourceId.HasValue && string.Equals(tag.Source, dataSource.Key, StringComparison.OrdinalIgnoreCase))).ToArray();

        var points = new List<EspHomePoint>();
        foreach (var dto in sourceTags)
        {
            var binding = dto.CommunicationBinding;
            if (binding is null)
            {
                issues.Add(Error("ESPHOME_BINDING_REQUIRED", $"ESPHome TAG '{dto.Path}' requires CommunicationBinding.", dataSource.Key, dto.Path));
                continue;
            }

            try { binding.Validate(); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                issues.Add(Error("ESPHOME_BINDING_INVALID", $"ESPHome TAG '{dto.Path}' has invalid binding: {ex.Message}", dataSource.Key, dto.Path));
                continue;
            }

            if (!string.Equals(binding.SchemaId, EspHomeNativeContract.SchemaId, StringComparison.Ordinal) ||
                binding.SchemaVersion != EspHomeNativeContract.SchemaVersion)
            {
                issues.Add(Error("ESPHOME_BINDING_SCHEMA", $"ESPHome TAG '{dto.Path}' uses unsupported binding schema.", dataSource.Key, dto.Path));
                continue;
            }

            if (!EspHomeEntityAddress.TryParse(binding.PortableAddress, out var address))
            {
                issues.Add(Error("ESPHOME_ADDRESS_INVALID", $"ESPHome TAG '{dto.Path}' has invalid entity address.", dataSource.Key, dto.Path));
                continue;
            }

            var writeKind = EspHomeWriteKind.None;
            if (!dto.ReadOnly)
            {
                if (!binding.EffectiveSettings.TryGetValue("writeKind", out var rawWriteKind) ||
                    !Enum.TryParse<EspHomeWriteKind>(rawWriteKind, true, out writeKind) ||
                    writeKind == EspHomeWriteKind.None)
                {
                    issues.Add(Error("ESPHOME_WRITE_MAPPING_REQUIRED", $"Writable ESPHome TAG '{dto.Path}' requires writeKind.", dataSource.Key, dto.Path));
                    continue;
                }

                if (!WriteKindMatchesAddress(writeKind, address))
                {
                    issues.Add(Error("ESPHOME_WRITE_MAPPING_MISMATCH", $"ESPHome TAG '{dto.Path}' writeKind '{writeKind}' does not match address '{address}'.", dataSource.Key, dto.Path));
                    continue;
                }
            }

            points.Add(new EspHomePoint(BuildCanonicalTag(dto), address, writeKind));
        }

        if (issues.Any(x => x.IsError))
            return new CommunicationDriverRuntimePlanningResult(null, issues);
        if (points.Count == 0)
            issues.Add(new EngineeringDriverIssue("ESPHOME_NO_TAGS", $"ESPHome data source '{dataSource.Key}' has no mapped TAGs.", dataSource.Key, IsError: false));

        return new CommunicationDriverRuntimePlanningResult(
            new EspHomeCommunicationRuntimePlan(dataSource.Key, dataSource.Name, connection, encryptionKeyReference, points),
            issues);
    }

    private static bool WriteKindMatchesAddress(EspHomeWriteKind kind, EspHomeEntityAddress address) => kind switch
    {
        EspHomeWriteKind.SwitchState => address.Kind == EspHomeEntityKind.Switch && address.Field == "state",
        EspHomeWriteKind.LightState => address.Kind == EspHomeEntityKind.Light && address.Field == "state",
        EspHomeWriteKind.LightBrightness => address.Kind == EspHomeEntityKind.Light && address.Field == "brightness",
        EspHomeWriteKind.CoverPosition => address.Kind == EspHomeEntityKind.Cover && address.Field == "position",
        EspHomeWriteKind.FanState => address.Kind == EspHomeEntityKind.Fan && address.Field == "state",
        EspHomeWriteKind.FanSpeedLevel => address.Kind == EspHomeEntityKind.Fan && address.Field == "speedLevel",
        EspHomeWriteKind.NumberState => address.Kind == EspHomeEntityKind.Number && address.Field == "state",
        EspHomeWriteKind.SelectState => address.Kind == EspHomeEntityKind.Select && address.Field == "state",
        _ => false
    };

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

    private static CommunicationDriverRuntimePlanningResult Result(ICommunicationDriverRuntimePlan? plan, params EngineeringDriverIssue[] issues) =>
        new(plan, issues);

    private static EngineeringDriverIssue Error(string code, string message, string source, string? tag = null) =>
        new(code, message, source, tag, IsError: true);
}

public sealed class EspHomeCommunicationRuntimeFactory : ICommunicationDriverRuntimeFactory
{
    private readonly Func<EspHomeConnectionSettings, EspHomeNoiseKeyProvider, IEspHomeNativeClient>? _clientFactory;

    public EspHomeCommunicationRuntimeFactory(
        Func<EspHomeConnectionSettings, EspHomeNoiseKeyProvider, IEspHomeNativeClient>? clientFactory = null) =>
        _clientFactory = clientFactory;

    public string DriverType => EspHomeNativeContract.DriverType;

    public ICommunicationDriver Create(ICommunicationDriverRuntimePlan plan, CommunicationDriverRuntimeServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();
        if (plan is not EspHomeCommunicationRuntimePlan espHome)
            throw new ArgumentException($"ESPHome runtime factory requires {nameof(EspHomeCommunicationRuntimePlan)}.", nameof(plan));

        if (espHome.Connection.EncryptionMode == EspHomeNativeEncryptionMode.Noise &&
            services.ProtectedMaterialResolver is null)
            throw new InvalidOperationException($"ESPHome data source '{espHome.DataSourceKey}' requires protected Noise material but no resolver is available.");

        EspHomeNoiseKeyProvider keyProvider = async cancellationToken =>
        {
            if (espHome.Connection.EncryptionMode == EspHomeNativeEncryptionMode.Plaintext)
                return null;

            return await EspHomeNoiseKeyResolver.ResolveAsync(
                services.ProtectedMaterialResolver!,
                services.ProjectKey,
                espHome.DataSourceKey,
                DriverType,
                espHome.EncryptionKeyReference!,
                cancellationToken).ConfigureAwait(false);
        };

        var client = _clientFactory?.Invoke(espHome.Connection, keyProvider)
            ?? new EspHomeNativeClient(espHome.Connection, keyProvider);

        return new EspHomeDriver(
            espHome.DataSourceKey,
            espHome.Name,
            espHome.Connection,
            services.Cache,
            services.Registry,
            espHome.Points,
            client);
    }
}
