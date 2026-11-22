using System.Text.Json;
using Scada.Api.Runtime;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Abstractions;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;

namespace Scada.Drivers.Tests;

public sealed class ContextualHelpMatterExamplesTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void PublishedReadExamplePassesActualPlannerButNeverProvesHostOrDeviceReadiness(string locale)
    {
        var (source, tag) = Example(locale);
        var result = new MatterWifiCommunicationRuntimePlanner().Plan(Package(source, tag), source);
        Assert.True(result.CanActivate, string.Join("; ", result.Issues.Select(i => i.Message)));
        var point = Assert.Single(Assert.IsType<MatterWifiCommunicationRuntimePlan>(result.Plan).Points);
        Assert.Equal("123", point.NodeId);
        Assert.Equal("1/6/0", point.PortableAddress);
        Assert.True(point.Tag.ReadOnly);
        Assert.Equal(TagDataType.Boolean, point.Tag.DataType);
        // This exercises planning only: no host key, sidecar, socket, device or commissioning.
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en")]
    [InlineData("es")]
    public void PublishedFailureCasesFailClosedInsteadOfBecomingAWorkingConnection(string locale)
    {
        var (source, tag) = Example(locale);
        var planner = new MatterWifiCommunicationRuntimePlanner();
        void Rejected(DataSourceEngineeringDto candidateSource, TagEngineeringDto candidateTag, string code)
        {
            var result = planner.Plan(Package(candidateSource, candidateTag), candidateSource);
            Assert.False(result.CanActivate);
            Assert.Contains(result.Issues, i => i.Code == code && i.IsError);
        }
        Rejected(source with { SecretReferences = null }, tag, "MATTER_WIFI_STORAGE_KEY_REQUIRED");
        Rejected(source, tag with { CommunicationBinding = null }, "MATTER_WIFI_TAG_BINDING_REQUIRED");
        Rejected(source, tag with { Address = "node/124/endpoint/1/cluster/6/attribute/0" }, "MATTER_WIFI_TAG_ADDRESS_MISMATCH");
        var invalidAddress = "node/0123/endpoint/1/cluster/6/attribute/0";
        Rejected(source, tag with { Address = invalidAddress, CommunicationBinding = tag.CommunicationBinding! with { PortableAddress = invalidAddress } }, "MATTER_WIFI_TAG_ADDRESS_INVALID");
        Rejected(source, tag with { CommunicationBinding = tag.CommunicationBinding! with { Settings = new Dictionary<string, string> { ["address"] = tag.Address! } } }, "MATTER_WIFI_TAG_BINDING_SETTINGS_UNSUPPORTED");
        Rejected(source with { Settings = new Dictionary<string, string> { ["host"] = "example.invalid" } }, tag, "MATTER_WIFI_SETTING_UNSUPPORTED");
        Rejected(source, tag with { DataType = TagDataType.DateTime }, "MATTER_WIFI_TAG_TYPE_UNSUPPORTED");
        var ha = new MatterWifiCommunicationRuntimePlanner(() => true).Plan(Package(source, tag), source);
        Assert.False(ha.CanActivate);
        Assert.Contains(ha.Issues, i => i.Code == "MATTER_WIFI_HA_UNSUPPORTED" && i.IsError);
    }

    [Fact]
    public void InstalledGuideExistsEvenThoughTheDefaultDriverCatalogDoesNotOfferMatter()
    {
        var types = EngineeringDataSourceTypeCatalog.BuildForCurrentSchema(CommunicationDriverRuntimeComposition.BuildForCurrentSchema()).Describe().DataSourceTypes;
        Assert.DoesNotContain(types, type => type.TypeKey == MatterWifiDriverDescriptorProvider.DriverType);
        var descriptor = new MatterWifiDriverDescriptorProvider().Descriptor;
        Assert.Equal(DriverEngineeringCapabilities.None, descriptor.EngineeringCapabilities);
        Assert.Empty(descriptor.ConfigurationSchema.TagBindingFields);
        foreach (var locale in ContextualHelpCatalog.SupportedLocales)
        {
            var catalog = ContextualHelpCatalog.Build(locale);
            var guide = Assert.Single(catalog.Topics, t => t.Id == "drivers.matter-test-workflow");
            Assert.DoesNotContain(catalog.Topics, t => t.Id == "driver.matter.wifi");
            Assert.Contains(guide.Id, catalog.Topics.Single(t => t.Id == "drivers.overview").RelatedTopicIds!);
            var keys = guide.Sections.SelectMany(s => s.Fields ?? []).Select(f => f.Key).ToHashSet();
            Assert.Contains("communicationBinding", keys);
            Assert.Contains("fabricStorageKey", keys);
            Assert.Contains("portableAddress", keys);
            foreach (var field in descriptor.ConfigurationSchema.DataSourceFields)
                Assert.Contains(field.Key, keys);
        }
    }

    private static (DataSourceEngineeringDto, TagEngineeringDto) Example(string locale)
    {
        var guide = ContextualHelpCatalog.Build(locale).Topics.Single(t => t.Id == "drivers.matter-test-workflow");
        using var json = JsonDocument.Parse(Assert.Single(guide.Sections, s => s.Code is not null).Code!);
        var root = json.RootElement;
        var sourceExample = root.GetProperty("source");
        var source = new DataSourceEngineeringDto(Guid.NewGuid(), sourceExample.GetProperty("key").GetString()!, "Matter example",
            sourceExample.GetProperty("driver").GetString()!, SecretReferences: new Dictionary<string, string>
            { [MatterWifiDriverDescriptorProvider.FabricStorageKeyField] = HostMaterialSlotReference.Marker });
        var value = root.GetProperty("tag");
        var binding = value.GetProperty("communicationBinding").Deserialize<CommunicationTagBinding>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var tag = new TagEngineeringDto(Guid.NewGuid(), "On", value.GetProperty("path").GetString()!,
            Enum.Parse<TagDataType>(value.GetProperty("dataType").GetString()!), source.Key,
            Address: value.GetProperty("address").GetString(), ReadOnly: value.GetProperty("readOnly").GetBoolean(),
            CommunicationBinding: binding, DataSourceId: source.Id);
        return (source, tag);
    }

    private static EngineeringPackage Package(DataSourceEngineeringDto source, TagEngineeringDto tag) => new(
        EngineeringExchangeService.CurrentSchema, EngineeringExchangeService.CurrentSchemaVersion, DateTimeOffset.UtcNow, [tag], [], DataSources: [source]);
}
