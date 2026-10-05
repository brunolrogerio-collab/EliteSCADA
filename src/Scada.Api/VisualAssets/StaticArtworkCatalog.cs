using System.Collections.Concurrent;
using System.Text.Json;
using Scada.Api.Persistence;
using Scada.Api.Security;
using Scada.Engineering.VisualAssets;

namespace Scada.Api.VisualAssets;

/// <summary>Original factory review candidates, not falsely approved product built-ins.</summary>
public sealed class StaticArtworkCatalog
{
    public sealed record Entry(string Id, string Key, string Name, string Category, string Style, string Status, string[] Tags);
    private sealed record Resource(Entry Entry, string ResourceName);
    private readonly Dictionary<string, Resource> resources = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte[]> sanitized = new(StringComparer.Ordinal);
    private const string Prefix = "EliteSCADA.StaticArtwork.";
    public IReadOnlyList<Entry> Entries { get; }

    public StaticArtworkCatalog()
    {
        var assembly = typeof(StaticArtworkCatalog).Assembly;
        var names = assembly.GetManifestResourceNames().ToDictionary(name => name.Replace('\\', '/'), StringComparer.Ordinal);
        foreach (var batchName in names.Keys.Where(name => name.StartsWith(Prefix, StringComparison.Ordinal) && name.EndsWith(".batch.json", StringComparison.Ordinal))) {
            using var stream = assembly.GetManifestResourceStream(names[batchName])!;
            using var batch = JsonDocument.Parse(stream);
            var defaults = batch.RootElement.GetProperty("defaults");
            foreach (var item in batch.RootElement.GetProperty("assets").EnumerateArray()) {
                var provenance = item.GetProperty("provenance").GetProperty("classification").GetString();
                var license = item.TryGetProperty("license", out var specific) ? specific : defaults.GetProperty("license");
                var status = item.GetProperty("status").GetString() ?? "draft";
                if (provenance is not ("generated/original" or "original-elitescada") || status is "rejected" or "deprecated" ||
                    !license.GetProperty("commercialRedistributionAllowed").GetBoolean()) continue;
                var source = item.GetProperty("sourceFile").GetString()!;
                const string sourcePrefix = "assets/sources/";
                if (!source.StartsWith(sourcePrefix, StringComparison.Ordinal)) throw new InvalidDataException("Invalid factory resource path.");
                var resourceName = Prefix + source[sourcePrefix.Length..];
                if (!names.TryGetValue(resourceName, out var embedded)) throw new InvalidDataException("Missing factory resource.");
                var entry = new Entry(item.GetProperty("id").GetString()!, item.GetProperty("key").GetString()!,
                    item.GetProperty("displayName").GetString()!, item.GetProperty("categoryPath").GetString()!,
                    item.TryGetProperty("styleFamily", out var style) ? style.GetString()! : defaults.GetProperty("styleFamily").GetString()!, status,
                    item.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()!).ToArray());
                resources.Add(entry.Id, new Resource(entry, embedded));
            }
        }
        Entries = resources.Values.Select(resource => resource.Entry).OrderBy(entry => entry.Category).ThenBy(entry => entry.Name).ToArray();
    }

    public byte[]? Content(string id) => resources.TryGetValue(id, out var resource) ? sanitized.GetOrAdd(id, _ => {
        using var stream = typeof(StaticArtworkCatalog).Assembly.GetManifestResourceStream(resource.ResourceName)!;
        using var buffer = new MemoryStream(); stream.CopyTo(buffer);
        return StaticSvgInspector.InspectAndSanitize(buffer.ToArray()).CanonicalContent;
    }) : null;
}

public static class StaticArtworkEndpoints
{
    public static IEndpointRouteBuilder MapStaticArtworkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/engineering/static-artwork", (StaticArtworkCatalog catalog) => Results.Ok(new {
            origin = "EliteSCADA factory", reviewRequired = true, entries = catalog.Entries
        })).RequireWorkspaceEngineeringRead();
        endpoints.MapGet("/api/engineering/static-artwork/{id}/content", (string id, StaticArtworkCatalog catalog, HttpContext context) => {
            var content = catalog.Content(id);
            if (content is null) return Results.NotFound();
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
            context.Response.Headers.CacheControl = "private, max-age=3600";
            return Results.File(content, "image/svg+xml");
        }).RequireWorkspaceEngineeringRead();
        return endpoints;
    }
}
