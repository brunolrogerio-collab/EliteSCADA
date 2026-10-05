using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Scada.Api.Runtime;
using Scada.Engineering.Contracts;
using Xunit;

namespace Scada.Drivers.Tests;

public sealed class MediaGatewayLabFactAttribute : FactAttribute
{
    public MediaGatewayLabFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ELITESCADA_MEDIA_LAB_CAMERA")))
            Skip = "Opt-in synthetic media lab; requires docker-compose.media-lab.yml.";
    }
}

public sealed class RuntimeMediaGatewayLabTests
{
    [MediaGatewayLabFact]
    public async Task SyntheticRtspRemuxesToOpaquePlayableHlsResources()
    {
        var relay = new RuntimeMediaRelay(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["MediaSources:AllowPrivateNetwork"] = "true",
            ["MediaSources:GatewayControlUrl"] = "http://127.0.0.1:19997",
            ["MediaSources:GatewayPlaybackUrl"] = "http://127.0.0.1:19888"
        }).Build());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var source = new MediaSourceEngineeringDto(Guid.NewGuid(), "lab.rtsp", "Synthetic H264", MediaSourceProtocol.Rtsp,
            Environment.GetEnvironmentVariable("ELITESCADA_MEDIA_LAB_CAMERA")!);
        const string scope = "isolated-media-lab:1:camera";
        var uri = await relay.PrepareRtspAsync(source, scope, null, null, deadline.Token);
        var again = await relay.PrepareRtspAsync(source, scope, null, null, deadline.Token);
        Assert.Equal(uri, again); // idempotent provisioning must not restart an unchanged stream.
        using var manifest = await relay.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri), true, deadline.Token);
        manifest.EnsureSuccessStatusCode();
        var text = await manifest.Content.ReadAsStringAsync(deadline.Token);
        Assert.StartsWith("#EXTM3U", text);
        // Follow both multivariant and media playlists through encrypted tickets.
        for (var depth = 0; depth < 3; depth++) {
            var rewritten = relay.RewritePlaylist(text, uri, uri, scope, "/relay");
            Assert.DoesNotContain("127.0.0.1", rewritten);
            var line = rewritten.Split('\n').First(item => item.StartsWith("/relay?ticket="));
            var resource = relay.ReadTicket(scope, line.Split("ticket=")[1]);
            using var response = await relay.SendAsync(new HttpRequestMessage(HttpMethod.Get, resource), true, deadline.Token);
            response.EnsureSuccessStatusCode();
            if (resource.AbsolutePath.EndsWith(".m3u8")) {
                text = await response.Content.ReadAsStringAsync(deadline.Token); uri = resource; continue;
            }
            var bytes = await RuntimeMediaRelay.ReadBoundedAsync(response.Content, 32 * 1024 * 1024, deadline.Token);
            Assert.True(bytes.Length > 100, "Synthetic H264 segment must contain media, not an empty placeholder.");
            Assert.Contains("#EXT-X-MAP", text); // fMP4 initialization is also rewritten.
            var map = Regex.Match(relay.RewritePlaylist(text, uri, uri, scope, "/relay"), "URI=\"/relay\\?ticket=([^\"]+)\"");
            Assert.True(map.Success);
            using var initialization = await relay.SendAsync(new HttpRequestMessage(HttpMethod.Get, relay.ReadTicket(scope, map.Groups[1].Value)), true, deadline.Token);
            initialization.EnsureSuccessStatusCode();
            Assert.True((await RuntimeMediaRelay.ReadBoundedAsync(initialization.Content, 1024 * 1024, deadline.Token)).Length > 100);
            return;
        }
        Assert.Fail("Gateway did not expose a playable media segment.");
    }
}
