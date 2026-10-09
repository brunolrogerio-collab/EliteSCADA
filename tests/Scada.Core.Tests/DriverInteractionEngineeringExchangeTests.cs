using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.Interactions;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Interactions;
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Engineering.Validation;

namespace Scada.Core.Tests;

public sealed class DriverInteractionEngineeringExchangeTests
{
    [Fact]
    public void ParseJson_V22WithoutInteractionCollections_NormalizesToEmpty()
    {
        using var fixture = CreateFixture();
        var json = """
            {
              "schema": "scada.engineering",
              "schemaVersion": 22,
              "exportedAt": "2026-10-07T00:00:00Z",
              "tags": [],
              "alarms": []
            }
            """;

        var package = fixture.Exchange.ParseJson(json);

        Assert.Empty(package.TransientEventDefinitions!);
        Assert.Empty(package.CapabilityEventReferences!);
        Assert.Empty(package.RichCommandDefinitions!);
        Assert.Empty(package.DriverCommandBindings!);
    }

    [Fact]
    public void JsonV23_RoundTripsAllInteractionCollections()
    {
        using var fixture = CreateFixture();
        var package = ValidPackage(fixture.EquipmentId, fixture.DataSourceId);

        var preview = fixture.Exchange.Preview(package, ImportMode.CreateAndUpdate);
        var applied = fixture.Exchange.Apply(package, ImportMode.CreateAndUpdate);
        var json = fixture.Exchange.ExportJson(indented: false);
        var restored = fixture.Exchange.ParseJson(json);

        Assert.True(preview.CanApply);
        Assert.Empty(applied.Issues);
        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, restored.SchemaVersion);
        Assert.Single(restored.TransientEventDefinitions!);
        Assert.Single(restored.CapabilityEventReferences!);
        Assert.Single(restored.RichCommandDefinitions!);
        Assert.Single(restored.DriverCommandBindings!);
        Assert.Equal(2, Assert.Single(Assert.Single(Assert.Single(restored.Screens!).Elements!).Actions!).Version);
        Assert.Equal(2, Assert.Single(Assert.Single(Assert.Single(restored.Popups!).Elements!).Actions!).Version);
    }

    [Fact]
    public void ProjectPackage_RoundTripsInteractionGraph()
    {
        using var source = CreateFixture();
        var package = ValidPackage(source.EquipmentId, source.DataSourceId);
        Assert.True(source.Exchange.Preview(package, ImportMode.CreateAndUpdate).CanApply);
        Assert.Empty(source.Exchange.Apply(package, ImportMode.CreateAndUpdate).Issues);

        var service = new ProjectPackageService(source.Exchange);
        var bytes = service.Export("interaction-project", "Interaction Project");
        var inspection = service.Inspect(bytes);

        Assert.Equal(EngineeringExchangeService.CurrentSchemaVersion, inspection.Manifest.EngineeringSchemaVersion);
        Assert.Single(inspection.Engineering.TransientEventDefinitions!);
        Assert.Single(inspection.Engineering.CapabilityEventReferences!);
        Assert.Single(inspection.Engineering.RichCommandDefinitions!);
        Assert.Single(inspection.Engineering.DriverCommandBindings!);
        Assert.Equal(
            VisualNavigationActionKind.ExecuteRichCommand,
            Assert.Single(Assert.Single(Assert.Single(inspection.Engineering.Screens!).Elements!).Actions!).Kind);
        Assert.Equal(
            VisualNavigationActionKind.ExecuteRichCommand,
            Assert.Single(Assert.Single(Assert.Single(inspection.Engineering.Popups!).Elements!).Actions!).Kind);

        using var target = CreateFixture(seedResources: false);
        var targetService = new ProjectPackageService(target.Exchange);
        var preview = targetService.Preview(bytes, ImportMode.CreateAndUpdate);
        var result = targetService.Apply(bytes, ImportMode.CreateAndUpdate);

        Assert.True(preview.CanApply);
        Assert.Empty(result.Issues);
        Assert.Single(target.Interactions.SnapshotTransientEventDefinitions());
        Assert.Single(target.Interactions.SnapshotCapabilityEventReferences());
        Assert.Single(target.Interactions.SnapshotRichCommandDefinitions());
        Assert.Single(target.Interactions.SnapshotDriverCommandBindings());
        var restored = target.Exchange.ParseJson(target.Exchange.ExportJson(indented: false));
        Assert.Equal(
            VisualNavigationActionKind.ExecuteRichCommand,
            Assert.Single(Assert.Single(Assert.Single(restored.Screens!).Elements!).Actions!).Kind);
        Assert.Equal(
            VisualNavigationActionKind.ExecuteRichCommand,
            Assert.Single(Assert.Single(Assert.Single(restored.Popups!).Elements!).Actions!).Kind);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void VisualActionVersions_KeepLegacyAtOneAndRejectRichMismatches(int richVersion)
    {
        var richCommandId = Guid.NewGuid();
        var richAction = new VisualNavigationActionEngineeringDto(
            "click",
            VisualNavigationActionKind.ExecuteRichCommand,
            Version: richVersion,
            CommandId: richCommandId);
        var richIssues = VisualCompositionEngineeringValidation.ValidateElement(
            new VisualElementEngineeringDto(
                "rich-button",
                "button",
                Actions: [richAction]),
            ImportEntityKind.Screen,
            "screen.home");

        Assert.Contains(richIssues, issue =>
            issue.Code == "VISUAL_RICH_COMMAND_ACTION_VERSION_UNSUPPORTED");

        var implicitLegacyAction = new VisualNavigationActionEngineeringDto(
            "click",
            VisualNavigationActionKind.ExecuteCommand);
        Assert.Equal(VisualCompositionEngineeringVersions.Current, implicitLegacyAction.Version);

        var elevatedLegacyAction = implicitLegacyAction with { Version = 2, CommandId = richCommandId };
        var legacyIssues = VisualCompositionEngineeringValidation.ValidateElement(
            new VisualElementEngineeringDto(
                "legacy-button",
                "button",
                Actions: [elevatedLegacyAction]),
            ImportEntityKind.Screen,
            "screen.home");

        Assert.Contains(legacyIssues, issue =>
            issue.Code == "VISUAL_COMPOSITION_VERSION_UNSUPPORTED");
    }

    [Fact]
    public void Preview_RejectsRichCommandActionWhenDefinitionIsNotProspectiveOrActive()
    {
        using var fixture = CreateFixture();
        var missingCommandId = Guid.NewGuid();
        var package = EmptyPackage(fixture.EquipmentId, fixture.DataSourceId) with
        {
            Screens =
            [
                new ScreenEngineeringDto(
                    Guid.NewGuid(),
                    "screen.home",
                    "Home",
                    Elements:
                    [
                        new VisualElementEngineeringDto(
                            "open-cover",
                            "button",
                            Actions:
                            [
                                new VisualNavigationActionEngineeringDto(
                                    "click",
                                    VisualNavigationActionKind.ExecuteRichCommand,
                                    Version: VisualNavigationActionVersions.RichCommand,
                                    CommandId: missingCommandId)
                            ])
                    ])
            ]
        };

        var preview = fixture.Exchange.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(item => item.Issues),
            issue => issue.Code == "VISUAL_RICH_COMMAND_NOT_FOUND");
    }

    [Fact]
    public void Preview_RejectsBindingWithoutRichCommand()
    {
        using var fixture = CreateFixture();
        var package = EmptyPackage(fixture.EquipmentId, fixture.DataSourceId) with
        {
            DriverCommandBindings =
            [
                ValidBinding(Guid.NewGuid(), fixture.DataSourceId, fixture.EquipmentId)
            ]
        };

        var preview = fixture.Exchange.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(item => item.Issues),
            issue => issue.Code == "DRIVER_INTERACTION_GRAPH_INVALID" &&
                     issue.Message.Contains("unknown Rich Command", StringComparison.Ordinal));
    }

    [Fact]
    public void Preview_RejectsBindingWithoutDataSource()
    {
        using var fixture = CreateFixture();
        var commandId = Guid.NewGuid();
        var package = EmptyPackage(fixture.EquipmentId, null) with
        {
            RichCommandDefinitions = [ValidCommand(commandId)],
            DriverCommandBindings = [ValidBinding(commandId, Guid.NewGuid(), fixture.EquipmentId)]
        };

        var preview = fixture.Exchange.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(item => item.Issues),
            issue => issue.Code == "DRIVER_INTERACTION_GRAPH_INVALID" &&
                     issue.Message.Contains("unknown Data Source", StringComparison.Ordinal));
    }

    [Fact]
    public void Preview_RejectsInconsistentCapabilityEventReference()
    {
        using var fixture = CreateFixture();
        var eventId = Guid.NewGuid();
        var package = EmptyPackage(fixture.EquipmentId, fixture.DataSourceId) with
        {
            TransientEventDefinitions =
            [
                ValidEvent(eventId, fixture.EquipmentId, "cover.main", "cover.stopped")
            ],
            CapabilityEventReferences =
            [
                new CapabilityEventReferenceEngineeringDto(
                    fixture.EquipmentId,
                    "cover.main",
                    "stopped",
                    eventId,
                    "cover.moved")
            ]
        };

        var preview = fixture.Exchange.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(item => item.Issues),
            issue => issue.Code == "DRIVER_INTERACTION_GRAPH_INVALID");
    }

    [Fact]
    public void Preview_RejectsDuplicateIdsAndInvalidScalarSchema()
    {
        using var fixture = CreateFixture();
        var commandId = Guid.NewGuid();
        var invalid = new RichCommandDefinitionEngineeringDto(
            commandId,
            "cover.move",
            [
                new RichCommandParameterDefinition(
                    "label",
                    new InteractionScalarSchema(
                        InteractionScalarKind.String,
                        MaximumLength: 0))
            ]);

        var package = EmptyPackage(fixture.EquipmentId, fixture.DataSourceId) with
        {
            RichCommandDefinitions =
            [
                invalid,
                ValidCommand(commandId)
            ]
        };

        var preview = fixture.Exchange.Preview(package, ImportMode.CreateAndUpdate);
        var issues = preview.Items.SelectMany(item => item.Issues).ToArray();

        Assert.False(preview.CanApply);
        Assert.Contains(issues, issue => issue.Code == "RICH_COMMAND_DEFINITION_ID_DUPLICATE");
        Assert.Contains(issues, issue => issue.Code == "RICH_COMMAND_DEFINITION_INVALID");
    }

    [Fact]
    public void Preview_RejectsRawProtocolAndProtectedMaterialBindingSettings()
    {
        using var fixture = CreateFixture();
        var commandId = Guid.NewGuid();
        var package = EmptyPackage(fixture.EquipmentId, fixture.DataSourceId) with
        {
            RichCommandDefinitions = [ValidCommand(commandId)],
            DriverCommandBindings =
            [
                ValidBinding(commandId, fixture.DataSourceId, fixture.EquipmentId) with
                {
                    Settings =
                    [
                        new DriverCommandBindingSetting("rpc.method", "open"),
                        new DriverCommandBindingSetting("credentialRef", "secret://driver")
                    ]
                }
            ]
        };

        var preview = fixture.Exchange.Preview(package, ImportMode.CreateAndUpdate);

        Assert.False(preview.CanApply);
        Assert.Contains(
            preview.Items.SelectMany(item => item.Issues),
            issue => issue.Code == "DRIVER_COMMAND_BINDING_INVALID" ||
                     issue.Code == "DRIVER_INTERACTION_GRAPH_INVALID");
    }

    [Fact]
    public void LegacyCommandCollection_RemainsIndependent()
    {
        using var fixture = CreateFixture();
        var legacy = new CommandEngineeringDto(
            Guid.NewGuid(),
            "legacy.write",
            "Legacy write",
            CommandKind.WriteTagValue,
            "true",
            TargetTagPath: "Plant.Legacy");

        var package = EmptyPackage(fixture.EquipmentId, fixture.DataSourceId) with
        {
            Commands = [legacy]
        };

        Assert.Single(package.Commands!);
        Assert.Empty(package.RichCommandDefinitions!);
        Assert.Empty(package.DriverCommandBindings!);
    }

    private static EngineeringPackage ValidPackage(Guid equipmentId, Guid dataSourceId)
    {
        var eventId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        return EmptyPackage(equipmentId, dataSourceId) with
        {
            TransientEventDefinitions =
            [
                ValidEvent(eventId, equipmentId, "cover.main", "cover.stopped")
            ],
            CapabilityEventReferences =
            [
                new CapabilityEventReferenceEngineeringDto(
                    equipmentId,
                    "cover.main",
                    "stopped",
                    eventId,
                    "cover.stopped")
            ],
            RichCommandDefinitions = [ValidCommand(commandId)],
            DriverCommandBindings = [ValidBinding(commandId, dataSourceId, equipmentId)],
            Screens =
            [
                new ScreenEngineeringDto(
                    Guid.NewGuid(),
                    "screen.home",
                    "Home",
                    Elements:
                    [
                        new VisualElementEngineeringDto(
                            "execute-cover",
                            "button",
                            Actions:
                            [
                                new VisualNavigationActionEngineeringDto(
                                    "click",
                                    VisualNavigationActionKind.ExecuteRichCommand,
                                    Version: VisualNavigationActionVersions.RichCommand,
                                    CommandId: commandId)
                            ])
                    ])
            ],
            Popups =
            [
                new PopupEngineeringDto(
                    Guid.NewGuid(),
                    "popup.cover",
                    "Cover",
                    Elements:
                    [
                        new VisualElementEngineeringDto(
                            "execute-cover",
                            "button",
                            Actions:
                            [
                                new VisualNavigationActionEngineeringDto(
                                    "click",
                                    VisualNavigationActionKind.ExecuteRichCommand,
                                    Version: VisualNavigationActionVersions.RichCommand,
                                    CommandId: commandId)
                            ])
                    ])
            ]
        };
    }

    private static TransientEventDefinitionEngineeringDto ValidEvent(
        Guid eventId,
        Guid equipmentId,
        string capabilityId,
        string semanticKey) =>
        new(
            eventId,
            semanticKey,
            [
                new TransientEventFieldDefinition(
                    "position",
                    new InteractionScalarSchema(
                        InteractionScalarKind.Percentage,
                        Minimum: 0,
                        Maximum: 100))
            ],
            equipmentId,
            capabilityId);

    private static RichCommandDefinitionEngineeringDto ValidCommand(Guid commandId) =>
        new(
            commandId,
            "cover.move",
            [
                new RichCommandParameterDefinition(
                    "position",
                    new InteractionScalarSchema(
                        InteractionScalarKind.Percentage,
                        Minimum: 0,
                        Maximum: 100),
                    Required: true)
            ]);

    private static DriverCommandBindingEngineeringDto ValidBinding(
        Guid commandId,
        Guid dataSourceId,
        Guid equipmentId) =>
        new(
            commandId,
            dataSourceId,
            "cover-01",
            "cover.move",
            [new DriverCommandBindingSetting("transition.profile", "normal")],
            equipmentId,
            "cover.main");

    private static EngineeringPackage EmptyPackage(Guid equipmentId, Guid? dataSourceId) =>
        new(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            DataSources: dataSourceId.HasValue
                ? [new DataSourceEngineeringDto(dataSourceId, "source.cover", "Cover source", "test.driver")]
                : Array.Empty<DataSourceEngineeringDto>(),
            Equipment:
            [
                new EquipmentEngineeringDto(
                    equipmentId,
                    "Plant.Cover01",
                    "Cover 01",
                    Capabilities:
                    [
                        new EquipmentCapabilityEngineeringDto(
                            "cover.main",
                            EquipmentCapabilityKinds.Cover)
                    ])
            ],
            TransientEventDefinitions: Array.Empty<TransientEventDefinitionEngineeringDto>(),
            CapabilityEventReferences: Array.Empty<CapabilityEventReferenceEngineeringDto>(),
            RichCommandDefinitions: Array.Empty<RichCommandDefinitionEngineeringDto>(),
            DriverCommandBindings: Array.Empty<DriverCommandBindingEngineeringDto>());

    private static Fixture CreateFixture(bool seedResources = true)
    {
        var tags = new InMemoryTagRegistry();
        var alarms = new InMemoryAlarmEngine(new InMemoryScadaEventBus());
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        var assets = new InMemoryEngineeringAssetRegistry();
        var interactions = new InMemoryDriverInteractionEngineeringRegistry();
        var equipmentId = Guid.NewGuid();
        var dataSourceId = Guid.NewGuid();

        if (seedResources)
        {
            dataSources.Upsert(new DataSourceEngineeringDto(
                dataSourceId,
                "source.cover",
                "Cover source",
                "test.driver"));
            assets.UpsertEquipment(new EquipmentEngineeringDto(
                equipmentId,
                "Plant.Cover01",
                "Cover 01",
                Capabilities:
                [
                    new EquipmentCapabilityEngineeringDto(
                        "cover.main",
                        EquipmentCapabilityKinds.Cover)
                ]));
        }

        var exchange = new EngineeringExchangeService(
            tags,
            alarms,
            dataSources,
            assets,
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry(),
            new InMemoryGatewayEngineeringRegistry(),
            driverInteractions: interactions);

        return new Fixture(
            exchange,
            alarms,
            interactions,
            equipmentId,
            dataSourceId);
    }

    private sealed record Fixture(
        EngineeringExchangeService Exchange,
        InMemoryAlarmEngine Alarms,
        InMemoryDriverInteractionEngineeringRegistry Interactions,
        Guid EquipmentId,
        Guid DataSourceId) : IDisposable
    {
        public void Dispose() => Alarms.Dispose();
    }
}
