using System.Collections.Concurrent;
using System.Buffers;
using System.Text.Json;
using Scada.Drivers.Abstractions;

namespace Scada.Drivers.HomeAssistant;

public static class HomeAssistantContract
{
    public const string DriverType = "homeassistant.websocket";
    public const string SchemaId = "elitescada.driver.homeassistant.websocket";
    public const int SchemaVersion = 1;
    public const string AccessTokenPurpose = "homeassistant.access-token";
    public const int MaximumFrameBytes = 1_048_576;
}

public sealed record HomeAssistantConnectionSettings(
    string Host,
    int Port = 8123,
    bool UseTls = false,
    string BasePath = "",
    TimeSpan? ConnectTimeout = null,
    TimeSpan? RequestTimeout = null,
    TimeSpan? ReconnectMinimumDelay = null,
    TimeSpan? ReconnectMaximumDelay = null)
{
    public TimeSpan EffectiveConnectTimeout => ConnectTimeout ?? TimeSpan.FromSeconds(10);
    public TimeSpan EffectiveRequestTimeout => RequestTimeout ?? TimeSpan.FromSeconds(10);
    public TimeSpan EffectiveReconnectMinimumDelay => ReconnectMinimumDelay ?? TimeSpan.FromSeconds(1);
    public TimeSpan EffectiveReconnectMaximumDelay => ReconnectMaximumDelay ?? TimeSpan.FromSeconds(30);

    public Uri WebSocketUri
    {
        get
        {
            var path = string.IsNullOrWhiteSpace(BasePath)
                ? "/api/websocket"
                : $"/{BasePath.Trim('/')}/api/websocket";
            return new UriBuilder(UseTls ? "wss" : "ws", Host, Port, path).Uri;
        }
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host)) throw new ArgumentException("Home Assistant host is required.", nameof(Host));
        if (Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
        if (EffectiveConnectTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ConnectTimeout));
        if (EffectiveRequestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
        if (EffectiveReconnectMinimumDelay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ReconnectMinimumDelay));
        if (EffectiveReconnectMaximumDelay < EffectiveReconnectMinimumDelay) throw new ArgumentOutOfRangeException(nameof(ReconnectMaximumDelay));
    }
}

public enum HomeAssistantHandshakeKind
{
    AuthRequired,
    AuthOk,
    AuthInvalid
}

public sealed record HomeAssistantHandshakeMessage(
    HomeAssistantHandshakeKind Kind,
    string? Version,
    string? Message);

public sealed record HomeAssistantCommandResult(
    int Id,
    bool Success,
    JsonElement? Result,
    JsonElement? Error);

public sealed record HomeAssistantState(
    string EntityId,
    string State,
    JsonElement Attributes,
    DateTimeOffset? LastChanged,
    DateTimeOffset? LastUpdated,
    JsonElement Context);

public sealed record HomeAssistantStateChangedEvent(
    string EntityId,
    HomeAssistantState? OldState,
    HomeAssistantState? NewState);

public static class HomeAssistantProtocol
{
    public static byte[] BuildAuth(ReadOnlySpan<byte> accessToken)
    {
        if (accessToken.IsEmpty)
            throw new ArgumentException("Home Assistant access token is required.", nameof(accessToken));

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "auth");
            writer.WriteString("access_token", accessToken);
            writer.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }

    public static byte[] BuildCommand(int id, string type, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("Home Assistant command type is required.", nameof(type));

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["type"] = type
        };
        if (arguments is not null)
            foreach (var pair in arguments)
                payload[pair.Key] = pair.Value;
        return JsonSerializer.SerializeToUtf8Bytes(payload);
    }

    public static HomeAssistantHandshakeMessage ParseHandshake(ReadOnlySpan<byte> utf8)
    {
        using var document = JsonDocument.Parse(utf8.ToArray());
        var root = document.RootElement;
        var type = RequireString(root, "type");
        return type switch
        {
            "auth_required" => new(HomeAssistantHandshakeKind.AuthRequired, GetString(root, "ha_version"), null),
            "auth_ok" => new(HomeAssistantHandshakeKind.AuthOk, GetString(root, "ha_version"), null),
            "auth_invalid" => new(HomeAssistantHandshakeKind.AuthInvalid, GetString(root, "ha_version"), GetString(root, "message")),
            _ => throw new FormatException("Home Assistant handshake message type is unsupported.")
        };
    }

    public static HomeAssistantCommandResult ParseResult(ReadOnlySpan<byte> utf8)
    {
        using var document = JsonDocument.Parse(utf8.ToArray());
        var root = document.RootElement;
        if (!string.Equals(RequireString(root, "type"), "result", StringComparison.Ordinal))
            throw new FormatException("Home Assistant command response is not a result message.");
        if (!root.TryGetProperty("id", out var idElement) || !idElement.TryGetInt32(out var id) || id <= 0)
            throw new FormatException("Home Assistant result is missing a positive integer request id.");
        if (!root.TryGetProperty("success", out var successElement) ||
            successElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new FormatException("Home Assistant result is missing success.");
        JsonElement? result = root.TryGetProperty("result", out var r) ? r.Clone() : null;
        JsonElement? error = root.TryGetProperty("error", out var e) ? e.Clone() : null;
        return new HomeAssistantCommandResult(id, successElement.GetBoolean(), result, error);
    }

    public static IReadOnlyList<HomeAssistantState> ParseStates(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Array)
            throw new FormatException("Home Assistant get_states result must be an array.");
        var states = new List<HomeAssistantState>();
        foreach (var item in result.EnumerateArray())
            states.Add(ParseState(item));
        return states;
    }

    public static bool TryParseStateChangedEvent(ReadOnlySpan<byte> utf8, out HomeAssistantStateChangedEvent? stateChanged)
    {
        stateChanged = null;
        using var document = JsonDocument.Parse(utf8.ToArray());
        var root = document.RootElement;
        if (!string.Equals(GetString(root, "type"), "event", StringComparison.Ordinal))
            return false;
        if (!root.TryGetProperty("event", out var evt) || evt.ValueKind != JsonValueKind.Object)
            return false;
        if (!string.Equals(GetString(evt, "event_type"), "state_changed", StringComparison.Ordinal))
            return false;
        if (!evt.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            throw new FormatException("Home Assistant state_changed event is missing data.");
        var entityId = RequireString(data, "entity_id");
        HomeAssistantState? oldState = null;
        HomeAssistantState? newState = null;
        if (data.TryGetProperty("old_state", out var oldElement) && oldElement.ValueKind == JsonValueKind.Object)
            oldState = ParseState(oldElement);
        if (data.TryGetProperty("new_state", out var newElement) && newElement.ValueKind == JsonValueKind.Object)
            newState = ParseState(newElement);
        stateChanged = new HomeAssistantStateChangedEvent(entityId, oldState, newState);
        return true;
    }

    public static string SanitizeFailure(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "Home Assistant request failed.";
        var clean = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length <= 256 ? clean : clean[..256];
    }

    private static HomeAssistantState ParseState(JsonElement item)
    {
        var entityId = RequireString(item, "entity_id");
        var state = RequireString(item, "state");
        var attributes = item.TryGetProperty("attributes", out var attrs) ? attrs.Clone() : JsonSerializer.SerializeToElement(new { });
        var context = item.TryGetProperty("context", out var ctx) ? ctx.Clone() : JsonSerializer.SerializeToElement(new { });
        return new HomeAssistantState(
            entityId,
            state,
            attributes,
            ParseTimestamp(GetString(item, "last_changed")),
            ParseTimestamp(GetString(item, "last_updated")),
            context);
    }

    private static DateTimeOffset? ParseTimestamp(string? value) =>
        DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var parsed) ? parsed : null;

    private static string RequireString(JsonElement obj, string name) =>
        GetString(obj, name) ?? throw new FormatException($"Home Assistant message is missing '{name}'.");

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public sealed class HomeAssistantRequestCorrelator
{
    private readonly ConcurrentDictionary<int, TaskCompletionSource<HomeAssistantCommandResult>> _pending = new();
    private int _nextId;

    public (int Id, Task<HomeAssistantCommandResult> Completion) Create()
    {
        var id = Interlocked.Increment(ref _nextId);
        if (id <= 0) throw new InvalidOperationException("Home Assistant request id space was exhausted.");
        var source = new TaskCompletionSource<HomeAssistantCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, source))
            throw new InvalidOperationException("Home Assistant request id collision.");
        return (id, source.Task);
    }

    public bool TryComplete(HomeAssistantCommandResult result)
    {
        if (!_pending.TryRemove(result.Id, out var source))
            return false;
        source.TrySetResult(result);
        return true;
    }

    public bool TryFail(int id, Exception exception)
    {
        if (!_pending.TryRemove(id, out var source))
            return false;
        source.TrySetException(exception);
        return true;
    }

    public bool TryCancel(int id, CancellationToken cancellationToken)
    {
        if (!_pending.TryRemove(id, out var source))
            return false;
        source.TrySetCanceled(cancellationToken);
        return true;
    }

    public int PendingCount => _pending.Count;
}

public static class HomeAssistantProtectedMaterial
{
    public static CommunicationDriverProtectedMaterialRequest CreateAccessTokenRequest(
        string projectKey,
        string dataSourceKey,
        string secretReference)
    {
        var request = new CommunicationDriverProtectedMaterialRequest(
            projectKey,
            dataSourceKey,
            HomeAssistantContract.DriverType,
            HomeAssistantContract.AccessTokenPurpose,
            secretReference);
        request.Validate();
        return request;
    }
}
