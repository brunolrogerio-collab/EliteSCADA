using System.Text.Json.Serialization;

namespace Scada.Core.Tags;

/// <summary>
/// Driver-independent physical byte/word transformation. Drivers apply this at
/// the raw representation boundary before canonical typed decode and apply the
/// inverse/symmetric transform after canonical typed encode on writes. Bit
/// selection remains a separate TagValueSelector step after transformed decode.
/// </summary>
public sealed record TagPhysicalValueTransform(
    int ContractVersion = 1,
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
/// Canonical library-independent envelope for one external communication TAG
/// binding. SchemaId/SchemaVersion identify the Driver-owned public binding
/// schema, PortableAddress is the stable portable protocol address/identity and
/// Settings contains only non-secret public values. Protected material belongs
/// to Data Source secretReferences and is resolved only through host-owned seams.
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
        public static readonly IReadOnlyDictionary<string, string> Instance = new EmptySettings();
        private EmptySettings() : base(StringComparer.Ordinal) { }
    }
}
