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

    [Theory]
    [InlineData(SocketError.ConnectionRefused, "Refused")]
    [InlineData(SocketError.NetworkUnreachable, "Unreachable")]
    [InlineData(SocketError.HostUnreachable, "Unreachable")]
    [InlineData(SocketError.AccessDenied, "Unavailable")]
    public async Task ProbeAsync_ClassifiesTcpSocketFailuresWithoutExposingSocketDetails(
        SocketError socketError,
        string expectedStatus)
    {
        var probe = new NetworkReachabilityProbe((_, _, _) =>
            Task.FromException(new SocketException((int)socketError)));

        var result = await probe.ProbeAsync(new NetworkReachabilityProbeRequest(
            "127.0.0.1",
            502,
            TimeoutMilliseconds: 500));

        Assert.Equal(expectedStatus, result.Tcp.Status);
        Assert.Equal("127.0.0.1", result.Tcp.Address);
        Assert.DoesNotContain(socketError.ToString(), result.Tcp.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProbeAsync_ReportsBoundedTcpTimeout()
    {
        var probe = new NetworkReachabilityProbe((_, _, cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));

        var result = await probe.ProbeAsync(new NetworkReachabilityProbeRequest(
            "127.0.0.1",
            502,
            TimeoutMilliseconds: 100));

        Assert.Equal("Timeout", result.Tcp.Status);
        Assert.InRange(result.Tcp.ElapsedMilliseconds!.Value, 80, 1000);
    }

    [Fact]
    public async Task ProbeAsync_PreservesCallerCancellationInsteadOfReportingProbeTimeout()
    {
        using var cancellation = new CancellationTokenSource();
        var probe = new NetworkReachabilityProbe((_, _, cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.ProbeAsync(
            new NetworkReachabilityProbeRequest("127.0.0.1", 502, TimeoutMilliseconds: 500),
            cancellation.Token));
    }
}
