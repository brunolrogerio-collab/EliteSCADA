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

    public sealed record NativeAccessProfile(string Platform, int WebPort, int ApiPort, bool AdministrativeProfileAvailable,
        int? DatabasePort = null, int? MediaControlPort = null, int? MediaPlaybackPort = null);

    public static NativeAccessProfile? ReadNative(IConfiguration configuration)
    {
        var profile = configuration["InstallationNetwork:Profile"];
        if (profile is not ("native-windows" or "native-linux")) return null;
        if (!Uri.TryCreate(configuration["Kestrel:Endpoints:Web:Url"], UriKind.Absolute, out var web) ||
            !Uri.TryCreate(configuration["Kestrel:Endpoints:Api:Url"], UriKind.Absolute, out var api) ||
            web.Scheme is not ("http" or "https") || api.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(web.UserInfo) || !string.IsNullOrEmpty(api.UserInfo) ||
            !string.IsNullOrEmpty(web.Query) || !string.IsNullOrEmpty(api.Query) ||
            !string.IsNullOrEmpty(web.Fragment) || !string.IsNullOrEmpty(api.Fragment) ||
            web.AbsolutePath != "/" || api.AbsolutePath != "/" ||
            web.Port == api.Port || web.Port < 1 || api.Port < 1) return null;
        var platform = profile == "native-windows" ? "windows" : "linux";
        var available = false;
        int? databasePort = null, mediaControlPort = null, mediaPlaybackPort = null;
        try
        {
            var path = configuration["InstallationNetwork:AdministrativeProfilePath"];
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                var service = root.GetProperty("serviceName").GetString();
                var configPath = root.GetProperty("configurationFile").GetString();
                available = root.GetProperty("schema").GetString() == "scadivo-native-network-v1" &&
                    root.GetProperty("platform").GetString() == platform &&
                    root.GetProperty("endpointName").GetString() == "Web" &&
                    root.GetProperty("apiEndpointName").GetString() == "Api" &&
                    !string.IsNullOrWhiteSpace(service) &&
                    System.Text.RegularExpressions.Regex.IsMatch(service, @"\A[A-Za-z0-9_.-]+\z") &&
                    !string.IsNullOrWhiteSpace(configPath) && Path.IsPathFullyQualified(configPath) &&
                    File.Exists(configPath);
                if (available)
                {
                    using var persisted = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configPath!));
                    var endpoints = persisted.RootElement.GetProperty("Kestrel").GetProperty("Endpoints");
                    available = Uri.TryCreate(endpoints.GetProperty("Web").GetProperty("Url").GetString(), UriKind.Absolute, out var persistedWeb) &&
                        Uri.TryCreate(endpoints.GetProperty("Api").GetProperty("Url").GetString(), UriKind.Absolute, out var persistedApi) &&
                        persistedWeb == web && persistedApi == api;
                    if (available)
                    {
                        static bool RegisteredService(System.Text.Json.JsonElement section) =>
                            section.TryGetProperty("serviceName", out var serviceName) &&
                            serviceName.ValueKind == System.Text.Json.JsonValueKind.String &&
                            System.Text.RegularExpressions.Regex.IsMatch(serviceName.GetString() ?? "", @"\A[A-Za-z0-9_.-]+\z") &&
                            section.TryGetProperty("configurationFile", out var file) &&
                            file.ValueKind == System.Text.Json.JsonValueKind.String && File.Exists(file.GetString());
                        if (root.TryGetProperty("database", out var database) && RegisteredService(database) &&
                            root.TryGetProperty("topologyFile", out var topologyPath) && File.Exists(topologyPath.GetString()))
                        {
                            using var topology = System.Text.Json.JsonDocument.Parse(File.ReadAllText(topologyPath.GetString()!));
                            var state = topology.RootElement;
                            if (state.GetProperty("schemaVersion").GetInt32() == 1 &&
                                state.GetProperty("active").GetProperty("mode").GetString() == "LocalManaged" &&
                                (!state.TryGetProperty("pending", out var pending) || pending.ValueKind == System.Text.Json.JsonValueKind.Null))
                            {
                                var connection = new Npgsql.NpgsqlConnectionStringBuilder(configuration.GetConnectionString("EliteScada"));
                                if (connection.Host is "127.0.0.1" or "localhost" or "::1")
                                {
                                    var port = configuration.GetValue<int?>("DatabaseTopology:LocalManagedPort") ?? connection.Port;
                                    if (port is >= 1 and <= 65535) databasePort = port;
                                }
                            }
                        }
                        if (root.TryGetProperty("media", out var media) && RegisteredService(media) &&
                            Uri.TryCreate(configuration["MediaSources:GatewayControlUrl"], UriKind.Absolute, out var control) &&
                            Uri.TryCreate(configuration["MediaSources:GatewayPlaybackUrl"], UriKind.Absolute, out var playback) &&
                            control.Scheme is "http" or "https" && playback.Scheme is "http" or "https" &&
                            control.Host is "127.0.0.1" or "localhost" or "[::1]" && playback.Host is "127.0.0.1" or "localhost" or "[::1]" &&
                            control.Port > 0 && playback.Port > 0 && control.Port != playback.Port)
                        {
                            mediaControlPort = control.Port; mediaPlaybackPort = playback.Port;
                        }
                    }
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException or FormatException)
        {
            available = false;
            databasePort = null; mediaControlPort = null; mediaPlaybackPort = null;
        }
        return new(platform, web.Port, api.Port, available, databasePort, mediaControlPort, mediaPlaybackPort);
    }

    private static int ReadPort(IConfiguration configuration, string key, int fallback)
    {
        var raw = configuration[key];
        return int.TryParse(raw, out var parsed) && parsed is >= 1 and <= 65535
            ? parsed
            : fallback;
    }
}
