using Scada.Core.Sources;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Modbus;
using Scada.Drivers.Simulation;
using Scada.Engineering.Contracts;

namespace Scada.Drivers.Tests;

public sealed class EngineeringDataSourceTypeCatalogTests
{
    [Fact]
    public void Catalog_MatchesRuntimeBuildAndCanonicalBuiltInsWithoutDuplicates()
    {
        var runtime = CommunicationDriverRuntimeComposition.BuildForCurrentSchema();
        var catalog = EngineeringDataSourceTypeCatalog.BuildForCurrentSchema(runtime).Describe();
        var returned = catalog.DataSourceTypes.Select(x => x.TypeKey).ToArray();

        var expected = runtime.Registrations.Select(x => x.Descriptor.DriverType)
            .Concat(new[]
            {
                ModbusTcpDriverDescriptorProvider.DriverTypeId,
                SimulationDriverDescriptorProvider.DriverTypeId,
                BuiltInSourceProviderDescriptors.ServerMemory.TypeKey,
                BuiltInSourceProviderDescriptors.ClientMemory.TypeKey
            })
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(expected, returned.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray());
        Assert.Equal(returned.Length, returned.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(runtime.Registrations, registration =>
            Assert.Contains(catalog.DataSourceTypes, entry =>
                entry.TypeKey.Equals(registration.Descriptor.DriverType, StringComparison.OrdinalIgnoreCase) &&
                entry.DisplayName == registration.Descriptor.DisplayName));
    }

    [Fact]
    public void Catalog_ProjectsModbusTypedConfigurationAndCapabilityContract()
    {
        var catalog = BuildCatalog().Describe();
        var modbus = Assert.Single(catalog.DataSourceTypes, x => x.TypeKey == ModbusTcpDriverDescriptorProvider.DriverTypeId);
        var schema = Assert.NotNull(modbus.ConfigurationSchema);

        Assert.Equal("communicationDriver", modbus.Kind);
        Assert.False(modbus.Capabilities.SupportsBrowse);
        Assert.False(modbus.Capabilities.SupportsDiscovery);
        Assert.Contains(schema.DataSourceFields, field => field.Key == "host" && field.ValueKind == "host" && field.Required);
        Assert.Contains(schema.DataSourceFields, field => field.Key == "port" && field.ValueKind == "port" && field.DefaultValue == "502");
        Assert.Contains(schema.DataSourceFields, field => field.Key == "unitId" && field.Minimum == 0 && field.Maximum == 255);
    }

    [Fact]
    public void Validator_RejectsUnavailableTypeUnknownSettingsAndInvalidTypedValues()
    {
        var catalog = BuildCatalog();

        var unavailable = catalog.Validate(new DataSourceEngineeringDto(
            null,
            "legacy",
            "Legacy",
            "opc.da"));
        Assert.Contains(unavailable, x => x.Code == "DATASOURCE_TYPE_UNAVAILABLE" && x.IsError);

        var invalidModbus = catalog.Validate(new DataSourceEngineeringDto(
            null,
            "plc",
            "PLC",
            ModbusTcpDriverDescriptorProvider.DriverTypeId,
            Settings: new Dictionary<string, string>
            {
                ["host"] = "192.0.2.10",
                ["port"] = "70000",
                ["obsoleteField"] = "do-not-reinterpret"
            }));

        Assert.Contains(invalidModbus, x => x.Code == "DATASOURCE_SETTING_INVALID" && x.IsError);
        Assert.Contains(invalidModbus, x => x.Code == "DATASOURCE_SETTING_UNKNOWN" && x.IsError);
    }

    [Fact]
    public void Validator_AcceptsCanonicalDefaultsAndMemorySources()
    {
        var catalog = BuildCatalog();

        var modbus = catalog.Validate(new DataSourceEngineeringDto(
            null,
            "plc",
            "PLC",
            ModbusTcpDriverDescriptorProvider.DriverTypeId,
            Settings: new Dictionary<string, string> { ["host"] = "192.0.2.10" }));
        var serverMemory = catalog.Validate(new DataSourceEngineeringDto(
            null,
            "server-memory",
            "Server Memory",
            BuiltInSourceProviderDescriptors.ServerMemory.TypeKey));

        Assert.Empty(modbus);
        Assert.Empty(serverMemory);
    }

    private static EngineeringDataSourceTypeCatalog BuildCatalog() =>
        EngineeringDataSourceTypeCatalog.BuildForCurrentSchema(
            CommunicationDriverRuntimeComposition.BuildForCurrentSchema());
}
