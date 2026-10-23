using System.Diagnostics;
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
        Assert.Equal(48, ContextualHelpCatalog.RequiredManualTopicIds.Count);

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

    [Theory]
    [InlineData("pt-BR", "Pacote portátil e autocontido de uma aplicação SCADIVO.")]
    [InlineData("en", "Portable self-contained package for a SCADIVO application.")]
    [InlineData("es", "Paquete portátil y autocontenido de una aplicación SCADIVO.")]
    public void ProjectPackageHelp_UsesTheCommercialBrandWithoutChangingItsTopicId(string locale, string expectedSummary)
    {
        var catalog = ContextualHelpCatalog.Build(locale);
        var topic = Assert.Single(catalog.Topics, item => item.Id == "packages.escadapkg");

        Assert.Equal(expectedSummary, topic.Summary);
    }

    [Fact]
    public void DriverHelpInventory_MatchesCanonicalProductionCatalogAndExcludesSimulation()
    {
        var canonicalProductionDrivers = ProductionDrivers();
        var expected = canonicalProductionDrivers
            .Select(source => $"driver.{source.TypeKey}")
            .ToArray();
        var actual = ContextualHelpCatalog.Build("pt-BR").Topics
            .Where(topic => topic.Category == "drivers" && topic.Id.StartsWith("driver.", StringComparison.Ordinal))
            .Select(topic => topic.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal("builtin.simulation", ContextualHelpCatalog.ExcludedSimulationDriverTypeKey);
        Assert.Contains(canonicalProductionDrivers, driver => driver.TypeKey == "shelly.rpc");
        Assert.Contains(canonicalProductionDrivers, driver => driver.TypeKey == "modbus.rtu");
        Assert.Contains(canonicalProductionDrivers, driver => driver.TypeKey == "modbus.tcp.server");
        Assert.Contains(canonicalProductionDrivers, driver => driver.TypeKey == "modbus.rtu.server");
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
        Assert.Contains("integrity hashes", text, StringComparison.Ordinal);
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

            Assert.All(new[] { alarms, operationalEvents, audit }, topic =>
            {
                Assert.Equal(8, topic.Sections.Count);
                Assert.Contains(topic.Sections, section => section.Heading is "Steps" or "Pasos" or "Passos");
                Assert.Contains(topic.Sections, section => section.Heading is "Prerequisites" or "Requisitos previos" or "Pré-requisitos");
                Assert.NotEmpty(topic.RelatedTopicIds ?? Array.Empty<string>());
            });

            var alarmText = string.Join("\n", alarms.Sections.Select(section => section.Body));
            var eventText = string.Join("\n", operationalEvents.Sections.Select(section => section.Body));
            var auditText = string.Join("\n", audit.Sections.Select(section => section.Body));
            Assert.Contains("TAG", alarmText, StringComparison.Ordinal);
            Assert.Contains("AlarmAcknowledge", alarmText, StringComparison.Ordinal);
            Assert.Contains("Communication", alarmText, StringComparison.Ordinal);
            Assert.Contains("System", alarmText, StringComparison.Ordinal);
            Assert.Contains(locale switch
            {
                "en" => "current Runtime does not use it",
                "es" => "Runtime actual no lo usa",
                _ => "Runtime atual não o usa"
            }, alarmText, StringComparison.Ordinal);
            Assert.Contains(locale switch
            {
                "en" => "Clearing Requires acknowledgement also does not yet remove the ACK requirement",
                "es" => "Desmarcar Requiere reconocimiento tampoco elimina todavía el requisito de ACK",
                _ => "Desmarcar Requer reconhecimento também ainda não dispensa o ACK"
            }, alarmText, StringComparison.Ordinal);
            Assert.Contains("emit_operational_event", eventText, StringComparison.Ordinal);
            Assert.Contains("operational.events", eventText, StringComparison.Ordinal);
            Assert.Contains("SystemAdmin", auditText, StringComparison.Ordinal);
            Assert.Contains("Correlation ID", auditText, StringComparison.Ordinal);

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
            Assert.Contains("Plant.Pump.Running", documentedCode, StringComparison.Ordinal);
            Assert.DoesNotContain("<stable-server-memory-tag-id>", documentedCode, StringComparison.Ordinal);
            foreach (var apiFunction in runtimeApi)
                Assert.Contains($"{apiFunction}(", documentedCode, StringComparison.Ordinal);

            Assert.Equal(runtimeApi, catalog.ServerScriptApiDetails.Select(item => item.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
            Assert.All(catalog.ServerScriptApiDetails, item =>
            {
                Assert.False(string.IsNullOrWhiteSpace(item.Signature));
                Assert.False(string.IsNullOrWhiteSpace(item.Parameters));
                Assert.False(string.IsNullOrWhiteSpace(item.Result));
                Assert.False(string.IsNullOrWhiteSpace(item.Safety));
                Assert.Contains($"{item.Name}(", item.Example, StringComparison.Ordinal);
            });
        }
    }

    [Fact]
    public void ServerScriptHelp_LocalizesApiExplanationsAndDoesNotInventSourceQuality()
    {
        var portuguese = ContextualHelpCatalog.Build("pt-BR");
        var english = ContextualHelpCatalog.Build("en");
        var spanish = ContextualHelpCatalog.Build("es");

        var ptApi = portuguese.ServerScriptApiDetails.ToDictionary(item => item.Name, StringComparer.Ordinal);
        var enApi = english.ServerScriptApiDetails.ToDictionary(item => item.Name, StringComparer.Ordinal);
        var esApi = spanish.ServerScriptApiDetails.ToDictionary(item => item.Name, StringComparer.Ordinal);
        foreach (var name in ContextualHelpCatalog.ServerScriptApiFunctions)
        {
            Assert.NotEqual(ptApi[name].Parameters, enApi[name].Parameters);
            Assert.NotEqual(ptApi[name].Parameters, esApi[name].Parameters);
            Assert.NotEqual(ptApi[name].Result, enApi[name].Result);
            Assert.NotEqual(ptApi[name].Result, esApi[name].Result);
            Assert.NotEqual(ptApi[name].Safety, enApi[name].Safety);
            Assert.NotEqual(ptApi[name].Safety, esApi[name].Safety);
        }

        var scriptTopic = Assert.Single(portuguese.Topics, topic => topic.Id == "scripts.server");
        var qualitySection = Assert.Single(scriptTopic.Sections, section => section.Heading == "Quality: não invente Good");
        Assert.Contains("não expõe", qualitySection.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Good somente", qualitySection.Body, StringComparison.Ordinal);
        Assert.Contains("\"Uncertain\"", qualitySection.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Good\"", qualitySection.Code, StringComparison.Ordinal);

        var englishScriptTopic = Assert.Single(english.Topics, topic => topic.Id == "scripts.server");
        var englishQualitySection = Assert.Single(englishScriptTopic.Sections, section => section.Heading == "Quality: do not invent Good");
        Assert.Contains("source quality and timestamp are not exposed", englishQualitySection.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServerScriptHelp_CodeExamplesExecuteInTheActualSandboxInterpreter()
    {
        const string exampleCommandId = "11111111-1111-4111-8111-111111111111";
        var catalog = ContextualHelpCatalog.Build("pt-BR");
        var topic = Assert.Single(catalog.Topics, item => item.Id == "scripts.server");
        var examples = topic.Sections
            .Select(section => section.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Cast<string>()
            .Concat(catalog.ServerScriptApiDetails.Select(item => item.Example))
            .Concat(ReadManualServerScriptExamples())
            .ToArray();
        Assert.NotEmpty(examples);

        var runnerPath = Path.Combine(AppContext.BaseDirectory, "ServerScriptRunner.py");
        foreach (var originalSource in examples)
        {
            var source = originalSource.Replace("<stable-rich-command-guid>", exampleCommandId, StringComparison.Ordinal);
            var handlers = Regex.Matches(source, @"(?m)^def\s+(?<name>[A-Za-z_]\w*)\s*\(")
                .Cast<Match>()
                .Select(match => match.Groups["name"].Value)
                .ToArray();
            Assert.NotEmpty(handlers);

            var tagPaths = GetScriptReferences(
                source,
                "read_tag",
                "read_server_memory",
                "write_tag",
                "write_server_memory",
                "publish_server_memory_sample");
            var memoryPaths = GetScriptReferences(
                source,
                "read_server_memory",
                "write_server_memory",
                "publish_server_memory_sample");
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var path in tagPaths)
            {
                values[path] = path.EndsWith(".Running", StringComparison.Ordinal) ||
                               path.EndsWith(".HighPressure", StringComparison.Ordinal)
                    ? (object)false
                    : 10;
            }
            var richCommandIds = source.Contains("invoke_rich_command", StringComparison.Ordinal)
                ? new[] { exampleCommandId }
                : Array.Empty<string>();

            foreach (var handler in handlers)
            {
                var output = await RunServerScriptExampleAsync(
                    runnerPath,
                    source,
                    handler,
                    values,
                    memoryPaths,
                    richCommandIds);
                using var document = JsonDocument.Parse(output);
                var response = document.RootElement.GetProperty("response");
                Assert.True(
                    response.GetProperty("succeeded").GetBoolean(),
                    $"Server Script example handler '{handler}' failed in the actual interpreter: {response.GetProperty("error").GetString()}");
            }
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
        Assert.Contains("recovery probe", text, StringComparison.Ordinal);
        Assert.Contains("configurable cooldown", text, StringComparison.Ordinal);
        Assert.Contains("filesystem", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shell/process execution", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("arbitrary network", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("database", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("industrial-driver", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secrets", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sandbox enforcement must not depend on text scanning", text, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetScriptReferences(string source, params string[] functionNames)
    {
        return functionNames
            .SelectMany(functionName =>
            {
                var pattern = Regex.Escape(functionName) + "\\s*\\(\\s*\"(?<path>[^\"]+)\"";
                return Regex.Matches(source, pattern)
                    .Cast<Match>()
                    .Select(match => match.Groups["path"].Value);
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> ReadManualServerScriptExamples()
    {
        var manualPath = Path.Combine(
            AppContext.BaseDirectory,
            "manual",
            "pt-BR",
            "06-SCRIPTING-ALARMES-HISTORICO.md");
        var manual = File.ReadAllText(manualPath);
        return Regex.Matches(manual, "```python\\r?\\n(?<source>[\\s\\S]*?)\\r?\\n```")
            .Cast<Match>()
            .Select(match => match.Groups["source"].Value)
            .ToArray();
    }

    private static async Task<string> RunServerScriptExampleAsync(
        string runnerPath,
        string source,
        string handler,
        IReadOnlyDictionary<string, object?> values,
        IReadOnlyCollection<string> serverMemoryTagIds,
        IReadOnlyCollection<string> richCommandIds)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "python" : "python3",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(runnerPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the Python Server Script runner.");
        var standardError = process.StandardError.ReadToEndAsync();
        var payload = JsonSerializer.Serialize(new
        {
            source,
            handler,
            values,
            @event = new
            {
                canonicalEvent = new
                {
                    payload = new { action = "single" }
                }
            },
            serverMemoryTagIds,
            richCommandIds
        });
        await process.StandardInput.WriteLineAsync(payload);

        string? completedLine = null;
        while (await process.StandardOutput.ReadLineAsync() is { } line)
        {
            using var message = JsonDocument.Parse(line);
            var root = message.RootElement;
            if (root.GetProperty("type").GetString() == "richCommandRequest")
            {
                var commandResponse = JsonSerializer.Serialize(new
                {
                    type = "richCommandResponse",
                    requestId = root.GetProperty("requestId").GetString(),
                    result = new { outcome = "Completed" }
                });
                await process.StandardInput.WriteLineAsync(commandResponse);
            }
            else
            {
                completedLine = line;
            }
        }

        await process.WaitForExitAsync();
        var error = await standardError;
        Assert.True(process.ExitCode == 0, $"Server Script runner exited with {process.ExitCode}: {error}");
        return completedLine ?? throw new InvalidOperationException($"Server Script runner returned no completion response: {error}");
    }


    [Fact]
    public void TopicIds_AreUniqueAndRelatedTopicLinksResolveInEveryLocale()
    {
        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var catalog = ContextualHelpCatalog.Build(locale);
            var ids = catalog.Topics.Select(topic => topic.Id).ToArray();
            Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());

            var idSet = ids.ToHashSet(StringComparer.Ordinal);
            foreach (var topic in catalog.Topics)
            {
                foreach (var relatedId in topic.RelatedTopicIds ?? Array.Empty<string>())
                    Assert.Contains(relatedId, idSet);
            }
        }
    }

    [Fact]
    public void Phase1TaskTopics_HaveTaskGuidanceAndLocaleParity()
    {
        var taskTopicIds = new[]
        {
            "screens.overview",
            "popups.overview",
            "sources.data-sources",
            "engineering.lifecycle",
            "packages.escadapkg",
            "diagnostics.overview",
            "recovery.backup-system-recovery",
            "getting-started.neutral-bootstrap",
            "engineering.shell-navigation",
            "drivers.overview",
            "tags.copy-duplicate-sequential",
            "visual.properties",
            "visual.dynamics",
            "visual.events",
            "scripts.engineering",
            "scripts.python-validation",
            "engineering.object-browser",
            "memory.client",
            "security.scopes-authority",
            "security.engineering-lock",
            "licensing.generator",
            "runtime.session-classes",
            "application.export-import",
            "intelligence.process-stage-member-insights"
        };
        var headings = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["pt-BR"] = new[] { "O que é?", "Para que serve?", "Quando usar?", "Pré-requisitos", "Passos", "Resultado esperado", "Problemas comuns", "Como diagnosticar/corrigir" },
            ["en"] = new[] { "What is it?", "What is it for?", "When to use it?", "Prerequisites", "Steps", "Expected result", "Common problems", "How to diagnose/fix" },
            ["es"] = new[] { "¿Qué es?", "¿Para qué sirve?", "¿Cuándo usarlo?", "Requisitos previos", "Pasos", "Resultado esperado", "Problemas comunes", "Cómo diagnosticar/corregir" }
        };

        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var catalog = ContextualHelpCatalog.Build(locale);
            foreach (var id in taskTopicIds)
            {
                var topic = Assert.Single(catalog.Topics, item => item.Id == id);
                Assert.Equal(headings[locale], topic.Sections.Select(section => section.Heading).ToArray());
                Assert.NotEmpty(topic.RelatedTopicIds ?? Array.Empty<string>());
            }
        }
    }

    [Fact]
    public void ScreenEditorHelp_ExplainsIdentityAuthoringPreviewApplyAndLifecycleInEveryLocale()
    {
        var requiredText = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["pt-BR"] = new[]
            {
                "identidade estável", "endereço de navegação", "Paisagem (1920 × 1080)", "Tela inteira",
                "binding de propriedade visual", "Preview não altera o Working", "Apply ao Workspace", "salvar, publicar e ativar"
            },
            ["en"] = new[]
            {
                "stable identity", "navigation route", "Landscape (1920 × 1080)", "complete Screen",
                "bind a visual property", "Preview does not mutate Working", "Apply to Workspace", "save, publish, and activate"
            },
            ["es"] = new[]
            {
                "identidad estable", "ruta de navegación", "Paisaje (1920 × 1080)", "toda la Pantalla",
                "asocie una propiedad visual", "Preview no modifica Working", "Aplicar al Workspace", "guardar, publicar y activar"
            }
        };

        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var topic = Assert.Single(ContextualHelpCatalog.Build(locale).Topics, item => item.Id == "screens.overview");
            var text = string.Join("\n", topic.Sections.Select(section => section.Body));
            foreach (var phrase in requiredText[locale])
                Assert.Contains(phrase, text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("engineering.lifecycle", topic.RelatedTopicIds ?? Array.Empty<string>());
            Assert.Contains("visual.dynamics", topic.RelatedTopicIds ?? Array.Empty<string>());
        }
    }

    [Fact]
    public void PopupEditorHelp_ExplainsAuthoringPreviewApplyAndRuntimeLifecycleInEveryLocale()
    {
        var requiredText = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["pt-BR"] = new[]
            {
                "identificador único e estável", "Template vazio", "área de edição visual", "Estrutura", "Inspector",
                "limites lógicos", "posição é limitada", "Preview não altera o Working", "Apply ao Workspace",
                "não concede permissões", "revisão ativada"
            },
            ["en"] = new[]
            {
                "unique and stable", "Leave Template empty", "design area", "Structure", "Inspector",
                "logical bounds", "Position is constrained", "Preview does not mutate Working", "Apply to Workspace",
                "grants no permissions", "activated revision"
            },
            ["es"] = new[]
            {
                "único y estable", "Deje Plantilla vacía", "área visual", "Estructura", "Inspector",
                "límites lógicos", "posición se limita", "Preview no modifica Working", "Aplicar al Workspace",
                "no concede permisos", "revisión activada"
            }
        };

        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var topic = Assert.Single(ContextualHelpCatalog.Build(locale).Topics, item => item.Id == "popups.overview");
            var text = string.Join("\n", topic.Sections.Select(section => section.Body));
            foreach (var phrase in requiredText[locale])
                Assert.Contains(phrase, text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("screens.overview", topic.RelatedTopicIds ?? Array.Empty<string>());
            Assert.Contains("engineering.lifecycle", topic.RelatedTopicIds ?? Array.Empty<string>());
            Assert.Contains("visual.events", topic.RelatedTopicIds ?? Array.Empty<string>());
        }
    }

    [Fact]
    public void DataSourceHelp_ExplainsSchemaToolsPreviewApplyAndRuntimeLifecycleInEveryLocale()
    {
        var requiredText = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["pt-BR"] = new[] { "schema", "referência de credencial protegida", "Connection Test", "não aplica alterações", "Preview", "Apply", "revisão Active" },
            ["en"] = new[] { "schema", "protected credential-reference field", "Connection Test", "does not apply changes", "Preview", "Apply", "Active revision" },
            ["es"] = new[] { "schema", "referencia de credencial protegida", "Connection Test", "no aplica cambios", "Preview", "Apply", "revisión Active" }
        };

        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var topic = Assert.Single(ContextualHelpCatalog.Build(locale).Topics, item => item.Id == "sources.data-sources");
            var text = string.Join("\n", topic.Sections.Select(section => section.Body));
            foreach (var phrase in requiredText[locale])
                Assert.Contains(phrase, text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("drivers.overview", topic.RelatedTopicIds ?? Array.Empty<string>());
            Assert.Contains("engineering.lifecycle", topic.RelatedTopicIds ?? Array.Empty<string>());
        }
    }

    [Fact]
    public void ProcessStageMemberInsightsHelp_PreservesItsScopeAndLimitationsInEveryLocale()
    {
        var expectations = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["pt-BR"] = new[]
            {
                "cada solicitação é independente",
                "temporário, somente leitura",
                "Trocar de equipamento apaga o resultado anterior",
                "Não se calcula score do processo",
                "Não trate valor ausente ou indisponível como zero ou normal",
                "resposta estiver desatualizada",
                "não é diagnóstico nem comprovação de eficiência"
            },
            ["en"] = new[]
            {
                "each request is independent",
                "temporary, read-only",
                "Changing Equipment clears the previous result",
                "No process-wide score",
                "Do not treat missing or unavailable values as zero or normal",
                "response is stale",
                "not a diagnosis or proof of efficiency"
            },
            ["es"] = new[]
            {
                "cada solicitud es independiente",
                "temporal, de solo lectura",
                "Al cambiar de equipo se borra el resultado anterior",
                "No se calcula una puntuación global del proceso",
                "No interprete valores ausentes o no disponibles como cero o normales",
                "respuesta está desactualizada",
                "no es un diagnóstico ni una prueba de eficiencia"
            }
        };

        foreach (var (locale, expectedPhrases) in expectations)
        {
            var topic = Assert.Single(ContextualHelpCatalog.Build(locale).Topics,
                item => item.Id == "intelligence.process-stage-member-insights");
            var text = string.Join("\n", new[] { topic.Title, topic.Summary }
                .Concat(topic.Sections.Select(section => section.Body)));

            foreach (var phrase in expectedPhrases)
                Assert.Contains(phrase, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void UserFacingHelp_DoesNotExposeImplementationBrandsOrCoordinationJargon()
    {
        var forbiddenPhrases = new[]
        {
            "Wave 15",
            "control plane",
            "contract ID",
            "fail-closed",
            "fail closed",
            "falhar fechado",
            "test harness",
            "Monaco",
            "Vite",
            "React",
            "Pyodide",
            "Web Worker",
            "Canvas",
            "canonical",
            "canônico",
            "canônica",
            "canónico",
            "canónica"
        };
        var forbiddenCoordinationTokens = new Regex(
            @"(?<![\p{L}\p{N}_])(?:SHA(?!-256)|tree|branch|PR)(?![\p{L}\p{N}_])|(?<![0-9a-f])[0-9a-f]{40}(?![0-9a-f])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var catalog = ContextualHelpCatalog.Build(locale);
            var text = string.Join("\n", catalog.Topics.SelectMany(topic =>
                new[] { topic.Title, topic.Summary }.Concat(topic.Sections.SelectMany(section => new[] { section.Heading, section.Body }))));

            foreach (var term in forbiddenPhrases)
                Assert.DoesNotContain(term, text, StringComparison.OrdinalIgnoreCase);

            Assert.False(
                forbiddenCoordinationTokens.IsMatch(text),
                $"User-facing Help for {locale} contains a coordination identifier: {forbiddenCoordinationTokens.Match(text).Value}");
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

    private static EngineeringDataSourceTypeView[] ProductionDrivers() => EngineeringDataSourceTypeCatalog
        .BuildForCurrentSchema(CommunicationDriverRuntimeComposition.BuildForCurrentSchema())
        .Describe()
        .DataSourceTypes
        .Where(source => source.Kind == "communicationDriver")
        .Where(source => !source.TypeKey.Equals(ContextualHelpCatalog.ExcludedSimulationDriverTypeKey, StringComparison.OrdinalIgnoreCase))
        .OrderBy(source => source.TypeKey, StringComparer.Ordinal)
        .ToArray();
}
