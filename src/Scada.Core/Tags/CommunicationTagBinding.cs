using System.Text.Json.Serialization;

namespace Scada.Core.Tags;

/// <summary>
/// Driver-independent physical byte/word transformation applied at the raw
/// representation boundary before canonical typed decode and, symmetrically,
/// after canonical typed encode on writes.
/// </summary>
public sealed record TagPhysicalValueTransform(
    int ContractVersion = CurrentContractVersion,
    bool ByteSwap = false,
    bool WordSwap = false)
{
    public const int CurrentContractVersion = 1;

    [JsonIgnore]
    public bool IsIdentity => !ByteSwap && !WordSwap;

    public void Validate()
    {
        if (ContractVersion != CurrentContractVersion)
            throw new NotSupportedException(
                $"Physical value transform contract version '{ContractVersion}' is not supported.");
    }
}

/// <summary>
/// Canonical, versioned envelope for one physical Data Source -> TAG binding.
/// SchemaId/SchemaVersion identify the public driver-owned binding schema;
/// PortableAddress is the library-independent portable identity/address;
/// Settings contains only public non-secret schema values. Driver secrets remain
/// in DataSource SecretReferences and are resolved by host-owned security APIs.
///
/// AddressSelector remains on TagDefinition as the single canonical source-value
/// selector model and is therefore deliberately not duplicated here. A bit
/// selector is applied only after ValueTransform has produced the canonical
/// integer representation.
/// </summary>
public sealed record CommunicationTagBinding(
    int ContractVersion,
    string SchemaId,
    int SchemaVersion,
    string PortableAddress,
    IReadOnlyDictionary<string, string>? Settings = null,
    TagPhysicalValueTransform? ValueTransform = null)
{
    public const int CurrentContractVersion = 1;

    [JsonIgnore]
    public IReadOnlyDictionary<string, string> EffectiveSettings =>
        Settings ?? EmptySettings.Instance;

    public void Validate()
    {
        if (ContractVersion != CurrentContractVersion)
            throw new NotSupportedException(
                $"Communication TAG binding contract version '{ContractVersion}' is not supported.");
        if (SchemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(SchemaVersion), "Driver binding schema version must be positive.");

        ValidateToken(SchemaId, nameof(SchemaId), "schema ID");
        ValidateToken(PortableAddress, nameof(PortableAddress), "portable address");

        foreach (var setting in EffectiveSettings)
        {
            ValidateToken(setting.Key, nameof(Settings), "setting key");
            if (setting.Value is null)
                throw new ArgumentException("Driver binding setting values cannot be null.", nameof(Settings));
            ValidateControlCharacters(setting.Value, nameof(Settings), "setting value");
        }

        ValueTransform?.Validate();
    }

    private static void ValidateToken(string value, string parameterName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Communication TAG binding {displayName} is required.", parameterName);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException(
                $"Communication TAG binding {displayName} must not contain leading or trailing whitespace.",
                parameterName);
        ValidateControlCharacters(value, parameterName, displayName);
    }

    private static void ValidateControlCharacters(string value, string parameterName, string displayName)
    {
        if (value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0 || value.IndexOf('\0') >= 0)
            throw new ArgumentException(
                $"Communication TAG binding {displayName} contains invalid control characters.",
                parameterName);
    }

    private sealed class EmptySettings : Dictionary<string, string>
    {
        public static readonly IReadOnlyDictionary<string, string> Instance =
            new EmptySettings();

        private EmptySettings() : base(StringComparer.Ordinal) { }
    }
}
