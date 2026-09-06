using System.Text.Json;
using System.Text.RegularExpressions;
using Scada.Api.Runtime;
using Scada.DriverHost.Engineering;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpTests
{
    [Fact]
    public void LocaleCatalogs_ExposeTheSameStableTopicIds()
    {
        var catalogs = ContextualHelpCatalog.SupportedLocales
            .Select(ContextualHelpCatalog.Build)
            .ToArray();

        var expected = catalogs[0].Topics.Select(topic => topic.Id).OrderBy(id => id).ToArray();
        Assert.NotEmpty(expected);

        foreach (var catalog in catalogs)
        {
            Assert.Equal(expected, catalog.Topics.Select(topic => topic.Id).OrderBy(id => id).ToArray());
            Assert.All(catalog.Topics, topic =>
                Assert.Matches("^[a-z0-9][a-z0-9._-]+$", topic.Id));
        }
    }

    [Fact]
    public void DriverHelpInventory_MatchesCanonicalProductionCatalog()
    {
        var expected = EngineeringDataSourceTypeCatalog
            .BuildForCurrentSchema(CommunicationDriverRuntimeComposition.BuildForCurrentSchema())
            .Describe()
            .DataSourceTypes
            .Where(source => source.Kind == "communicationDriver")
            .Select(source => $"driver.{source.TypeKey}")
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        var actual = ContextualHelpCatalog.Build("pt-BR").Topics
            .Where(topic => topic.Category == "drivers")
            .Select(topic => topic.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SourceProviderHelpInventory_MatchesCanonicalProductionCatalog()
    {
        var expected = EngineeringDataSourceTypeCatalog
            .BuildForCurrentSchema(CommunicationDriverRuntimeComposition.BuildForCurrentSchema())
            .Describe()
            .DataSourceTypes
            .Where(source => source.Kind == "sourceProvider")
            .Select(source => $"source.{source.TypeKey}")
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        var actual = ContextualHelpCatalog.Build("en").Topics
            .Where(topic => topic.Category == "sources")
            .Select(topic => topic.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ServerScriptHelp_DocumentsExactlyTheRuntimeSpecificApiSurface()
    {
        var runnerPath = Path.Combine(AppContext.BaseDirectory, "ServerScriptRunner.py");
        var runner = File.ReadAllText(runnerPath);
        var matches = Regex.Matches(runner, "if name == \"(?<name>[a-z_]+)\":");
        var runtimeApi = matches
            .Cast<Match>()
            .Select(match => match.Groups["name"].Value)
            .Where(name => name is
                "read_tag" or
                "read_server_memory" or
                "write_tag" or
                "write_server_memory" or
                "publish_server_memory_sample" or
                "emit_operational_event")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            runtimeApi,
            ContextualHelpCatalog.ServerScriptApiFunctions.OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.Equal(6, runtimeApi.Length);
    }

    [Fact]
    public void HelpCatalog_IsLocalAndContainsNoExternalDocumentationDependency()
    {
        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var json = JsonSerializer.Serialize(ContextualHelpCatalog.Build(locale));
            Assert.DoesNotContain("https://", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("http://", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
