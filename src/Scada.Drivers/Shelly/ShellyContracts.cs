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

public enum ShellyAuthTransport
{
    Http,
    WebSocket
}

public sealed record ShellyDigestChallenge(
    string Realm,
    string NonceText,
    long? LegacyNumericNonce,
    string Algorithm,
    bool Stale,
    bool ReusableNonce)
{
    public static ShellyDigestChallenge Parse(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
            throw new FormatException("Shelly auth challenge data must be an object.");

        var realm = GetString(data, "realm")
            ?? throw new FormatException("Shelly auth challenge is missing realm.");
        var algorithm = GetString(data, "algorithm") ?? "SHA-256";
        if (!algorithm.Equals("SHA-256", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Shelly digest algorithm '{algorithm}' is not supported.");

        if (!data.TryGetProperty("nonce", out var nonce))
            throw new FormatException("Shelly auth challenge is missing nonce.");

        string nonceText;
        long? legacyNumericNonce = null;
        bool reusable;
        if (nonce.ValueKind == JsonValueKind.String)
        {
            nonceText = nonce.GetString()
                ?? throw new FormatException("Shelly auth challenge nonce is empty.");
            reusable = true;
        }
        else if (nonce.ValueKind == JsonValueKind.Number && nonce.TryGetInt64(out var numeric))
        {
            nonceText = numeric.ToString(CultureInfo.InvariantCulture);
            legacyNumericNonce = numeric;
            reusable = false;
        }
        else
        {
            throw new FormatException("Shelly auth challenge nonce must be a string or integer.");
        }

        var stale = data.TryGetProperty("stale", out var staleElement) &&
                    staleElement.ValueKind is JsonValueKind.True;

        return new ShellyDigestChallenge(realm, nonceText, legacyNumericNonce, algorithm, stale, reusable);
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public sealed class ShellyDigestSession
{
    private readonly string _username;
    private readonly byte[] _password;
    private ShellyDigestChallenge? _challenge;
    private uint _nonceCount;

    public ShellyDigestSession(string username, ReadOnlyMemory<byte> password)
    {
        _username = username;
        _password = password.ToArray();
    }

    public void AcceptChallenge(ShellyDigestChallenge challenge)
    {
        _challenge = challenge;
        _nonceCount = 0;
    }

    public object BuildAuth(ShellyAuthTransport transport)
    {
        var challenge = _challenge
            ?? throw new InvalidOperationException("No Shelly digest challenge is active.");
        var nc = challenge.ReusableNonce ? checked(++_nonceCount) : 1u;
        var ncText = nc.ToString("x8", CultureInfo.InvariantCulture);
        var cnonce = RandomNumberGenerator.GetInt32(1, int.MaxValue);
        var ha1 = HashA1(_username, challenge.Realm, _password);
        var ha2 = transport == ShellyAuthTransport.Http
            ? Hash("POST:/rpc")
            : Hash("dummy_method:dummy_uri");
        var response = Hash($"{ha1}:{challenge.NonceText}:{ncText}:{cnonce}:auth:{ha2}");

        return new Dictionary<string, object?>
        {
            ["realm"] = challenge.Realm,
            ["username"] = _username,
            ["nonce"] = challenge.LegacyNumericNonce is { } legacy ? legacy : challenge.NonceText,
            ["cnonce"] = cnonce,
            ["nc"] = ncText,
            ["response"] = response,
            ["algorithm"] = "SHA-256"
        };
    }

    public void Clear()
    {
        CryptographicOperations.ZeroMemory(_password);
        _challenge = null;
        _nonceCount = 0;
    }

    private static string HashA1(string username, string realm, ReadOnlySpan<byte> password)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(username));
        hash.AppendData(":"u8);
        hash.AppendData(Encoding.UTF8.GetBytes(realm));
        hash.AppendData(":"u8);
        hash.AppendData(password);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
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
        using var document = JsonDocument.Parse(utf8.ToArray());
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
        using var document = JsonDocument.Parse(utf8.ToArray());
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
        {
            if (property.NameEquals("ts")) continue;
            state[property.Name] = MergeElement(state.TryGetValue(property.Name, out var old) ? old : default, property.Value);
        }
    }

    public static bool TryReadPoint(IReadOnlyDictionary<string, JsonElement> state, ShellyPoint point, out object? value)
    {
        value = null;
        if (!state.TryGetValue(point.ComponentKey, out var component) || component.ValueKind != JsonValueKind.Object)
            return false;
        var field = component;
        foreach (var segment in point.Field.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (field.ValueKind != JsonValueKind.Object || !field.TryGetProperty(segment, out var nested))
                return false;
            field = nested;
        }

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
