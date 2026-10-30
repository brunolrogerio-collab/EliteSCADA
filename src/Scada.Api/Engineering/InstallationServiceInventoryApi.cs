using Npgsql;
using Scada.Api.Runtime;
using Scada.Api.Security;
using Scada.Security.Authorization;

namespace Scada.Api.Engineering;

public sealed record InstallationServiceEndpoint(string Service, string Source, string? Host, int? Port);

public static class InstallationServiceInventoryApi
{
    public static void MapInstallationServiceInventoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/installation/service-endpoints", async (
            HttpContext context, ScadaRuntimeFacade runtime, ApiAuthorizationService security,
            IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var authorization = await security.CheckRuntimeAsync(context, runtime,
                SecurityCapability.SystemAdmin, cancellationToken: cancellationToken);
            var failure = authorization.FailureResult();
            return failure ?? Results.Ok(Read(configuration));
        });
    }

    // Report configured endpoints only. No configuration value, credential, URI path,
    // or query is returned, and this inventory grants no host-management capability.
    public static IReadOnlyList<InstallationServiceEndpoint> Read(IConfiguration configuration)
    {
        var endpoints = new List<InstallationServiceEndpoint>();
        foreach (var endpoint in configuration.GetSection("Kestrel:Endpoints").GetChildren())
            AddUri(endpoints, "API", $"Kestrel:Endpoints:{endpoint.Key}:Url", endpoint["Url"]);
        if (endpoints.Count == 0)
            foreach (var url in (configuration["urls"] ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
                AddUri(endpoints, "API", "urls", url);
        if (endpoints.Count == 0) endpoints.Add(new("API", "not-reported", null, null));
        AddUri(endpoints, "Web", "InstallationNetwork:WebUrl", configuration["InstallationNetwork:WebUrl"]);
        AddUri(endpoints, "MediaControl", "MediaSources:GatewayControlUrl", configuration["MediaSources:GatewayControlUrl"]);
        AddUri(endpoints, "MediaPlayback", "MediaSources:GatewayPlaybackUrl", configuration["MediaSources:GatewayPlaybackUrl"]);
        foreach (var name in new[] { "EliteScada", "Historian" })
        {
            var connection = configuration.GetConnectionString(name);
            try
            {
                var parsed = string.IsNullOrWhiteSpace(connection) ? null : new NpgsqlConnectionStringBuilder(connection);
                endpoints.Add(new(name, $"ConnectionStrings:{name}", parsed?.Host, parsed?.Port));
            }
            catch (ArgumentException)
            {
                endpoints.Add(new(name, $"ConnectionStrings:{name}", null, null));
            }
        }
        return endpoints;
    }

    private static void AddUri(List<InstallationServiceEndpoint> endpoints, string service, string source, string? value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https"))
            endpoints.Add(new(service, source, uri.Host, uri.Port));
        else endpoints.Add(new(service, source, null, null));
    }
}