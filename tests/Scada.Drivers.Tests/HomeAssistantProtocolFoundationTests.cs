using System.Text;
using System.Text.Json;
using Scada.Drivers.HomeAssistant;

namespace Scada.Drivers.Tests;

public sealed class HomeAssistantProtocolFoundationTests
{
    [Fact]
    public void Handshake_ParsesRequiredOkAndInvalid()
    {
        var required = HomeAssistantProtocol.ParseHandshake("""{"type":"auth_required","ha_version":"2026.10.0"}"""u8);
        var ok = HomeAssistantProtocol.ParseHandshake("""{"type":"auth_ok","ha_version":"2026.10.0"}"""u8);
        var invalid = HomeAssistantProtocol.ParseHandshake("""{"type":"auth_invalid","message":"invalid access token"}"""u8);

        Assert.Equal(HomeAssistantHandshakeKind.AuthRequired, required.Kind);
        Assert.Equal("2026.10.0", required.Version);
        Assert.Equal(HomeAssistantHandshakeKind.AuthOk, ok.Kind);
        Assert.Equal(HomeAssistantHandshakeKind.AuthInvalid, invalid.Kind);
        Assert.Equal("invalid access token", invalid.Message);
    }

    [Fact]
    public void AuthPayload_ContainsTokenOnlyInWirePayload()
    {
        var tokenBytes = Encoding.UTF8.GetBytes("llat-super-secret");
        try
        {
            var payload = HomeAssistantProtocol.BuildAuth(tokenBytes);
            using var document = JsonDocument.Parse(payload);
            Assert.Equal("auth", document.RootElement.GetProperty("type").GetString());
            Assert.Equal("llat-super-secret", document.RootElement.GetProperty("access_token").GetString());
            Assert.DoesNotContain("llat-super-secret", HomeAssistantProtocol.SanitizeFailure("authentication failed"));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(tokenBytes);
        }
    }

    [Fact]
    public async Task RequestCorrelator_CompletesConcurrentRequestsOutOfOrder()
    {
        var correlator = new HomeAssistantRequestCorrelator();
        var first = correlator.Create();
        var second = correlator.Create();

        Assert.True(correlator.TryComplete(new HomeAssistantCommandResult(
            second.Id, true, JsonSerializer.SerializeToElement(new { value = 2 }), null)));
        Assert.True(correlator.TryComplete(new HomeAssistantCommandResult(
            first.Id, true, JsonSerializer.SerializeToElement(new { value = 1 }), null)));

        Assert.Equal(1, (await first.Completion).Result!.Value.GetProperty("value").GetInt32());
        Assert.Equal(2, (await second.Completion).Result!.Value.GetProperty("value").GetInt32());
        Assert.Equal(0, correlator.PendingCount);
    }

    [Fact]
    public void RequestCorrelator_RejectsUnknownResponseId()
    {
        var correlator = new HomeAssistantRequestCorrelator();
        Assert.False(correlator.TryComplete(new HomeAssistantCommandResult(
            999, true, JsonSerializer.SerializeToElement(new { }), null)));
    }

    [Fact]
    public void ResultParser_RequiresPositiveIntegerIdAndSuccess()
    {
        Assert.Throws<FormatException>(() => HomeAssistantProtocol.ParseResult("""{"type":"result","success":true}"""u8));
        Assert.Throws<FormatException>(() => HomeAssistantProtocol.ParseResult("""{"id":1,"type":"result"}"""u8));

        var parsed = HomeAssistantProtocol.ParseResult(
            """{"id":4,"type":"result","success":true,"result":[{"entity_id":"sensor.room","state":"21.5","attributes":{},"context":{}}]}"""u8);
        Assert.Equal(4, parsed.Id);
        Assert.True(parsed.Success);
    }

    [Fact]
    public void GetStates_ParsesCanonicalStateFields()
    {
        using var document = JsonDocument.Parse("""
            [
              {
                "entity_id":"sensor.room_temperature",
                "state":"21.5",
                "attributes":{"device_class":"temperature","unit_of_measurement":"°C"},
                "last_changed":"2026-10-06T19:00:00+00:00",
                "last_updated":"2026-10-06T19:00:01+00:00",
                "context":{"id":"abc"}
              }
            ]
            """);
        var states = HomeAssistantProtocol.ParseStates(document.RootElement);

        var state = Assert.Single(states);
        Assert.Equal("sensor.room_temperature", state.EntityId);
        Assert.Equal("21.5", state.State);
        Assert.Equal("temperature", state.Attributes.GetProperty("device_class").GetString());
    }

    [Fact]
    public void StateChanged_ParsesOldAndNewState()
    {
        var frame = """
            {
              "id":7,
              "type":"event",
              "event":{
                "event_type":"state_changed",
                "data":{
                  "entity_id":"switch.pump",
                  "old_state":{"entity_id":"switch.pump","state":"off","attributes":{},"context":{}},
                  "new_state":{"entity_id":"switch.pump","state":"on","attributes":{},"context":{}}
                }
              }
            }
            """u8;

        Assert.True(HomeAssistantProtocol.TryParseStateChangedEvent(frame, out var changed));
        Assert.NotNull(changed);
        Assert.Equal("switch.pump", changed!.EntityId);
        Assert.Equal("off", changed.OldState!.State);
        Assert.Equal("on", changed.NewState!.State);
    }

    [Fact]
    public void Protocol_RejectsMalformedJsonAndUnknownHandshake()
    {
        Assert.ThrowsAny<JsonException>(() => HomeAssistantProtocol.ParseHandshake("not-json"u8));
        Assert.Throws<FormatException>(() => HomeAssistantProtocol.ParseHandshake("""{"type":"mystery"}"""u8));
        Assert.Throws<FormatException>(() => HomeAssistantProtocol.ParseResult("""{"id":"1","type":"result","success":true}"""u8));
    }

    [Fact]
    public void ProtectedMaterialRequest_IsScopedAndContainsReferenceNotToken()
    {
        var request = HomeAssistantProtectedMaterial.CreateAccessTokenRequest(
            "project-a", "ha-main", "vault://home/ha-token");

        Assert.Equal(HomeAssistantContract.DriverType, request.DriverType);
        Assert.Equal(HomeAssistantContract.AccessTokenPurpose, request.Purpose);
        Assert.Equal("vault://home/ha-token", request.Reference);
    }

    [Fact]
    public void ConnectionSettings_BuildsOfficialWebSocketEndpoint()
    {
        var direct = new HomeAssistantConnectionSettings("ha.local");
        var proxied = new HomeAssistantConnectionSettings("ha.local", 443, true, "home");

        Assert.Equal("ws://ha.local:8123/api/websocket", direct.WebSocketUri.ToString());
        Assert.Equal("wss://ha.local/home/api/websocket", proxied.WebSocketUri.ToString());
    }
}