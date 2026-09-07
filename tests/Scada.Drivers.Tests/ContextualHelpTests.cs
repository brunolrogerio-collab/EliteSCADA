using System.Text.Json;
using System.Text.RegularExpressions;
using Scada.Api.Runtime;
using Scada.DriverHost.Engineering;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpTests
{
    [Fact]
    public void RequiredManualTopics_ArePresentInEveryLocale()
    {
        Assert.Equal(30, ContextualHelpCatalog.RequiredManualTopicIds.Count);

        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var catalog = ContextualHelpCatalog.Build(locale);
            var ids = catalog.Topics.Select(topic => topic.Id).ToHashSet(StringComparer.Ordinal);

            foreach (var requiredTopicId in ContextualHelpCatalog.RequiredManualTopicIds)
                Assert.Contains(requiredTopicId, ids);
        }
    }

    [Fact]
    public void LocaleCatalogs_ExposeTheSameStableTopicIdsAndSemanticStructure()
    {
        var catalogs = ContextualHelpCatalog.SupportedLocales
            .Select(ContextualHelpCatalog.Build)
            .ToArray();

        var expected = catalogs[0].Topics
            .Select(topic => (topic.Id, topic.Category, SectionCount: topic.Sections.Count, CodeShape: topic.Sections.Select(section => section.Code is not null).ToArray()))
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(expected);

        foreach (var catalog in catalogs)
        {
            var actual = catalog.Topics
                .Select(topic => (topic.Id, topic.Category, SectionCount: topic.Sections.Count, CodeShape: topic.Sections.Select(section => section.Code is not null).ToArray()))
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected.Length, actual.Length);
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.Equal(expected[index].Id, actual[index].Id);
                Assert.Equal(expected[index].Category, actual[index].Category);
                Assert.Equal(expected[index].SectionCount, actual[index].SectionCount);
                Assert.Equal(expected[index].CodeShape, actual[index].CodeShape);
            }

            Assert.All(catalog.Topics, topic =>
            {
                Assert.Matches("^[a-z0-9][a-z0-9._-]+$", topic.Id);
                Assert.False(string.IsNullOrWhiteSpace(topic.Title));
                Assert.False(string.IsNullOrWhiteSpace(topic.Summary));
                Assert.NotEmpty(topic.Sections);
                Assert.All(topic.Sections, section =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(section.Heading));
                    Assert.False(string.IsNullOrWhiteSpace(section.Body));
                });
            });
        }
    }

    [Fact]
    public void DriverHelpInventory_MatchesCanonicalProductionCatalogAndExcludesSimulation()
    {
        var canonicalProductionDrivers = EngineeringDataSourceTypeCatalog
            .BuildForCurrentSchema(CommunicationDriverRuntimeComposition.BuildForCurrentSchema())
            .Describe()
            .DataSourceTypes
            .Where(source => source.Kind == "communicationDriver")
            .Where(source => !source.TypeKey.Equals("simulation", StringComparison.OrdinalIgnoreCase))
            .OrderBy(source => source.TypeKey, StringComparer.Ordinal)
            .ToArray();

        var expected = canonicalProductionDrivers
            .Select(source => $"driver.{source.TypeKey}")
            .ToArray();
        var actual = ContextualHelpCatalog.Build("pt-BR").Topics
            .Where(topic => topic.Category == "drivers")
            .Select(topic => topic.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(8, canonicalProductionDrivers.Length);
        Assert.Equal(expected, actual);
        Assert.DoesNotContain("driver.simulation", actual);
    }

    [Fact]
    public void DriverHelp_DerivesConfigurationAddressingAndCapabilitiesFromCanonicalDescriptors()
    {
        var drivers = EngineeringDataSourceTypeCatalog
            .BuildForCurrentSchema(CommunicationDriverRuntimeComposition.BuildForCurrentSchema())
            .Describe()
            .DataSourceTypes
            .Where(source => source.Kind == "communicationDriver")
            .Where(source => !source.TypeKey.Equals("simulation", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var catalog = ContextualHelpCatalog.Build("en");

        foreach (var driver in drivers)
        {
            var topic = Assert.Single(catalog.Topics, item => item.Id == $"driver.{driver.TypeKey}");
            var text = string.Join("\n", topic.Sections.Select(section => section.Body));
            var schema = Assert.IsType<EngineeringDriverConfigurationSchemaView>(driver.ConfigurationSchema);

            Assert.Contains(driver.TypeKey, text, StringComparison.Ordinal);
            Assert.Contains(schema.SchemaId, text, StringComparison.Ordinal);
            Assert.Contains($"v{schema.SchemaVersion}", text, StringComparison.Ordinal);

            foreach (var field in schema.DataSourceFields.Concat(schema.TagBindingFields))
            {
                Assert.Contains(field.Key, text, StringComparison.Ordinal);
                Assert.Contains(field.ValueKind, text, StringComparison.Ordinal);
                if (!string.IsNullOrWhiteSpace(field.ExpectedFormat))
                    Assert.Contains(field.ExpectedFormat, text, StringComparison.Ordinal);
            }

            if (driver.Capabilities.SupportsConnectionTest)
                Assert.Contains("Connection test", text, StringComparison.Ordinal);
            if (driver.Capabilities.SupportsDiscovery)
                Assert.Contains("Discovery", text, StringComparison.Ordinal);
            if (driver.Capabilities.SupportsBrowse)
                Assert.Contains("Browse", text, StringComparison.Ordinal);
            if (driver.Capabilities.SupportsFileImport)
                Assert.Contains("File import", text, StringComparison.Ordinal);
            if (driver.Capabilities.SupportsReconcile)
                Assert.Contains("Reconcile", text, StringComparison.Ordinal);
            if (driver.Capabilities.SupportsSharedTransportInfrastructure)
                Assert.Contains("Shared transport", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SourceProviderHelpInventory_MatchesCanonicalCatalogAndDoesNotCountAsDrivers()
    {
        var sourceProviders = EngineeringDataSourceTypeCatalog
            .BuildForCurrentSchema(CommunicationDriverRuntimeComposition.BuildForCurrentSchema())
            .Describe()
            .DataSourceTypes
            .Where(source => source.Kind == "sourceProvider")
            .OrderBy(source => source.TypeKey, StringComparer.Ordinal)
            .ToArray();
        var catalog = ContextualHelpCatalog.Build("en");
        var actual = catalog.Topics
            .Where(topic => topic.Category == "sources" && topic.Id.StartsWith("source.", StringComparison.Ordinal))
            .Select(topic => topic.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(sourceProviders.Select(source => $"source.{source.TypeKey}").ToArray(), actual);
        Assert.Contains(catalog.Topics, topic => topic.Id == "sources.internal-memory");
        Assert.Contains(catalog.Topics, topic => topic.Id == "gateway.overview");
    }

    [Fact]
    public void ReusableLibraryHelp_PreservesAcceptedC25_6SemanticsAcrossLocales()
    {
        var expectedAssociationSentence = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pt-BR"] = "Associar uma Library não importa conteúdo e, sozinho, não altera Working",
            ["en"] = "Associating a Library does not import content and, by itself, does not change Working",
            ["es"] = "Asociar una Library no importa contenido y, por sí solo, no cambia Working"
        };
        var expectedUseSentence = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pt-BR"] = "Usar incorpora seletivamente o recurso escolhido e o closure validado de suas dependências",
            ["en"] = "Use selectively incorporates the chosen resource and its validated dependency closure",
            ["es"] = "Usar incorpora selectivamente el recurso elegido y el closure validado de sus dependencias"
        };

        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var topic = Assert.Single(ContextualHelpCatalog.Build(locale).Topics, item => item.Id == "libraries.reusable-resources");
            var text = string.Join("\n", topic.Sections.Select(section => section.Body));

            Assert.Contains(".escadalib", text, StringComparison.Ordinal);
            Assert.Contains(".escadapkg", text, StringComparison.Ordinal);
            Assert.Contains(expectedAssociationSentence[locale], text, StringComparison.Ordinal);
            Assert.Contains(expectedUseSentence[locale], text, StringComparison.Ordinal);
            Assert.Contains("self-contained", text, StringComparison.Ordinal);
            Assert.Contains("Runtime", text, StringComparison.Ordinal);
            Assert.Contains("Active", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AlarmOperationalEventAndAudit_RemainSemanticallyDistinctManualDomains()
    {
        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var catalog = ContextualHelpCatalog.Build(locale);
            var alarms = Assert.Single(catalog.Topics, topic => topic.Id == "alarms.overview");
            var operationalEvents = Assert.Single(catalog.Topics, topic => topic.Id == "operational-events.overview");
            var audit = Assert.Single(catalog.Topics, topic => topic.Id == "audit.overview");

            Assert.NotEqual(alarms.Category, operationalEvents.Category);
            Assert.NotEqual(alarms.Category, audit.Category);
            Assert.NotEqual(operationalEvents.Category, audit.Category);

            var combined = string.Join("\n", alarms.Sections.Concat(operationalEvents.Sections).Concat(audit.Sections).Select(section => section.Body));
            Assert.Contains("Alarm", combined, StringComparison.Ordinal);
            Assert.Contains("Operational Event", combined, StringComparison.Ordinal);
            Assert.Contains("Audit", combined, StringComparison.Ordinal);
        }
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

        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var catalog = ContextualHelpCatalog.Build(locale);
            var scriptTopic = Assert.Single(catalog.Topics, topic => topic.Id == "scripts.server");
            var documentedCode = string.Join("\n", scriptTopic.Sections.Select(section => section.Code ?? string.Empty));
            foreach (var apiFunction in runtimeApi)
                Assert.Contains($"{apiFunction}(", documentedCode, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void HelpCatalog_IsEntirelyLocalAndHasNoExternalDocumentationContract()
    {
        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var catalog = ContextualHelpCatalog.Build(locale);
            var json = JsonSerializer.Serialize(catalog);

            Assert.DoesNotContain("documentationUrl", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("externalUrl", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("externalDocumentation", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
