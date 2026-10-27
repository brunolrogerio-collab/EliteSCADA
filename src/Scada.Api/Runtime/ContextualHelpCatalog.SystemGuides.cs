using System.Text.Json;

namespace Scada.Api.Runtime;

public static partial class ContextualHelpCatalog
{
    // Installed content uses the existing topic/section contract. The same source supplies manual chapters.
    private static readonly Lazy<Dictionary<string, ContextualHelpTopic[]>> SystemGuides = new(() =>
    {
        using var stream = typeof(ContextualHelpCatalog).Assembly.GetManifestResourceStream(
            "Scada.Api.Runtime.HelpGuides.system-guides.json")
            ?? throw new InvalidOperationException("Installed system Help content is missing.");
        return JsonSerializer.Deserialize<Dictionary<string, ContextualHelpTopic[]>>(stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Installed system Help content is invalid.");
    });

    private static IEnumerable<ContextualHelpTopic> BuildSystemGuides(string locale) => SystemGuides.Value[locale];
}
