using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Engineering.VisualScripting;
using Scada.Core.Events;
using Scada.Core.Tags;
using Scada.Historian.Policies;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpLearningTests
{
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
