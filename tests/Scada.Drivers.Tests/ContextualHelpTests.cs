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
        var canonicalProductionDrivers = ProductionDrivers();
        var expected = canonicalProductionDrivers
            .Select(source => $"driver.{source.TypeKey}")
            .ToArray();
        var actual = ContextualHelpCatalog.Build("pt-BR").Topics
            .Where(topic => topic.Category == "drivers")
            .Select(topic => topic.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal("builtin.simulation", ContextualHelpCatalog.ExcludedSimulationDriverTypeKey);
        Assert.Equal(8, canonicalProductionDrivers.Length);
        Assert.Equal(expected, actual);
        Assert.DoesNotContain($"driver.{ContextualHelpCatalog.ExcludedSimulationDriverTypeKey}", actual);
    }

    [Fact]
    public void DriverHelp_FollowsTheBindingUserFacingTemplate()
    {
        var requiredHeadings = new[]
        {
            "Proven purpose and profile",
            "Data Source configuration",
            "TAG addressing / binding",
            "Data types and mapping",
            "Bit access",
            "Byte/word order",
            "Read/write and restrictions",
            "Polling/subscription",
            "Reconnect and timeouts",
            "Security and certificates",
            "Engineering capabilities",
            "Valid and invalid examples",
            "Quality, timestamps and writeability",
            "Diagnostics and troubleshooting",
            "Interoperability limits"
        };
        var catalog = ContextualHelpCatalog.Build("en");

        foreach (var driver in ProductionDrivers())
        {
            var topic = Assert.Single(catalog.Topics, item => item.Id == $"driver.{driver.TypeKey}");
            var headings = topic.Sections.Select(section => section.Heading).ToArray();

            foreach (var heading in requiredHeadings)
                Assert.Contains(heading, headings);
        }
    }

    [Fact]
    public void DriverHelp_DerivesConfigurationAddressingAndCapabilitiesFromCanonicalDescriptors()
    {
        var catalog = ContextualHelpCatalog.Build("en");

        foreach (var driver in ProductionDrivers())
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
        var expectedUsePrefix = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pt-BR"] = "Usar incorpora seletivamente o recurso escolhido",
            ["en"] = "Use selectively incorporates the chosen resource",
            ["es"] = "Usar incorpora selectivamente el recurso elegido"
        };

        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var topic = Assert.Single(ContextualHelpCatalog.Build(locale).Topics, item => item.Id == "libraries.reusable-resources");
            var text = string.Join("\n", topic.Sections.Select(section => section.Body));

            Assert.Contains(".escadalib", text, StringComparison.Ordinal);
            Assert.Contains(".escadapkg", text, StringComparison.Ordinal);
            Assert.Contains(expectedAssociationSentence[locale], text, StringComparison.Ordinal);
            Assert.Contains(expectedUsePrefix[locale], text, StringComparison.Ordinal);
            Assert.Contains("closure", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Working", text, StringComparison.Ordinal);
            Assert.Contains("self-contained", text, StringComparison.Ordinal);
            Assert.Contains("Runtime", text, StringComparison.Ordinal);
            Assert.Contains("Active", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ReusableLibraryHelp_DocumentsCreationExportInspectionAndDependencyClosure()
    {
        var topic = Assert.Single(ContextualHelpCatalog.Build("en").Topics, item => item.Id == "libraries.reusable-resources");
        var text = string.Join("\n", topic.Sections.Select(section => $"{section.Heading}\n{section.Body}"));

        Assert.Contains("Create and export .escadalib", text, StringComparison.Ordinal);
        Assert.Contains("selected Working resources", text, StringComparison.Ordinal);
        Assert.Contains("dependency closure", text, StringComparison.Ordinal);
        Assert.Contains("Equipment Template", text, StringComparison.Ordinal);
        Assert.Contains("Dynamo", text, StringComparison.Ordinal);
        Assert.Contains("Screen", text, StringComparison.Ordinal);
        Assert.Contains("Popup", text, StringComparison.Ordinal);
        Assert.Contains("Script", text, StringComparison.Ordinal);
        Assert.Contains("Visual Asset", text, StringComparison.Ordinal);
        Assert.Contains("SHA-256", text, StringComparison.Ordinal);
        Assert.Contains("Inspect validates", text, StringComparison.Ordinal);
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
    public void ServerScriptHelp_DocumentsRuntimeTriggersLifecycleFailurePolicyAndSandbox()
    {
        var expectedTriggers = new[] { "Initialize", "Dispose", "TagChanged", "Timer", "ServerRuntimeEvent" };
        Assert.Equal(expectedTriggers, ContextualHelpCatalog.ServerScriptRuntimeTriggers);

        var topic = Assert.Single(ContextualHelpCatalog.Build("en").Topics, item => item.Id == "scripts.server");
        var text = string.Join("\n", topic.Sections.Select(section => $"{section.Heading}\n{section.Body}"));

        foreach (var trigger in expectedTriggers)
            Assert.Contains(trigger, text, StringComparison.Ordinal);

        Assert.Contains("Active revision", text, StringComparison.Ordinal);
        Assert.Contains("previous generation is cancelled", text, StringComparison.Ordinal);
        Assert.Contains("250 ms", text, StringComparison.Ordinal);
        Assert.Contains("128 events", text, StringComparison.Ordinal);
        Assert.Contains("50 ms", text, StringComparison.Ordinal);
        Assert.Contains("5 consecutive failures", text, StringComparison.Ordinal);
        Assert.Contains("filesystem", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shell/process execution", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("arbitrary network", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("database", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("industrial-driver", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secrets", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sandbox enforcement must not depend on text scanning", text, StringComparison.OrdinalIgnoreCase);
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

    private static EngineeringDataSourceTypeView[] ProductionDrivers() => EngineeringDataSourceTypeCatalog
        .BuildForCurrentSchema(CommunicationDriverRuntimeComposition.BuildForCurrentSchema())
        .Describe()
        .DataSourceTypes
        .Where(source => source.Kind == "communicationDriver")
        .Where(source => !source.TypeKey.Equals(ContextualHelpCatalog.ExcludedSimulationDriverTypeKey, StringComparison.OrdinalIgnoreCase))
        .OrderBy(source => source.TypeKey, StringComparer.Ordinal)
        .ToArray();
}
