using System.Reflection;

namespace Scada.Api.Product;

public sealed record ProductIdentityView(
    string ProductName,
    string Channel,
    string Version,
    string DisplayVersion,
    string InformationalVersion,
    string? BuildCommit);

public static class ProductIdentityApi
{
    public static IEndpointRouteBuilder MapProductIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/product/info", () =>
            Results.Ok(Describe(typeof(ProductIdentityApi).Assembly)));

        return endpoints;
    }

    internal static ProductIdentityView Describe(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var metadata = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Value ?? string.Empty, StringComparer.Ordinal);

        var productName = RequiredMetadata(metadata, "EliteScadaProductName");
        var channel = RequiredMetadata(metadata, "EliteScadaProductChannel");
        var version = RequiredMetadata(metadata, "EliteScadaProductVersion");
        var informationalVersion =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? $"{productName} {channel} {version}";

        return new ProductIdentityView(
            productName,
            channel,
            version,
            $"{productName} {channel} {version}",
            informationalVersion,
            ResolveBuildCommit(informationalVersion));
    }

    private static string RequiredMetadata(
        IReadOnlyDictionary<string, string> metadata,
        string key)
    {
        if (metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value.Trim();

        throw new InvalidOperationException(
            $"Required EliteSCADA product identity metadata '{key}' is missing.");
    }

    private static string? ResolveBuildCommit(string informationalVersion)
    {
        var explicitCommit = Environment.GetEnvironmentVariable("ELITESCADA_BUILD_COMMIT");
        if (!string.IsNullOrWhiteSpace(explicitCommit))
            return explicitCommit.Trim();

        var githubCommit = Environment.GetEnvironmentVariable("GITHUB_SHA");
        if (!string.IsNullOrWhiteSpace(githubCommit))
            return githubCommit.Trim();

        var plus = informationalVersion.LastIndexOf('+');
        if (plus >= 0 && plus + 1 < informationalVersion.Length)
        {
            var suffix = informationalVersion[(plus + 1)..].Trim();
            if (suffix.Length >= 7 && suffix.All(char.IsAsciiHexDigit))
                return suffix;
        }

        return null;
    }
}
