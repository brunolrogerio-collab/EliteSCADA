using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.RateLimiting;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Api.Engineering;

public sealed record NetworkReachabilityProbeRequest(
    string Host,
    int Port,
    int TimeoutMilliseconds = 3000);

public sealed record NetworkProbeResult(
    string Status,
    string? Address,
    double? ElapsedMilliseconds,
    string? Detail = null);

public sealed record NetworkReachabilityProbeResponse(
    string Authority,
    DateTimeOffset ObservedAtUtc,
    string Host,
    int Port,
    NetworkProbeResult Tcp,
    NetworkProbeResult Icmp);

public sealed record DriverHostHealthSnapshot(
    string Status,
    string Service,
    string? NodeIdentity,
    DateTimeOffset ObservedAtUtc,
    int FreshForSeconds,
    TimeSpan Uptime,
    bool ActiveRuntimeAvailable,
    long? ActiveRevision);

public sealed class NetworkReachabilityProbe
{
    public const int MaximumTimeoutMilliseconds = 5000;
    internal static readonly DateTimeOffset ProcessStartedAtUtc = DateTimeOffset.UtcNow;
    private readonly Func<IPAddress, int, CancellationToken, Task> _connectTcp;

    public NetworkReachabilityProbe() : this(ConnectTcpAsync) { }

    internal NetworkReachabilityProbe(Func<IPAddress, int, CancellationToken, Task> connectTcp) =>
        _connectTcp = connectTcp ?? throw new ArgumentNullException(nameof(connectTcp));

    public async Task<NetworkReachabilityProbeResponse> ProbeAsync(
        NetworkReachabilityProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        var host = request.Host.Trim();
        Validate(request, host);

        var observedAt = DateTimeOffset.UtcNow;
        IPAddress address;
        try
        {
            address = IPAddress.TryParse(host, out var literal)
                ? literal
                : (await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false))
                    .FirstOrDefault()
                  ?? throw new SocketException((int)SocketError.HostNotFound);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData)
        {
            return new NetworkReachabilityProbeResponse(
                "EliteSCADA.Api.DriverHost",
                observedAt,
                host,
                request.Port,
                new NetworkProbeResult("DnsFailure", null, null, "The host name could not be resolved."),
                new NetworkProbeResult("Unavailable", null, null, "ICMP was skipped because DNS resolution failed."));
        }
        catch (SocketException)
        {
            return new NetworkReachabilityProbeResponse(
                "EliteSCADA.Api.DriverHost",
                observedAt,
                host,
                request.Port,
                new NetworkProbeResult("Unavailable", null, null, "The server host could not resolve the network target."),
                new NetworkProbeResult("Unavailable", null, null, "ICMP was skipped because target resolution was unavailable."));
        }

        var tcp = await ProbeTcpAsync(address, request.Port, request.TimeoutMilliseconds, cancellationToken, _connectTcp)
            .ConfigureAwait(false);
        var icmp = await ProbeIcmpAsync(address, request.TimeoutMilliseconds, cancellationToken)
            .ConfigureAwait(false);

        return new NetworkReachabilityProbeResponse(
            "EliteSCADA.Api.DriverHost",
            observedAt,
            host,
            request.Port,
            tcp,
            icmp);
    }

    private static void Validate(NetworkReachabilityProbeRequest request, string host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253 ||
            host.Any(character => char.IsControl(character) || char.IsWhiteSpace(character) || "/\\@?#".Contains(character)))
            throw new ArgumentException("Host must be one DNS name or IP address.", nameof(request.Host));

        if (!IPAddress.TryParse(host, out _) && Uri.CheckHostName(host) != UriHostNameType.Dns)
            throw new ArgumentException("Host must be one DNS name or IP address.", nameof(request.Host));

        if (request.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(request.Port), "Port must be from 1 to 65535.");

        if (request.TimeoutMilliseconds is < 100 or > MaximumTimeoutMilliseconds)
            throw new ArgumentOutOfRangeException(
                nameof(request.TimeoutMilliseconds),
                $"Timeout must be from 100 to {MaximumTimeoutMilliseconds} milliseconds.");
    }

    private static async Task<NetworkProbeResult> ProbeTcpAsync(
        IPAddress address,
        int port,
        int timeoutMilliseconds,
        CancellationToken cancellationToken,
        Func<IPAddress, int, CancellationToken, Task> connectTcp)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMilliseconds);
        try
        {
            await connectTcp(address, port, timeout.Token).ConfigureAwait(false);
            return new NetworkProbeResult("Connected", address.ToString(), stopwatch.Elapsed.TotalMilliseconds,
                "PORT_REACHABLE; protocol health was not tested.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new NetworkProbeResult("Timeout", address.ToString(), stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
        {
            return new NetworkProbeResult("Refused", address.ToString(), stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.NetworkUnreachable or SocketError.HostUnreachable)
        {
            return new NetworkProbeResult("Unreachable", address.ToString(), stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (SocketException)
        {
            return new NetworkProbeResult("Unavailable", address.ToString(), stopwatch.Elapsed.TotalMilliseconds,
                "The server host could not complete the TCP probe.");
        }
    }

    private static async Task ConnectTcpAsync(IPAddress address, int port, CancellationToken cancellationToken)
    {
        using var client = new TcpClient(address.AddressFamily);
        await client.ConnectAsync(address, port, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<NetworkProbeResult> ProbeIcmpAsync(
        IPAddress address,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, TimeSpan.FromMilliseconds(timeoutMilliseconds), new byte[8], new PingOptions(64, true), cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();
            return reply.Status switch
            {
                IPStatus.Success => new NetworkProbeResult("Success", address.ToString(), reply.RoundtripTime),
                IPStatus.TimedOut => new NetworkProbeResult("Timeout", address.ToString(), stopwatch.Elapsed.TotalMilliseconds),
                _ => new NetworkProbeResult("Unavailable", address.ToString(), stopwatch.Elapsed.TotalMilliseconds,
                    "ICMP did not receive a successful reply; TCP reachability is reported separately.")
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PlatformNotSupportedException)
        {
            return new NetworkProbeResult("Unavailable", address.ToString(), null, "ICMP is not supported by this host.");
        }
        catch (PingException)
        {
            return new NetworkProbeResult("Unavailable", address.ToString(), stopwatch.Elapsed.TotalMilliseconds,
                "ICMP is unavailable or blocked by host policy.");
        }
        catch (SocketException)
        {
            return new NetworkProbeResult("Unavailable", address.ToString(), stopwatch.Elapsed.TotalMilliseconds,
                "ICMP is unavailable or blocked by host policy.");
        }
    }
}

public static class NetworkReachabilityDiagnosticsApi
{
    public const string RateLimitPolicy = "engineering-network-probe";
    private const string ProbeAction = "engineering.network-probe";

    public static void MapNetworkReachabilityDiagnostics(this WebApplication app)
    {
        app.MapGet("/api/engineering/diagnostics/driver-host", (
            ScadaRuntimeFacade runtime,
            CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var descriptor = runtime.Describe();
            return Results.Ok(new DriverHostHealthSnapshot(
                "Healthy",
                "EliteSCADA API / DriverHost",
                Environment.GetEnvironmentVariable("ELITESCADA_NODE_ID"),
                DateTimeOffset.UtcNow,
                30,
                DateTimeOffset.UtcNow - NetworkReachabilityProbe.ProcessStartedAtUtc,
                descriptor.Mode.Equals("engineering", StringComparison.OrdinalIgnoreCase),
                descriptor.Revision));
        })
        .RequireWorkspaceEngineeringRead();

        app.MapPost("/api/engineering/diagnostics/network-probe", async (
            NetworkReachabilityProbeRequest request,
            HttpContext context,
            ApiAuthorizationService security,
            ApiAuditService audit,
            NetworkReachabilityProbe probe,
            CancellationToken cancellationToken) =>
        {
            var authorization = security.CheckWorkspace(context, SecurityCapability.EngineeringModify);
            var failure = authorization.FailureResult();
            if (failure is not null)
            {
                await audit.RecordAuthorizationDeniedAsync(
                    context,
                    authorization,
                    ProbeAction,
                    "network-target",
                    "single-target");
                return failure;
            }

            NetworkReachabilityProbeResponse result;
            try
            {
                result = await probe.ProbeAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new
                {
                    error = "Network probe request is invalid.",
                    code = "NETWORK_PROBE_REQUEST_INVALID",
                    fieldKey = ex.ParamName,
                    detail = ex.Message
                });
            }

            await audit.RecordAsync(
                context,
                authorization.Principal,
                ProbeAction,
                AuditOutcome.Succeeded,
                "network-target",
                $"{result.Host}:{result.Port}",
                new Dictionary<string, string>
                {
                    ["tcpStatus"] = result.Tcp.Status,
                    ["icmpStatus"] = result.Icmp.Status,
                    ["authority"] = result.Authority
                });

            return Results.Ok(result);
        })
        .RequireRateLimiting(RateLimitPolicy);
    }
}
