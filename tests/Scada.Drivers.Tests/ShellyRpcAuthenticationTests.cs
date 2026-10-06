using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Scada.Drivers.Shelly;

namespace Scada.Drivers.Tests;

public sealed class ShellyRpcAuthenticationTests
{
    [Fact]
    public async Task HttpDigest_CurrentFirmware_RetriesChallengeWithProtectedAuthObject()
    {
        var handler = new ScriptedHttpHandler((call, request) =>
        {
            if (call == 1)
                return Unauthorized("""Digest realm="shellyplus1-aabbcc", nonce="AAAA-current", algorithm=SHA-256, stale=false""");

            var rpc = Parse(request);
            var auth = rpc.GetProperty("auth");
            Assert.Equal("AAAA-current", auth.GetProperty("nonce").GetString());
            Assert.Equal("00000001", auth.GetProperty("nc").GetString());
            Assert.Equal("SHA-256", auth.GetProperty("algorithm").GetString());
            Assert.Equal(64, auth.GetProperty("response").GetString()!.Length);
            return Ok(rpc.GetProperty("id").GetInt64(), new { switch0 = true });
        });
        using var password = new PasswordScope("super-secret");
        await using var client = Client(handler);

        var result = await client.CallHttpAsync(
            "Shelly.GetStatus", null, password.Bytes, legacyAuthentication: false);

        Assert.True(result.GetProperty("switch0").GetBoolean());
        Assert.Equal(2, handler.CallCount);
        Assert.DoesNotContain("super-secret", handler.Payloads.Single(x => x.Contains(""auth"", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task HttpDigest_LegacyFirmware_ConvertsHexHeaderNonceToNumericWireAuth()
    {
        var handler = new ScriptedHttpHandler((call, request) =>
        {
            if (call == 1)
                return Unauthorized("""Digest realm="shellypro4pm-aabbcc", nonce="60dc59c6", algorithm=SHA-256""");

            var rpc = Parse(request);
            var auth = rpc.GetProperty("auth");
            Assert.Equal(JsonValueKind.Number, auth.GetProperty("nonce").ValueKind);
            Assert.Equal(Convert.ToInt64("60dc59c6", 16), auth.GetProperty("nonce").GetInt64());
            Assert.Equal("00000001", auth.GetProperty("nc").GetString());
            return Ok(rpc.GetProperty("id").GetInt64(), new { ok = true });
        });
        using var password = new PasswordScope("legacy-secret");
        await using var client = Client(handler);

        var result = await client.CallHttpAsync(
            "Shelly.GetStatus", null, password.Bytes, legacyAuthentication: true);

        Assert.True(result.GetProperty("ok").GetBoolean());
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task HttpDigest_CurrentFirmware_PreservesNonceCountAcrossRpcCalls()
    {
        var authenticatedNc = new List<string>();
        var handler = new ScriptedHttpHandler((call, request) =>
        {
            if (call is 1 or 3)
                return Unauthorized("""Digest realm="shellyplus1-aabbcc", nonce="shared-nonce", algorithm=SHA-256, stale=false""");

            var rpc = Parse(request);
            authenticatedNc.Add(rpc.GetProperty("auth").GetProperty("nc").GetString()!);
            return Ok(rpc.GetProperty("id").GetInt64(), new { ok = true });
        });
        using var password = new PasswordScope("shared-secret");
        await using var client = Client(handler);

        _ = await client.CallHttpAsync("Shelly.GetStatus", null, password.Bytes, legacyAuthentication: false);
        _ = await client.CallHttpAsync("Shelly.GetStatus", null, password.Bytes, legacyAuthentication: false);

        Assert.Equal(["00000001", "00000002"], authenticatedNc);
    }

    [Fact]
    public async Task HttpDigest_StaleChallenge_ReplacesNonceAndRestartsNonceCount()
    {
        var handler = new ScriptedHttpHandler((call, request) =>
        {
            if (call == 1)
                return Unauthorized("""Digest realm="shellyplus1-aabbcc", nonce="nonce-a", algorithm=SHA-256, stale=false""");
            if (call == 2)
            {
                var firstAuth = Parse(request).GetProperty("auth");
                Assert.Equal("nonce-a", firstAuth.GetProperty("nonce").GetString());
                return Unauthorized("""Digest realm="shellyplus1-aabbcc", nonce="nonce-b", algorithm=SHA-256, stale=true""");
            }

            var secondAuth = Parse(request).GetProperty("auth");
            Assert.Equal("nonce-b", secondAuth.GetProperty("nonce").GetString());
            Assert.Equal("00000001", secondAuth.GetProperty("nc").GetString());
            return Ok(Parse(request).GetProperty("id").GetInt64(), new { recovered = true });
        });
        using var password = new PasswordScope("rotated-secret");
        await using var client = Client(handler);

        var result = await client.CallHttpAsync(
            "Shelly.GetStatus", null, password.Bytes, legacyAuthentication: false);

        Assert.True(result.GetProperty("recovered").GetBoolean());
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task Http401_WithoutConfiguredProtectedPassword_FailsClosed()
    {
        var handler = new ScriptedHttpHandler((_, _) =>
            Unauthorized("""Digest realm="shellyplus1-aabbcc", nonce="nonce-a", algorithm=SHA-256, stale=false"""));
        await using var client = Client(handler);

        var error = await Assert.ThrowsAsync<ShellyRpcException>(async () =>
            await client.CallHttpAsync(
                "Shelly.GetStatus", null, ReadOnlyMemory<byte>.Empty, legacyAuthentication: false));

        Assert.Equal(401, error.RpcCode);
        Assert.Contains("protected password", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public void RpcCodec_RejectsMalformedAndUncorrelatablePayloads()
    {
        Assert.ThrowsAny<Exception>(() => ShellyRpcJson.ParseResponse("not-json"u8));
        Assert.Throws<FormatException>(() => ShellyRpcJson.ParseResponse("""{"result":{}}"""u8));
    }

    private static ShellyRpcClient Client(HttpMessageHandler handler) =>
        new(
            new ShellyConnectionSettings("127.0.0.1", RequestTimeout: TimeSpan.FromSeconds(2)),
            "elite-auth-test",
            new HttpClient(handler));

    private static HttpResponseMessage Unauthorized(string challenge)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Add(AuthenticationHeaderValue.Parse(challenge));
        return response;
    }

    private static HttpResponseMessage Ok(long id, object result) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { id, src = "shelly", dst = "elite-auth-test", result }),
                Encoding.UTF8,
                "application/json")
        };

    private static JsonElement Parse(HttpRequestMessage request)
    {
        var json = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class ScriptedHttpHandler(
        Func<int, HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public List<string> Payloads { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Payloads.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return responder(CallCount, request);
        }
    }

    private sealed class PasswordScope : IDisposable
    {
        private readonly byte[] _bytes;
        public PasswordScope(string value) => _bytes = Encoding.UTF8.GetBytes(value);
        public ReadOnlyMemory<byte> Bytes => _bytes;
        public void Dispose() => System.Security.Cryptography.CryptographicOperations.ZeroMemory(_bytes);
    }
}
