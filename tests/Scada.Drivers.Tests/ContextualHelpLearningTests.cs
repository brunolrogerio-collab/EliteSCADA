using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Engineering.VisualScripting;
using Scada.Core.Events;
using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.Historian.Policies;
using Scada.Security.Authorization;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpLearningTests
{
    [Theory]
    [InlineData("pt-BR", "Gerar painel para o Runtime", "Gerar rascunho do painel")]
    [InlineData("en", "Create a Runtime dashboard", "Generate dashboard draft")]
    [InlineData("es", "Crear un panel para Runtime", "Generar borrador del panel")]
    public void IntelligenceDashboardGuideExplainsTheVisibleGeneratorAndItsUnsavedDraft(string locale, string sectionLabel, string actionLabel)
    {
        var topic = ContextualHelpCatalog.Build(locale).Topics.Single(item => item.Id == "intelligence.dashboard-workflow");
        var fullText = string.Join("\n", topic.Sections.Select(section => section.Heading + "\n" + section.Body));

        Assert.Contains(sectionLabel, fullText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(actionLabel, fullText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Historian", fullText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(locale switch { "pt-BR" => "não foi salva", "en" => "has not been saved", _ => "todavía no está guardada" },
            fullText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(locale switch { "pt-BR" => "licença", "en" => "license", _ => "licencia" },
            fullText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeStationGuideExplainsCurrentWorkflowAndLimitationsInEveryLocale()
    {
        var expectedFields = new[] { "station.name", "station.screen", "station.mode", "station.windows" };
        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var catalog = ContextualHelpCatalog.Build(locale);
            var topic = Assert.Single(catalog.Topics, item => item.Id == "runtime.station-multimonitor");
            var fields = topic.Sections.SelectMany(section => section.Fields ?? []).Select(field => field.Key);
            Assert.Equal(expectedFields, fields);
            Assert.Contains("runtime.session-classes", topic.RelatedTopicIds ?? []);
            var fullText = string.Join("\n", topic.Sections.Select(section => section.Heading + "\n" + section.Body));
            var manualPlacement = locale switch
            {
                "pt-BR" => "posição física",
                "en" => "physical monitor",
                _ => "monitor físico"
            };
            Assert.Contains(manualPlacement, fullText, StringComparison.OrdinalIgnoreCase);
            var viewOnlyLabel = locale switch { "pt-BR" => "Visualização", "en" => "View Only", _ => "Solo visualización" };
            var interactiveLabel = locale switch { "pt-BR" => "Interativa", "en" => "Interactive", _ => "Interactiva" };
            Assert.Contains(topic.Sections, section => section.Body.Contains(viewOnlyLabel, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(topic.Sections, section => section.Body.Contains(interactiveLabel, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void ExactPublishedCsvExamplesParseIntoTypedTagsAndTheMatchingGatewayRoute(string locale)
    {
        var catalog = ContextualHelpCatalog.Build(locale);
        string Example(string id) => Assert.Single(catalog.Topics.Single(topic => topic.Id == id).Sections,
            section => section.Code is not null).Code!;
        var current = new EngineeringPackage(EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion, DateTimeOffset.UtcNow, [], [],
            DataSources: [new DataSourceEngineeringDto(Guid.NewGuid(), "memory.server", "Server Memory", "builtin.memory.server")]);
        var exchange = new EngineeringTabularExchange();
        var parsed = exchange.ParseCsv(current, EngineeringTabularEntity.Tags, Example("engineering.tabular-workflow"));
        var source = Assert.Single(parsed.Tags, tag => tag.Path == "Server.Source");
        var destination = Assert.Single(parsed.Tags, tag => tag.Path == "Server.Destination");
        Assert.Equal(TagDataType.Int16, source.DataType);
        Assert.Equal(TagDataType.Int32, destination.DataType);
        Assert.False(source.ReadOnly);
        Assert.Equal("memory.server", source.Source);
        var route = Assert.Single(exchange.ParseCsv(current, EngineeringTabularEntity.GatewayRoutes,
            Example("gateway.configure-test")).Gateways!);
        Assert.Equal(source.Path, route.SourceTagPath);
        Assert.Equal(destination.Path, route.DestinationTagPath);
        Assert.Equal(GatewayTransferMode.OnChange, route.TransferMode);
        Assert.Equal(GatewayConversionPolicy.CheckedNumeric, route.ConversionPolicy);
        Assert.Equal(GatewayInitialTransferPolicy.WaitForNextAcceptableValue, route.InitialTransferPolicy);
        Assert.Equal(2, route.Gain);
        Assert.Equal(5, route.Offset);
        Assert.True(route.Enabled);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void PublishedStationRoleKeepsResourceScopeAndDoesNotGrantOtherActions(string locale)
    {
        var guide = ContextualHelpCatalog.Build(locale).Topics.Single(topic => topic.Id == "security.policy-scopes");
        var actions = guide.Sections.SelectMany(section => section.Fields ?? [])
            .Where(field => field.Key.StartsWith("capability.", StringComparison.Ordinal)).Select(field => field.Example);
        Assert.Contains("CommandExecute", actions);
        Assert.Contains("ProcessValueWrite", actions);
        Assert.Contains("AssetProcessIntelligenceManage", actions);
        var station = Guid.NewGuid();
        var pump = Guid.NewGuid();
        var anotherStation = Guid.NewGuid();
        var scope = new AuthorizationScope(ScopeNodeId: station, IncludeDescendants: true);
        var authorization = new InMemoryCapabilityAuthorizationService([
            new RolePolicy("operador-estacao01", "Station 01 operator", [
                new CapabilityGrant(SecurityCapability.TagRead, scope),
                new CapabilityGrant(SecurityCapability.AlarmAcknowledge, scope)]),
            new RolePolicy("administration-only", "Administration only", [new CapabilityGrant(SecurityCapability.SystemAdmin)])
        ]);
        var principal = new SecurityPrincipal("local-test-user", "Test operator", ["operador-estacao01"]);
        var child = new AuthorizationResource(ScopeNodeId: pump, ScopeNodeAncestry: [pump, station]);
        Assert.True(authorization.Evaluate(principal, SecurityCapability.TagRead, child).Allowed);
        Assert.True(authorization.Evaluate(principal, SecurityCapability.AlarmAcknowledge, child).Allowed);
        Assert.False(authorization.Evaluate(principal, SecurityCapability.TagRead,
            new AuthorizationResource(ScopeNodeId: anotherStation, ScopeNodeAncestry: [anotherStation])).Allowed);
        Assert.False(authorization.Evaluate(principal, SecurityCapability.CommandExecute, child).Allowed);
        Assert.False(authorization.Evaluate(principal, SecurityCapability.ProcessValueWrite, child).Allowed);
        var administrationOnly = new SecurityPrincipal("admin-test", "Test administrator", ["administration-only"]);
        Assert.False(authorization.Evaluate(administrationOnly, SecurityCapability.AssetProcessIntelligenceManage, child).Allowed);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public async Task PublishedHighAlarmWalkthroughMatchesRuntimeAtEqualityAndOnBothAcknowledgementPaths(string locale)
    {
        var guide = ContextualHelpCatalog.Build(locale).Topics.Single(topic => topic.Id == "alarms.configuration");
        Assert.Contains(guide.Sections.SelectMany(section => section.Fields ?? []), field =>
            field.Key == "alarm.setpoint" && field.Description.Contains('≥'));
        var bus = new InMemoryScadaEventBus();
        var cache = new CurrentTagCache(bus);
        using var engine = new InMemoryAlarmEngine(bus);
        var tag = TagDefinition.Create("Pressure", "Planta.Bomba01.Pressao", TagDataType.Double, engineeringUnit: "bar");
        var definition = engine.Register(AlarmDefinition.Create("High pressure", tag.Id, AlarmType.High,
            AlarmPriority.High, setpoint: 10));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 9.0));
        Assert.Equal(AlarmState.Normal, Assert.Single(engine.Snapshot()).State);
        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 10.0));
        Assert.Equal(AlarmState.Active, Assert.Single(engine.Snapshot(activeOnly: true)).State);
        Assert.True(await engine.AcknowledgeAsync(definition.Id, "test-operator"));
        Assert.Equal(AlarmState.Acknowledged, Assert.Single(engine.Snapshot(activeOnly: true)).State);
        Assert.True(cache.TryGet(tag.Id, out var unchanged));
        Assert.Equal(10.0, unchanged!.Value);
        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 9.0));
        Assert.Equal(AlarmState.Returned, Assert.Single(engine.Snapshot()).State);
        Assert.Empty(engine.Snapshot(activeOnly: true));

        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 10.0));
        await cache.UpdateAsync(tag, TagValue.Good(tag.Id, 9.0));
        var pending = Assert.Single(engine.Snapshot(activeOnly: true));
        Assert.Equal(AlarmState.Returned, pending.State);
        Assert.Null(pending.AcknowledgedAt);
        Assert.True(await engine.AcknowledgeAsync(definition.Id, "test-operator"));
        Assert.Empty(engine.Snapshot(activeOnly: true));
    }

    [Theory]
    [InlineData("pt-BR", "desmarcado", "lista ativa", "visão completa")]
    [InlineData("en", "unchecked", "active list", "full view")]
    [InlineData("es", "desmarcado", "lista activa", "vista completa")]
    public void PublishedAlarmHelpExplainsAcknowledgementExemptionAndReturnedVisibility(
        string locale,
        string uncheckedLabel,
        string activeListLabel,
        string fullViewLabel)
    {
        var catalog = ContextualHelpCatalog.Build(locale);
        var configuration = catalog.Topics.Single(topic => topic.Id == "alarms.configuration");
        var acknowledgementField = Assert.Single(configuration.Sections
            .SelectMany(section => section.Fields ?? []), field => field.Key == "alarm.requiresAcknowledgement");
        Assert.Contains(uncheckedLabel, acknowledgementField.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(activeListLabel, acknowledgementField.Description, StringComparison.OrdinalIgnoreCase);

        var runtime = catalog.Topics.Single(topic => topic.Id == "alarms.runtime-operation");
        var runtimeText = string.Join("\n", runtime.Sections.Select(section => section.Body));
        var returnedField = Assert.Single(runtime.Sections.SelectMany(section => section.Fields ?? []),
            field => field.Key == "alarmState.returned");
        Assert.Contains(activeListLabel, runtimeText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fullViewLabel, runtimeText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(activeListLabel, returnedField.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fullViewLabel, returnedField.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void PublishedHistorianExampleMatchesActualCaptureAdmission(string locale)
    {
        var guide = ContextualHelpCatalog.Build(locale).Topics.Single(topic => topic.Id == "historian.capture-query");
        var prose = string.Join("\n", guide.Sections.Select(section => section.Body));
        foreach (var exampleValue in new[] { "0.1", "1.00", "1.03", "1.12" })
            Assert.Contains(exampleValue, prose, StringComparison.Ordinal);
        var tag = new TagDefinition(Guid.NewGuid(), "Pressure", "Test.Pump.Pressure", TagDataType.Double,
            Source: "memory.server", EngineeringUnit: "bar", Description: null, ReadOnly: false,
            Metadata: new Dictionary<string, string>
            {
                [HistorianCapturePolicy.EnabledMetadataKey] = "true",
                [HistorianCapturePolicy.StrategyMetadataKey] = "onChangeDeadbandMaxInterval",
                [HistorianCapturePolicy.DeadbandMetadataKey] = "0.1",
                [HistorianCapturePolicy.MaximumPeriodMetadataKey] = "60000"
            });
        var admission = new HistorianCaptureAdmission();
        var start = DateTimeOffset.Parse("2026-10-25T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        HistorianCaptureDecision Observe(double value, int seconds, TagQuality quality = TagQuality.Good)
        {
            var timestamp = start.AddSeconds(seconds);
            return admission.Evaluate(new TagValueChanged(tag, null,
                new TagValue(tag.Id, value, timestamp, quality, tag.Source), timestamp));
        }
        Assert.True(Observe(1.00, 0).Accepted);
        Assert.False(Observe(1.03, 1).Accepted);
        Assert.True(Observe(1.12, 2).Accepted);
        Assert.True(Observe(1.12, 3, TagQuality.BadCommunication).Accepted);
        Assert.True(Observe(1.12, 63, TagQuality.BadCommunication).Accepted);
    }

    [Fact]
    public void EveryBuiltInObjectHasAFieldReferenceForItsActualEditableProperties()
    {
        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var catalog = ContextualHelpCatalog.Build(locale);
            foreach (var schema in BuiltinVisualObjectSchemas.All)
            {
                var topic = Assert.Single(catalog.Topics, item => item.Id == ContextualHelpCatalog.VisualObjectTopicId(schema.ObjectTypeKey));
                var fields = topic.Sections.SelectMany(section => section.Fields ?? []).ToArray();
                Assert.Equal(schema.Properties.Values.Where(property => property.EngineeringEditable).Select(property => property.Key), fields.Select(field => field.Key));
                Assert.All(fields, field =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(field.Label));
                    Assert.True(field.Description.Length > 20, $"Missing explanation: {locale}/{schema.ObjectTypeKey}/{field.Key}");
                    Assert.False(string.IsNullOrWhiteSpace(field.Example));
                    Assert.False(string.IsNullOrWhiteSpace(field.Constraints));
                });
            }
        }
    }

    [Theory]
    [InlineData("pt-BR", "9999,99", "notação científica", "sete caracteres")]
    [InlineData("en", "9999.99", "scientific notation", "seven characters")]
    [InlineData("es", "9999,99", "notación científica", "siete caracteres")]
    public void TrendHelpExplainsAdaptiveSevenCharacterAxisLabelsInEveryLocale(string locale, string example, string scientificText, string characterText)
    {
        var catalog = ContextualHelpCatalog.Build(locale);
        var topic = Assert.Single(catalog.Topics, item => item.Id == ContextualHelpCatalog.VisualObjectTopicId(BuiltinVisualObjectSchemas.TrendType));
        var text = string.Join("\n", new[] { topic.Summary }.Concat(topic.Sections.Select(section => section.Body)));

        Assert.Contains(example, text, StringComparison.Ordinal);
        Assert.Contains(scientificText, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(characterText, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CheckBoxAndComboBoxHelpExplainGenericBindingsInEveryLocale()
    {
        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var catalog = ContextualHelpCatalog.Build(locale);
            var checkBox = Assert.Single(catalog.Topics, item => item.Id == ContextualHelpCatalog.VisualObjectTopicId(BuiltinVisualObjectSchemas.CheckBoxType));
            var checkBoxText = string.Join("\n", new[] { checkBox.Summary }.Concat(checkBox.Sections.Select(section => section.Body)));
            Assert.Contains("Boolean", checkBoxText);
            Assert.Contains("TAG", checkBoxText);
            Assert.Contains(locale == "en" ? "optional" : "opcional", checkBoxText);
            Assert.Contains(checkBox.Sections.SelectMany(section => section.Fields ?? []), field => field.Key == VisualPropertyKeys.Checked);

            var comboBox = Assert.Single(catalog.Topics, item => item.Id == ContextualHelpCatalog.VisualObjectTopicId(BuiltinVisualObjectSchemas.ComboBoxType));
            var comboBoxText = string.Join("\n", new[] { comboBox.Summary }.Concat(comboBox.Sections.Select(section => section.Body)));
            Assert.Contains("String", comboBoxText, StringComparison.Ordinal);
            Assert.Contains("Enum", comboBoxText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FieldReferencesKeepObjectSpecificDifferencesAndSourceConstraints()
    {
        var catalog = ContextualHelpCatalog.Build("en");
        ContextualHelpField[] Fields(string type) => catalog.Topics.Single(topic => topic.Id == ContextualHelpCatalog.VisualObjectTopicId(type))
            .Sections.SelectMany(section => section.Fields ?? []).ToArray();
        Assert.DoesNotContain(Fields(BuiltinVisualObjectSchemas.RectangleType), field => field.Key == "step");
        Assert.Contains(Fields(BuiltinVisualObjectSchemas.NumericInputType), field => field.Key == "step");
        Assert.DoesNotContain(Fields(BuiltinVisualObjectSchemas.NumericInputType), field => field.Key == "fillColor");
        Assert.Contains(Fields(BuiltinVisualObjectSchemas.RectangleType), field => field.Key == "fillColor");
        var opacity = Assert.Single(Fields(BuiltinVisualObjectSchemas.RectangleType), field => field.Key == "opacity");
        Assert.Contains("≥ 0", opacity.Constraints);
        Assert.Contains("≤ 1", opacity.Constraints);
        var asset = Assert.Single(Fields(BuiltinVisualObjectSchemas.ImageType), field => field.Key == "assetRef");
        Assert.Contains("Not writable", asset.Constraints);
        Assert.Contains("No dynamic binding", asset.Constraints);
    }

    [Fact]
    public void StructuredFieldsAndExamplesHaveTheSameShapeInAllLocales()
    {
        var catalogs = ContextualHelpCatalog.SupportedLocales.Select(ContextualHelpCatalog.Build).ToArray();
        foreach (var topic in catalogs[0].Topics)
        {
            var expected = topic.Sections.SelectMany(section => section.Fields ?? []).Select(field => field.Key).ToArray();
            var code = topic.Sections.Select(section => section.Code).ToArray();
            foreach (var catalog in catalogs.Skip(1))
            {
                var translated = catalog.Topics.Single(item => item.Id == topic.Id);
                Assert.Equal(expected, translated.Sections.SelectMany(section => section.Fields ?? []).Select(field => field.Key));
                Assert.Equal(code, translated.Sections.Select(section => section.Code));
            }
        }
    }

    [Fact]
    public void StructuredCatalogSerializesForTheBrowserWithoutLosingPropertyReferences()
    {
        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var catalog = ContextualHelpCatalog.Build(locale);
            var json = JsonSerializer.Serialize(catalog, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            using var document = JsonDocument.Parse(json);
            var rectangle = document.RootElement.GetProperty("topics").EnumerateArray()
                .Single(topic => topic.GetProperty("id").GetString() == "visual.object.core.rectangle");
            Assert.Contains(rectangle.GetProperty("sections").EnumerateArray(), section =>
                section.GetProperty("fields").ValueKind == JsonValueKind.Array && section.GetProperty("fields").GetArrayLength() > 0);
            // Optional local artifact connects browser review to this exact catalog, not a hand-written content mock.
            var output = Environment.GetEnvironmentVariable("SCADIVO_HELP_CATALOG_OUTPUT");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, $"help-{locale}.json"), json);
            }
        }
    }
}
