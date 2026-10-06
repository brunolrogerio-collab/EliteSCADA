using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.Shelly;

public static class ShellyRpcContract
{
    public const string DriverType = "shelly.rpc";
    public const string SchemaId = "elitescada.driver.shelly.rpc";
    public const int SchemaVersion = 1;
    public const string PasswordPurpose = "shelly.password";
}

public sealed record ShellyConnectionSettings(
    string Host,
    int Port = 80,
    bool UseTls = false,
    string Username = "admin",
    TimeSpan? RequestTimeout = null,
    TimeSpan? ReconnectMinimumDelay = null,
    TimeSpan? ReconnectMaximumDelay = null)
{
    public TimeSpan EffectiveRequestTimeout => RequestTimeout ?? TimeSpan.FromSeconds(5);
    public TimeSpan EffectiveReconnectMinimumDelay => ReconnectMinimumDelay ?? TimeSpan.FromSeconds(1);
    public TimeSpan EffectiveReconnectMaximumDelay => ReconnectMaximumDelay ?? TimeSpan.FromSeconds(30);

    public Uri HttpRpcUri => new UriBuilder(UseTls ? "https" : "http", Host, Port, "rpc").Uri;
    public Uri WebSocketRpcUri => new UriBuilder(UseTls ? "wss" : "ws", Host, Port, "rpc").Uri;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host)) throw new ArgumentException("Shelly host is required.", nameof(Host));
        if (Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
        if (string.IsNullOrWhiteSpace(Username)) throw new ArgumentException("Shelly username is required.", nameof(Username));
        if (EffectiveRequestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
        if (EffectiveReconnectMinimumDelay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ReconnectMinimumDelay));
        if (EffectiveReconnectMaximumDelay < EffectiveReconnectMinimumDelay) throw new ArgumentOutOfRangeException(nameof(ReconnectMaximumDelay));
    }
}

public sealed record ShellyPoint(
    TagDefinition Tag,
    string ComponentKey,
    string Field,
    string? WriteMethod = null,
    string? WriteParameter = null)
{
    public bool CanWrite => !Tag.ReadOnly && !string.IsNullOrWhiteSpace(WriteMethod) && !string.IsNullOrWhiteSpace(WriteParameter);
}

public sealed record ShellyDeviceInfo(
    string StableDeviceIdentity,
    string Model,
    string? Firmware,
    string? Mac,
    string? Name);

public sealed record ShellyRpcError(int Code, string Message);

public sealed record ShellyRpcEnvelope(long Id, JsonElement? Result, ShellyRpcError? Error);

public sealed record ShellyDigestChallenge(
    string Realm,
    long Nonce,
    string Algorithm,
    int? Stale,
    bool ReusableNonce)
{
    public static ShellyDigestChallenge Parse(JsonElement errorData)
    {
        if (errorData.ValueKind != JsonValueKind.Object)
            throw new FormatException("Shelly auth challenge data must be an object.");

        var realm = GetString(errorData, "realm") ?? throw new FormatException("Shelly auth challenge is missing realm.");
        var algorithm = GetString(errorData, "algorithm") ?? "SHA-256";
        if (!algorithm.Equals("SHA-256", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Shelly digest algorithm '{algorithm}' is not supported.");
        if (!TryGetInt64(errorData, "nonce", out var nonce))
            throw new FormatException("Shelly auth challenge is missing nonce.");
        int? stale = TryGetInt64(errorData, "stale", out var staleValue) ? checked((int)staleValue) : null;

        // Firmware 2.0+ reusable nonce challenges are distinguishable by stale being present
        // and require a monotonically increasing hexadecimal nc. Legacy firmware uses a
        // one-shot numeric nonce and tolerates nc=1.
        return new ShellyDigestChallenge(realm, nonce, algorithm, stale, stale.HasValue);
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool TryGetInt64(JsonElement obj, string name, out long value)
    {
        value = 0;
        if (!obj.TryGetProperty(name, out var element)) return false;
        if (element.ValueKind == JsonValueKind.Number) return element.TryGetInt64(out value);
        return element.ValueKind == JsonValueKind.String &&
               long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}

public sealed class ShellyDigestSession
{
    private readonly string _username;
    private readonly ReadOnlyMemory<byte> _password;
    private ShellyDigestChallenge? _challenge;
    private uint _nonceCount;

    public ShellyDigestSession(string username, ReadOnlyMemory<byte> password)
    {
        _username = username;
        _password = password;
    }

    public void AcceptChallenge(ShellyDigestChallenge challenge)
    {
        _challenge = challenge;
        _nonceCount = 0;
    }

    public object BuildAuth(long rpcId)
    {
        var challenge = _challenge ?? throw new InvalidOperationException("No Shelly digest challenge is active.");
        var nc = checked(++_nonceCount);
        var cnonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var password = Encoding.UTF8.GetString(_password.Span);
        var ha1 = Hash($"{_username}:{challenge.Realm}:{password}");
        var ha2 = Hash($"dummy_method:dummy_uri");
        var response = Hash($"{ha1}:{challenge.Nonce}:{nc}:{cnonce}:auth:{ha2}");

        return new
        {
            realm = challenge.Realm,
            username = _username,
            nonce = challenge.Nonce,
            cnonce,
            response,
            algorithm = "SHA-256",
            nc = challenge.ReusableNonce ? nc.ToString("x8", CultureInfo.InvariantCulture) : "1"
        };
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

public static class ShellyRpcJson
{
    public static byte[] BuildRequest(long id, string source, string method, object? parameters = null, object? auth = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["src"] = source,
            ["method"] = method
        };
        if (parameters is not null) payload["params"] = parameters;
        if (auth is not null) payload["auth"] = auth;
        return JsonSerializer.SerializeToUtf8Bytes(payload);
    }

    public static ShellyRpcEnvelope ParseResponse(ReadOnlySpan<byte> utf8)
    {
        using var document = JsonDocument.Parse(utf8);
        var root = document.RootElement;
        if (!root.TryGetProperty("id", out var idElement) || !idElement.TryGetInt64(out var id))
            throw new FormatException("Shelly RPC response is missing a numeric id.");

        JsonElement? result = root.TryGetProperty("result", out var resultElement) ? resultElement.Clone() : null;
        ShellyRpcError? error = null;
        if (root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind == JsonValueKind.Object)
        {
            var code = errorElement.TryGetProperty("code", out var codeElement) && codeElement.TryGetInt32(out var c) ? c : -1;
            var message = errorElement.TryGetProperty("message", out var messageElement) ? messageElement.GetString() ?? "Shelly RPC error." : "Shelly RPC error.";
            error = new ShellyRpcError(code, message);
        }
        return new ShellyRpcEnvelope(id, result, error);
    }

    public static bool TryParseNotification(ReadOnlySpan<byte> utf8, out string? method, out JsonElement parameters)
    {
        using var document = JsonDocument.Parse(utf8);
        var root = document.RootElement;
        method = root.TryGetProperty("method", out var methodElement) ? methodElement.GetString() : null;
        if ((method == "NotifyStatus" || method == "NotifyEvent") &&
            root.TryGetProperty("params", out var paramsElement))
        {
            parameters = paramsElement.Clone();
            return true;
        }
        parameters = default;
        return false;
    }
}

public static class ShellyStateMapper
{
    public static void MergeStatus(Dictionary<string, JsonElement> state, JsonElement status)
    {
        if (status.ValueKind != JsonValueKind.Object) return;
        foreach (var property in status.EnumerateObject())
            state[property.Name] = MergeElement(state.TryGetValue(property.Name, out var old) ? old : default, property.Value);
    }

    public static bool TryReadPoint(IReadOnlyDictionary<string, JsonElement> state, ShellyPoint point, out object? value)
    {
        value = null;
        if (!state.TryGetValue(point.ComponentKey, out var component) || component.ValueKind != JsonValueKind.Object)
            return false;
        if (!component.TryGetProperty(point.Field, out var field))
            return false;

        value = field.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when point.Tag.DataType is TagDataType.Int16 or TagDataType.Int32 or TagDataType.Int64 =>
                field.TryGetInt64(out var integer) ? integer : field.GetDouble(),
            JsonValueKind.Number => field.GetDouble(),
            JsonValueKind.String => field.GetString(),
            JsonValueKind.Null => null,
            _ => field.GetRawText()
        };
        return true;
    }

    public static ShellyDeviceInfo ParseDeviceInfo(JsonElement result)
    {
        var id = GetString(result, "id") ?? throw new FormatException("Shelly.GetDeviceInfo result is missing id.");
        var model = GetString(result, "model") ?? "unknown";
        var fw = GetString(result, "ver");
        var mac = GetString(result, "mac");
        var name = GetString(result, "name");
        return new ShellyDeviceInfo(id, model, fw, mac, name);
    }

    private static JsonElement MergeElement(JsonElement previous, JsonElement incoming)
    {
        if (incoming.ValueKind != JsonValueKind.Object || previous.ValueKind != JsonValueKind.Object)
            return incoming.Clone();

        var merged = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in previous.EnumerateObject()) merged[property.Name] = property.Value.Clone();
        foreach (var property in incoming.EnumerateObject())
            merged[property.Name] = MergeElement(merged.TryGetValue(property.Name, out var old) ? old : default, property.Value);

        return JsonSerializer.SerializeToElement(merged);
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

public sealed class ShellyDriverDescriptorProvider : ICommunicationDriverDescriptorProvider
{
    public CommunicationDriverTypeDescriptor Descriptor { get; } = new(
        ShellyRpcContract.DriverType,
        "Shelly Gen2+ RPC",
        DriverContractVersion: 1,
        RuntimeCapabilities: DriverCapabilities.Read | DriverCapabilities.Write | DriverCapabilities.Subscribe | DriverCapabilities.Diagnostics,
        EngineeringCapabilities: DriverEngineeringCapabilities.ConnectionTest | DriverEngineeringCapabilities.Discover,
        AcquisitionModes: [DriverAcquisitionMode.Hybrid],
        ConfigurationSchema: new DriverConfigurationSchemaDescriptor(
            ShellyRpcContract.SchemaId,
            ShellyRpcContract.SchemaVersion,
            DataSourceFields:
            [
                new("host", DriverConfigurationValueKind.Host, Required: true, DisplayName: "Device host"),
                new("port", DriverConfigurationValueKind.Port, DefaultValue: "80", Minimum: 1, Maximum: 65535),
                new("tls", DriverConfigurationValueKind.Boolean, DefaultValue: "false"),
                new("username", DriverConfigurationValueKind.String, DefaultValue: "admin", Advanced: true),
                new("password", DriverConfigurationValueKind.SecretReference, DisplayName: "Password secret reference"),
                new("requestTimeoutMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "5000", Minimum: 100, Maximum: 300000, Advanced: true),
                new("reconnectMinimumMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "1000", Minimum: 100, Maximum: 300000, Advanced: true),
                new("reconnectMaximumMilliseconds", DriverConfigurationValueKind.Integer, DefaultValue: "30000", Minimum: 100, Maximum: 3600000, Advanced: true)
            ],
            TagBindingFields:
            [
                new("address", DriverConfigurationValueKind.String, Required: true, DisplayName: "Component field", Description: "Portable form: <component>.<field>, for example switch:0.output"),
                new("writeMethod", DriverConfigurationValueKind.String, Advanced: true),
                new("writeParameter", DriverConfigurationValueKind.String, Advanced: true)
            ]),
        Description: "Direct local/LAN Shelly Gen2+ JSON-RPC driver. No Shelly Cloud dependency.",
        IntegrationDomains: [IntegrationDomain.Building, IntegrationDomain.Residential, IntegrationDomain.IoT],
        ConnectionModel: DriverConnectionModel.DirectNetwork,
        ExternalDependencies: [new DriverExternalDependencyDescriptor(DriverExternalDependencyKind.BuiltIn)]);
}
