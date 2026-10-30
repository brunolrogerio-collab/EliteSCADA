using System.Net;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Drivers.Abstractions;
using Scada.Drivers.Modbus;
using Scada.Security.Authorization;

namespace Scada.Api.Engineering;

public sealed record InstallationNetworkEndpointProfile(
    string Transport,
    string BindAddress,
    int PublishedPort,
    int InternalPort,
    bool PublishedToHost);

public sealed record InstallationNetworkDriverPortProfile(
    string Transport,
    string BindAddress,
    int PublishedPort,
    int InternalPort,
    bool PublishedToHost);

public sealed record InstallationNetworkConfiguredDriverListener(
    string DataSourceName,
    string Transport,
    string BindAddress,
    int InternalPort,
    string State);

public sealed record InstallationNetworkProfileView(
    string Profile,
    string InstanceId,
    InstallationNetworkEndpointProfile Web,
    InstallationNetworkEndpointProfile Api,
    InstallationNetworkEndpointProfile Database,
    InstallationNetworkEndpointProfile MediaControl,
    InstallationNetworkEndpointProfile MediaPlayback,
    IReadOnlyList<InstallationNetworkDriverPortProfile> DriverPorts,
    IReadOnlyList<InstallationNetworkConfiguredDriverListener> ConfiguredDriverListeners,
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

            var profile = Read(configuration, runtime.Describe().CommunicationDrivers);
            if (profile is null)
                return Results.NotFound(new { error = "This installation does not report a supported Docker network profile." });
            return Results.Ok(profile);
        });
    }

    public static InstallationNetworkProfileView? Read(
        IConfiguration configuration,
        IReadOnlyCollection<CommunicationDriverDiagnosticSnapshot>? runtimeDrivers = null)
    {
        if (!string.Equals(configuration["InstallationNetwork:Profile"], "stable-docker", StringComparison.Ordinal))
            return null;

        var instanceId = configuration["InstallationNetwork:InstanceId"] ?? "elitescada-stable";
        var webPort = ReadPort(configuration, "InstallationNetwork:WebPublishedPort", 18080);
        var apiPort = ReadPort(configuration, "InstallationNetwork:ApiPublishedPort", 15080);
        var databasePort = ReadPort(configuration, "InstallationNetwork:DatabasePublishedPort", 15432);
        var webBind = configuration["InstallationNetwork:WebBindAddress"] ?? "127.0.0.1";
        var apiBind = configuration["InstallationNetwork:ApiBindAddress"] ?? "127.0.0.1";
        var driverPorts = ReadDriverPorts(configuration["InstallationNetwork:DriverPorts"]);

        return new InstallationNetworkProfileView(
                "stable-docker",
                instanceId,
                new("TCP", webBind, webPort, 80, true),
                new("TCP", apiBind, apiPort, 5080, true),
                new("TCP", "127.0.0.1", databasePort, 5432, true),
                new("TCP", "Docker network only", 9997, 9997, false),
                new("TCP", "Docker network only", 8888, 8888, false),
                driverPorts,
                ReadConfiguredDriverListeners(runtimeDrivers),
                ApplyThroughUi: false,
                HostMappingIndependentlyVerified: false);
    }

    private static IReadOnlyList<InstallationNetworkConfiguredDriverListener> ReadConfiguredDriverListeners(
        IReadOnlyCollection<CommunicationDriverDiagnosticSnapshot>? runtimeDrivers)
    {
        if (runtimeDrivers is null || runtimeDrivers.Count == 0)
            return Array.Empty<InstallationNetworkConfiguredDriverListener>();

        var listeners = new List<InstallationNetworkConfiguredDriverListener>();
        foreach (var driver in runtimeDrivers)
        {
            var rawEndpoint = driver.Endpoint;
            if (!string.Equals(
                    driver.DriverType,
                    ModbusTcpServerDriverDescriptorProvider.DriverTypeId,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(rawEndpoint) ||
                !IPEndPoint.TryParse(rawEndpoint, out var endpoint))
                continue;

            listeners.Add(new InstallationNetworkConfiguredDriverListener(
                driver.DataSourceName,
                "TCP",
                endpoint.Address.ToString(),
                endpoint.Port,
                driver.State.ToString()));
        }

        return listeners;
    }

    private static int ReadPort(IConfiguration configuration, string key, int fallback)
    {
        var raw = configuration[key];
        return int.TryParse(raw, out var parsed) && parsed is >= 1 and <= 65535
            ? parsed
            : fallback;
    }

    private static IReadOnlyList<InstallationNetworkDriverPortProfile> ReadDriverPorts(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<InstallationNetworkDriverPortProfile>();
        var result = new List<InstallationNetworkDriverPortProfile>();
        foreach (var item in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = item.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 4 ||
                parts[0] is not ("tcp" or "udp") ||
                parts[1] is not ("127.0.0.1" or "0.0.0.0") ||
                !int.TryParse(parts[2], out var publishedPort) || publishedPort is < 1 or > 65535 ||
                !int.TryParse(parts[3], out var internalPort) || internalPort is < 1024 or > 65535 ||
                parts[0] == "tcp" && internalPort == 5080)
                continue;

            result.Add(new InstallationNetworkDriverPortProfile(
                parts[0].ToUpperInvariant(), parts[1], publishedPort, internalPort, true));
        }

        return result;
    }
}
