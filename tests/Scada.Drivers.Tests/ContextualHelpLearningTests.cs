using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Engineering.VisualScripting;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpLearningTests
{
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
