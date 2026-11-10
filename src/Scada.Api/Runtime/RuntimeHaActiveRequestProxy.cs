using System.Net;
using System.Net.Http;
using Yarp.ReverseProxy.Forwarder;

namespace Scada.Api.Runtime;

internal enum RuntimeHaProxyDisposition
{
    ContinueLocally,
    ForwardToActive,
    Unavailable
}

internal sealed record RuntimeHaProxyDecision(
    RuntimeHaProxyDisposition Disposition,
    string ReasonCode,
    string? DestinationPrefix = null);

internal interface IRuntimeHaProxyDecisionSource
{
    RuntimeHaProxyDecision Resolve(HttpContext context);
}

internal sealed class RuntimeHaProxyDecisionSource(
    RuntimeHaProtectionCoordinator protection) : IRuntimeHaProxyDecisionSource
{
    public RuntimeHaProxyDecision Resolve(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var diagnostics = protection.Diagnostics();
        return RuntimeHaActiveRequestProxyPolicy.Resolve(
            diagnostics.Topology,
            diagnostics.Status,
            context.Request.Scheme,
            context.Request.Host);
    }
}

internal static class RuntimeHaActiveRequestProxyPolicy
{
    public static RuntimeHaProxyDecision Resolve(
        RuntimeHaTopologySnapshot topology,
        string protectionStatus,
        string requestScheme,
        HostString requestHost)
    {
        ArgumentNullException.ThrowIfNull(topology);

        if (!topology.Enabled)
            return Local("ha-disabled");

        if (topology.AmbiguousAuthority)
            return Unavailable("ambiguous-authority");
        if (topology.PendingTransfer is not null)
            return Unavailable("transfer-in-progress");
        if (string.IsNullOrWhiteSpace(topology.EffectiveActiveNodeId))
            return Unavailable("active-not-established");

        var activeNodes = topology.Nodes
            .Where(node => node.NodeId.Equals(
                topology.EffectiveActiveNodeId,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (activeNodes.Length != 1)
            return Unavailable("active-node-not-unique");

        var active = activeNodes[0];
        if (active.State != RuntimeHaState.Active ||
            !active.Ready ||
            !active.Healthy ||
            !active.Fresh ||
            !active.HaLicenseEntitled)
        {
            return Unavailable("active-not-ready");
        }

        var isLocalActive = active.NodeId.Equals(
            topology.LocalNodeId,
            StringComparison.OrdinalIgnoreCase);
        var expectedProtectionStatus = isLocalActive ? "active" : "standby";
        if (!protectionStatus.Equals(expectedProtectionStatus, StringComparison.OrdinalIgnoreCase))
            return Unavailable("protection-not-authoritative");

        if (isLocalActive)
            return Local("local-node-is-effective-active");

        var localNodes = topology.Nodes
            .Where(node => node.NodeId.Equals(
                topology.LocalNodeId,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (localNodes.Length != 1 ||
            !localNodes[0].Fresh ||
            !localNodes[0].Healthy ||
            localNodes[0].State is RuntimeHaState.Faulted or RuntimeHaState.Isolated or RuntimeHaState.Maintenance)
        {
            return Unavailable("gateway-node-not-healthy");
        }

        // The local endpoint is the node's direct cluster-network address. The remote endpoint
        // is an optional externally reachable override and may resolve through the public ingress.
        // Routing to that ingress from behind it can return to this standby and form a loop.
        var localEndpoints = active.Endpoints
            .Where(endpoint => endpoint.Kind == RuntimeHaEndpointKind.Local)
            .ToArray();
        if (localEndpoints.Length != 1 ||
            !TryGetDestination(localEndpoints[0].Address, out var destination))
        {
            return Unavailable("active-endpoint-invalid");
        }

        if (destination.Scheme != Uri.UriSchemeHttps && !IsLoopback(destination.Host))
            return Unavailable("active-endpoint-transport-not-secure");

        if (HasSameOrigin(destination, requestScheme, requestHost))
            return Unavailable("active-endpoint-resolves-to-ingress");

        return new RuntimeHaProxyDecision(
            RuntimeHaProxyDisposition.ForwardToActive,
            "effective-active-is-remote",
            destination.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
                ? destination.AbsoluteUri
                : destination.AbsoluteUri + "/");
    }

    private static bool TryGetDestination(string? address, out Uri destination)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out destination!) ||
            !destination.IsWellFormedOriginalString() ||
            (destination.Scheme != Uri.UriSchemeHttp && destination.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(destination.UserInfo) ||
            !string.IsNullOrEmpty(destination.Query) ||
            !string.IsNullOrEmpty(destination.Fragment))
        {
            destination = null!;
            return false;
        }

        return true;
    }

    private static bool HasSameOrigin(Uri destination, string requestScheme, HostString requestHost)
    {
        if (!requestHost.HasValue ||
            !Uri.TryCreate($"{requestScheme}://{requestHost.Value}/", UriKind.Absolute, out var requestOrigin))
        {
            return false;
        }

        return Uri.Compare(
            destination,
            requestOrigin,
            UriComponents.SchemeAndServer,
            UriFormat.SafeUnescaped,
            StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static bool IsLoopback(string host) =>
        (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address)) ||
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

    private static RuntimeHaProxyDecision Local(string reason) =>
        new(RuntimeHaProxyDisposition.ContinueLocally, reason);

    private static RuntimeHaProxyDecision Unavailable(string reason) =>
        new(RuntimeHaProxyDisposition.Unavailable, reason);
}

internal sealed class RuntimeHaActiveRequestProxyMiddleware(
    RequestDelegate next,
    IHttpForwarder forwarder,
    HttpMessageInvoker outboundClient,
    IRuntimeHaProxyDecisionSource decisions,
    ILogger<RuntimeHaActiveRequestProxyMiddleware> logger)
{
    internal const string ForwardedHopHeader = "X-Scada-HA-Forwarded-Hop";
    private static readonly ForwarderRequestConfig RequestConfig = new()
    {
        ActivityTimeout = TimeSpan.FromMinutes(3)
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldRouteThroughActive(context.Request.Path))
        {
            await next(context);
            return;
        }

        RuntimeHaProxyDecision decision;
        try
        {
            decision = decisions.Resolve(context);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Runtime HA routing authority could not be evaluated; refusing local execution and forwarding.");
            await WriteUnavailableAsync(context, "authority-check-failed");
            return;
        }

        switch (decision.Disposition)
        {
            case RuntimeHaProxyDisposition.ContinueLocally:
                context.Request.Headers.Remove(ForwardedHopHeader);
                await next(context);
                return;

            case RuntimeHaProxyDisposition.Unavailable:
                await WriteUnavailableAsync(context, decision.ReasonCode);
                return;

            case RuntimeHaProxyDisposition.ForwardToActive:
                if (context.Request.Headers.ContainsKey(ForwardedHopHeader) ||
                    string.IsNullOrWhiteSpace(decision.DestinationPrefix))
                {
                    await WriteUnavailableAsync(context, "proxy-hop-not-unique");
                    return;
                }

                context.Request.Headers[ForwardedHopHeader] = "1";
                try
                {
                    var error = await forwarder.SendAsync(
                        context,
                        decision.DestinationPrefix,
                        outboundClient,
                        RequestConfig,
                        HttpTransformer.Default,
                        context.RequestAborted);
                    if (error != ForwarderError.None)
                    {
                        var detail = context.Features.Get<IForwarderErrorFeature>();
                        logger.LogWarning(
                            detail?.Exception,
                            "Runtime HA forwarding failed with {ForwarderError} ({ReasonCode}).",
                            error,
                            decision.ReasonCode);
                    }
                }
                finally
                {
                    context.Request.Headers.Remove(ForwardedHopHeader);
                }

                return;

            default:
                await WriteUnavailableAsync(context, "proxy-decision-invalid");
                return;
        }
    }

    private static bool ShouldRouteThroughActive(PathString path) =>
        (path.StartsWithSegments("/api") &&
         !path.StartsWithSegments("/api/runtime/ha")) ||
        path.StartsWithSegments("/ws/tags");

    private static async Task WriteUnavailableAsync(HttpContext context, string reasonCode)
    {
        if (context.Response.HasStarted)
        {
            context.Abort();
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.RetryAfter = "2";
        await context.Response.WriteAsJsonAsync(
            new
            {
                type = "about:blank",
                title = "Runtime indisponível durante a validação da autoridade HA.",
                status = StatusCodes.Status503ServiceUnavailable,
                code = "runtime-ha-route-unavailable",
                reasonCode
            },
            context.RequestAborted);
    }
}

internal static class RuntimeHaActiveRequestProxyComposition
{
    public static IServiceCollection AddRuntimeHaActiveRequestProxy(this IServiceCollection services)
    {
        services.AddHttpForwarder();
        services.AddSingleton<HttpMessageInvoker>(_ => new HttpMessageInvoker(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            EnableMultipleHttp2Connections = true,
            ConnectTimeout = TimeSpan.FromSeconds(5)
        }));
        services.AddSingleton<IRuntimeHaProxyDecisionSource, RuntimeHaProxyDecisionSource>();
        return services;
    }
}
