using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;
using Scada.Api.Runtime;

namespace Scada.Drivers.Tests;

public sealed class RuntimeHaActiveRequestProxyTests
{
    [Fact]
    public void Policy_UsesLocalActiveAndForwardsStandbyToRecognizedActive()
    {
        var localActive = RuntimeHaActiveRequestProxyPolicy.Resolve(
            CreateTopology(localNodeId: "node-a"),
            "active",
            "https",
            new HostString("cluster.example.test"));
        var standby = RuntimeHaActiveRequestProxyPolicy.Resolve(
            CreateTopology(),
            "standby",
            "https",
            new HostString("cluster.example.test"));

        Assert.Equal(RuntimeHaProxyDisposition.ContinueLocally, localActive.Disposition);
        Assert.Equal(RuntimeHaProxyDisposition.ForwardToActive, standby.Disposition);
        Assert.Equal("http://127.0.0.1:5080/", standby.DestinationPrefix);
    }

    [Fact]
    public void Policy_FailsClosedForUnresolvedOrUnsafeAuthority()
    {
        var topology = CreateTopology();
        var cases = new[]
        {
            (topology with { EffectiveActiveNodeId = null }, "standby"),
            (topology with { AmbiguousAuthority = true }, "standby"),
            (topology with
            {
                PendingTransfer = new RuntimeHaTransferOperation(
                    Guid.NewGuid(), "node-a", "node-b", 2, DateTimeOffset.UtcNow)
            }, "standby"),
            (WithNode(topology, "node-a", node => node with { Fresh = false }), "standby"),
            (WithNode(topology, "node-a", node => node with { Ready = false }), "standby"),
            (topology, "blocked"),
            (topology, "degraded"),
            (topology, "disabled"),
            (WithNode(topology, "node-a", node => node with
            {
                Endpoints = new[]
                {
                    new RuntimeHaEndpoint(RuntimeHaEndpointKind.Local, "http://active.example.test:5080", 0)
                }
            }), "standby"),
            (WithNode(topology, "node-a", node => node with
            {
                Endpoints = new[]
                {
                    new RuntimeHaEndpoint(RuntimeHaEndpointKind.Local, "https://user:password@active.example.test", 0)
                }
            }), "standby")
        };

        foreach (var (candidate, protectionStatus) in cases)
        {
            var decision = RuntimeHaActiveRequestProxyPolicy.Resolve(
                candidate,
                protectionStatus,
                "https",
                new HostString("cluster.example.test"));

            Assert.Equal(RuntimeHaProxyDisposition.Unavailable, decision.Disposition);
        }
    }

    [Fact]
    public void Policy_RejectsAnActiveEndpointThatIsTheCurrentClusterIngress()
    {
        var topology = WithNode(CreateTopology(), "node-a", node => node with
        {
            Endpoints = new[]
            {
                new RuntimeHaEndpoint(RuntimeHaEndpointKind.Local, "https://cluster.example.test", 0)
            }
        });

        var decision = RuntimeHaActiveRequestProxyPolicy.Resolve(
            topology,
            "standby",
            "https",
            new HostString("cluster.example.test"));

        Assert.Equal(RuntimeHaProxyDisposition.Unavailable, decision.Disposition);
        Assert.Equal("active-endpoint-resolves-to-ingress", decision.ReasonCode);
    }

    [Fact]
    public void Policy_FollowsTheNewActiveAfterAuthorityChanges()
    {
        var failedOver = CreateTopology(localNodeId: "node-a") with
        {
            EffectiveActiveNodeId = "node-b",
            Nodes = new[]
            {
                CreateTopology().Nodes.Single(node => node.NodeId == "node-a") with
                {
                    Role = "standby",
                    State = RuntimeHaState.ReadyStandby
                },
                CreateTopology().Nodes.Single(node => node.NodeId == "node-b") with
                {
                    Role = "active",
                    State = RuntimeHaState.Active
                }
            }
        };

        var decision = RuntimeHaActiveRequestProxyPolicy.Resolve(
            failedOver,
            "standby",
            "https",
            new HostString("cluster.example.test"));

        Assert.Equal(RuntimeHaProxyDisposition.ForwardToActive, decision.Disposition);
        Assert.Equal("http://127.0.0.1:5081/", decision.DestinationPrefix);
    }

    [Fact]
    public async Task Middleware_ForwardsHttpCookiesAndWebSocketWithoutChangingBrowserOrigin()
    {
        await using var active = await StartActiveServerAsync();
        await using var gateway = await StartGatewayAsync(new RuntimeHaProxyDecision(
            RuntimeHaProxyDisposition.ForwardToActive,
            "effective-active-is-remote",
            active.Address.TrimEnd('/') + "/"));

        using var client = new HttpClient { BaseAddress = new Uri(gateway.Address) };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/runtime/probe?case=ha")
        {
            Content = new StringContent("runtime-body", Encoding.UTF8, "text/plain")
        };
        request.Headers.TryAddWithoutValidation("Cookie", "scada-session=valid");

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("POST|case=ha|scada-session=valid|runtime-body|", body);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith("scada-session=rotated", StringComparison.Ordinal));

        using var socket = new ClientWebSocket();
        var webSocketAddress = new Uri(
            gateway.Address.Replace("http://", "ws://", StringComparison.Ordinal).TrimEnd('/') + "/ws/tags");
        await socket.ConnectAsync(webSocketAddress, CancellationToken.None);
        var sent = Encoding.UTF8.GetBytes("tag-update");
        await socket.SendAsync(sent, WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[64];
        var received = await socket.ReceiveAsync(buffer, CancellationToken.None);

        Assert.Equal(WebSocketMessageType.Text, received.MessageType);
        Assert.Equal("tag-update", Encoding.UTF8.GetString(buffer, 0, received.Count));
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
    }

    [Fact]
    public async Task Middleware_FailsClosedAndDoesNotInvokeLocalHandlerWhenAuthorityIsUnavailable()
    {
        var nextCalls = 0;
        await using var gateway = await StartGatewayAsync(
            new RuntimeHaProxyDecision(RuntimeHaProxyDisposition.Unavailable, "ambiguous-authority"),
            configureEndpoints: app => app.MapGet("/api/runtime/probe", () =>
            {
                Interlocked.Increment(ref nextCalls);
                return Results.Ok("must-not-run");
            }));
        using var client = new HttpClient { BaseAddress = new Uri(gateway.Address) };

        using var response = await client.GetAsync("/api/runtime/probe");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("ambiguous-authority", body, StringComparison.Ordinal);
        Assert.Equal(0, nextCalls);
    }

    [Fact]
    public async Task Middleware_LeavesHaAdministrationAndHealthOnTheReceivingNode()
    {
        await using var gateway = await StartGatewayAsync(
            new RuntimeHaProxyDecision(RuntimeHaProxyDisposition.ForwardToActive, "active", "http://127.0.0.1:1/"),
            configureEndpoints: app =>
            {
                app.MapGet("/api/runtime/ha/topology", () => Results.Text("local-ha-topology"));
                app.MapGet("/health", () => Results.Text("local-health"));
            });
        using var client = new HttpClient { BaseAddress = new Uri(gateway.Address) };

        Assert.Equal("local-ha-topology", await client.GetStringAsync("/api/runtime/ha/topology"));
        Assert.Equal("local-health", await client.GetStringAsync("/health"));
    }

    [Fact]
    public async Task Middleware_RejectsForwardedHopInsteadOfCreatingAProxyLoop()
    {
        var nextCalls = 0;
        await using var gateway = await StartGatewayAsync(
            new RuntimeHaProxyDecision(RuntimeHaProxyDisposition.ForwardToActive, "effective-active-is-remote", "http://127.0.0.1:1/"),
            configureEndpoints: app => app.MapGet("/api/runtime/probe", () =>
            {
                Interlocked.Increment(ref nextCalls);
                return Results.Ok("must-not-run");
            }));
        using var client = new HttpClient { BaseAddress = new Uri(gateway.Address) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/runtime/probe");
        request.Headers.TryAddWithoutValidation(RuntimeHaActiveRequestProxyMiddleware.ForwardedHopHeader, "1");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, nextCalls);
    }

    [Fact]
    public async Task Middleware_FailsClosedWhenAuthorityCannotBeRead()
    {
        var nextCalls = 0;
        await using var gateway = await StartGatewayAsync(
            new RuntimeHaProxyDecision(RuntimeHaProxyDisposition.ContinueLocally, "unused"),
            configureEndpoints: app => app.MapGet("/api/runtime/probe", () =>
            {
                Interlocked.Increment(ref nextCalls);
                return Results.Ok("must-not-run");
            }),
            decisionSource: new ThrowingDecisionSource());
        using var client = new HttpClient { BaseAddress = new Uri(gateway.Address) };

        using var response = await client.GetAsync("/api/runtime/probe");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, nextCalls);
    }

    [Fact]
    public async Task Middleware_DoesNotRetryLocallyWhenTheRecognizedActiveCannotBeReached()
    {
        var nextCalls = 0;
        await using var gateway = await StartGatewayAsync(
            new RuntimeHaProxyDecision(RuntimeHaProxyDisposition.ForwardToActive, "effective-active-is-remote", "http://127.0.0.1:1/"),
            configureEndpoints: app => app.MapGet("/api/runtime/probe", () =>
            {
                Interlocked.Increment(ref nextCalls);
                return Results.Ok("must-not-run");
            }));
        using var client = new HttpClient { BaseAddress = new Uri(gateway.Address) };

        using var response = await client.GetAsync("/api/runtime/probe");

        Assert.True(
            response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout,
            $"Expected the forwarding error to remain a 5xx, got {(int)response.StatusCode}.");
        Assert.Equal(0, nextCalls);
    }

    private static async Task<RunningServer> StartActiveServerAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Test",
            Args = []
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        AddProxyServices(builder, new RuntimeHaProxyDecision(
            RuntimeHaProxyDisposition.ContinueLocally,
            "local-node-is-effective-active"));
        var app = builder.Build();
        app.UseWebSockets();
        app.UseMiddleware<RuntimeHaActiveRequestProxyMiddleware>();
        app.MapPost("/api/runtime/probe", async context =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync(context.RequestAborted);
            var cookie = context.Request.Headers.Cookie.ToString();
            var hop = context.Request.Headers[RuntimeHaActiveRequestProxyMiddleware.ForwardedHopHeader].ToString();
            context.Response.StatusCode = StatusCodes.Status201Created;
            context.Response.Headers.Append("Set-Cookie", "scada-session=rotated; HttpOnly; SameSite=Lax");
            await context.Response.WriteAsync(
                $"{context.Request.Method}|{context.Request.QueryString.Value?.TrimStart('?')}|{cookie}|{body}|{hop}",
                context.RequestAborted);
        });
        app.Map("/ws/tags", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var bytes = new byte[64];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(bytes, context.RequestAborted);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseOutputAsync(
                        result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                        result.CloseStatusDescription,
                        context.RequestAborted);
                    break;
                }

                await socket.SendAsync(
                    bytes.AsMemory(0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    context.RequestAborted);
            }
        });
        await app.StartAsync();
        return new RunningServer(app, GetAddress(app));
    }

    private static async Task<RunningServer> StartGatewayAsync(
        RuntimeHaProxyDecision decision,
        Action<WebApplication>? configureEndpoints = null,
        IRuntimeHaProxyDecisionSource? decisionSource = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Test",
            Args = []
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        AddProxyServices(builder, decisionSource ?? new FixedDecisionSource(decision));
        var app = builder.Build();
        app.UseWebSockets();
        app.UseMiddleware<RuntimeHaActiveRequestProxyMiddleware>();
        configureEndpoints?.Invoke(app);
        await app.StartAsync();
        return new RunningServer(app, GetAddress(app));
    }

    private static void AddProxyServices(
        WebApplicationBuilder builder,
        RuntimeHaProxyDecision decision)
        => AddProxyServices(builder, new FixedDecisionSource(decision));

    private static void AddProxyServices(
        WebApplicationBuilder builder,
        IRuntimeHaProxyDecisionSource decisionSource)
    {
        builder.Services.AddHttpForwarder();
        builder.Services.AddSingleton<HttpMessageInvoker>(_ => new HttpMessageInvoker(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(2)
        }));
        builder.Services.AddSingleton(decisionSource);
    }

    private static string GetAddress(WebApplication app) =>
        app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();

    private static RuntimeHaTopologySnapshot CreateTopology(string localNodeId = "node-b")
    {
        var observedAt = DateTimeOffset.UtcNow;
        var runtime = new RuntimeHaRuntimeIdentity("runtime", "project-a", 7);
        return new RuntimeHaTopologySnapshot(
            RuntimeHaTopologyDefinition.Schema,
            RuntimeHaTopologyDefinition.SchemaVersion,
            Enabled: true,
            ClusterId: "cluster-a",
            TopologyVersion: 4,
            StateVersion: 12,
            AuthorityEpoch: 2,
            AuthorityInstanceId: Guid.NewGuid(),
            LocalNodeId: localNodeId,
            EffectiveActiveNodeId: "node-a",
            AmbiguousAuthority: false,
            PendingTransfer: null,
            GeneratedAtUtc: observedAt,
            Nodes: new[]
            {
                new RuntimeHaNodeSnapshot(
                    "node-a", "active", RuntimeHaState.Active, true, true, true, true,
                    runtime, observedAt, true, null,
                    new[]
                    {
                        new RuntimeHaEndpoint(RuntimeHaEndpointKind.Local, "http://127.0.0.1:5080", 0),
                        new RuntimeHaEndpoint(RuntimeHaEndpointKind.Remote, "https://active.example.test", 1)
                    }),
                new RuntimeHaNodeSnapshot(
                    "node-b", "standby", RuntimeHaState.ReadyStandby, true, true, true, true,
                    runtime, observedAt, true, null,
                    new[]
                    {
                        new RuntimeHaEndpoint(RuntimeHaEndpointKind.Local, "http://127.0.0.1:5081", 0),
                        new RuntimeHaEndpoint(RuntimeHaEndpointKind.Remote, "https://standby.example.test", 1)
                    })
            });
    }

    private static RuntimeHaTopologySnapshot WithNode(
        RuntimeHaTopologySnapshot topology,
        string nodeId,
        Func<RuntimeHaNodeSnapshot, RuntimeHaNodeSnapshot> update) => topology with
        {
            Nodes = topology.Nodes.Select(node => node.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase)
                ? update(node)
                : node).ToArray()
        };

    private sealed class FixedDecisionSource(RuntimeHaProxyDecision decision) : IRuntimeHaProxyDecisionSource
    {
        public RuntimeHaProxyDecision Resolve(HttpContext context) => decision;
    }

    private sealed class ThrowingDecisionSource : IRuntimeHaProxyDecisionSource
    {
        public RuntimeHaProxyDecision Resolve(HttpContext context) =>
            throw new InvalidOperationException("synthetic authority observation failure");
    }

    private sealed class RunningServer(WebApplication app, string address) : IAsyncDisposable
    {
        public string Address { get; } = address;

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
