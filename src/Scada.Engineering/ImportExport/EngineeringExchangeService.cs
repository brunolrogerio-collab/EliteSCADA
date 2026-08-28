using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport.Handlers;
using Scada.Engineering.Scripts;
using Scada.Engineering.Security;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;

namespace Scada.Engineering.ImportExport;

public sealed class EngineeringExchangeService : IEngineeringExchangeService
{
    public const string CurrentSchema = "scada.engineering";
    public const int CurrentSchemaVersion = 13;

    private readonly ITagRegistry _tags;
    private readonly IAlarmEngine _alarms;
    private readonly IDataSourceEngineeringRegistry _dataSources;
    private readonly IEngineeringAssetRegistry _assets;
    private readonly IEngineeringViewRegistry _views;
    private readonly ISecurityPolicyEngineeringRegistry _securityPolicies;
    private readonly ICommandEngineeringRegistry _commands;
    private readonly IGatewayEngineeringRegistry _gateways;
    private readonly IScriptEngineeringRegistry _scripts;
    private readonly IVisualAssetEngineeringRegistry _visualAssets;
    private readonly JsonSerializerOptions _json;
    private readonly EngineeringCsvExchange _csv;
    private readonly DataSourceEngineeringHandler _dataSourceHandler;
    private readonly TagEngineeringHandler _tagHandler;
    private readonly AlarmEngineeringHandler _alarmHandler;
    private readonly AssetEngineeringHandler _assetHandler;
    private readonly VisualAssetEngineeringHandler _visualAssetHandler;
    private readonly ViewEngineeringHandler _viewHandler;
    private readonly SecurityPolicyEngineeringHandler _securityPolicyHandler;
    private readonly CommandEngineeringHandler _commandHandler;
    private readonly GatewayEngineeringHandler _gatewayHandler;
    private readonly ScriptEngineeringHandler _scriptHandler;

    public EngineeringExchangeService(ITagRegistry tags, IAlarmEngine alarms)
        : this(
            tags,
            alarms,
            new InMemoryDataSourceEngineeringRegistry(),
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry())
    {
    }

    public EngineeringExchangeService(
        ITagRegistry tags,
        IAlarmEngine alarms,
        IDataSourceEngineeringRegistry dataSources)
        : this(
            tags,
            alarms,
            dataSources,
            new InMemoryEngineeringAssetRegistry(),
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry())
    {
    }

    public EngineeringExchangeService(
        ITagRegistry tags,
        IAlarmEngine alarms,
        IDataSourceEngineeringRegistry dataSources,
        IEngineeringAssetRegistry assets)
        : this(
            tags,
            alarms,
            dataSources,
            assets,
            new InMemoryEngineeringViewRegistry(),
            new InMemorySecurityPolicyEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry())
    {
    }

    public EngineeringExchangeService(
        ITagRegistry tags,
        IAlarmEngine alarms,
        IDataSourceEngineeringRegistry dataSources,
        IEngineeringAssetRegistry assets,
        IEngineeringViewRegistry views)
        : this(
            tags,
            alarms,
            dataSources,
            assets,
            views,
            new InMemorySecurityPolicyEngineeringRegistry(),
            new InMemoryCommandEngineeringRegistry())
    {
    }

    public EngineeringExchangeService(
        ITagRegistry tags,
        IAlarmEngine alarms,
        IDataSourceEngineeringRegistry dataSources,
        IEngineeringAssetRegistry assets,
        IEngineeringViewRegistry views,
        ISecurityPolicyEngineeringRegistry securityPolicies)
        : this(
            tags,
            alarms,
            dataSources,
            assets,
            views,
            securityPolicies,
            new InMemoryCommandEngineeringRegistry())
    {
    }

    public EngineeringExchangeService(
        ITagRegistry tags,
        IAlarmEngine alarms,
        IDataSourceEngineeringRegistry dataSources,
        IEngineeringAssetRegistry assets,
        IEngineeringViewRegistry views,
        ISecurityPolicyEngineeringRegistry securityPolicies,
        ICommandEngineeringRegistry commands)
        : this(
            tags,
            alarms,
            dataSources,
            assets,
            views,
            securityPolicies,
            commands,
            new InMemoryGatewayEngineeringRegistry())
    {
    }

    public EngineeringExchangeService(
        ITagRegistry tags,
        IAlarmEngine alarms,
        IDataSourceEngineeringRegistry dataSources,
        IEngineeringAssetRegistry assets,
        IEngineeringViewRegistry views,
        ISecurityPolicyEngineeringRegistry securityPolicies,
        ICommandEngineeringRegistry commands,
        IGatewayEngineeringRegistry gateways,
        IScriptEngineeringRegistry? scripts = null,
        IVisualAssetEngineeringRegistry? visualAssets = null)
    {
        _tags = tags;
        _alarms = alarms;
        _dataSources = dataSources;
        _assets = assets;
        _views = views;
        _securityPolicies = securityPolicies;
        _commands = commands;
        _gateways = gateways;
        _scripts = scripts ?? new InMemoryScriptEngineeringRegistry();
        _visualAssets = visualAssets ?? new InMemoryVisualAssetEngineeringRegistry();
        _json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        _csv = new EngineeringCsvExchange(_json);
        _dataSourceHandler = new DataSourceEngineeringHandler(dataSources, tags, alarms, commands);
        _tagHandler = new TagEngineeringHandler(tags, dataSources, alarms);
        _alarmHandler = new AlarmEngineeringHandler(alarms, _tagHandler);
        _assetHandler = new AssetEngineeringHandler(assets, tags);
        _visualAssetHandler = new VisualAssetEngineeringHandler(_visualAssets);
        _viewHandler = new ViewEngineeringHandler(views, assets, tags);
        _securityPolicyHandler = new SecurityPolicyEngineeringHandler(securityPolicies);
        _commandHandler = new CommandEngineeringHandler(commands, tags, dataSources);
        _gatewayHandler = new GatewayEngineeringHandler(gateways, tags, dataSources);
        _scriptHandler = new ScriptEngineeringHandler(_scripts, tags, dataSources, assets, views);
    }

    public EngineeringPackage ExportPackage()
    {
        var tagDefinitions = _tags.Snapshot();
        var tagDtos = tagDefinitions.Select(EngineeringDtoMapper.ToDto).ToArray();
        var paths = tagDefinitions.ToDictionary(x => x.Id, x => x.Path);
        var alarmDtos = _alarms.Definitions()
            .Select(alarm => EngineeringDtoMapper.ToDto(alarm, paths.GetValueOrDefault(alarm.TagId)))
            .ToArray();

        return new EngineeringPackage(
            CurrentSchema,
            CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            tagDtos,
            alarmDtos,
            _dataSources.Snapshot(),
            _assets.SnapshotTemplates(),
            _assets.SnapshotEquipment(),
            _assets.SnapshotDynamos(),
            _views.SnapshotScreens(),
            _views.SnapshotPopups(),
            _securityPolicies.SnapshotRoles(),
            _commands.Snapshot(),
            _gateways.Snapshot(),
            _scripts.SnapshotScripts(),
            _scripts.SnapshotVisualEventReferences(),
            _visualAssets.SnapshotAssets());
    }

    public string ExportJson(bool indented = true)
    {
        var options = new JsonSerializerOptions(_json) { WriteIndented = indented };
        return JsonSerializer.Serialize(ExportPackage(), options);
    }

    public string ExportTagsCsv() => _csv.ExportTags(ExportPackage().Tags);

    public string ExportAlarmsCsv() => _csv.ExportAlarms(ExportPackage().Alarms);

    public string ExportDataSourcesCsv() =>
        _csv.ExportDataSources(ExportPackage().DataSources ?? Array.Empty<DataSourceEngineeringDto>());

    public EngineeringPackage ParseJson(string json)
    {
        var package = JsonSerializer.Deserialize<EngineeringPackage>(json, _json)
            ?? throw new InvalidDataException("Invalid engineering package.");

        if (!string.Equals(package.Schema, CurrentSchema, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unsupported schema '{package.Schema}'.");
        if (package.SchemaVersion < 1)
            throw new InvalidDataException($"Schema version {package.SchemaVersion} is invalid.");
        if (package.SchemaVersion > CurrentSchemaVersion)
            throw new InvalidDataException(
                $"Schema version {package.SchemaVersion} is newer than supported version {CurrentSchemaVersion}.");

        return package with
        {
            DataSources = package.DataSources ?? Array.Empty<DataSourceEngineeringDto>(),
            Templates = package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>(),
            Equipment = package.Equipment ?? Array.Empty<EquipmentEngineeringDto>(),
            Dynamos = package.Dynamos ?? Array.Empty<DynamoEngineeringDto>(),
            Screens = package.Screens ?? Array.Empty<ScreenEngineeringDto>(),
            Popups = package.Popups ?? Array.Empty<PopupEngineeringDto>(),
            SecurityRoles = package.SecurityRoles ?? Array.Empty<SecurityRoleEngineeringDto>(),
            Commands = package.Commands ?? Array.Empty<CommandEngineeringDto>(),
            Gateways = package.Gateways ?? Array.Empty<GatewayRouteEngineeringDto>(),
            Scripts = package.Scripts ?? Array.Empty<ScriptEngineeringDefinition>(),
            ScriptVisualEventReferences = package.ScriptVisualEventReferences ?? Array.Empty<ScriptVisualEventReference>(),
            VisualAssets = package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>()
        };
    }

    public EngineeringPackage ParseTagsCsv(string csv) =>
        Empty() with { Tags = _csv.ParseTags(csv) };

    public EngineeringPackage ParseAlarmsCsv(string csv) =>
        Empty() with { Alarms = _csv.ParseAlarms(csv) };

    public EngineeringPackage ParseDataSourcesCsv(string csv) =>
        Empty() with { DataSources = _csv.ParseDataSources(csv) };

    public ImportPreview Preview(EngineeringPackage package, ImportMode mode) =>
        Preview(package, mode, null);

    public ImportPreview Preview(
        EngineeringPackage package,
        ImportMode mode,
        EngineeringImportContext? context)
    {
        var items = new List<ImportPreviewItem>();
        _dataSourceHandler.Preview(package, mode, items);
        _tagHandler.Preview(package, mode, items);
        _alarmHandler.Preview(package, mode, items);
        _assetHandler.Preview(package, mode, items);
        _visualAssetHandler.Preview(package, mode, items, context);
        _viewHandler.Preview(package, mode, items);
        _commandHandler.Preview(package, mode, items);
        _gatewayHandler.Preview(package, mode, items);
        _scriptHandler.Preview(package, mode, items);
        _securityPolicyHandler.Preview(package, mode, items);

        return new ImportPreview(
            mode,
            items.Count(x => x.Operation == ImportOperation.Create),
            items.Count(x => x.Operation == ImportOperation.Update),
            items.Count(x => x.Operation == ImportOperation.Skip),
            items.Count(x => x.Operation == ImportOperation.Error),
            items);
    }

    public ImportResult Apply(EngineeringPackage package, ImportMode mode) =>
        Apply(package, mode, null);

    public ImportResult Apply(
        EngineeringPackage package,
        ImportMode mode,
        EngineeringImportContext? context)
    {
        var preview = Preview(package, mode, context);
        if (!preview.CanApply)
            return new ImportResult(
                mode,
                0,
                0,
                preview.SkipCount,
                preview.Items.SelectMany(x => x.Issues).ToArray());

        var created = 0;
        var updated = 0;
        var skipped = 0;

        _dataSourceHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _tagHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _alarmHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _assetHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _visualAssetHandler.Apply(package, mode, ref created, ref updated, ref skipped, context);
        _viewHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _commandHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _gatewayHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _scriptHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _securityPolicyHandler.Apply(package, mode, ref created, ref updated, ref skipped);

        return new ImportResult(mode, created, updated, skipped, Array.Empty<ImportIssue>());
    }

    private static EngineeringPackage Empty() => new(
        CurrentSchema,
        CurrentSchemaVersion,
        DateTimeOffset.UtcNow,
        Array.Empty<TagEngineeringDto>(),
        Array.Empty<AlarmEngineeringDto>(),
        Array.Empty<DataSourceEngineeringDto>(),
        Array.Empty<EquipmentTemplateEngineeringDto>(),
        Array.Empty<EquipmentEngineeringDto>(),
        Array.Empty<DynamoEngineeringDto>(),
        Array.Empty<ScreenEngineeringDto>(),
        Array.Empty<PopupEngineeringDto>(),
        Array.Empty<SecurityRoleEngineeringDto>(),
        Array.Empty<CommandEngineeringDto>(),
        Array.Empty<GatewayRouteEngineeringDto>(),
        Array.Empty<ScriptEngineeringDefinition>(),
        Array.Empty<ScriptVisualEventReference>(),
        Array.Empty<VisualAssetEngineeringDto>());
}
