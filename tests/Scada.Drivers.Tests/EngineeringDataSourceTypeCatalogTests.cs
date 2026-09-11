using Scada.Core.Sources;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Bacnet;
using Scada.Drivers.Modbus;
using Scada.Drivers.OpcUa;
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
        Assert.NotNull(modbus.ConfigurationSchema);
        var schema = modbus.ConfigurationSchema!;

        Assert.Equal("communicationDriver", modbus.Kind);
        Assert.False(modbus.Capabilities.SupportsBrowse);
        Assert.False(modbus.Capabilities.SupportsDiscovery);
        Assert.Contains(schema.DataSourceFields, field => field.Key == "host" && field.ValueKind == "host" && field.Required);
        Assert.Contains(schema.DataSourceFields, field => field.Key == "port" && field.ValueKind == "port" && field.DefaultValue == "502");
        Assert.Contains(schema.DataSourceFields, field => field.Key == "unitId" && field.Minimum == 0 && field.Maximum == 255);
    }

    [Fact]
    public void Catalog_ProvidesHumanLabelsExpectedFormatsAndExamples()
    {
        var catalog = BuildCatalog().Describe();
        var modbus = Assert.Single(catalog.DataSourceTypes, x => x.TypeKey == ModbusTcpDriverDescriptorProvider.DriverTypeId);
        Assert.NotNull(modbus.ConfigurationSchema);
        var modbusSchema = modbus.ConfigurationSchema!;
        var host = Assert.Single(modbusSchema.DataSourceFields, field => field.Key == "host");
        var port = Assert.Single(modbusSchema.DataSourceFields, field => field.Key == "port");

        Assert.Equal("Host", host.DisplayName);
        var hostFormat = Assert.IsType<string>(host.ExpectedFormat);
        Assert.Contains("DNS", hostFormat, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("192.168.1.10", host.ExampleValue);
        var portFormat = Assert.IsType<string>(port.ExpectedFormat);
        Assert.Contains("port", portFormat, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("502", port.ExampleValue);

        var opcUa = Assert.Single(catalog.DataSourceTypes, x => x.TypeKey == OpcUaDriverDescriptorProvider.DriverTypeId);
        Assert.NotNull(opcUa.ConfigurationSchema);
        var opcUaSchema = opcUa.ConfigurationSchema!;
        var endpoint = Assert.Single(opcUaSchema.DataSourceFields, field => field.Key == "endpointUrl");
        var duration = Assert.Single(opcUaSchema.DataSourceFields, field => field.Key == "sessionTimeout");
        var endpointFormat = Assert.IsType<string>(endpoint.ExpectedFormat);
        var endpointExample = Assert.IsType<string>(endpoint.ExampleValue);
        var durationFormat = Assert.IsType<string>(duration.ExpectedFormat);
        Assert.Contains("URL", endpointFormat, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("opc.tcp://", endpointExample);
        Assert.Contains("hh:mm:ss", durationFormat, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Catalog_BacnetMillisecondFieldsAreIntegersInVersionedSchema()
    {
        var catalog = BuildCatalog().Describe();
        var bacnet = Assert.Single(catalog.DataSourceTypes, x => x.TypeKey == BacnetDriverDescriptor.DriverType);
        Assert.NotNull(bacnet.ConfigurationSchema);
        var schema = bacnet.ConfigurationSchema!;

        Assert.Equal(2, schema.SchemaVersion);
        Assert.Contains(schema.DataSourceFields, field =>
            field.Key == "scanIntervalMilliseconds" &&
            field.ValueKind == "integer" &&
            field.DefaultValue == "1000");
        Assert.Contains(schema.DataSourceFields, field =>
            field.Key == "requestTimeoutMilliseconds" &&
            field.ValueKind == "integer" &&
            field.DefaultValue == "3000");
        Assert.Contains(schema.DataSourceFields, field =>
            field.Key == "discoveryWindowMilliseconds" &&
            field.ValueKind == "integer" &&
            field.DefaultValue == "1500");
    }

    [Fact]
    public void Catalog_SimulationAcceptsCanonicalDemoScanInterval()
    {
        var catalog = BuildCatalog();
        var described = catalog.Describe();
        var simulation = Assert.Single(described.DataSourceTypes, x => x.TypeKey == SimulationDriverDescriptorProvider.DriverTypeId);
        Assert.NotNull(simulation.ConfigurationSchema);
        var schema = simulation.ConfigurationSchema!;
        var scanInterval = Assert.Single(schema.DataSourceFields, field => field.Key == "scanIntervalMilliseconds");

        Assert.Equal("integer", scanInterval.ValueKind);
        Assert.Equal("500", scanInterval.DefaultValue);

        var issues = catalog.Validate(new DataSourceEngineeringDto(
            null,
            "builtin.simulation",
            "Simulation",
            SimulationDriverDescriptorProvider.DriverTypeId,
            Settings: new Dictionary<string, string>
            {
                ["scanIntervalMilliseconds"] = "500"
            }));

        Assert.Empty(issues);
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
    public void Validator_AcceptsCanonicalTimeSpanDurationAndRejectsInvalidDuration()
    {
        var catalog = BuildCatalog();
        var validSettings = new Dictionary<string, string>
        {
            ["endpointUrl"] = "opc.tcp://192.0.2.20:4840",
            ["securityPolicyUri"] = "http://opcfoundation.org/UA/SecurityPolicy#Basic256Sha256",
            ["sessionTimeout"] = "00:01:00"
        };

        var valid = catalog.Validate(new DataSourceEngineeringDto(
            null,
            "opc",
            "OPC UA",
            OpcUaDriverDescriptorProvider.DriverTypeId,
            Settings: validSettings));
        Assert.Empty(valid);

        validSettings["sessionTimeout"] = "not-a-duration";
        var invalid = catalog.Validate(new DataSourceEngineeringDto(
            null,
            "opc",
            "OPC UA",
            OpcUaDriverDescriptorProvider.DriverTypeId,
            Settings: validSettings));
        Assert.Contains(invalid, issue =>
            issue.Code == "DATASOURCE_SETTING_INVALID" &&
            issue.Message.Contains("00:00:05", StringComparison.Ordinal));
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
