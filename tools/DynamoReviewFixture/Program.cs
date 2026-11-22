using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Api.Runtime;

// Generated test artifact from the real catalog, never a second hand-maintained
// drawing fixture. No database, project, source code or credentials are changed.
if (args.Length != 1) throw new ArgumentException("Pass the explicit output JSON artifact path.");
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
json.Converters.Add(new JsonStringEnumConverter());
var fixture = new {
    dynamos = BuiltinDynamoCatalogV1.Create(),
    artwork = BuiltinDynamoCatalogV1.CreateArtworkAssets().Select(item => new {
        asset = item.Asset, content = Convert.ToBase64String(item.Payload.Content)
    }).ToArray()
};
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(fixture, json));
Console.WriteLine($"Exported {fixture.dynamos.Count} canonical Dynamo definitions and {fixture.artwork.Length} artwork assets.");
