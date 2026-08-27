using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.Sources;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Security;
using Scada.Engineering.Views;

namespace Scada.Core.Tests;

public sealed class EngineeringGatewayExchangeTests
{
    [Fact]
    public void SchemaV9_RoundTripsAndAppliesGatewayRoutesWithStableTagReferences()
    {
        var sourceTags = new InMemoryTagRegistry();
        var source = TagDefinition.Create("Source", "Plant.Source", TagDataType.Double);
        var destination = TagDefinition.Create("Destination", "Plant.Destination", TagDataType.Double);
        sourceTags.Register(source);
        sourceTags.Register(destination);
        using var sourceAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var sourceGateways = new InMemoryGatewayEngineeringRegistry();
        var routeId = Guid.NewGuid();
        sourceGateways.Upsert(new GatewayRouteEngineeringDto(
            routeId,
            "plant.source-to-destination",
            "Source to destination",
            source.Id,
            source.Path,
            destination.Id,
            destination.Path,
            GatewayTransferMode.OnChange,
            GatewayQualityPolicy.GoodOnly,
            GatewayConversionPolicy.Exact,
            GatewayInitialTransferPolicy.SynchronizeFirstAcceptableValue,
            Deadband: 0.1,
            MinimumIntervalMilliseconds: 250));
        var sourceService = CreateService(sourceTags, sourceAlarms, gateways: sourceGateways);

        var json = sourceService.ExportJson(indented: false);
        var package = sourceService.ParseJson(json);

        Assert.Equal(9, package.SchemaVersion);
        var exportedRoute = Assert.Single(package.Gateways!);
        Assert.Equal(routeId, exportedRoute.Id);
        Assert.Equal(source.Id, exportedRoute.SourceTagId);
        Assert.Equal(source.Path, exportedRoute.SourceTagPath);
        Assert.Equal(destination.Id, exportedRoute.DestinationTagId);
        Assert.Equal(destination.Path, exportedRoute.DestinationTagPath);

        var targetTags = new InMemoryTagRegistry();
        using var targetAlarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var targetGateways = new InMemoryGatewayEngineeringRegistry();
        var targetService = CreateService(targetTags, targetAlarms, gateways: targetGateways);

        var preview = targetService.Preview(package, ImportMode.CreateAndUpdate);
        var result = targetService.Apply(package, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Empty(result.Issues);
        var restored = Assert.Single(targetGateways.Snapshot());
        Assert.Equal(routeId, restored.Id);
        Assert.Equal(source.Id, restored.SourceTagId);
        Assert.Equal(destination.Id, restored.DestinationTagId);
    }

    [Fact]
    public void SchemaV8WithoutGateways_RemainsReadableAndReExportsAsCurrentSchema()
    {
        var tags = new InMemoryTagRegistry();
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = CreateService(tags, alarms);
        const string json = """
        {
          "schema": "scada.engineering",
          "schemaVersion": 8,
          "exportedAt": "2026-08-26T00:00:00Z",
          "tags": [],
          "alarms": []
        }
        """;

        var historical = service.ParseJson(json);
        Assert.Equal(8, historical.SchemaVersion);
        Assert.Empty(historical.Gateways!);
        Assert.True(service.Preview(historical, ImportMode.CreateAndUpdate).CanApply);
        Assert.Empty(service.Apply(historical, ImportMode.CreateAndUpdate).Issues);

        var current = service.ParseJson(service.ExportJson());
        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, current.SchemaVersion);
        Assert.Equal(9, current.SchemaVersion);
        Assert.Empty(current.Gateways!);
    }

    [Fact]
    public void Preview_RejectsMissingAndMismatchedEndpointReferences()
    {
        var tags = new InMemoryTagRegistry();
        var source = TagDefinition.Create("Source", "Plant.Source", TagDataType.Double);
        var other = TagDefinition.Create("Other", "Plant.Other", TagDataType.Double);
        var destination = TagDefinition.Create("Destination", "Plant.Destination", TagDataType.Double);
        tags.Register(source);
        tags.Register(other);
        tags.Register(destination);
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = CreateService(tags, alarms);
        var package = Package(
            new GatewayRouteEngineeringDto(null, "missing-source", "Missing source", SourceTagPath: "Plant.DoesNotExist", DestinationTagId: destination.Id, DestinationTagPath: destination.Path),
            new GatewayRouteEngineeringDto(null, "missing-destination", "Missing destination", SourceTagId: source.Id, SourceTagPath: source.Path, DestinationTagPath: "Plant.DoesNotExist"),
            new GatewayRouteEngineeringDto(null, "mismatched-source", "Mismatched source", SourceTagId: source.Id, SourceTagPath: other.Path, DestinationTagId: destination.Id, DestinationTagPath: destination.Path));

        var codes = IssueCodes(service.Preview(package, ImportMode.CreateAndUpdate));

        Assert.Contains("GATEWAY_SOURCE_TAG_NOT_FOUND", codes);
        Assert.Contains("GATEWAY_DESTINATION_TAG_NOT_FOUND", codes);
        Assert.Contains("GATEWAY_SOURCE_TAG_MISMATCH", codes);
    }

    [Fact]
    public void Preview_RejectsSelfRouteReadOnlyDestinationAndDisabledDataSource()
    {
        var tags = new InMemoryTagRegistry();
        var source = TagDefinition.Create("Source", "Plant.Source", TagDataType.Double);
        var readOnly = TagDefinition.Create("ReadOnly", "Plant.ReadOnly", TagDataType.Double, source: "disabled.source", readOnly: true);
        tags.Register(source);
        tags.Register(readOnly);
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        dataSources.Upsert(new DataSourceEngineeringDto(null, "disabled.source", "Disabled", "modbus.tcp", Enabled: false));
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = CreateService(tags, alarms, dataSources);
        var package = Package(
            new GatewayRouteEngineeringDto(null, "self", "Self", source.Id, source.Path, source.Id, source.Path),
            new GatewayRouteEngineeringDto(null, "readonly", "Read only", source.Id, source.Path, readOnly.Id, readOnly.Path));

        var codes = IssueCodes(service.Preview(package, ImportMode.CreateAndUpdate));

        Assert.Contains("GATEWAY_SELF_ROUTE_NOT_ALLOWED", codes);
        Assert.Contains("GATEWAY_DESTINATION_READ_ONLY", codes);
        Assert.Contains("GATEWAY_DESTINATION_DATASOURCE_DISABLED", codes);
    }

    [Fact]
    public void Preview_RejectsClientMemoryEndpointsButAllowsServerMemory()
    {
        var tags = new InMemoryTagRegistry();
        var client = TagDefinition.Create("Client", "Memory.Client.Value", TagDataType.Double, source: "memory.client");
        var server = TagDefinition.Create("Server", "Memory.Server.Value", TagDataType.Double, source: "memory.server");
        var process = TagDefinition.Create("Process", "Plant.Value", TagDataType.Double);
        tags.Register(client);
        tags.Register(server);
        tags.Register(process);
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        dataSources.Upsert(new DataSourceEngineeringDto(null, "memory.client", "Client Memory", BuiltInSourceProviderDescriptors.ClientMemory.TypeKey));
        dataSources.Upsert(new DataSourceEngineeringDto(null, "memory.server", "Server Memory", BuiltInSourceProviderDescriptors.ServerMemory.TypeKey));
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = CreateService(tags, alarms, dataSources);

        var clientPreview = service.Preview(Package(
            new GatewayRouteEngineeringDto(null, "client-source", "Client source", client.Id, client.Path, process.Id, process.Path),
            new GatewayRouteEngineeringDto(null, "client-destination", "Client destination", process.Id, process.Path, client.Id, client.Path)), ImportMode.CreateAndUpdate);
        var serverPreview = service.Preview(Package(
            new GatewayRouteEngineeringDto(null, "server-source", "Server source", server.Id, server.Path, process.Id, process.Path)), ImportMode.CreateAndUpdate);

        var clientCodes = IssueCodes(clientPreview);
        Assert.Contains("GATEWAY_SOURCE_CLIENT_MEMORY_NOT_ALLOWED", clientCodes);
        Assert.Contains("GATEWAY_DESTINATION_CLIENT_MEMORY_NOT_ALLOWED", clientCodes);
        Assert.True(serverPreview.CanApply);
    }

    [Fact]
    public void Preview_RequiresExplicitSafeTypeConversionAndNumericTransforms()
    {
        var tags = new InMemoryTagRegistry();
        var int16 = TagDefinition.Create("Int16", "Plant.Int16", TagDataType.Int16);
        var int32 = TagDefinition.Create("Int32", "Plant.Int32", TagDataType.Int32);
        var boolean = TagDefinition.Create("Boolean", "Plant.Boolean", TagDataType.Boolean);
        tags.Register(int16);
        tags.Register(int32);
        tags.Register(boolean);
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = CreateService(tags, alarms);

        var exactMismatch = service.Preview(Package(new GatewayRouteEngineeringDto(
            null, "exact", "Exact", int16.Id, int16.Path, int32.Id, int32.Path)), ImportMode.CreateAndUpdate);
        var numeric = service.Preview(Package(new GatewayRouteEngineeringDto(
            null, "numeric", "Numeric", int16.Id, int16.Path, int32.Id, int32.Path,
            ConversionPolicy: GatewayConversionPolicy.CheckedNumeric, Gain: 2, Offset: 1)), ImportMode.CreateAndUpdate);
        var nonNumeric = service.Preview(Package(new GatewayRouteEngineeringDto(
            null, "non-numeric", "Non numeric", boolean.Id, boolean.Path, int32.Id, int32.Path,
            ConversionPolicy: GatewayConversionPolicy.CheckedNumeric)), ImportMode.CreateAndUpdate);
        var implicitTransform = service.Preview(Package(new GatewayRouteEngineeringDto(
            null, "implicit-transform", "Implicit transform", int32.Id, int32.Path, int32.Id, "Plant.Int32",
            Gain: 2)), ImportMode.CreateAndUpdate);

        Assert.Contains("GATEWAY_EXACT_TYPE_MISMATCH", IssueCodes(exactMismatch));
        Assert.True(numeric.CanApply);
        Assert.Contains("GATEWAY_NUMERIC_CONVERSION_REQUIRES_NUMERIC_TYPES", IssueCodes(nonNumeric));
        Assert.Contains("GATEWAY_TRANSFORM_REQUIRES_NUMERIC_CONVERSION", IssueCodes(implicitTransform));
    }

    [Fact]
    public void Preview_RejectsInvalidRateDeadbandAndPeriodicConfiguration()
    {
        var tags = new InMemoryTagRegistry();
        var numericSource = TagDefinition.Create("Source", "Plant.Source", TagDataType.Double);
        var numericDestination = TagDefinition.Create("Destination", "Plant.Destination", TagDataType.Double);
        var booleanSource = TagDefinition.Create("Bool", "Plant.Bool", TagDataType.Boolean);
        var booleanDestination = TagDefinition.Create("BoolDest", "Plant.BoolDest", TagDataType.Boolean);
        tags.Register(numericSource);
        tags.Register(numericDestination);
        tags.Register(booleanSource);
        tags.Register(booleanDestination);
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = CreateService(tags, alarms);
        var package = Package(
            new GatewayRouteEngineeringDto(null, "fast", "Too fast", numericSource.Id, numericSource.Path, numericDestination.Id, numericDestination.Path, MinimumIntervalMilliseconds: 1),
            new GatewayRouteEngineeringDto(null, "negative-deadband", "Negative deadband", numericSource.Id, numericSource.Path, numericDestination.Id, numericDestination.Path, Deadband: -1),
            new GatewayRouteEngineeringDto(null, "bool-deadband", "Boolean deadband", booleanSource.Id, booleanSource.Path, booleanDestination.Id, booleanDestination.Path, Deadband: 1),
            new GatewayRouteEngineeringDto(null, "period-missing", "Period missing", numericSource.Id, numericSource.Path, numericDestination.Id, numericDestination.Path, TransferMode: GatewayTransferMode.Periodic),
            new GatewayRouteEngineeringDto(null, "period-deadband", "Period deadband", numericSource.Id, numericSource.Path, numericDestination.Id, numericDestination.Path, TransferMode: GatewayTransferMode.Periodic, Deadband: 0.1, PeriodMilliseconds: 1000));

        var codes = IssueCodes(service.Preview(package, ImportMode.CreateAndUpdate));

        Assert.Contains("GATEWAY_ON_CHANGE_INTERVAL_OUT_OF_RANGE", codes);
        Assert.Contains("GATEWAY_DEADBAND_NEGATIVE", codes);
        Assert.Contains("GATEWAY_DEADBAND_REQUIRES_NUMERIC_SOURCE", codes);
        Assert.Contains("GATEWAY_PERIOD_REQUIRED", codes);
        Assert.Contains("GATEWAY_PERIODIC_DEADBAND_NOT_ALLOWED", codes);
    }

    [Fact]
    public void Preview_RejectsDuplicateKeysAndMultipleActiveWriters()
    {
        var tags = new InMemoryTagRegistry();
        var sourceA = TagDefinition.Create("A", "Plant.A", TagDataType.Double);
        var sourceB = TagDefinition.Create("B", "Plant.B", TagDataType.Double);
        var destination = TagDefinition.Create("Destination", "Plant.Destination", TagDataType.Double);
        var otherDestination = TagDefinition.Create("Other", "Plant.Other", TagDataType.Double);
        tags.Register(sourceA);
        tags.Register(sourceB);
        tags.Register(destination);
        tags.Register(otherDestination);
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = CreateService(tags, alarms);

        var duplicate = service.Preview(Package(
            new GatewayRouteEngineeringDto(null, "duplicate", "First", sourceA.Id, sourceA.Path, destination.Id, destination.Path),
            new GatewayRouteEngineeringDto(null, "duplicate", "Second", sourceB.Id, sourceB.Path, otherDestination.Id, otherDestination.Path)), ImportMode.CreateAndUpdate);
        var multiWriter = service.Preview(Package(
            new GatewayRouteEngineeringDto(null, "writer-a", "Writer A", sourceA.Id, sourceA.Path, destination.Id, destination.Path),
            new GatewayRouteEngineeringDto(null, "writer-b", "Writer B", sourceB.Id, sourceB.Path, destination.Id, destination.Path)), ImportMode.CreateAndUpdate);

        Assert.Contains("GATEWAY_DUPLICATE_IN_FILE", IssueCodes(duplicate));
        Assert.Contains("GATEWAY_DESTINATION_MULTI_WRITER", IssueCodes(multiWriter));
    }

    [Fact]
    public void Preview_RejectsDirectAndIndirectCyclesButAllowsAcyclicFanOut()
    {
        var tags = new InMemoryTagRegistry();
        var a = TagDefinition.Create("A", "Plant.A", TagDataType.Double);
        var b = TagDefinition.Create("B", "Plant.B", TagDataType.Double);
        var c = TagDefinition.Create("C", "Plant.C", TagDataType.Double);
        var d = TagDefinition.Create("D", "Plant.D", TagDataType.Double);
        tags.Register(a);
        tags.Register(b);
        tags.Register(c);
        tags.Register(d);
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = CreateService(tags, alarms);

        var direct = service.Preview(Package(
            Route("a-b", a, b),
            Route("b-a", b, a)), ImportMode.CreateAndUpdate);
        var indirect = service.Preview(Package(
            Route("a-b", a, b),
            Route("b-c", b, c),
            Route("c-a", c, a)), ImportMode.CreateAndUpdate);
        var acyclic = service.Preview(Package(
            Route("a-b", a, b),
            Route("a-c", a, c),
            Route("c-d", c, d)), ImportMode.CreateAndUpdate);

        Assert.Contains("GATEWAY_CYCLE_DETECTED", IssueCodes(direct));
        Assert.Contains("GATEWAY_CYCLE_DETECTED", IssueCodes(indirect));
        Assert.True(acyclic.CanApply);
    }

    [Fact]
    public void Preview_ExcludesDisabledRouteFromCycleAndMultiWriterArbitration()
    {
        var tags = new InMemoryTagRegistry();
        var a = TagDefinition.Create("A", "Plant.A", TagDataType.Double);
        var b = TagDefinition.Create("B", "Plant.B", TagDataType.Double);
        var c = TagDefinition.Create("C", "Plant.C", TagDataType.Double);
        tags.Register(a);
        tags.Register(b);
        tags.Register(c);
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var service = CreateService(tags, alarms);
        var package = Package(
            Route("a-b", a, b),
            Route("b-a-disabled", b, a) with { Enabled = false },
            Route("c-b-disabled", c, b) with { Enabled = false });

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.DoesNotContain("GATEWAY_CYCLE_DETECTED", IssueCodes(preview));
        Assert.DoesNotContain("GATEWAY_DESTINATION_MULTI_WRITER", IssueCodes(preview));
    }

    [Fact]
    public void Preview_UsesEffectiveIncomingTagStateAndRejectsExistingRouteMadeReadOnly()
    {
        var tags = new InMemoryTagRegistry();
        var source = TagDefinition.Create("Source", "Plant.Source", TagDataType.Double);
        var destination = TagDefinition.Create("Destination", "Plant.Destination", TagDataType.Double, readOnly: false);
        tags.Register(source);
        tags.Register(destination);
        using var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var gateways = new InMemoryGatewayEngineeringRegistry();
        gateways.Upsert(Route("existing", source, destination));
        var service = CreateService(tags, alarms, gateways: gateways);
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new[]
            {
                new TagEngineeringDto(
                    destination.Id,
                    destination.Name,
                    destination.Path,
                    destination.DataType,
                    ReadOnly: true)
            },
            Array.Empty<AlarmEngineeringDto>());

        var preview = service.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Items, item =>
            item.EntityKind == ImportEntityKind.Gateway &&
            item.EntityKey == "existing" &&
            item.Operation == ImportOperation.Error &&
            item.Issues.Any(issue => issue.Code == "GATEWAY_DESTINATION_READ_ONLY"));
    }

    private static EngineeringExchangeService CreateService(
        ITagRegistry tags,
        IAlarmEngine alarms,
        IDataSourceEngineeringRegistry? dataSources = null,
        IGatewayEngineeringRegistry? gateways = null) =>
        new(
            tags,
            alarms,
            dataSources ?? new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry(),
            gateways ?? new InMemoryGatewayEngineeringRegistry());

    private static EngineeringPackage Package(params GatewayRouteEngineeringDto[] routes) =>
        new(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            Gateways: routes);

    private static GatewayRouteEngineeringDto Route(string key, TagDefinition source, TagDefinition destination) =>
        new(
            null,
            key,
            key,
            source.Id,
            source.Path,
            destination.Id,
            destination.Path);

    private static string[] IssueCodes(ImportPreview preview) =>
        preview.Items.SelectMany(item => item.Issues).Select(issue => issue.Code).Distinct().ToArray();
}