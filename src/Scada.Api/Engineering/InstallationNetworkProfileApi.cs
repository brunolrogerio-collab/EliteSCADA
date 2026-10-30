using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Security.Authorization;

namespace Scada.Api.Engineering;

public sealed record InstallationNetworkEndpointProfile(
    string Transport,
    string BindAddress,
    int PublishedPort,
    int InternalPort,
    bool PublishedToHost);

public sealed record InstallationNetworkProfileView(
    string Profile,
    string InstanceId,
    InstallationNetworkEndpointProfile Web,
    InstallationNetworkEndpointProfile Api,
    InstallationNetworkEndpointProfile Database,
    InstallationNetworkEndpointProfile MediaControl,
    InstallationNetworkEndpointProfile MediaPlayback,
    bool ApplyThroughUi,
    bool HostMappingIndependentlyVerified);

public static class InstallationNetworkProfileApi
{
    public static void MapInstallationNetworkProfileEndpoints(this WebApplication app)
    {
        app.MapInstallationServiceInventoryEndpoints();
        app.MapGet("/api/installation/native-network-profile", async (
            HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security,
            IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var authorization = await security.CheckRuntimeAsync(context, runtime,
                SecurityCapability.SystemAdmin, cancellationToken: cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;
            var profile = ReadNative(configuration);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        });
        app.MapGet("/api/installation/network-profile", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var authorization = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.SystemAdmin,
                cancellationToken: cancellationToken);
            var failure = authorization.FailureResult();
            if (failure is not null) return failure;

            var profile = Read(configuration);
            if (profile is null)
                return Results.NotFound(new { error = "This installation does not report a supported Docker network profile." });
            return Results.Ok(profile);
        });
    }

    public static InstallationNetworkProfileView? Read(IConfiguration configuration)
    {
        if (!string.Equals(configuration["InstallationNetwork:Profile"], "stable-docker", StringComparison.Ordinal))
            return null;

        var instanceId = configuration["InstallationNetwork:InstanceId"] ?? "elitescada-stable";
        var webPort = ReadPort(configuration, "InstallationNetwork:WebPublishedPort", 18080);
        var apiPort = ReadPort(configuration, "InstallationNetwork:ApiPublishedPort", 15120);
        var databasePort = ReadPort(configuration, "InstallationNetwork:DatabasePublishedPort", 15432);
        var mediaControlPort = ReadPort(configuration, "InstallationNetwork:MediaControlPort", 9997);
        var mediaPlaybackPort = ReadPort(configuration, "InstallationNetwork:MediaPlaybackPort", 8888);
        var webBind = configuration["InstallationNetwork:WebBindAddress"] ?? "127.0.0.1";
        var apiBind = configuration["InstallationNetwork:ApiBindAddress"] ?? "127.0.0.1";

        return new InstallationNetworkProfileView(
                "stable-docker",
                instanceId,
                new("TCP", webBind, webPort, 80, true),
                new("TCP", apiBind, apiPort, 5080, true),
                new("TCP", "127.0.0.1", databasePort, 5432, true),
                new("TCP", "Docker network only", mediaControlPort, mediaControlPort, false),
                new("TCP", "Docker network only", mediaPlaybackPort, mediaPlaybackPort, false),
                ApplyThroughUi: false,
                HostMappingIndependentlyVerified: false);
    }

    public sealed record NativeAccessProfile(string Platform, int WebPort, int ApiPort);

    public static NativeAccessProfile? ReadNative(IConfiguration configuration)
    {
        var profile = configuration["InstallationNetwork:Profile"];
        if (profile is not ("native-windows" or "native-linux")) return null;
        if (!Uri.TryCreate(configuration["Kestrel:Endpoints:Web:Url"], UriKind.Absolute, out var web) ||
            !Uri.TryCreate(configuration["Kestrel:Endpoints:Api:Url"], UriKind.Absolute, out var api) ||
            web.Scheme is not ("http" or "https") || api.Scheme is not ("http" or "https") ||
            web.Port == api.Port || web.Port < 1 || api.Port < 1) return null;
        return new(profile == "native-windows" ? "windows" : "linux", web.Port, api.Port);
    }

    private static int ReadPort(IConfiguration configuration, string key, int fallback)
    {
        var raw = configuration[key];
        return int.TryParse(raw, out var parsed) && parsed is >= 1 and <= 65535
            ? parsed
            : fallback;
    }
}
