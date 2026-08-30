using System.Net;
using System.Net.Sockets;
using Scada.Drivers.Abstractions;
using Scada.Drivers.SiemensS7Iso;

namespace Scada.Drivers.Tests;

public sealed class S7IsoEngineeringFailureTests
{
    [Fact]
    public async Task ConnectionTest_ClosedPeerReturnsTransportUnavailableEvidence()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var peer = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            client.Close();
        });

        try
        {
            var settings = new Dictionary<string, string>
            {
                ["host"] = "127.0.0.1",
                ["port"] = port.ToString(),
                ["cpuFamily"] = nameof(S7CpuFamily.S71500),
                ["connectionMode"] = nameof(S7IsoConnectionMode.RackSlot),
                ["rack"] = "0",
                ["slot"] = "1",
                ["connectionRole"] = nameof(S7IsoConnectionRole.Basic),
                ["requestTimeoutMs"] = "500",
                ["connectTimeoutMs"] = "500",
                ["reconnectDelayMs"] = "0"
            };
            var context = new DriverEngineeringDataSourceContext(
                "s7-closed",
                "S7 Closed",
                "siemens.s7.iso",
                settings,
                new Dictionary<string, string>());

            var result = await new S7IsoEngineeringAdapter().TestConnectionAsync(context);

            Assert.False(result.Succeeded);
            Assert.Equal("TransportUnavailable", result.ObservedProperties!["failureKind"]);
            var issue = Assert.Single(result.Issues!);
            Assert.Equal("S7_TRANSPORT_UNAVAILABLE", issue.Code);
            Assert.Equal(DriverEngineeringIssueSeverity.Error, issue.Severity);
            Assert.DoesNotContain('\n', issue.Message);
            Assert.DoesNotContain('\r', issue.Message);
        }
        finally
        {
            listener.Stop();
            await peer;
        }
    }

    [Fact]
    public async Task ConnectionTest_PeerPduAboveRequestedMaximumReturnsSessionRejectedEvidence()
    {
        await using var server = new TestS7IsoServer(960);
        var settings = new Dictionary<string, string>
        {
            ["host"] = "127.0.0.1",
            ["port"] = server.Port.ToString(),
            ["cpuFamily"] = nameof(S7CpuFamily.S71500),
            ["connectionMode"] = nameof(S7IsoConnectionMode.RackSlot),
            ["rack"] = "0",
            ["slot"] = "1",
            ["connectionRole"] = nameof(S7IsoConnectionRole.Basic),
            ["requestedPduSize"] = "480",
            ["requestTimeoutMs"] = "500",
            ["connectTimeoutMs"] = "500",
            ["reconnectDelayMs"] = "0"
        };
        var context = new DriverEngineeringDataSourceContext(
            "s7-pdu-rejected",
            "S7 PDU Rejected",
            "siemens.s7.iso",
            settings,
            new Dictionary<string, string>());

        var result = await new S7IsoEngineeringAdapter().TestConnectionAsync(context);

        Assert.False(result.Succeeded);
        Assert.Equal("S7SessionRejected", result.ObservedProperties!["failureKind"]);
        Assert.Equal("false", result.ObservedProperties["sessionEstablished"]);
        Assert.Equal(string.Empty, result.ObservedProperties["negotiatedPduSize"]);
        var issue = Assert.Single(result.Issues!);
        Assert.Equal("S7_SESSION_REJECTED", issue.Code);
        Assert.Equal(DriverEngineeringIssueSeverity.Error, issue.Severity);
        Assert.Contains("requested maximum", issue.Message, StringComparison.Ordinal);
    }
}
