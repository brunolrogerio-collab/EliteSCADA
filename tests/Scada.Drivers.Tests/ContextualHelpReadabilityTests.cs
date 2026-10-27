using Scada.Api.Runtime;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpReadabilityTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void BeginnerProceduresAndExamplesPrecedeLookupTablesWithoutLosingTechnicalFields(string locale)
    {
        var catalog = ContextualHelpCatalog.Build(locale);
        foreach (var id in new[] { "getting-started.first-project", "installation.switch-workflow", "runtime.session-classes", "engineering.tag-monitor" })
        {
            var topic = catalog.Topics.Single(t => t.Id == id);
            var sections = topic.Sections.ToArray();
            var fieldsAt = Array.FindIndex(sections, s => s.Fields?.Count > 0);
            Assert.True(fieldsAt >= 3, $"{id}: provide context and task steps before field lookup.");
            Assert.Contains(sections.Take(fieldsAt), s => s.Body.StartsWith("1. ", StringComparison.Ordinal));
            Assert.All(sections.SelectMany(s => s.Fields ?? []), f => Assert.False(string.IsNullOrWhiteSpace(f.Constraints)));
        }
        var first = catalog.Topics.Single(t => t.Id == "getting-started.first-project");
        Assert.Single(first.Sections, s => s.Code is not null);
        Assert.Contains("publish_server_memory_sample", first.Sections.Single(s => s.Code is not null).Code!);
        var map = catalog.Topics.Single(t => t.Id == "help.application-map");
        Assert.Contains(first.Id, map.RelatedTopicIds!);
        // This verifies progression/retained contracts, not whether prose is comprehensible to a human reader.
    }
}
