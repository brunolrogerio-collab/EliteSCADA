using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Scada.Engineering.Contracts;

namespace Scada.Api.Runtime;

/// <summary>Host-owned relay. Browser tickets cannot select arbitrary destinations or disclose camera URLs.</summary>
public sealed class RuntimeMediaRelay
{
    private readonly IConfiguration configuration;
    private readonly byte[] ticketKey = RandomNumberGenerator.GetBytes(32);
    private readonly HttpClient upstream;
    private readonly HttpClient gateway = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(15) };
    private readonly SemaphoreSlim provisioning = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Regex AttributeUri = new("URI=\"([^\"]+)\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public RuntimeMediaRelay(IConfiguration configuration)
    {
        this.configuration = configuration;
        upstream = new HttpClient(new SocketsHttpHandler {
            AllowAutoRedirect = false, UseCookies = false, UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(8), MaxConnectionsPerServer = 4,
            PooledConnectionLifetime = TimeSpan.FromSeconds(30),
            ConnectCallback = async (context, cancellation) => {
                var addresses = await ResolveAllowedAsync(context.DnsEndPoint.Host, cancellation);
                var socket = new Socket(addresses[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try { await socket.ConnectAsync(new IPEndPoint(addresses[0], context.DnsEndPoint.Port), cancellation); return new NetworkStream(socket, ownsSocket: true); }
                catch { socket.Dispose(); throw; }
            }
        }) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public bool GatewayConfigured => Uri.TryCreate(configuration["MediaSources:GatewayControlUrl"], UriKind.Absolute, out _) &&
        Uri.TryCreate(configuration["MediaSources:GatewayPlaybackUrl"], UriKind.Absolute, out _);

    public static bool AddressAllowed(IPAddress address, bool allowPrivate)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
            address.IsIPv6LinkLocal || address.IsIPv6Multicast) return false;
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6) return (bytes[0] & 0xe0) == 0x20 || allowPrivate && (bytes[0] & 0xfe) == 0xfc;
        if (bytes[0] == 0 || bytes[0] >= 224 || bytes[0] == 169 && bytes[1] == 254) return false;
        var privateAddress = bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 100 && bytes[1] is >= 64 and <= 127;
        return !privateAddress || allowPrivate;
    }

    public async Task<IPAddress[]> ResolveAllowedAsync(string host, CancellationToken cancellation)
    {
        var addresses = await Dns.GetHostAddressesAsync(host, cancellation);
        if (addresses.Length == 0 || addresses.Any(address => !AddressAllowed(address, configuration.GetValue<bool>("MediaSources:AllowPrivateNetwork"))))
            throw new IOException("Media destination is blocked by host policy.");
        return addresses;
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool trustedGateway, CancellationToken cancellation) =>
        (trustedGateway ? gateway : upstream).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);

    public async Task<Uri> PrepareRtspAsync(MediaSourceEngineeringDto source, string scope, string? username, string? password, CancellationToken cancellation)
    {
        if (!GatewayConfigured) throw new NotSupportedException("Media gateway is not provisioned.");
        var sourceUri = new Uri(source.Endpoint);
        var addresses = await ResolveAllowedAsync(sourceUri.Host, cancellation);
        // Pin RTSP DNS to the policy-checked address. RTSPS keeps its certificate hostname;
        // only literal addresses are accepted until a TLS-aware pinned resolver is provisioned.
        if (sourceUri.Scheme == "rtsps" && !IPAddress.TryParse(sourceUri.Host, out _)) throw new NotSupportedException("RTSPS requires a literal, certificate-valid address.");
        var pinned = new UriBuilder(sourceUri) { Host = addresses[0].ToString() };
        if (!string.IsNullOrEmpty(username)) { pinned.UserName = username; pinned.Password = password ?? ""; }
        var path = "scada-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope))).ToLowerInvariant()[..24];
        await provisioning.WaitAsync(cancellation);
        try {
            var control = configuration["MediaSources:GatewayControlUrl"]!.TrimEnd('/');
            var body = new { source = pinned.Uri.AbsoluteUri, sourceOnDemand = true, sourceOnDemandStartTimeout = "10s", sourceOnDemandCloseAfter = "10s", rtspTransport = "tcp", maxReaders = 8 };
            using var get = await gateway.GetAsync($"{control}/v3/config/paths/get/{path}", cancellation);
            var unchanged = false;
            if (get.IsSuccessStatusCode) {
                using var current = JsonDocument.Parse(await get.Content.ReadAsStringAsync(cancellation));
                unchanged = current.RootElement.TryGetProperty("source", out var currentSource) && currentSource.GetString() == body.source;
            }
            if (!unchanged) {
                using var mutation = new HttpRequestMessage(get.IsSuccessStatusCode ? HttpMethod.Patch : HttpMethod.Post,
                    $"{control}/v3/config/paths/{(get.IsSuccessStatusCode ? "patch" : "add")}/{path}") { Content = JsonContent.Create(body) };
                using var result = await gateway.SendAsync(mutation, cancellation);
                if (!result.IsSuccessStatusCode) throw new IOException("Media gateway provisioning failed.");
            }
            // MediaMTX 1.21 checks browser cookie support by redirecting its first
            // manifest. This server relay deliberately has no shared cookie jar:
            // request the documented cookieless session-query path directly. Its
            // session URLs are then encrypted into our revision-bound tickets.
            return new Uri(configuration["MediaSources:GatewayPlaybackUrl"]!.TrimEnd('/') + "/" + path + "/index.m3u8?cookieCheck=1");
        } finally { provisioning.Release(); }
    }

    public string CreateTicket(string scope, Uri uri)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Ticket(scope, uri.AbsoluteUri, DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()), Json);
        var nonce = RandomNumberGenerator.GetBytes(12); var encrypted = new byte[payload.Length]; var tag = new byte[16];
        using var aes = new AesGcm(ticketKey, 16); aes.Encrypt(nonce, payload, encrypted, tag);
        return WebEncoders.Base64UrlEncode(nonce.Concat(tag).Concat(encrypted).ToArray());
    }

    public Uri ReadTicket(string scope, string token)
    {
        if (token.Length > 8192) throw new CryptographicException();
        var bytes = WebEncoders.Base64UrlDecode(token);
        if (bytes.Length < 29) throw new CryptographicException();
        var plaintext = new byte[bytes.Length - 28];
        using var aes = new AesGcm(ticketKey, 16); aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(28), bytes.AsSpan(12, 16), plaintext);
        var ticket = JsonSerializer.Deserialize<Ticket>(plaintext, Json);
        if (ticket is null || ticket.Scope != scope || ticket.Expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) throw new CryptographicException();
        return new Uri(ticket.Uri);
    }

    public string RewritePlaylist(string text, Uri playlist, Uri origin, string scope, string route)
    {
        if (text.Length > 1024 * 1024 || !text.TrimStart().StartsWith("#EXTM3U", StringComparison.Ordinal)) throw new IOException("Invalid HLS playlist.");
        // Variable substitution and content steering could escape the checked origin. Reject rather than relay them implicitly.
        if (text.Contains("#EXT-X-DEFINE", StringComparison.Ordinal) || text.Contains("#EXT-X-CONTENT-STEERING", StringComparison.Ordinal)) throw new NotSupportedException();
        string Rewrite(string value) {
            var uri = new Uri(playlist, value);
            if (uri.Scheme != origin.Scheme || uri.Host != origin.Host || uri.Port != origin.Port || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment)) throw new IOException("Cross-origin HLS resource is blocked.");
            return route + "?ticket=" + CreateTicket(scope, uri);
        }
        return string.Join('\n', text.Replace("\r", "").Split('\n').Select(line => line.StartsWith('#')
            ? AttributeUri.Replace(line, match => "URI=\"" + Rewrite(match.Groups[1].Value) + "\"")
            : string.IsNullOrWhiteSpace(line) ? line : Rewrite(line.Trim())));
    }

    public static async Task<byte[]> ReadBoundedAsync(HttpContent content, int maximum, CancellationToken cancellation)
    {
        if (content.Headers.ContentLength > maximum) throw new IOException("Media payload exceeds the host limit.");
        await using var input = await content.ReadAsStreamAsync(cancellation); using var output = new MemoryStream();
        var buffer = new byte[16384]; int read;
        while ((read = await input.ReadAsync(buffer, cancellation)) > 0) {
            if (output.Length + read > maximum) throw new IOException("Media payload exceeds the host limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
    private sealed record Ticket(string Scope, string Uri, long Expires);
}
