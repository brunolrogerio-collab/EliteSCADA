using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Scada.Api.Runtime;
using Xunit;

namespace Scada.Drivers.Tests;

public sealed class RuntimeMediaRelayTests
{
    private static RuntimeMediaRelay Relay() => new(new ConfigurationBuilder().Build());

    [Theory]
    [InlineData("127.0.0.1", true, false)]
    [InlineData("169.254.169.254", true, false)]
    [InlineData("0.0.0.0", true, false)]
    [InlineData("224.0.0.1", true, false)]
    [InlineData("::1", true, false)]
    [InlineData("::ffff:127.0.0.1", true, false)]
    [InlineData("fe80::1", true, false)]
    [InlineData("64:ff9b::a9fe:a9fe", true, false)]
    [InlineData("10.0.0.2", false, false)]
    [InlineData("10.0.0.2", true, true)]
    [InlineData("192.168.0.2", true, true)]
    [InlineData("fc00::2", false, false)]
    [InlineData("fc00::2", true, true)]
    [InlineData("8.8.8.8", false, true)]
    public void DestinationPolicyProtectsLocalAndMetadataAddresses(string ip, bool allowPrivate, bool expected) =>
        Assert.Equal(expected, RuntimeMediaRelay.AddressAllowed(IPAddress.Parse(ip), allowPrivate));

    [Fact]
    public void TicketsAreOpaqueAndBoundToTheActiveSourceRevision()
    {
        var relay = Relay(); var uri = new Uri("https://camera.example/live/segment.ts?session=private");
        var ticket = relay.CreateTicket("project:revision:camera", uri);
        Assert.DoesNotContain("camera.example", ticket);
        Assert.Equal(uri, relay.ReadTicket("project:revision:camera", ticket));
        Assert.Throws<CryptographicException>(() => relay.ReadTicket("project:other-revision:camera", ticket));
        Assert.ThrowsAny<CryptographicException>(() => Relay().ReadTicket("project:revision:camera", ticket));
    }

    [Fact]
    public void PlaylistRewritesSegmentsKeysAndVariantsWithoutLeakingUpstreamUrls()
    {
        var relay = Relay(); var origin = new Uri("https://camera.example/live/index.m3u8");
        var playlist = "#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI=\"key.bin\"\n#EXT-X-MAP:URI=\"init.mp4\"\n#EXTINF:1,\nsegment.ts\nvariant.m3u8\n";
        var result = relay.RewritePlaylist(playlist, origin, origin, "scope", "/relay");
        Assert.DoesNotContain("camera.example", result);
        Assert.DoesNotContain("segment.ts", result);
        Assert.Equal(4, result.Split("/relay?ticket=").Length - 1);
        var token = result.Split('\n').First(line => line.StartsWith("/relay?ticket=")).Split("ticket=")[1];
        Assert.Equal(new Uri("https://camera.example/live/segment.ts"), relay.ReadTicket("scope", token));
    }

    [Theory]
    [InlineData("#EXTM3U\nhttp://127.0.0.1/secret")]
    [InlineData("#EXTM3U\nhttps://evil.example/segment.ts")]
    [InlineData("#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI=\"file:///etc/passwd\"")]
    [InlineData("<html>not a playlist</html>")]
    public void PlaylistCannotEscapeThePersistedOrigin(string text)
    {
        var relay = Relay(); var origin = new Uri("https://camera.example/live/index.m3u8");
        Assert.Throws<IOException>(() => relay.RewritePlaylist(text, origin, origin, "scope", "/relay"));
    }
}
