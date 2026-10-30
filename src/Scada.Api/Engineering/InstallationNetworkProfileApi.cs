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
        var apiPort = ReadPort(configuration, "InstallationNetwork:ApiPublishedPort", 15080);
        var databasePort = ReadPort(configuration, "InstallationNetwork:DatabasePublishedPort", 15432);
        var webBind = configuration["InstallationNetwork:WebBindAddress"] ?? "127.0.0.1";
        var apiBind = configuration["InstallationNetwork:ApiBindAddress"] ?? "127.0.0.1";

        return new InstallationNetworkProfileView(
                "stable-docker",
                instanceId,
                new("TCP", webBind, webPort, 80, true),
                new("TCP", apiBind, apiPort, 5080, true),
                new("TCP", "127.0.0.1", databasePort, 5432, true),
                new("TCP", "Docker network only", 9997, 9997, false),
                new("TCP", "Docker network only", 8888, 8888, false),
                ApplyThroughUi: false,
                HostMappingIndependentlyVerified: false);
    }

    private static int ReadPort(IConfiguration configuration, string key, int fallback)
    {
        var raw = configuration[key];
        return int.TryParse(raw, out var parsed) && parsed is >= 1 and <= 65535
            ? parsed
            : fallback;
    }
}
