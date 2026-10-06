using System.Text.Json;
using Scada.Core.Tags;
using Scada.Drivers.Shelly;

namespace Scada.Drivers.Tests;

public sealed class ShellyRpcContractTests
{
    [Fact]
    public void CurrentDigestChallenge_UsesStringNonceAndMonotonicHexNc()
    {
        using var document = JsonDocument.Parse("""
            {"auth_type":"digest","nonce":"AAAA-current-nonce","realm":"shellyplus1-abc","algorithm":"SHA-256","stale":false}
            """);
        var challenge = ShellyDigestChallenge.Parse(document.RootElement);

        Assert.True(challenge.ReusableNonce);
        Assert.Null(challenge.LegacyNumericNonce);
        using var sessionPassword = new PasswordScope("secret");
        var session = new ShellyDigestSession("admin", sessionPassword.Bytes);
        session.AcceptChallenge(challenge);

        var first = JsonSerializer.SerializeToElement(session.BuildAuth(ShellyAuthTransport.Http));
        var second = JsonSerializer.SerializeToElement(session.BuildAuth(ShellyAuthTransport.Http));

        Assert.Equal("AAAA-current-nonce", first.GetProperty("nonce").GetString());
        Assert.Equal("00000001", first.GetProperty("nc").GetString());
        Assert.Equal("00000002", second.GetProperty("nc").GetString());
        Assert.Equal(JsonValueKind.Number, first.GetProperty("cnonce").ValueKind);
        Assert.Equal(64, first.GetProperty("response").GetString()!.Length);
        session.Clear();
    }

    [Fact]
    public void LegacyDigestChallenge_PreservesNumericNonce()
    {
        using var document = JsonDocument.Parse("""
            {"auth_type":"digest","nonce":1625053638,"nc":1,"realm":"shellypro4pm-abc","algorithm":"SHA-256"}
            """);
        var challenge = ShellyDigestChallenge.Parse(document.RootElement);

        Assert.False(challenge.ReusableNonce);
        Assert.Equal(1625053638L, challenge.LegacyNumericNonce);
        using var password = new PasswordScope("secret");
        var session = new ShellyDigestSession("admin", password.Bytes);
        session.AcceptChallenge(challenge);
        var auth = JsonSerializer.SerializeToElement(session.BuildAuth(ShellyAuthTransport.WebSocket));

        Assert.Equal(JsonValueKind.Number, auth.GetProperty("nonce").ValueKind);
        Assert.Equal("00000001", auth.GetProperty("nc").GetString());
        session.Clear();
    }

    [Fact]
    public void NotifyStatus_PartialOverlay_PreservesUnchangedFields()
    {
        var state = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        using var fullDoc = JsonDocument.Parse("""
            {"switch:0":{"id":0,"output":true,"apower":12.5,"aenergy":{"total":42.0}}}
            """);
        ShellyStateMapper.MergeStatus(state, fullDoc.RootElement);

        using var deltaDoc = JsonDocument.Parse("""
            {"ts":1631186545.04,"switch:0":{"apower":8.25}}
            """);
        ShellyStateMapper.MergeStatus(state, deltaDoc.RootElement);

        var component = state["switch:0"];
        Assert.True(component.GetProperty("output").GetBoolean());
        Assert.Equal(8.25, component.GetProperty("apower").GetDouble());
        Assert.Equal(42.0, component.GetProperty("aenergy").GetProperty("total").GetDouble());
        Assert.False(state.ContainsKey("ts"));
    }

    [Fact]
    public void NotifyEvent_IsParsedWithoutCreatingPersistentState()
    {
        var frame = """
            {"src":"shelly","dst":"elite","method":"NotifyEvent","params":{"ts":1,"events":[{"component":"input:0","id":0,"event":"single_push","ts":1}]}}
            """u8.ToArray();

        Assert.True(ShellyRpcJson.TryParseNotification(frame, out var method, out var parameters));
        Assert.Equal("NotifyEvent", method);
        Assert.Equal("single_push", parameters.GetProperty("events")[0].GetProperty("event").GetString());
    }

    [Fact]
    public void ComponentMapper_MapsOnlyConfirmedFields_AndKeepsUnknownDiagnostic()
    {
        var info = new ShellyDeviceInfo("shellyplus1-abc", "SNSW-001X16EU", "2.0.1", "AABBCC", "Pump");
        using var componentsDoc = JsonDocument.Parse("""
            {"components":[{"key":"switch:0"},{"key":"mystery:0"}],"total":2}
            """);
        using var statusDoc = JsonDocument.Parse("""
            {"switch:0":{"id":0,"output":false,"apower":1.2,"voltage":230.0,"current":0.1,"aenergy":{"total":9.5}},"mystery:0":{"id":0,"foo":1}}
            """);

        var candidate = ShellyComponentMapper.BuildMaterialization(info, componentsDoc.RootElement, statusDoc.RootElement);

        Assert.Equal("shellyplus1-abc", candidate.Equipment.StableDeviceIdentity);
        Assert.Contains(candidate.Tags!, x => x.PortableAddress == "switch:0.output" && !x.ReadOnly);
        Assert.Contains(candidate.Tags!, x => x.PortableAddress == "switch:0.aenergy.total" && x.ReadOnly);
        Assert.Contains(candidate.Commands!, x => x.CandidateId == "switch:0.command.on");
        Assert.Contains(candidate.Equipment.Capabilities!, x => x.Kind == "OnOff");
        Assert.Contains("mystery:0", candidate.Equipment.Metadata!["unmappedComponents"]);
    }

    [Fact]
    public void FirmwareClassifier_DistinguishesLegacyBeforeTwoDotZero()
    {
        Assert.True(ShellyDriver.IsLegacyFirmware("1.7.1"));
        Assert.True(ShellyDriver.IsLegacyFirmware("0.14.4"));
        Assert.False(ShellyDriver.IsLegacyFirmware("2.0.0"));
        Assert.False(ShellyDriver.IsLegacyFirmware("2.1.0-beta1"));
    }

    private sealed class PasswordScope : IDisposable
    {
        private readonly byte[] _bytes;
        public PasswordScope(string value) => _bytes = System.Text.Encoding.UTF8.GetBytes(value);
        public ReadOnlyMemory<byte> Bytes => _bytes;
        public void Dispose() => System.Security.Cryptography.CryptographicOperations.ZeroMemory(_bytes);
    }
}
