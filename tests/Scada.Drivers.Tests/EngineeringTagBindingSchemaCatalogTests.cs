using Scada.Core.Sources;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Dnp3;
using Scada.Drivers.Iec60870;
using Scada.Drivers.Modbus;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class EngineeringTagBindingSchemaCatalogTests
{
    [Fact]
    public void Catalog_ProjectsDriverOwnedTagBindingSchemaWithConfigurationFallback()
    {
        var catalog = EngineeringDataSourceTypeCatalog.BuildForCurrentSchema(
            CommunicationDriverRuntimeComposition.BuildForCurrentSchema()).Describe();

        var dnp3 = Assert.Single(catalog.DataSourceTypes, x =>
            x.TypeKey == Dnp3DriverDescriptorProvider.DriverType);
        Assert.NotNull(dnp3.ConfigurationSchema);
        Assert.Equal(dnp3.ConfigurationSchema!.SchemaId, dnp3.TagBindingSchemaId);
        Assert.Equal(dnp3.ConfigurationSchema.SchemaVersion, dnp3.TagBindingSchemaVersion);

        var opcUa = Assert.Single(catalog.DataSourceTypes, x =>
            x.TypeKey == OpcUaDriverDescriptorProvider.DriverTypeId);
        Assert.NotNull(opcUa.ConfigurationSchema);
        Assert.Equal(opcUa.ConfigurationSchema!.SchemaId, opcUa.TagBindingSchemaId);
        Assert.Equal(opcUa.ConfigurationSchema.SchemaVersion, opcUa.TagBindingSchemaVersion);

        var iec104 = Assert.Single(catalog.DataSourceTypes, x =>
            x.TypeKey == Iec104DriverDescriptorProvider.SharedDescriptor.DriverType);
        Assert.Equal(Iec104DriverDescriptorProvider.BindingSchemaId, iec104.TagBindingSchemaId);
        Assert.Equal(Iec104DriverDescriptorProvider.BindingSchemaVersion, iec104.TagBindingSchemaVersion);
        Assert.NotEqual(iec104.ConfigurationSchema!.SchemaId, iec104.TagBindingSchemaId);
    }

    [Fact]
    public void Catalog_ProjectsDriverFieldLocalizationResourceKeys()
    {
        var catalog = EngineeringDataSourceTypeCatalog.BuildForCurrentSchema(
            CommunicationDriverRuntimeComposition.BuildForCurrentSchema()).Describe();

        var modbus = Assert.Single(catalog.DataSourceTypes, x =>
            x.TypeKey == ModbusTcpDriverDescriptorProvider.DriverTypeId);
        var modbusHost = Assert.Single(modbus.ConfigurationSchema!.DataSourceFields, field =>
            field.Key == "host");
        Assert.Equal("driver.modbus.tcp.datasource.host.label", modbusHost.DisplayNameResourceKey);
        Assert.Equal("driver.modbus.tcp.datasource.host.description", modbusHost.DescriptionResourceKey);

        var opcUa = Assert.Single(catalog.DataSourceTypes, x =>
            x.TypeKey == OpcUaDriverDescriptorProvider.DriverTypeId);
        var endpointUrl = Assert.Single(opcUa.ConfigurationSchema!.DataSourceFields, field =>
            field.Key == "endpointUrl");
        Assert.Equal("driver.opcua.datasource.endpointUrl.label", endpointUrl.DisplayNameResourceKey);
    }

    [Fact]
    public void Catalog_DoesNotInventTagBindingSchemaForSourceProviders()
    {
        var catalog = EngineeringDataSourceTypeCatalog.BuildForCurrentSchema(
            CommunicationDriverRuntimeComposition.BuildForCurrentSchema()).Describe();

        var serverMemory = Assert.Single(catalog.DataSourceTypes, x =>
            x.TypeKey == BuiltInSourceProviderDescriptors.ServerMemory.TypeKey);

        Assert.Null(serverMemory.ConfigurationSchema);
        Assert.Null(serverMemory.TagBindingSchemaId);
        Assert.Null(serverMemory.TagBindingSchemaVersion);
    }
}
