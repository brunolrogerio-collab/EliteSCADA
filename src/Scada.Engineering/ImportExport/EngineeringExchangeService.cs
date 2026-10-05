using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Core.Alarms;
using Scada.Core.Tags;
using Scada.Engineering.Assets;
using Scada.Engineering.Branding;
using Scada.Engineering.Commands;
using Scada.Engineering.Contracts;
using Scada.Engineering.DataSources;
using Scada.Engineering.Events;
using Scada.Engineering.Gateways;
using Scada.Engineering.ImportExport.Handlers;
using Scada.Engineering.Media;
using Scada.Engineering.Reports;
using Scada.Engineering.Scripts;
using Scada.Engineering.Security;
using Scada.Engineering.Validation;
using Scada.Engineering.Views;
using Scada.Engineering.VisualAssets;
using Scada.Security.Authorization;

namespace Scada.Engineering.ImportExport;

public sealed class EngineeringExchangeService : IEngineeringExchangeService
{
    public const string CurrentSchema = "scada.engineering";
    public const int CurrentSchemaVersion = 21;

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
    private readonly IReportEngineeringRegistry _reports;
    private readonly IOperationalEventEngineeringRegistry _operationalEvents;
    private readonly IEngineeringLockRegistry _engineeringLock;
    private readonly IApplicationBrandingEngineeringRegistry _branding;
    private readonly IMediaSourceEngineeringRegistry _mediaSources;
    private RuntimePresentationEngineeringDto _runtimePresentation = new();
    private readonly JsonSerializerOptions _json;
    private readonly EngineeringCsvExchange _csv;
    private readonly DataSourceEngineeringHandler _dataSourceHandler;
    private readonly TagEngineeringHandler _tagHandler;
    private readonly SecurityScopeEngineeringHandler _securityScopeHandler;
    private readonly AlarmEngineeringHandler _alarmHandler;
    private readonly AssetEngineeringHandler _assetHandler;
    private readonly VisualAssetEngineeringHandler _visualAssetHandler;
    private readonly ViewEngineeringHandler _viewHandler;
    private readonly SecurityPolicyEngineeringHandler _securityPolicyHandler;
    private readonly CommandEngineeringHandler _commandHandler;
    private readonly GatewayEngineeringHandler _gatewayHandler;
    private readonly ScriptEngineeringHandler _scriptHandler;
    private readonly ReportEngineeringHandler _reportHandler;
    private readonly OperationalEventEngineeringHandler _operationalEventHandler;
    private readonly MediaSourceEngineeringHandler _mediaSourceHandler;

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
        IVisualAssetEngineeringRegistry? visualAssets = null,
        IReportEngineeringRegistry? reports = null,
        IDataSourceConfigurationValidator? dataSourceConfigurationValidator = null,
        IOperationalEventEngineeringRegistry? operationalEvents = null,
        IEngineeringLockRegistry? engineeringLock = null,
        IApplicationBrandingEngineeringRegistry? branding = null,
        IMediaSourceEngineeringRegistry? mediaSources = null)
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
        _reports = reports ?? new InMemoryReportEngineeringRegistry();
        _operationalEvents = operationalEvents
            ?? (_scripts as IOperationalEventEngineeringRegistry)
            ?? new InMemoryOperationalEventEngineeringRegistry();
        _engineeringLock = engineeringLock ?? new InMemoryEngineeringLockRegistry();
        _branding = branding ?? new InMemoryApplicationBrandingEngineeringRegistry();
        _mediaSources = mediaSources ?? new InMemoryMediaSourceEngineeringRegistry();
        _json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new SecurityCapabilityJsonConverter(),
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
            }
        };

        _csv = new EngineeringCsvExchange(_json);
        _dataSourceHandler = new DataSourceEngineeringHandler(dataSources, tags, alarms, commands, dataSourceConfigurationValidator);
        _tagHandler = new TagEngineeringHandler(tags, dataSources, alarms, securityPolicies);
        _securityScopeHandler = new SecurityScopeEngineeringHandler(
            securityPolicies,
            tags,
            assets,
            views,
            commands);
        _alarmHandler = new AlarmEngineeringHandler(alarms, _tagHandler);
        _assetHandler = new AssetEngineeringHandler(assets, tags, _visualAssets);
        _visualAssetHandler = new VisualAssetEngineeringHandler(_visualAssets);
        _viewHandler = new ViewEngineeringHandler(views, assets, tags, dataSources, _visualAssets, commands, _mediaSources);
        _securityPolicyHandler = new SecurityPolicyEngineeringHandler(securityPolicies, _securityScopeHandler);
        _commandHandler = new CommandEngineeringHandler(commands, tags, dataSources);
        _gatewayHandler = new GatewayEngineeringHandler(gateways, tags, dataSources);
        _scriptHandler = new ScriptEngineeringHandler(_scripts, tags, dataSources, assets, views);
        _reportHandler = new ReportEngineeringHandler(_reports, _visualAssets);
        _operationalEventHandler = new OperationalEventEngineeringHandler(_operationalEvents);
        _mediaSourceHandler = new MediaSourceEngineeringHandler(_mediaSources);
    }

    public EngineeringPackage ExportPackage()
    {
        var authorityOwnedPolicies = _securityPolicies is IAuthorityPolicyEngineeringRegistryView;
        var authoritySnapshot = (_securityPolicies as IAuthorityPolicyEngineeringRegistryView)?.AuthoritySnapshot();
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
            authorityOwnedPolicies ? Array.Empty<SecurityRoleEngineeringDto>() : _securityPolicies.SnapshotRoles(),
            _commands.Snapshot(),
            _gateways.Snapshot(),
            _scripts.SnapshotScripts(),
            _scripts.SnapshotVisualEventReferences(),
            _visualAssets.SnapshotAssets(),
            _reports.SnapshotReports(),
            _operationalEvents.SnapshotOperationalEvents(),
            _views.StartupScreenId,
            _engineeringLock.Snapshot(),
            authorityOwnedPolicies ? Array.Empty<SecurityScopeEngineeringDto>() : _securityPolicies.SnapshotScopes(),
            authoritySnapshot is null ? null : new AuthorityPolicyReferenceEngineeringDto(
                AuthorityPolicyContract.Schema,
                AuthorityPolicyContract.SchemaVersion,
                authoritySnapshot.Version,
                authoritySnapshot.Roles.Select(role => role.Id!.Value).Order().ToArray(),
                authoritySnapshot.Scopes.Select(scope => scope.Id).Order().ToArray()),
            HistorianCaptureProfiles: Array.Empty<HistorianCaptureProfileEngineeringDto>(),
            DataQueries: Array.Empty<DataQueryEngineeringDto>(),
            AlarmViews: Array.Empty<AlarmViewEngineeringDto>(),
            Branding: _branding.Snapshot(),
            RuntimePresentation: _runtimePresentation,
            MediaSources: _mediaSources.Snapshot());
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

        var normalized = package with
        {
            DataSources = package.DataSources ?? Array.Empty<DataSourceEngineeringDto>(),
            Templates = package.Templates ?? Array.Empty<EquipmentTemplateEngineeringDto>(),
            Equipment = package.Equipment ?? Array.Empty<EquipmentEngineeringDto>(),
            Dynamos = package.Dynamos ?? Array.Empty<DynamoEngineeringDto>(),
            Screens = package.Screens ?? Array.Empty<ScreenEngineeringDto>(),
            Popups = package.Popups ?? Array.Empty<PopupEngineeringDto>(),
            SecurityRoles = AuthorityPolicyEngineeringMigration.NormalizeRoles(
                package.SecurityRoles,
                package.SchemaVersion),
            SecurityScopes = package.SecurityScopes ?? Array.Empty<SecurityScopeEngineeringDto>(),
            AuthorityPolicyReference = package.AuthorityPolicyReference,
            HistorianCaptureProfiles = package.HistorianCaptureProfiles ?? Array.Empty<HistorianCaptureProfileEngineeringDto>(),
            DataQueries = package.DataQueries ?? Array.Empty<DataQueryEngineeringDto>(),
            AlarmViews = package.AlarmViews ?? Array.Empty<AlarmViewEngineeringDto>(),
            MediaSources = package.MediaSources ?? Array.Empty<MediaSourceEngineeringDto>(),
            Commands = package.Commands ?? Array.Empty<CommandEngineeringDto>(),
            Gateways = package.Gateways ?? Array.Empty<GatewayRouteEngineeringDto>(),
            Scripts = package.Scripts ?? Array.Empty<ScriptEngineeringDefinition>(),
            ScriptVisualEventReferences = package.ScriptVisualEventReferences ?? Array.Empty<ScriptVisualEventReference>(),
            VisualAssets = package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>(),
            Reports = package.Reports ?? Array.Empty<ReportEngineeringDto>(),
            OperationalEvents = package.OperationalEvents ?? Array.Empty<OperationalEventEngineeringDto>(),
            EngineeringLock = EngineeringLockContract.Normalize(package.EngineeringLock),
            RuntimePresentation = package.RuntimePresentation ?? new RuntimePresentationEngineeringDto()
        };
        return AuthorityScopeEngineeringMigration.Normalize(normalized);
    }

    public EngineeringPackage ParseTagsCsv(string csv) =>
        EmptyWithCurrentAuthorityReference() with { Tags = _csv.ParseTags(csv) };

    public EngineeringPackage ParseAlarmsCsv(string csv) =>
        EmptyWithCurrentAuthorityReference() with { Alarms = _csv.ParseAlarms(csv) };

    public EngineeringPackage ParseDataSourcesCsv(string csv) =>
        EmptyWithCurrentAuthorityReference() with { DataSources = _csv.ParseDataSources(csv) };

    public ImportPreview Preview(EngineeringPackage package, ImportMode mode) =>
        Preview(package, mode, null);

    public ImportPreview Preview(
        EngineeringPackage package,
        ImportMode mode,
        EngineeringImportContext? context)
    {
        _ = EngineeringLockContract.Normalize(package.EngineeringLock);
        var items = new List<ImportPreviewItem>();
        if (package.RuntimePresentation is { Version: not 1 })
        {
            items.Add(new ImportPreviewItem(
                ImportEntityKind.RuntimePresentation,
                "runtime-presentation",
                ImportOperation.Error,
                [new ImportIssue(
                    "RUNTIME_PRESENTATION_VERSION_UNSUPPORTED",
                    $"Runtime presentation version {package.RuntimePresentation.Version} is unsupported; expected 1.",
                    ImportEntityKind.RuntimePresentation,
                    "runtime-presentation",
                    true)]));
        }
        if (_securityPolicies is IAuthorityPolicyEngineeringRegistryView authorityView)
        {
            var reference = AuthorityPolicyReferenceValidator.ValidateCurrentIdentitySet(
                package.AuthorityPolicyReference,
                authorityView.AuthoritySnapshot());
            if (!reference.IsValid)
            {
                var issue = new ImportIssue(
                    reference.ErrorCode!,
                    reference.Message!,
                    ImportEntityKind.SecurityRole,
                    "authority-policy",
                    true);
                items.Add(new ImportPreviewItem(ImportEntityKind.SecurityRole, "authority-policy", ImportOperation.Error, [issue]));
            }

            if ((package.SecurityRoles?.Count ?? 0) > 0 || (package.SecurityScopes?.Count ?? 0) > 0)
            {
                var issue = new ImportIssue(
                    "SECURITY_AUTHORITY_POLICY_IMPORT_BLOCKED",
                    "Security roles and scope hierarchy are owned by Security Authority. Engineering packages are reference-only and cannot mutate the live Authority.",
                    ImportEntityKind.SecurityRole,
                    "authority-policy",
                    true);
                items.Add(new ImportPreviewItem(ImportEntityKind.SecurityRole, "authority-policy", ImportOperation.Error, [issue]));
            }
        }
        _dataSourceHandler.Preview(package, mode, items);
        _tagHandler.Preview(package, mode, items);
        _alarmHandler.Preview(package, mode, items);
        _assetHandler.Preview(package, mode, items);
        _visualAssetHandler.Preview(package, mode, items, context);
        var brandingIssues = ApplicationBrandingEngineeringValidator.Validate(
            package.Branding,
            _visualAssets,
            package.VisualAssets);
        if (brandingIssues.Any(issue => issue.IsError))
        {
            items.Add(new ImportPreviewItem(
                ImportEntityKind.Branding,
                "application-branding",
                ImportOperation.Error,
                brandingIssues));
        }
        _viewHandler.Preview(package, mode, items);
        _commandHandler.Preview(package, mode, items);
        _gatewayHandler.Preview(package, mode, items);
        _scriptHandler.Preview(package, mode, items);
        _operationalEventHandler.Preview(package, mode, items);
        _reportHandler.Preview(package, mode, items);
        _mediaSourceHandler.Preview(package, mode, items);
        _securityScopeHandler.Preview(package, mode, items);
        _securityPolicyHandler.Preview(package, mode, items);
        PreviewOperationalHmiReferences(package, items);
        var requestedRuntimePresentation = package.RuntimePresentation ?? new RuntimePresentationEngineeringDto();
        var runtimeScreenKeys = (package.Screens ?? Array.Empty<ScreenEngineeringDto>())
            .Where(screen => screen is not null && !string.IsNullOrWhiteSpace(screen.Key))
            .Select(screen => screen.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var header = requestedRuntimePresentation.Header;
        var invalidHeader = header is not null &&
            (header.Height is < 32 or > 160 || header.TitlePosition is not ("left" or "center" or "right") ||
             (header.BackgroundColor is not null && !System.Text.RegularExpressions.Regex.IsMatch(header.BackgroundColor, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")) ||
             (header.Links?.Count ?? 0) > 16 ||
             (header.Links?.Any(link => link is null || string.IsNullOrWhiteSpace(link.Label) || link.Label.Length > 128 ||
                 string.IsNullOrWhiteSpace(link.ScreenKey) ||
                 !runtimeScreenKeys.Contains(link.ScreenKey) ||
                 (link.VisualAssetId.HasValue && !(package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>()).Any(asset => asset is not null && asset.Id == link.VisualAssetId && asset.MediaType?.StartsWith("image/", StringComparison.Ordinal) == true))) ?? false));
        var invalidMobile = requestedRuntimePresentation.MobileScreens?.Any(pair => !runtimeScreenKeys.Contains(pair.Key) || !runtimeScreenKeys.Contains(pair.Value)) ?? false;
        if (invalidHeader || invalidMobile)
            items.Add(new ImportPreviewItem(ImportEntityKind.RuntimePresentation, "runtime-presentation", ImportOperation.Error,
                [new ImportIssue("RUNTIME_PRESENTATION_REFERENCE_INVALID", "Header settings or mobile screen references are invalid.", ImportEntityKind.RuntimePresentation, "runtime-presentation", true)]));
        if (requestedRuntimePresentation.MobileOrientation is not ("landscape" or "portrait"))
            items.Add(new ImportPreviewItem(
                ImportEntityKind.RuntimePresentation,
                "runtime-presentation",
                ImportOperation.Error,
                [new ImportIssue(
                    "RUNTIME_MOBILE_ORIENTATION_INVALID",
                    "Mobile orientation must be 'landscape' or 'portrait'.",
                    ImportEntityKind.RuntimePresentation,
                    "runtime-presentation",
                    true)]));
        items.Add(new ImportPreviewItem(
            ImportEntityKind.RuntimePresentation,
            "runtime-presentation",
            requestedRuntimePresentation == _runtimePresentation ? ImportOperation.Skip : ImportOperation.Update,
            Array.Empty<ImportIssue>()));

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
        if (package.Branding is not null)
            _branding.Replace(package.Branding);
        var requestedRuntimePresentation = package.RuntimePresentation ?? new RuntimePresentationEngineeringDto();
        if (requestedRuntimePresentation == _runtimePresentation) skipped++;
        else
        {
            _runtimePresentation = requestedRuntimePresentation;
            updated++;
        }
        _viewHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        if ((package.Screens?.Count ?? 0) > 0 || package.StartupScreenId.HasValue)
            _views.SetStartupScreen(package.StartupScreenId);
        _commandHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _gatewayHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _scriptHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _operationalEventHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _reportHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _mediaSourceHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _securityScopeHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _securityPolicyHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _engineeringLock.Replace(package.EngineeringLock);

        return new ImportResult(mode, created, updated, skipped, Array.Empty<ImportIssue>());
    }

    private void PreviewOperationalHmiReferences(
        EngineeringPackage package,
        List<ImportPreviewItem> items)
    {
        if (package.StartupScreenId.HasValue)
        {
            var startupId = package.StartupScreenId.Value;
            var exists = startupId != Guid.Empty &&
                (_views.FindScreen(startupId) is not null ||
                 (package.Screens ?? Array.Empty<ScreenEngineeringDto>())
                     .Any(screen => screen is not null && screen.Id == startupId));
            if (!exists)
            {
                var issue = new ImportIssue(
                    "STARTUP_SCREEN_NOT_FOUND",
                    $"Startup/Home Screen identity '{startupId:D}' does not resolve in the prospective Engineering model.",
                    ImportEntityKind.Screen,
                    startupId.ToString("D"),
                    true);
                items.Add(new ImportPreviewItem(
                    ImportEntityKind.Screen,
                    startupId.ToString("D"),
                    ImportOperation.Error,
                    [issue]));
            }
        }

        foreach (var screen in package.Screens ?? Array.Empty<ScreenEngineeringDto>())
        {
            if (screen is null) continue;
            PreviewOperationalActions(screen.Elements, ImportEntityKind.Screen, screen.Key, package, items);
        }

        foreach (var popup in package.Popups ?? Array.Empty<PopupEngineeringDto>())
        {
            if (popup is null) continue;
            PreviewOperationalActions(popup.Elements, ImportEntityKind.Popup, popup.Key, package, items);
        }

        foreach (var dynamo in package.Dynamos ?? Array.Empty<DynamoEngineeringDto>())
        {
            if (dynamo is null) continue;
            PreviewOperationalActions(dynamo.Elements, ImportEntityKind.Dynamo, dynamo.Key, package, items);
        }
    }

    private void PreviewOperationalActions(
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        ImportEntityKind kind,
        string entityKey,
        EngineeringPackage package,
        List<ImportPreviewItem> items)
    {
        var issues = new List<ImportIssue>();
        ValidateOperationalActions(elements, kind, entityKey, package, issues);
        if (issues.Count == 0) return;

        items.Add(new ImportPreviewItem(kind, entityKey, ImportOperation.Error, issues));
    }

    private void ValidateOperationalActions(
        IReadOnlyCollection<VisualElementEngineeringDto>? elements,
        ImportEntityKind kind,
        string entityKey,
        EngineeringPackage package,
        List<ImportIssue> issues)
    {
        foreach (var element in elements ?? Array.Empty<VisualElementEngineeringDto>())
        {
            if (element is null) continue;

            foreach (var action in element.Actions ?? Array.Empty<VisualNavigationActionEngineeringDto>())
            {
                if (action is null) continue;

                if (action.Kind == VisualNavigationActionKind.ExecuteCommand)
                {
                    var portableDynamoCommand = kind == ImportEntityKind.Dynamo &&
                        !string.IsNullOrWhiteSpace(action.CommandParameterKey) &&
                        !action.CommandId.HasValue;
                    if (portableDynamoCommand)
                    {
                        if (!string.IsNullOrWhiteSpace(action.TargetKey) || action.Parameters is { Count: > 0 })
                            issues.Add(new ImportIssue(
                                "VISUAL_ACTION_COMMAND_PARAMETER_SHAPE_INVALID",
                                $"Parameterized ExecuteCommand action '{action.EventKey}' cannot declare TargetKey or Parameters.",
                                kind,
                                entityKey,
                                true));
                        continue;
                    }

                    if (!action.CommandId.HasValue || action.CommandId == Guid.Empty)
                    {
                        issues.Add(new ImportIssue(
                            "VISUAL_ACTION_COMMAND_REQUIRED",
                            $"ExecuteCommand action '{action.EventKey}' on visual element '{element.Key}' requires a stable Command identity.",
                            kind,
                            entityKey,
                            true));
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(action.TargetKey))
                    {
                        issues.Add(new ImportIssue(
                            "VISUAL_ACTION_COMMAND_TARGET_NOT_ALLOWED",
                            $"ExecuteCommand action '{action.EventKey}' cannot declare a navigation target key.",
                            kind,
                            entityKey,
                            true));
                    }

                    if (action.Parameters is { Count: > 0 })
                    {
                        issues.Add(new ImportIssue(
                            "VISUAL_ACTION_COMMAND_PARAMETERS_NOT_ALLOWED",
                            $"ExecuteCommand action '{action.EventKey}' cannot override canonical Command parameters or value.",
                            kind,
                            entityKey,
                            true));
                    }

                    var commandId = action.CommandId.Value;
                    var commandExists = _commands.Find(commandId) is not null ||
                        (package.Commands ?? Array.Empty<CommandEngineeringDto>())
                            .Any(command => command is not null && command.Id == commandId);
                    if (!commandExists)
                    {
                        issues.Add(new ImportIssue(
                            "VISUAL_ACTION_COMMAND_NOT_FOUND",
                            $"ExecuteCommand action '{action.EventKey}' references Command identity '{commandId:D}', which was not found in the prospective Engineering model.",
                            kind,
                            entityKey,
                            true));
                    }
                }
                else if (action.CommandId.HasValue)
                {
                    issues.Add(new ImportIssue(
                        "VISUAL_ACTION_COMMAND_NOT_ALLOWED",
                        $"Visual action '{action.EventKey}' of kind {action.Kind} cannot carry a Command identity.",
                        kind,
                        entityKey,
                        true));
                }
            }

            ValidateOperationalActions(element.Children, kind, entityKey, package, issues);
        }
    }

    private EngineeringPackage EmptyWithCurrentAuthorityReference() =>
        Empty() with
        {
            AuthorityPolicyReference = ExportPackage().AuthorityPolicyReference,
            RuntimePresentation = _runtimePresentation
        };

    private EngineeringPackage Empty() => new(
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
        Array.Empty<VisualAssetEngineeringDto>(),
        Array.Empty<ReportEngineeringDto>(),
        Array.Empty<OperationalEventEngineeringDto>(),
        null,
        _engineeringLock.Snapshot());
}
