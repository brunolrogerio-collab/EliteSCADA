using Scada.Api.Runtime;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpJourneyTests
{
    [Fact]
    public void FirstProjectAndSwitchGuidesPreserveLocaleStructureRequiredFieldsAndExistingEntryLinks()
    {
        var firstKeys = new[] { "projectKey", "projectName", "source", "tagPath", "dataType", "scriptScope", "entryPoint", "scriptBinding", "displayBinding", "screen" };
        var switchKeys = new[] { "application", "working", "publishedActive", "authority", "historian", "engineeringProtection", "exportApplication", "exportAuthority", "backupDecision", "ackUnsavedWorking", "acknowledgeApplicationRemoval", "acknowledgeAuthorityRemoval", "acknowledgeHistorianPreserved", "licenseAction", "replacementLicenseCode", "blockers", "detach", "cancel" };
        string? publishedCode = null;
        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var catalog = ContextualHelpCatalog.Build(locale);
            var first = catalog.Topics.Single(t => t.Id == "getting-started.first-project");
            var replace = catalog.Topics.Single(t => t.Id == "installation.switch-workflow");
            Assert.Equal(firstKeys, first.Sections.SelectMany(s => s.Fields ?? []).Select(f => f.Key));
            Assert.Equal(switchKeys, replace.Sections.SelectMany(s => s.Fields ?? []).Select(f => f.Key));
            foreach (var guide in new[] { first, replace })
            {
                Assert.Contains(guide.Sections, s => s.Body.StartsWith("1. ", StringComparison.Ordinal));
                Assert.All(guide.RelatedTopicIds!, id => Assert.Contains(catalog.Topics, t => t.Id == id));
                Assert.All(guide.Sections.SelectMany(s => s.Fields ?? []), f => Assert.False(string.IsNullOrWhiteSpace(f.Constraints)));
            }
            var code = Assert.Single(first.Sections, s => s.Code is not null).Code!;
            publishedCode ??= code;
            Assert.Equal(publishedCode, code);
            Assert.Contains("getting-started.first-project", catalog.Topics.Single(t => t.Id == "getting-started.neutral-bootstrap").RelatedTopicIds!);
            Assert.Contains("installation.switch-workflow", catalog.Topics.Single(t => t.Id == "installation.switching").RelatedTopicIds!);
        }
    }
}
