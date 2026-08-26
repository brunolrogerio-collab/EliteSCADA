using System.Text.Json;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.InternalMemory;
using Scada.Core.Sources;
using Scada.Core.Tags;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.ImportExport;

namespace Scada.Core.Tests;

public sealed class InternalMemoryEngineeringTests
{
    [Fact]
    public void SchemaV8_ServerMemory_RoundTripsTypedInitialValueThroughApplyAndExport()
    {
        var (service, _, _) = CreateService();
        var tagId = Guid.NewGuid();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(
                tagId,
                "Counter",
                "Memory.Counter",
                TagDataType.Int32,
                Source: "memory.server.main",
                ReadOnly: false,
                Historian: new HistorianSettingsDto(Enabled: true, Strategy: "change"),
                InitialValue: Initial(TagDataType.Int32, "17"))],
            Array.Empty<AlarmEngineeringDto>(),
            [new DataSourceEngineeringDto(
                null,
                "memory.server.main",
                "Server Memory",
                BuiltInSourceProviderDescriptors.ServerMemory.TypeKey)]);

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        Assert.True(preview.CanApply);
        var applied = service.Apply(package, ImportMode.CreateAndUpdate);
        Assert.Empty(applied.Issues);

        var exportedJson = service.ExportJson();
        var exported = service.ParseJson(exportedJson);
        var tag = Assert.Single(exported.Tags);
        var dataSource = Assert.Single(exported.DataSources!);

        Assert.Equal(8, exported.SchemaVersion);
        Assert.Equal(BuiltInSourceProviderDescriptors.ServerMemory.TypeKey, dataSource.Driver);
        Assert.NotNull(tag.InitialValue);
        Assert.Equal(TagDataType.Int32, tag.InitialValue!.DataType);
        Assert.Equal(17, Assert.IsType<int>(MemoryEngineeringValueCodec.ToTypedValue(tag.InitialValue).Value));
        Assert.DoesNotContain("engineering.memory.initial", exportedJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SchemaV8_ClientMemory_RoundTripsAsRuntimeClientLocalSource()
    {
        var (service, _, _) = CreateService();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(
                Guid.NewGuid(),
                "DraftText",
                "Memory.Client.DraftText",
                TagDataType.String,
                Source: "memory.client.ui",
                ReadOnly: false,
                InitialValue: Initial(TagDataType.String, "\"initial\""))],
            Array.Empty<AlarmEngineeringDto>(),
            [new DataSourceEngineeringDto(
                null,
                "memory.client.ui",
                "Client Memory",
                BuiltInSourceProviderDescriptors.ClientMemory.TypeKey)]);

        Assert.True(service.Preview(package, ImportMode.CreateAndUpdate).CanApply);
        Assert.Empty(service.Apply(package, ImportMode.CreateAndUpdate).Issues);

        var exported = service.ParseJson(service.ExportJson());
        var dataSource = Assert.Single(exported.DataSources!);
        var tag = Assert.Single(exported.Tags);

        Assert.Equal(BuiltInSourceProviderDescriptors.ClientMemory.TypeKey, dataSource.Driver);
        Assert.Equal("initial", Assert.IsType<string>(MemoryEngineeringValueCodec.ToTypedValue(tag.InitialValue!).Value));
        Assert.False(tag.Historian!.Enabled);
    }

    [Fact]
    public void TagCsv_RoundTripsInternalMemoryTypedInitialValue()
    {
        var (service, _, _) = CreateService();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(
                Guid.NewGuid(),
                "Setpoint",
                "Memory.Server.Setpoint",
                TagDataType.Double,
                Source: "memory.server.main",
                ReadOnly: false,
                InitialValue: Initial(TagDataType.Double, "12.5"))],
            Array.Empty<AlarmEngineeringDto>(),
            [new DataSourceEngineeringDto(
                null,
                "memory.server.main",
                "Server Memory",
                BuiltInSourceProviderDescriptors.ServerMemory.TypeKey)]);

        Assert.Empty(service.Apply(package, ImportMode.CreateAndUpdate).Issues);
        var csv = service.ExportTagsCsv();
        var parsed = service.ParseTagsCsv(csv);
        var tag = Assert.Single(parsed.Tags);

        Assert.Contains("InitialValueDataType", csv);
        Assert.Contains("InitialValueJson", csv);
        Assert.NotNull(tag.InitialValue);
        Assert.Equal(TagDataType.Double, tag.InitialValue!.DataType);
        Assert.Equal(12.5D, Assert.IsType<double>(MemoryEngineeringValueCodec.ToTypedValue(tag.InitialValue).Value));
    }

    [Fact]
    public void LegacyTagCsv_WithoutInitialValueColumns_RemainsCompatible()
    {
        var (service, _, _) = CreateService();
        const string csv = "Id,Path,Name,DataType,Unit,Source,Address,ReadOnly,ScaleMinimum,ScaleMaximum,HistorianEnabled,HistorianStrategy,Deadband,PeriodMilliseconds,MaximumPeriodMilliseconds,Description,MetadataJson,ReadRolesJson,WriteRolesJson,ConfigureRolesJson\n,Memory.Counter,Counter,Int32,,memory.server.main,,False,,,False,none,,,,,,,,\n";

        var parsed = service.ParseTagsCsv(csv);
        var tag = Assert.Single(parsed.Tags);

        Assert.Equal(TagDataType.Int32, tag.DataType);
        Assert.Null(tag.InitialValue);
    }

    [Fact]
    public void SchemaV7_WithoutMemoryInitialValue_StillImportsAndReExportsAsCurrent()
    {
        var (service, _, _) = CreateService();
        const string json = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 7,
          "exportedAt": "2026-08-26T00:00:00Z",
          "tags": [
            {
              "name": "Counter",
              "path": "Memory.Counter",
              "dataType": "int32",
              "source": "memory.server.main",
              "readOnly": false
            }
          ],
          "alarms": [],
          "dataSources": [
            {
              "key": "memory.server.main",
              "name": "Server Memory",
              "driver": "builtin.memory.server"
            }
          ]
        }
        """;

        var historical = service.ParseJson(json);
        Assert.Equal(7, historical.SchemaVersion);
        Assert.True(service.Preview(historical, ImportMode.CreateAndUpdate).CanApply);
        Assert.Empty(service.Apply(historical, ImportMode.CreateAndUpdate).Issues);

        var migrated = service.ParseJson(service.ExportJson());
        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Null(Assert.Single(migrated.Tags).InitialValue);
    }

    [Fact]
    public void Preview_RejectsClientMemoryHistorianAndGlobalAlarm()
    {
        var (service, _, _) = CreateService();
        var tagId = Guid.NewGuid();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(
                tagId,
                "ClientValue",
                "Memory.Client.Value",
                TagDataType.Double,
                Source: "memory.client.ui",
                ReadOnly: false,
                Historian: new HistorianSettingsDto(Enabled: true),
                InitialValue: Initial(TagDataType.Double, "1.5"))],
            [new AlarmEngineeringDto(
                null,
                "Client high",
                tagId,
                "Memory.Client.Value",
                AlarmType.High,
                AlarmPriority.Medium,
                Setpoint: 10)],
            [new DataSourceEngineeringDto(
                null,
                "memory.client.ui",
                "Client Memory",
                BuiltInSourceProviderDescriptors.ClientMemory.TypeKey)]);

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);
        var issues = preview.Items.SelectMany(item => item.Issues).ToArray();

        Assert.False(preview.CanApply);
        Assert.Contains(issues, issue => issue.Code == "CLIENT_MEMORY_HISTORIAN_NOT_ALLOWED");
        Assert.Contains(issues, issue => issue.Code == "CLIENT_MEMORY_ALARM_NOT_ALLOWED");
    }

    [Fact]
    public void Preview_AllowsServerMemoryHistorianAndAlarm()
    {
        var (service, _, _) = CreateService();
        var tagId = Guid.NewGuid();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(
                tagId,
                "ServerValue",
                "Memory.Server.Value",
                TagDataType.Double,
                Source: "memory.server.main",
                ReadOnly: false,
                Historian: new HistorianSettingsDto(Enabled: true),
                InitialValue: Initial(TagDataType.Double, "1.5"))],
            [new AlarmEngineeringDto(
                null,
                "Server high",
                tagId,
                "Memory.Server.Value",
                AlarmType.High,
                AlarmPriority.Medium,
                Setpoint: 10)],
            [new DataSourceEngineeringDto(
                null,
                "memory.server.main",
                "Server Memory",
                BuiltInSourceProviderDescriptors.ServerMemory.TypeKey)]);

        Assert.True(service.Preview(package, ImportMode.CreateAndUpdate).CanApply);
    }

    [Fact]
    public void Preview_RejectsFakeNetworkConfigurationForMemorySourcesAndTags()
    {
        var (service, _, _) = CreateService();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(
                null,
                "Counter",
                "Memory.Counter",
                TagDataType.Int32,
                Source: "memory.server.main",
                Address: "40001")],
            Array.Empty<AlarmEngineeringDto>(),
            [new DataSourceEngineeringDto(
                null,
                "memory.server.main",
                "Server Memory",
                BuiltInSourceProviderDescriptors.ServerMemory.TypeKey,
                Settings: new() { ["host"] = "127.0.0.1" },
                SecretReferences: new() { ["credential"] = "secret://memory" })]);

        var issues = service.Preview(package, ImportMode.CreateAndUpdate)
            .Items.SelectMany(item => item.Issues).ToArray();

        Assert.Contains(issues, issue => issue.Code == "MEMORY_DATASOURCE_SETTINGS_NOT_ALLOWED");
        Assert.Contains(issues, issue => issue.Code == "MEMORY_DATASOURCE_SECRETS_NOT_ALLOWED");
        Assert.Contains(issues, issue => issue.Code == "MEMORY_TAG_ADDRESS_NOT_ALLOWED");
    }

    [Fact]
    public void Preview_RejectsInitialValueTypeMismatchAndNonMemoryUse()
    {
        var (service, _, _) = CreateService();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [
                new TagEngineeringDto(
                    null,
                    "Mismatch",
                    "Memory.Mismatch",
                    TagDataType.Int32,
                    Source: "memory.server.main",
                    InitialValue: Initial(TagDataType.Int64, "4")),
                new TagEngineeringDto(
                    null,
                    "External",
                    "Plant.External",
                    TagDataType.Int32,
                    Source: "plant.external",
                    InitialValue: Initial(TagDataType.Int32, "4"))
            ],
            Array.Empty<AlarmEngineeringDto>(),
            [
                new DataSourceEngineeringDto(null, "memory.server.main", "Server Memory", BuiltInSourceProviderDescriptors.ServerMemory.TypeKey),
                new DataSourceEngineeringDto(null, "plant.external", "External", "modbus.tcp")
            ]);

        var issues = service.Preview(package, ImportMode.CreateAndUpdate)
            .Items.SelectMany(item => item.Issues).ToArray();

        Assert.Contains(issues, issue => issue.Code == "MEMORY_INITIAL_VALUE_TYPE_MISMATCH");
        Assert.Contains(issues, issue => issue.Code == "MEMORY_INITIAL_VALUE_SOURCE_REQUIRED");
    }

    [Fact]
    public void Preview_RejectsReservedMemoryMetadataSmuggling()
    {
        var (service, _, _) = CreateService();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(
                null,
                "Counter",
                "Memory.Counter",
                TagDataType.Int32,
                Source: "memory.server.main",
                Metadata: new() { ["engineering.memory.initial.json"] = "99" })],
            Array.Empty<AlarmEngineeringDto>(),
            [new DataSourceEngineeringDto(
                null,
                "memory.server.main",
                "Server Memory",
                BuiltInSourceProviderDescriptors.ServerMemory.TypeKey)]);

        var issues = service.Preview(package, ImportMode.CreateAndUpdate)
            .Items.SelectMany(item => item.Issues).ToArray();

        Assert.Contains(issues, issue => issue.Code == "MEMORY_RESERVED_METADATA_NOT_ALLOWED");
    }

    [Theory]
    [InlineData(TagDataType.Boolean, "true", typeof(bool))]
    [InlineData(TagDataType.Int16, "12", typeof(short))]
    [InlineData(TagDataType.Int32, "12", typeof(int))]
    [InlineData(TagDataType.Int64, "12", typeof(long))]
    [InlineData(TagDataType.Float, "12.5", typeof(float))]
    [InlineData(TagDataType.Double, "12.5", typeof(double))]
    [InlineData(TagDataType.String, "\"value\"", typeof(string))]
    [InlineData(TagDataType.DateTime, "\"2026-08-26T12:34:56Z\"", typeof(DateTimeOffset))]
    [InlineData(TagDataType.Enum, "7", typeof(int))]
    public void TypedInitialValueCodec_PreservesExpectedRuntimeType(
        TagDataType dataType,
        string json,
        Type expectedType)
    {
        var typed = MemoryEngineeringValueCodec.ToTypedValue(Initial(dataType, json));
        Assert.IsType(expectedType, typed.Value);
    }

    private static MemoryInitialValueDto Initial(TagDataType type, string json)
    {
        using var document = JsonDocument.Parse(json);
        return new MemoryInitialValueDto(type, document.RootElement.Clone());
    }

    private static (EngineeringExchangeService Service, InMemoryTagRegistry Tags, InMemoryDataSourceEngineeringRegistry DataSources) CreateService()
    {
        var tags = new InMemoryTagRegistry();
        var bus = new InMemoryScadaEventBus();
        var alarms = new InMemoryAlarmEngine(bus);
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        return (new EngineeringExchangeService(tags, alarms, dataSources), tags, dataSources);
    }
}
