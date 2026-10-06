using System.Globalization;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.ESPHome;

public static class EspHomeNativeContract
{
    public const string DriverType = "esphome.native";
    public const string SchemaId = "elitescada.driver.esphome.native";
    public const int SchemaVersion = 1;
    public const string EncryptionKeyPurpose = "esphome.noise-api-key";
}

public enum EspHomeEntityKind
{
    BinarySensor,
    Cover,
    Fan,
    Light,
    Sensor,
    Switch,
    TextSensor,
    Number,
    Select,
    Button,
    Climate,
    Lock,
    Unsupported
}

public enum EspHomeWriteKind
{
    None,
    SwitchState,
    LightState,
    LightBrightness,
    CoverPosition,
    FanState,
    FanSpeedLevel,
    NumberState,
    SelectState
}

public sealed record EspHomeConnectionSettings(
    string Host,
    int Port = 6053,
    EspHomeNativeEncryptionMode EncryptionMode = EspHomeNativeEncryptionMode.Noise,
    TimeSpan? RequestTimeout = null,
    TimeSpan? WriteReconcileTimeout = null)
{
    public TimeSpan EffectiveRequestTimeout => RequestTimeout ?? TimeSpan.FromSeconds(5);
    public TimeSpan EffectiveWriteReconcileTimeout => WriteReconcileTimeout ?? TimeSpan.FromSeconds(3);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new ArgumentException("ESPHome host is required.", nameof(Host));
        if (Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(Port));
        if (EffectiveRequestTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
        if (EffectiveWriteReconcileTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(WriteReconcileTimeout));
    }

    public string SanitizedEndpoint => $"tcp://{Host}:{Port}";
}

public readonly record struct EspHomeEntityAddress(
    EspHomeEntityKind Kind,
    uint DeviceId,
    uint Key,
    string Field)
{
    public override string ToString() =>
        $"{Kind.ToString().ToLowerInvariant()}:{DeviceId.ToString(CultureInfo.InvariantCulture)}:{Key:X8}.{Field}";

    public static bool TryParse(string? value, out EspHomeEntityAddress address)
    {
        address = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var dot = value.LastIndexOf('.');
        if (dot <= 0 || dot == value.Length - 1) return false;
        var head = value[..dot].Split(':', StringSplitOptions.None);
        if (head.Length != 3 ||
            !Enum.TryParse<EspHomeEntityKind>(head[0], true, out var kind) ||
            !uint.TryParse(head[1], NumberStyles.None, CultureInfo.InvariantCulture, out var deviceId) ||
            !uint.TryParse(head[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var key))
            return false;
        var field = value[(dot + 1)..];
        if (field.IndexOfAny(['\r', '\n', '\0']) >= 0) return false;
        address = new EspHomeEntityAddress(kind, deviceId, key, field);
        return true;
    }
}

public sealed record EspHomeEntityDescriptor(
    EspHomeEntityKind Kind,
    uint Key,
    uint DeviceId,
    string ObjectId,
    string Name,
    int EntityCategory,
    string? DeviceClass = null,
    string? Unit = null,
    bool SupportsPosition = false,
    bool SupportsStop = false,
    bool SupportsBrightness = false,
    bool SupportsRgb = false,
    bool SupportsFanSpeed = false,
    int SupportedFanSpeedCount = 0,
    double? MinValue = null,
    double? MaxValue = null,
    double? Step = null,
    IReadOnlyCollection<string>? Options = null)
{
    public bool IsProcessEntity => EntityCategory == 0;
}

public sealed record EspHomeNativeInventory(
    EspHomeApiVersion NegotiatedVersion,
    EspHomeDeviceIdentity Device,
    IReadOnlyCollection<EspHomeEntityDescriptor> Entities,
    int UnsupportedEntityMessageCount = 0);

public sealed record EspHomeStateUpdate(
    EspHomeEntityAddress Address,
    object? Value,
    bool MissingState = false,
    DateTimeOffset? SourceTimestamp = null);

public sealed record EspHomeCommand(
    EspHomeEntityAddress Address,
    EspHomeWriteKind Kind,
    object? Value);

public sealed record EspHomePoint(
    TagDefinition Tag,
    EspHomeEntityAddress Address,
    EspHomeWriteKind WriteKind = EspHomeWriteKind.None)
{
    public bool CanWrite => !Tag.ReadOnly && WriteKind != EspHomeWriteKind.None;
}

public delegate ValueTask<EspHomeResolvedNoiseKey?> EspHomeNoiseKeyProvider(CancellationToken cancellationToken);

public interface IEspHomeNativeClient : IAsyncDisposable
{
    bool Connected { get; }
    ValueTask<EspHomeNativeInventory> ConnectAsync(CancellationToken cancellationToken = default);
    ValueTask SubscribeStatesAsync(CancellationToken cancellationToken = default);
    ValueTask<EspHomeStateUpdate> ReceiveStateAsync(CancellationToken cancellationToken = default);
    ValueTask SendCommandAsync(EspHomeCommand command, CancellationToken cancellationToken = default);
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);
}

public sealed class EspHomeDriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public CommunicationDriverTypeDescriptor Descriptor { get; } = new(
        EspHomeNativeContract.DriverType,
        "ESPHome Native API",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Subscribe,
        EngineeringCapabilities: DriverEngineeringCapabilities.ConnectionTest | DriverEngineeringCapabilities.Discover,
        AcquisitionModes: [DriverAcquisitionMode.Subscription],
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            EspHomeNativeContract.SchemaId,
            EspHomeNativeContract.SchemaVersion,
            DataSourceFields:
            [
                new("host", DriverConfigurationValueKind.Host, Required: true, DisplayName: "Device host"),
                new("port", DriverConfigurationValueKind.Port, DefaultValue: "6053", Minimum: 1, Maximum: 65535),
                new("encryptionMode", DriverConfigurationValueKind.Enum, DefaultValue: "noise",
                    AllowedValues: ["noise", "plaintext"],
                    Description: "Noise is preferred. Plaintext is explicit trusted-network compatibility mode."),
                new("encryptionKey", DriverConfigurationValueKind.SecretReference,
                    DisplayName: "Noise API encryption-key reference"),
                new("requestTimeoutMilliseconds", DriverConfigurationValueKind.Integer,
                    DefaultValue: "5000", Minimum: 100, Maximum: 300000, Advanced: true),
                new("writeReconcileTimeoutMilliseconds", DriverConfigurationValueKind.Integer,
                    DefaultValue: "3000", Minimum: 100, Maximum: 300000, Advanced: true)
            ],
            TagBindingFields:
            [
                new("address", DriverConfigurationValueKind.String, Required: true,
                    DisplayName: "Entity address",
                    Description: "<entity-kind>:<device-id>:<fixed32-key-hex>.<field>"),
                new("writeKind", DriverConfigurationValueKind.Enum, Advanced: true,
                    AllowedValues: Enum.GetNames<EspHomeWriteKind>().Where(x => x != nameof(EspHomeWriteKind.None)).Select(x => x.ToLowerInvariant()).ToArray())
            ]),
        Description: "Direct local/LAN ESPHome Native API driver. No Home Assistant, ESPHome Dashboard or Python sidecar required.",
        IntegrationDomains: [IntegrationDomain.Building, IntegrationDomain.Residential, IntegrationDomain.IoT],
        ConnectionModel: DriverConnectionModel.DirectNetwork,
        ExternalDependencies: [new DriverExternalDependencyDescriptor(DriverExternalDependencyKind.BuiltIn)]);
}
