using System.Net;
using System.Net.Sockets;
using Scada.Api.Engineering;

namespace Scada.Drivers.Tests;

public sealed class NetworkReachabilityProbeTests
{
    [Fact]
    public async Task ProbeAsync_ConnectsToOneAuthorizedTcpTarget_AndLabelsProtocolHealthSeparately()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var probe = new NetworkReachabilityProbe();

        var result = await probe.ProbeAsync(new NetworkReachabilityProbeRequest(
            "127.0.0.1",
            port,
            TimeoutMilliseconds: 500));

        Assert.Equal("EliteSCADA.Api.DriverHost", result.Authority);
        Assert.Equal("Connected", result.Tcp.Status);
        Assert.Equal("127.0.0.1", result.Tcp.Address);
        Assert.Contains("PORT_REACHABLE", result.Tcp.Detail, StringComparison.Ordinal);
        Assert.True(result.Tcp.ElapsedMilliseconds >= 0);
        Assert.Contains(result.Icmp.Status, new[] { "Success", "Timeout", "Unavailable" });
    }

    [Theory]
    [InlineData("http://127.0.0.1", 502, 1000)]
    [InlineData("127.0.0.1", 0, 1000)]
    [InlineData("127.0.0.1", 502, 50)]
    [InlineData("127.0.0.1", 502, 5001)]
    public async Task ProbeAsync_RejectsUriPortRangesAndUnboundedTimeouts(
        string host,
        int port,
        int timeoutMilliseconds)
    {
        var probe = new NetworkReachabilityProbe();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => probe.ProbeAsync(new NetworkReachabilityProbeRequest(
            host,
            port,
            timeoutMilliseconds)));
    }

    [Fact]
    public async Task ProbeAsync_ReportsDnsFailureWithoutLeakingResolverErrors()
    {
        var probe = new NetworkReachabilityProbe();

        var result = await probe.ProbeAsync(new NetworkReachabilityProbeRequest(
            "no-such-host.elitescada.invalid",
            502,
            TimeoutMilliseconds: 500));

        Assert.Equal("DnsFailure", result.Tcp.Status);
        Assert.Equal("Unavailable", result.Icmp.Status);
        Assert.Null(result.Tcp.ElapsedMilliseconds);
        Assert.DoesNotContain("no-such-host", result.Tcp.Detail, StringComparison.OrdinalIgnoreCase);
    }
}
