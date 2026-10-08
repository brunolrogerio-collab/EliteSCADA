using System.Buffers.Binary;
using System.Text.Json;
using Scada.Core.Alarms;
using Scada.Core.Events;
using Scada.Core.HistoricalQueries;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.DataQueries;
using Scada.Engineering.Gateways;
using Scada.Engineering.Historian;
using Scada.Engineering.Interactions;
using Scada.Engineering.ImportExport;
using Scada.Engineering.ProjectPackages;
using Scada.Engineering.Scripts;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;

namespace Scada.Core.Tests;

public sealed class EngineeringFragmentServiceTests
{
    [Fact]
    public void ScreenExport_ResolvesClosure_AndTargetPreviewRemapsIdenticalDynamo()
    {
        using var source = CreateHarness();
        var sourceDynamoId = Guid.NewGuid();
        var targetDynamoId = Guid.NewGuid();
        var screenId = Guid.NewGuid();
        var scriptId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        source.Assets.UpsertDynamo(new DynamoEngineeringDto(
            sourceDynamoId,
            "dynamo.fragment.pump",
            "Pump"));
        var payload = VisualAssetPayload.Create("image/bmp", CreateBmp());
        source.VisualAssets.PutPayload(payload);
        source.VisualAssets.UpsertAsset(new VisualAssetEngineeringDto(
            assetId,
            "asset.fragment.pump",
            "Pump Symbol",
            "pump.bmp",
            payload.MediaType,
            payload.ByteLength,
            payload.Sha256,
            1,
            1));
        source.Views.UpsertScreen(new ScreenEngineeringDto(
            screenId,
            "screen.fragment.main",
            "Main",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "pump",
                    "dynamo",
                    DynamoKey: "dynamo.fragment.pump",
                    DynamoDefinitionId: sourceDynamoId),
                new VisualElementEngineeringDto(
                    "image",
                    "core.image",
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["assetRef"] = JsonSerializer.SerializeToElement(new { assetId = $"asset:{assetId:D}" })
                    })
            ]));
        source.Scripts.Upsert(new ScriptEngineeringDefinition(
            scriptId,
            "scripts/fragment/main",
            "Main Script",
            ScriptEngineeringScope.ClientVisual,
            "def init():\n    pass",
            entryPoints:
            [new ScriptEngineeringEntryPoint(ScriptEngineeringEventKind.Initialize, "init")]));
        source.Scripts.ReplaceVisualEventReferences(
            scriptId,
            [new ScriptVisualEventReference(
                screenId,
                null,
                ScriptEngineeringEventKind.Initialize,
                scriptId,
                "init")]);

        var bytes = source.Fragments.Export(new EngineeringFragmentExportRequest(
            [new EngineeringFragmentEntityReference(
                ImportEntityKind.Screen,
                "screen.fragment.main",
                screenId)]));
        var inspection = source.Fragments.Inspect(bytes);

        Assert.Single(inspection.Envelope.Manifest.Roots);
        Assert.Contains(inspection.Envelope.Manifest.Dependencies!, item =>
            item.EntityKind == ImportEntityKind.Dynamo && item.EntityId == sourceDynamoId);
        Assert.Contains(inspection.Envelope.Manifest.Dependencies!, item =>
            item.EntityKind == ImportEntityKind.VisualAsset && item.EntityId == assetId);
        Assert.Contains(inspection.Envelope.Manifest.Dependencies!, item =>
            item.EntityKind == ImportEntityKind.Script && item.EntityId == scriptId);

        using var target = CreateHarness();
        target.Assets.UpsertDynamo(new DynamoEngineeringDto(
            targetDynamoId,
            "dynamo.fragment.pump",
            "Pump"));

        var plan = target.Fragments.Preview(bytes);

        Assert.True(plan.CanApply, DescribePlanFailure(plan));
        Assert.Contains(plan.Items, item =>
            item.Source.EntityKind == ImportEntityKind.Dynamo &&
            item.Source.EntityId == sourceDynamoId &&
            item.Operation == EngineeringFragmentPlanOperation.Remap &&
            item.Target?.EntityId == targetDynamoId);
        Assert.Contains(plan.Items, item =>
            item.Source.EntityKind == ImportEntityKind.Screen &&
            item.Operation == EngineeringFragmentPlanOperation.Create);
        Assert.Null(target.Views.FindScreen(screenId));

        var result = target.Fragments.Apply(plan);

        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        var imported = Assert.IsType<ScreenEngineeringDto>(target.Views.FindScreen(screenId));
        Assert.Equal(
            targetDynamoId,
            imported.Elements!.Single(element => element.Key == "pump").DynamoDefinitionId);
        Assert.NotNull(target.Scripts.Find(scriptId));
        Assert.NotNull(target.VisualAssets.FindAsset(assetId));
    }

    [Fact]
    public void SelectedPopup_TransfersAsStructuredFragment()
    {
        using var source = CreateHarness();
        var popupId = Guid.NewGuid();
        source.Views.UpsertPopup(new PopupEngineeringDto(
            popupId,
            "popup.fragment.faceplate",
            "Pump Faceplate",
            Elements:
            [
                new VisualElementEngineeringDto(
                    "title",
                    "core.text",
                    Properties: new Dictionary<string, JsonElement>
                    {
                        ["text"] = JsonSerializer.SerializeToElement("Pump")
                    })
            ],
            X: 32,
            Y: 48));

        var bytes = source.Fragments.Export(new EngineeringFragmentExportRequest(
            [new EngineeringFragmentEntityReference(
                ImportEntityKind.Popup,
                "popup.fragment.faceplate",
                popupId)]));

        using var target = CreateHarness();
        var plan = target.Fragments.Preview(bytes);

        Assert.True(plan.CanApply, DescribePlanFailure(plan));
        Assert.Contains(plan.Items, item =>
            item.Source.EntityKind == ImportEntityKind.Popup &&
            item.Source.EntityId == popupId &&
            item.Operation == EngineeringFragmentPlanOperation.Create);

        var result = target.Fragments.Apply(plan);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);

        var imported = Assert.IsType<PopupEngineeringDto>(target.Views.FindPopup(popupId));
        Assert.Equal(32, imported.X);
        Assert.Equal(48, imported.Y);
        Assert.Equal("Pump", imported.Elements!.Single().Properties!["text"].GetString());
    }

    [Fact]
    public void SelectedScript_TransfersItsScriptDependencyClosure()
    {
        using var source = CreateHarness();
        var helperId = Guid.NewGuid();
        var rootId = Guid.NewGuid();

        source.Scripts.Upsert(new ScriptEngineeringDefinition(
            helperId,
            "scripts/fragment/helper",
            "Helper",
            ScriptEngineeringScope.Server,
            "def helper():\n    return 1"));
        source.Scripts.Upsert(new ScriptEngineeringDefinition(
            rootId,
            "scripts/fragment/root",
            "Root",
            ScriptEngineeringScope.Server,
            "def run():\n    return helper()",
            dependencies:
            [new ScriptEngineeringDependency(
                ScriptEngineeringDependencyKind.Script,
                helperId.ToString("D"))]));

        var bytes = source.Fragments.Export(new EngineeringFragmentExportRequest(
            [new EngineeringFragmentEntityReference(
                ImportEntityKind.Script,
                "scripts/fragment/root",
                rootId)]));
        var inspection = source.Fragments.Inspect(bytes);

        Assert.Contains(inspection.Envelope.Manifest.Dependencies!, item =>
            item.EntityKind == ImportEntityKind.Script &&
            item.EntityId == helperId);

        using var target = CreateHarness();
        var plan = target.Fragments.Preview(bytes);
        Assert.True(plan.CanApply, DescribePlanFailure(plan));

        var result = target.Fragments.Apply(plan);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        Assert.Equal(
            "def run():\n    return helper()",
            target.Scripts.Find(rootId)!.Source);
        Assert.Equal(
            helperId.ToString("D"),
            target.Scripts.Find(rootId)!.Dependencies.Single().StableReference);
        Assert.NotNull(target.Scripts.Find(helperId));
    }

    [Fact]
    public void SelectedServerScript_TransfersRichCommandBindingAndDataSourceClosure()
    {
        using var source = CreateHarness();
        var scriptId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var dataSourceId = Guid.NewGuid();
        source.DataSources.Upsert(new DataSourceEngineeringDto(
            dataSourceId,
            "hvac",
            "HVAC",
            "mock"));
        source.DriverInteractions.UpsertRichCommandDefinition(new RichCommandDefinitionEngineeringDto(
            commandId,
            "climate.set-mode",
            Array.Empty<Scada.Core.Commands.RichCommandParameterDefinition>()));
        source.DriverInteractions.UpsertDriverCommandBinding(new DriverCommandBindingEngineeringDto(
            commandId,
            dataSourceId,
            "hvac-controller",
            "climate.set-mode"));
        source.Scripts.Upsert(new ScriptEngineeringDefinition(
            scriptId,
            "scripts/fragment/command",
            "Command Script",
            ScriptEngineeringScope.Server,
            "def run():\n    return None",
            dependencies:
            [new ScriptEngineeringDependency(
                ScriptEngineeringDependencyKind.RichCommand,
                ScriptEngineeringReferenceKeys.RichCommand(commandId))]));

        var bytes = source.Fragments.Export(new EngineeringFragmentExportRequest(
            [new EngineeringFragmentEntityReference(
                ImportEntityKind.Script,
                "scripts/fragment/command",
                scriptId)]));
        var manifest = source.Fragments.Inspect(bytes).Envelope.Manifest;

        Assert.Contains(manifest.Dependencies!, item =>
            item.EntityKind == ImportEntityKind.RichCommandDefinition && item.EntityId == commandId);
        Assert.Contains(manifest.Dependencies!, item =>
            item.EntityKind == ImportEntityKind.DriverCommandBinding && item.EntityId == commandId);
        Assert.Contains(manifest.Dependencies!, item =>
            item.EntityKind == ImportEntityKind.DataSource && item.EntityId == dataSourceId);

        using var target = CreateHarness();
        var plan = target.Fragments.Preview(bytes);
        Assert.True(plan.CanApply, DescribePlanFailure(plan));
        var result = target.Fragments.Apply(plan);

        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        Assert.NotNull(target.DriverInteractions.FindRichCommandDefinition(commandId));
        Assert.NotNull(target.DriverInteractions.FindDriverCommandBinding(commandId));
        Assert.NotNull(target.DataSources.Find(dataSourceId));
        Assert.Equal(
            ScriptEngineeringReferenceKeys.RichCommand(commandId),
            target.Scripts.Find(scriptId)!.Dependencies.Single().StableReference);
    }

    [Fact]
    public void SelectedTagsAndAlarms_TransferWithDataSourceReference()
    {
        using var source = CreateHarness();
        var sourceId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var alarmId = Guid.NewGuid();
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            [new TagEngineeringDto(
                tagId,
                "Pressure",
                "Plant.Pressure",
                TagDataType.Double,
                Source: "source.fragment",
                DataSourceId: sourceId)],
            [new AlarmEngineeringDto(
                alarmId,
                "High Pressure",
                tagId,
                "Plant.Pressure",
                AlarmType.High,
                AlarmPriority.High,
                Setpoint: 100)],
            DataSources:
            [new DataSourceEngineeringDto(
                sourceId,
                "source.fragment",
                "Fragment Source",
                "builtin.simulation")]);

        var sourceApply = source.Exchange.Apply(package, ImportMode.CreateOnly);
        Assert.DoesNotContain(sourceApply.Issues, issue => issue.IsError);

        var bytes = source.Fragments.Export(new EngineeringFragmentExportRequest(
        [
            new EngineeringFragmentEntityReference(ImportEntityKind.Tag, "Plant.Pressure", tagId),
            new EngineeringFragmentEntityReference(ImportEntityKind.Alarm, "High Pressure", alarmId)
        ]));

        using var target = CreateHarness();
        var plan = target.Fragments.Preview(bytes);

        Assert.True(plan.CanApply, DescribePlanFailure(plan));
        Assert.Contains(plan.Items, item =>
            item.Source.EntityKind == ImportEntityKind.DataSource &&
            item.Source.EntityId == sourceId &&
            item.Operation == EngineeringFragmentPlanOperation.Create);

        var result = target.Fragments.Apply(plan);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);

        var exported = target.Exchange.ExportPackage();
        Assert.Equal(sourceId, Assert.Single(exported.DataSources!).Id);
        Assert.Equal(tagId, Assert.Single(exported.Tags).Id);
        Assert.Equal(alarmId, Assert.Single(exported.Alarms).Id);
        Assert.Equal(sourceId, Assert.Single(exported.Tags).DataSourceId);
        Assert.Equal(tagId, Assert.Single(exported.Alarms).TagId);

        var projectPackages = new ProjectPackageService(target.Exchange, target.VisualAssets);
        var projectBytes = projectPackages.Export("fragment-roundtrip", "Fragment Roundtrip");

        using var reopened = CreateHarness();
        var reopenedPackages = new ProjectPackageService(reopened.Exchange, reopened.VisualAssets);
        var projectPreview = reopenedPackages.Preview(projectBytes, ImportMode.CreateOnly);
        Assert.True(projectPreview.CanApply);
        var projectResult = reopenedPackages.Apply(projectBytes, ImportMode.CreateOnly);
        Assert.DoesNotContain(projectResult.Issues, issue => issue.IsError);

        var reopenedEngineering = reopened.Exchange.ExportPackage();
        Assert.Equal(sourceId, Assert.Single(reopenedEngineering.DataSources!).Id);
        Assert.Equal(tagId, Assert.Single(reopenedEngineering.Tags).Id);
        Assert.Equal(alarmId, Assert.Single(reopenedEngineering.Alarms).Id);
    }


    [Fact]
    public void CanonicalF0PortableKinds_CreateReuseUpdateConflictAndProjectRoundTrip()
    {
        using var source = CreateHarness();
        var profileId = Guid.NewGuid();
        var queryId = Guid.NewGuid();
        var alarmViewId = Guid.NewGuid();

        source.HistorianCaptureProfiles.Upsert(CaptureProfile(profileId, "Fast capture", 1000));
        source.DataQueries.Upsert(FragmentQuery(queryId, "History"));
        source.AlarmViews.Upsert(FragmentAlarmView(alarmViewId, "Active alarms"));

        EngineeringFragmentExportRequest Request() => new(
        [
            new EngineeringFragmentEntityReference(ImportEntityKind.HistorianCaptureProfile, "capture.fragment.fast", profileId),
            new EngineeringFragmentEntityReference(ImportEntityKind.DataQuery, "query.fragment.history", queryId),
            new EngineeringFragmentEntityReference(ImportEntityKind.AlarmView, "alarmview.fragment.active", alarmViewId)
        ]);

        var bytes = source.Fragments.Export(Request());
        using var target = CreateHarness();

        var create = target.Fragments.Preview(bytes);
        Assert.True(create.CanApply, DescribePlanFailure(create));
        AssertPlanOperation(create, ImportEntityKind.HistorianCaptureProfile, EngineeringFragmentPlanOperation.Create);
        AssertPlanOperation(create, ImportEntityKind.DataQuery, EngineeringFragmentPlanOperation.Create);
        AssertPlanOperation(create, ImportEntityKind.AlarmView, EngineeringFragmentPlanOperation.Create);
        var created = target.Fragments.Apply(create);
        Assert.DoesNotContain(created.Issues, issue => issue.IsError);

        var reuse = target.Fragments.Preview(bytes);
        Assert.True(reuse.CanApply, DescribePlanFailure(reuse));
        AssertPlanOperation(reuse, ImportEntityKind.HistorianCaptureProfile, EngineeringFragmentPlanOperation.ReuseIdentical);
        AssertPlanOperation(reuse, ImportEntityKind.DataQuery, EngineeringFragmentPlanOperation.ReuseIdentical);
        AssertPlanOperation(reuse, ImportEntityKind.AlarmView, EngineeringFragmentPlanOperation.ReuseIdentical);

        source.HistorianCaptureProfiles.Upsert(CaptureProfile(profileId, "Fast capture v2", 2000));
        source.DataQueries.Upsert(FragmentQuery(queryId, "History v2"));
        source.AlarmViews.Upsert(FragmentAlarmView(alarmViewId, "Active alarms v2"));
        var updateBytes = source.Fragments.Export(Request());

        var update = target.Fragments.Preview(updateBytes);
        Assert.True(update.CanApply, DescribePlanFailure(update));
        AssertPlanOperation(update, ImportEntityKind.HistorianCaptureProfile, EngineeringFragmentPlanOperation.Update);
        AssertPlanOperation(update, ImportEntityKind.DataQuery, EngineeringFragmentPlanOperation.Update);
        AssertPlanOperation(update, ImportEntityKind.AlarmView, EngineeringFragmentPlanOperation.Update);
        var updated = target.Fragments.Apply(update);
        Assert.DoesNotContain(updated.Issues, issue => issue.IsError);
        Assert.Equal("Fast capture v2", Assert.Single(target.HistorianCaptureProfiles.Snapshot()).Name);
        Assert.Equal(2000, Assert.Single(target.HistorianCaptureProfiles.Snapshot()).PeriodMilliseconds);
        Assert.Equal("History v2", Assert.Single(target.DataQueries.Snapshot()).Name);
        Assert.Equal("Active alarms v2", Assert.Single(target.AlarmViews.Snapshot()).Name);

        var projectPackages = new ProjectPackageService(target.Exchange, target.VisualAssets);
        var projectBytes = projectPackages.Export("fragment-f0-roundtrip", "Fragment F0 Roundtrip");
        using var reopened = CreateHarness();
        var reopenedPackages = new ProjectPackageService(reopened.Exchange, reopened.VisualAssets);
        var projectPreview = reopenedPackages.Preview(projectBytes, ImportMode.CreateOnly);
        Assert.True(projectPreview.CanApply);
        var projectResult = reopenedPackages.Apply(projectBytes, ImportMode.CreateOnly);
        Assert.DoesNotContain(projectResult.Issues, issue => issue.IsError);
        Assert.Equal(profileId, Assert.Single(reopened.HistorianCaptureProfiles.Snapshot()).Id);
        Assert.Equal(queryId, Assert.Single(reopened.DataQueries.Snapshot()).Id);
        Assert.Equal(alarmViewId, Assert.Single(reopened.AlarmViews.Snapshot()).Id);

        using var conflictSource = CreateHarness();
        var conflictProfileId = Guid.NewGuid();
        var conflictQueryId = Guid.NewGuid();
        var conflictAlarmViewId = Guid.NewGuid();
        conflictSource.HistorianCaptureProfiles.Upsert(CaptureProfile(conflictProfileId, "Conflicting capture", 3000));
        conflictSource.DataQueries.Upsert(FragmentQuery(conflictQueryId, "Conflicting history"));
        conflictSource.AlarmViews.Upsert(FragmentAlarmView(conflictAlarmViewId, "Conflicting alarms"));
        var conflictBytes = conflictSource.Fragments.Export(new EngineeringFragmentExportRequest(
        [
            new EngineeringFragmentEntityReference(ImportEntityKind.HistorianCaptureProfile, "capture.fragment.fast", conflictProfileId),
            new EngineeringFragmentEntityReference(ImportEntityKind.DataQuery, "query.fragment.history", conflictQueryId),
            new EngineeringFragmentEntityReference(ImportEntityKind.AlarmView, "alarmview.fragment.active", conflictAlarmViewId)
        ]));

        var beforeConflict = target.Exchange.ExportPackage();
        var conflict = target.Fragments.Preview(conflictBytes);
        Assert.False(conflict.CanApply);
        AssertPlanOperation(conflict, ImportEntityKind.HistorianCaptureProfile, EngineeringFragmentPlanOperation.Conflict);
        AssertPlanOperation(conflict, ImportEntityKind.DataQuery, EngineeringFragmentPlanOperation.Conflict);
        AssertPlanOperation(conflict, ImportEntityKind.AlarmView, EngineeringFragmentPlanOperation.Conflict);
        var blocked = target.Fragments.Apply(conflict);
        Assert.Contains(blocked.Issues, issue => issue.Code == "FRAGMENT_CONFLICT");

        var afterConflict = target.Exchange.ExportPackage();
        Assert.Equal(Assert.Single(beforeConflict.HistorianCaptureProfiles!).Id, Assert.Single(afterConflict.HistorianCaptureProfiles!).Id);
        Assert.Equal(Assert.Single(beforeConflict.DataQueries!).Id, Assert.Single(afterConflict.DataQueries!).Id);
        Assert.Equal(Assert.Single(beforeConflict.AlarmViews!).Id, Assert.Single(afterConflict.AlarmViews!).Id);
    }

    [Fact]
    public void CanonicalF0PortableKinds_IdenticalKeysRemapToTargetOwnedIds()
    {
        using var source = CreateHarness();
        var sourceProfileId = Guid.NewGuid();
        var sourceQueryId = Guid.NewGuid();
        var sourceAlarmViewId = Guid.NewGuid();
        source.HistorianCaptureProfiles.Upsert(CaptureProfile(sourceProfileId, "Fast capture", 1000));
        source.DataQueries.Upsert(FragmentQuery(sourceQueryId, "History"));
        source.AlarmViews.Upsert(FragmentAlarmView(sourceAlarmViewId, "Active alarms"));

        var bytes = source.Fragments.Export(new EngineeringFragmentExportRequest(
        [
            new EngineeringFragmentEntityReference(ImportEntityKind.HistorianCaptureProfile, "capture.fragment.fast", sourceProfileId),
            new EngineeringFragmentEntityReference(ImportEntityKind.DataQuery, "query.fragment.history", sourceQueryId),
            new EngineeringFragmentEntityReference(ImportEntityKind.AlarmView, "alarmview.fragment.active", sourceAlarmViewId)
        ]));

        using var target = CreateHarness();
        var targetProfileId = Guid.NewGuid();
        var targetQueryId = Guid.NewGuid();
        var targetAlarmViewId = Guid.NewGuid();
        target.HistorianCaptureProfiles.Upsert(CaptureProfile(targetProfileId, "Fast capture", 1000));
        target.DataQueries.Upsert(FragmentQuery(targetQueryId, "History"));
        target.AlarmViews.Upsert(FragmentAlarmView(targetAlarmViewId, "Active alarms"));

        var plan = target.Fragments.Preview(bytes);
        Assert.True(plan.CanApply, DescribePlanFailure(plan));
        AssertRemap(plan, ImportEntityKind.HistorianCaptureProfile, sourceProfileId, targetProfileId);
        AssertRemap(plan, ImportEntityKind.DataQuery, sourceQueryId, targetQueryId);
        AssertRemap(plan, ImportEntityKind.AlarmView, sourceAlarmViewId, targetAlarmViewId);

        var result = target.Fragments.Apply(plan);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        Assert.Equal(targetProfileId, Assert.Single(target.HistorianCaptureProfiles.Snapshot()).Id);
        Assert.Equal(targetQueryId, Assert.Single(target.DataQueries.Snapshot()).Id);
        Assert.Equal(targetAlarmViewId, Assert.Single(target.AlarmViews.Snapshot()).Id);
    }

    [Fact]
    public void TagHistorianCaptureProfile_ClosureRemapAndProjectRoundTripPreserveStableReference()
    {
        using var source = CreateHarness();
        var sourceProfileId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        source.HistorianCaptureProfiles.Upsert(CaptureProfile(sourceProfileId, "Fast capture", 1000));

        var sourceTagApply = source.Exchange.Apply(
            new EngineeringPackage(
                EngineeringExchangeService.CurrentSchema,
                EngineeringExchangeService.CurrentSchemaVersion,
                DateTimeOffset.UtcNow,
                [new TagEngineeringDto(
                    tagId,
                    "Pressure",
                    "Plant.Pressure",
                    TagDataType.Double,
                    HistorianCaptureProfileId: sourceProfileId)],
                Array.Empty<AlarmEngineeringDto>()),
            ImportMode.CreateOnly);
        Assert.DoesNotContain(sourceTagApply.Issues, issue => issue.IsError);
        Assert.Equal(sourceProfileId, Assert.Single(source.Exchange.ExportPackage().Tags).HistorianCaptureProfileId);

        var bytes = source.Fragments.Export(new EngineeringFragmentExportRequest(
            [new EngineeringFragmentEntityReference(ImportEntityKind.Tag, "Plant.Pressure", tagId)]));
        var inspection = source.Fragments.Inspect(bytes);
        Assert.Contains(inspection.Envelope.Manifest.Dependencies!, dependency =>
            dependency.EntityKind == ImportEntityKind.HistorianCaptureProfile &&
            dependency.EntityId == sourceProfileId);

        using var target = CreateHarness();
        var targetProfileId = Guid.NewGuid();
        target.HistorianCaptureProfiles.Upsert(CaptureProfile(targetProfileId, "Fast capture", 1000));

        var plan = target.Fragments.Preview(bytes);
        Assert.True(plan.CanApply, DescribePlanFailure(plan));
        AssertRemap(plan, ImportEntityKind.HistorianCaptureProfile, sourceProfileId, targetProfileId);
        var result = target.Fragments.Apply(plan);
        Assert.DoesNotContain(result.Issues, issue => issue.IsError);
        Assert.Equal(targetProfileId, Assert.Single(target.Exchange.ExportPackage().Tags).HistorianCaptureProfileId);

        var packages = new ProjectPackageService(target.Exchange, target.VisualAssets);
        var projectBytes = packages.Export("historian-profile-roundtrip", "Historian Profile Roundtrip");
        using var reopened = CreateHarness();
        var reopenedPackages = new ProjectPackageService(reopened.Exchange, reopened.VisualAssets);
        var preview = reopenedPackages.Preview(projectBytes, ImportMode.CreateOnly);
        Assert.True(preview.CanApply);
        var reopenedResult = reopenedPackages.Apply(projectBytes, ImportMode.CreateOnly);
        Assert.DoesNotContain(reopenedResult.Issues, issue => issue.IsError);
        Assert.Equal(targetProfileId, Assert.Single(reopened.HistorianCaptureProfiles.Snapshot()).Id);
        Assert.Equal(targetProfileId, Assert.Single(reopened.Exchange.ExportPackage().Tags).HistorianCaptureProfileId);
    }

    [Fact]
    public void ExchangeFormats_RemainDistinctAuthorities()
    {
        Assert.Equal(".escadapkg", EngineeringExchangeFormatAuthority.ProjectPackage.Format);
        Assert.Equal(EngineeringFragmentContract.FileExtension, EngineeringExchangeFormatAuthority.Fragment.Format);
        Assert.Equal(".escadalib", EngineeringExchangeFormatAuthority.ReusableLibrary.Format);
        Assert.False(EngineeringExchangeFormatAuthority.CsvXlsx.SupportsNestedResources);
        Assert.True(EngineeringExchangeFormatAuthority.Fragment.SupportsNestedResources);
        Assert.True(EngineeringExchangeFormatAuthority.ReusableLibrary.MaintainsReusableSourceRelationship);
        Assert.False(EngineeringExchangeFormatAuthority.Fragment.MaintainsReusableSourceRelationship);
    }

    [Fact]
    public void DataSourceExport_NeverCarriesResolvedSecretLikeSettings()
    {
        using var source = CreateHarness();
        var dataSourceId = Guid.NewGuid();
        source.DataSources.Upsert(new DataSourceEngineeringDto(
            dataSourceId,
            "source.secret-safe",
            "Secret Safe",
            "opcua",
            Settings: new Dictionary<string, string>
            {
                ["endpoint"] = "opc.tcp://controller:4840",
                ["password"] = "resolved-password",
                ["apiKey"] = "resolved-key",
                ["pwd"] = "resolved-short-password",
                ["clientSecret"] = "resolved-client-secret",
                ["connectionOptions"] = "mode=secure;token=resolved-token"
            },
            SecretReferences: new Dictionary<string, string>
            {
                ["password"] = "secret://datasources/controller/password",
                ["apiKey"] = "secret://datasources/controller/api-key"
            }));

        var bytes = source.Fragments.Export(new EngineeringFragmentExportRequest(
            [new EngineeringFragmentEntityReference(
                ImportEntityKind.DataSource,
                "source.secret-safe",
                dataSourceId)]));

        var exported = Assert.Single(source.Fragments.Inspect(bytes).Envelope.Engineering.DataSources!);
        Assert.Equal("opc.tcp://controller:4840", exported.Settings!["endpoint"]);
        Assert.Single(exported.Settings);
        Assert.DoesNotContain(exported.Settings.Keys, key =>
            key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("apikey", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("pwd", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("token", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            "secret://datasources/controller/password",
            exported.SecretReferences!["password"]);
    }

    [Fact]
    public void Collision_FailsPlanBeforeAnyTargetMutation()
    {
        using var source = CreateHarness();
        var sourceId = Guid.NewGuid();
        source.DataSources.Upsert(new DataSourceEngineeringDto(
            sourceId,
            "source.collision",
            "Incoming",
            "modbus-tcp",
            Settings: new Dictionary<string, string> { ["host"] = "10.0.0.10" }));

        var bytes = source.Fragments.Export(new EngineeringFragmentExportRequest(
            [new EngineeringFragmentEntityReference(
                ImportEntityKind.DataSource,
                "source.collision",
                sourceId)]));

        using var target = CreateHarness();
        var existingId = Guid.NewGuid();
        target.DataSources.Upsert(new DataSourceEngineeringDto(
            existingId,
            "source.collision",
            "Existing",
            "modbus-tcp",
            Settings: new Dictionary<string, string> { ["host"] = "10.0.0.20" }));
        var before = target.Exchange.ExportPackage();

        var plan = target.Fragments.Preview(bytes);

        Assert.False(plan.CanApply);
        Assert.Contains(plan.Items, item =>
            item.Source.EntityId == sourceId &&
            item.Operation == EngineeringFragmentPlanOperation.Conflict);

        var result = target.Fragments.Apply(plan);
        Assert.Contains(result.Issues, issue => issue.Code == "FRAGMENT_CONFLICT");

        var after = target.Exchange.ExportPackage();
        Assert.Equal(existingId, Assert.Single(after.DataSources!).Id);
        Assert.Equal(
            Assert.Single(before.DataSources!).Settings!["host"],
            Assert.Single(after.DataSources!).Settings!["host"]);
        Assert.Single(after.DataSources!);
    }


    private static HistorianCaptureProfileEngineeringDto CaptureProfile(Guid id, string name, int periodMilliseconds) =>
        new(
            id,
            "capture.fragment.fast",
            name,
            HistorianCaptureStrategy.Periodic,
            PeriodMilliseconds: periodMilliseconds);

    private static DataQueryEngineeringDto FragmentQuery(Guid id, string name) =>
        new(
            id,
            "query.fragment.history",
            name,
            DataQueryExecutionService.HistoricalProviderKey,
            new HistoricalQueryRequest(
                HistoricalDatasets.HistorianSamples,
                HistoricalTimeRange.Relative(3600)),
            HistorianRetrieval: new HistorianRetrievalEngineeringDto(HistorianRetrievalMode.Raw));

    private static AlarmViewEngineeringDto FragmentAlarmView(Guid id, string name) =>
        new(
            id,
            "alarmview.fragment.active",
            name,
            new AlarmViewFilterEngineeringDto(Active: AlarmViewMatchState.Yes));

    private static void AssertPlanOperation(
        EngineeringFragmentPlan plan,
        ImportEntityKind kind,
        EngineeringFragmentPlanOperation operation) =>
        Assert.Contains(plan.Items, item =>
            item.Source.EntityKind == kind &&
            item.Operation == operation);

    private static void AssertRemap(
        EngineeringFragmentPlan plan,
        ImportEntityKind kind,
        Guid sourceId,
        Guid targetId) =>
        Assert.Contains(plan.Items, item =>
            item.Source.EntityKind == kind &&
            item.Source.EntityId == sourceId &&
            item.Operation == EngineeringFragmentPlanOperation.Remap &&
            item.Target?.EntityId == targetId);

    private static string DescribePlanFailure(EngineeringFragmentPlan plan)
    {
        var operations = string.Join(
            "; ",
            plan.Items.Select(item =>
                $"{item.Source.EntityKind}:{item.Source.EntityKey}={item.Operation}" +
                (string.IsNullOrWhiteSpace(item.Reason) ? string.Empty : $" ({item.Reason})")));
        var issues = string.Join(
            "; ",
            plan.CanonicalPreview.Items
                .SelectMany(item => item.Issues)
                .Select(issue => $"{issue.Code}:{issue.EntityKind}:{issue.EntityKey}:{issue.Message}"));
        return $"Fragment plan operations=[{operations}] canonicalIssues=[{issues}]";
    }

    private static string DescribePlan(EngineeringFragmentPlan plan) =>
        string.Join(
            " | ",
            plan.Items.Select(item =>
                $"{item.Source.EntityKind}:{item.Source.EntityKey}:{item.Operation}:{item.Reason}")
            .Concat(plan.CanonicalPreview.Items.SelectMany(item =>
                item.Issues.Select(issue =>
                    $"{issue.Code}:{issue.EntityKind}:{issue.EntityKey}:{issue.Message}"))));

    private static Harness CreateHarness()
    {
        var events = new InMemoryScadaEventBus();
        var tags = new InMemoryTagRegistry();
        var alarms = new InMemoryAlarmEngine(events);
        var dataSources = new InMemoryDataSourceEngineeringRegistry();
        var historianCaptureProfiles = new InMemoryHistorianCaptureProfileEngineeringRegistry(
            isReferenced: profileId => tags.Snapshot().Any(tag =>
                HistorianCaptureProfileMetadata.ReadProfileId(tag.Metadata) == profileId));
        var dataQueries = new InMemoryDataQueryEngineeringRegistry();
        var alarmViews = new InMemoryAlarmViewEngineeringRegistry();
        var assets = new InMemoryEngineeringAssetRegistry();
        var views = new InMemoryEngineeringViewRegistry();
        var security = new InMemorySecurityPolicyEngineeringRegistry();
        var commands = new InMemoryCommandEngineeringRegistry();
        var gateways = new InMemoryGatewayEngineeringRegistry();
        var scripts = new InMemoryScriptEngineeringRegistry();
        var visualAssets = new InMemoryVisualAssetEngineeringRegistry();
        var driverInteractions = new InMemoryDriverInteractionEngineeringRegistry();

        IEngineeringExchangeService exchange = new EngineeringExchangeService(
            tags,
            alarms,
            dataSources,
            assets,
            views,
            security,
            commands,
            gateways,
            scripts,
            visualAssets,
            driverInteractions: driverInteractions);
        exchange = new HistorianCaptureProfileEngineeringExchangeDecorator(exchange, historianCaptureProfiles);
        exchange = new DataQueryEngineeringExchangeDecorator(exchange, dataQueries, alarmViews);

        var fragments = new EngineeringFragmentService(exchange, visualAssets);
        return new Harness(
            alarms,
            dataSources,
            historianCaptureProfiles,
            dataQueries,
            alarmViews,
            assets,
            views,
            scripts,
            driverInteractions,
            visualAssets,
            exchange,
            fragments);
    }

    private static byte[] CreateBmp()
    {
        const int fileSize = 58;
        var bytes = new byte[fileSize];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(2, 4), fileSize);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(10, 4), 54);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14, 4), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22, 4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28, 2), 24);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(34, 4), 4);
        return bytes;
    }

    private sealed class Harness(
        InMemoryAlarmEngine alarms,
        InMemoryDataSourceEngineeringRegistry dataSources,
        InMemoryHistorianCaptureProfileEngineeringRegistry historianCaptureProfiles,
        InMemoryDataQueryEngineeringRegistry dataQueries,
        InMemoryAlarmViewEngineeringRegistry alarmViews,
        InMemoryEngineeringAssetRegistry assets,
        InMemoryEngineeringViewRegistry views,
        InMemoryScriptEngineeringRegistry scripts,
        InMemoryDriverInteractionEngineeringRegistry driverInteractions,
        InMemoryVisualAssetEngineeringRegistry visualAssets,
        IEngineeringExchangeService exchange,
        EngineeringFragmentService fragments) : IDisposable
    {
        public InMemoryDataSourceEngineeringRegistry DataSources { get; } = dataSources;
        public InMemoryHistorianCaptureProfileEngineeringRegistry HistorianCaptureProfiles { get; } = historianCaptureProfiles;
        public InMemoryDataQueryEngineeringRegistry DataQueries { get; } = dataQueries;
        public InMemoryAlarmViewEngineeringRegistry AlarmViews { get; } = alarmViews;
        public InMemoryEngineeringAssetRegistry Assets { get; } = assets;
        public InMemoryEngineeringViewRegistry Views { get; } = views;
        public InMemoryScriptEngineeringRegistry Scripts { get; } = scripts;
        public InMemoryDriverInteractionEngineeringRegistry DriverInteractions { get; } = driverInteractions;
        public InMemoryVisualAssetEngineeringRegistry VisualAssets { get; } = visualAssets;
        public IEngineeringExchangeService Exchange { get; } = exchange;
        public EngineeringFragmentService Fragments { get; } = fragments;

        public void Dispose() => alarms.Dispose();
    }
}
