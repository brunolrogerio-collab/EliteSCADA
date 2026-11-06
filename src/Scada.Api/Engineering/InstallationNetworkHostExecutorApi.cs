using System.Net;
using System.Net.Sockets;
using System.IO.Pipes;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Security.Authorization;

namespace Scada.Api.Engineering;

// Installation-scoped IPC only. The public API never accepts an executable,
// service name, filesystem path, working directory or shell arguments.
public sealed record InstallationHostPortPlan(string Schema, string Module, Dictionary<string, int> Ports);
public sealed record InstallationHostPlanConfirmation(string Schema, string PlanId, bool Confirm);
public sealed record InstallationHostRecoveryConfirmation(string Schema, bool Confirm);

public static class InstallationNetworkHostExecutorApi
{
    public const string PlanSchema = "scadivo-network-port-plan-v1";
    private const string Prefix = "/api/installation/host-network";

    public static void MapInstallationHostNetworkEndpoints(this WebApplication app)
    {
        app.MapGet(Prefix + "/status", async (
            HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security,
            IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var failure = await Authorize(context, runtime, security, cancellationToken);
            return failure ?? await HostExecutorClient.SendAsync(configuration, "GET", "/v1/status", null, cancellationToken);
        });

        app.MapPost(Prefix + "/preflight", async (
            InstallationHostPortPlan plan,
            HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security,
            IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var failure = await Authorize(context, runtime, security, cancellationToken);
            if (failure is not null) return failure;
            if (!TryValidatePlan(plan, out var error))
                return Results.BadRequest(new { error });
            // Rebuild the payload with an allowlist to prevent unexpected fields
            // from crossing the trusted API -> host boundary.
            return await HostExecutorClient.SendAsync(configuration, "POST", "/v1/preflight",
                new { schema = PlanSchema, module = plan.Module, ports = plan.Ports }, cancellationToken);
        });

        app.MapPost(Prefix + "/apply", async (
            InstallationHostPlanConfirmation request,
            HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security,
            IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var failure = await Authorize(context, runtime, security, cancellationToken);
            if (failure is not null) return failure;
            if (request.Schema != PlanSchema || !request.Confirm ||
                request.PlanId is null || request.PlanId.Length != 32 ||
                request.PlanId.Any(c => !Uri.IsHexDigit(c)))
                return Results.BadRequest(new { error = "A confirmed prepared plan ID is required." });
            return await HostExecutorClient.SendAsync(configuration, "POST", "/v1/apply",
                new { schema = PlanSchema, planId = request.PlanId, confirm = true }, cancellationToken);
        });

        app.MapPost(Prefix + "/recover", async (
            InstallationHostRecoveryConfirmation request,
            HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security,
            IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var failure = await Authorize(context, runtime, security, cancellationToken);
            if (failure is not null) return failure;
            if (request.Schema != PlanSchema || !request.Confirm)
                return Results.BadRequest(new { error = "Explicit recovery confirmation is required." });
            return await HostExecutorClient.SendAsync(configuration, "POST", "/v1/recover",
                new { schema = PlanSchema, confirm = true }, cancellationToken);
        });
    }

    private static async Task<IResult?> Authorize(
        HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security,
        CancellationToken token)
    {
        var authorization = await security.CheckRuntimeAsync(
            context, runtime, SecurityCapability.SystemAdmin, cancellationToken: token);
        return authorization.FailureResult();
    }

    public static bool TryValidatePlan(InstallationHostPortPlan? plan, out string error)
    {
        error = "Invalid or unsupported installation host port plan.";
        if (plan is null || plan.Schema != PlanSchema || plan.Ports is null || plan.Module is null)
            return false;
        string[] expected = plan.Module switch
        {
            "access" => ["webPort", "apiPort"],
            "database" => ["databasePort"],
            "media" => ["mediaControlPort", "mediaPlaybackPort"],
            _ => []
        };
        if (expected.Length == 0 || plan.Ports.Count != expected.Length ||
            expected.Any(key => !plan.Ports.ContainsKey(key)) ||
            plan.Ports.Values.Any(port => port is < 1 or > 65535) ||
            plan.Ports.Values.Distinct().Count() != plan.Ports.Count)
            return false;
        error = string.Empty;
        return true;
    }
}

internal static class HostExecutorClient
{
    private const int MaxResponseBytes = 65536;

    public static async Task<IResult> SendAsync(IConfiguration configuration, string method, string route,
        object? request, CancellationToken cancellationToken)
    {
        var socketPath = configuration["InstallationNetwork:HostExecutorSocket"];
        var pipeName = configuration["InstallationNetwork:HostExecutorPipe"];
        var token = configuration["InstallationNetwork:HostExecutorToken"];
        var tokenFile = configuration["InstallationNetwork:HostExecutorTokenFile"];
        if (!string.IsNullOrWhiteSpace(tokenFile))
        {
            // Host-owned token file can be mounted read-only into the API container.
            // Never expose the file or token through an API response.
            if (!Path.IsPathFullyQualified(tokenFile))
                return Results.Json(new { error = "Invalid installation executor credential configuration." }, statusCode: 503);
            try { token = (await File.ReadAllTextAsync(tokenFile, cancellationToken)).Trim(); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return Results.Json(new { error = "Protected installation executor credential is unavailable." }, statusCode: 503);
            }
        }

        var linuxSocket = OperatingSystem.IsLinux() && !string.IsNullOrWhiteSpace(socketPath) &&
                          Path.IsPathFullyQualified(socketPath) && File.Exists(socketPath);
        var windowsPipe = OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(pipeName) &&
                          Regex.IsMatch(pipeName, @"\A[a-zA-Z0-9_-]{1,128}\z");
        if ((!linuxSocket && !windowsPipe) || string.IsNullOrWhiteSpace(token) ||
            Encoding.UTF8.GetByteCount(token) is < 32 or > 256)
            return Results.Json(new
            {
                error = "No registered compatible host executor is available for this installation. Use the protected host administrator procedure."
            }, statusCode: 503);

        try
        {
            using var handler = new SocketsHttpHandler
            {
                UseProxy = false,
                ConnectCallback = async (_, ct) =>
                {
                    if (windowsPipe)
                    {
                        var pipe = new NamedPipeClientStream(".", pipeName!, PipeDirection.InOut, PipeOptions.Asynchronous);
                        try
                        {
                            await pipe.ConnectAsync(5000, ct);
                            return pipe;
                        }
                        catch { await pipe.DisposeAsync(); throw; }
                    }
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    try
                    {
                        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath!), ct);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch { socket.Dispose(); throw; }
                }
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
            using var message = new HttpRequestMessage(new HttpMethod(method), "http://localhost" + route);
            message.Headers.TryAddWithoutValidation("X-Scadivo-Installation-Token", token);
            if (request is not null)
                message.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(
                message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            while (true)
            {
                var count = await stream.ReadAsync(chunk, cancellationToken);
                if (count == 0) break;
                if (buffer.Length + count > MaxResponseBytes)
                    return Results.Json(new { error = "Host executor response exceeded its safe limit." }, statusCode: 503);
                buffer.Write(chunk, 0, count);
            }
            var json = Encoding.UTF8.GetString(buffer.ToArray());
            using var parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Executor response must be an object.");
            return Results.Content(json, "application/json", statusCode: (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or IOException or SocketException or
                                     TaskCanceledException or JsonException or ArgumentException or UnauthorizedAccessException or TimeoutException)
        {
            return Results.Json(new
            {
                error = "Host executor is unreachable or incompatible; no successful application is assumed. Check the registered local service and its protected operation record."
            }, statusCode: 503);
        }
    }
}
