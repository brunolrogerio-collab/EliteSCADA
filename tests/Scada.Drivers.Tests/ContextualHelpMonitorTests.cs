using Scada.Api.Runtime;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpMonitorTests
{
    [Fact]
    public void ThreeDistinctMonitorInformationGuidesKeepStepsFieldsAndLinksInEveryLocale()
    {
        var ids = new[] { "engineering.development-monitor", "engineering.tag-monitor", "engineering.information" };
        var required = new[] {
            new[] { "quickAdd", "referenceBrowser", "reference", "source", "value", "dataType", "quality", "timestamp", "remove", "clear" },
            new[] { "workingProject", "workingRevision", "activeIdentity", "alignment", "activatedAtUtc", "query", "qualityFilter", "accessFilter", "refresh", "currentValue", "quality", "dataType", "source", "timestamp", "sourceTimestamp", "serverTimestamp", "freshness", "requestTimes", "identity", "recentHistory", "historyRefresh" },
            new[] { "displayVersion", "schema", "baseRevision", "exportedAt", "project", "buildCommit" }
        };
        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var catalog = ContextualHelpCatalog.Build(locale);
            for (var i = 0; i < ids.Length; i++)
            {
                var guide = Assert.Single(catalog.Topics, topic => topic.Id == ids[i]);
                Assert.Contains(guide.Sections, section => section.Body.StartsWith("1. ", StringComparison.Ordinal));
                Assert.Equal(required[i], guide.Sections.SelectMany(section => section.Fields ?? []).Select(field => field.Key));
                Assert.All(guide.Sections.SelectMany(section => section.Fields ?? []), field =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(field.Label));
                    Assert.False(string.IsNullOrWhiteSpace(field.Description));
                    Assert.False(string.IsNullOrWhiteSpace(field.Constraints));
                });
                Assert.All(guide.RelatedTopicIds!, related => Assert.Contains(catalog.Topics, topic => topic.Id == related));
            }
        }
    }
}
