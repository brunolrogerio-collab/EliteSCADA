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
using Scada.Engineering.Interactions;
using Scada.Engineering.Intelligence;
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
    public const int CurrentSchemaVersion = 30;

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
    private readonly IDriverInteractionEngineeringRegistry _driverInteractions;
    private readonly IAssetProcessIntelligenceEngineeringRegistry _assetProcessIntelligence;
    private RuntimePresentationEngineeringDto _runtimePresentation = new();
    private readonly object _resourceOrganizationGate = new();
    private readonly Dictionary<string, EngineeringResourceOrganizationEngineeringDto> _resourceOrganizations = new(StringComparer.Ordinal);
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
    private readonly DriverInteractionEngineeringHandler _driverInteractionHandler;

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
        IMediaSourceEngineeringRegistry? mediaSources = null,
        IDriverInteractionEngineeringRegistry? driverInteractions = null,
        IAssetProcessIntelligenceEngineeringRegistry? assetProcessIntelligence = null)
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
        _driverInteractions = driverInteractions ?? new InMemoryDriverInteractionEngineeringRegistry();
        _assetProcessIntelligence = assetProcessIntelligence ?? new InMemoryAssetProcessIntelligenceEngineeringRegistry();
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
            commands,
            _driverInteractions);
        _alarmHandler = new AlarmEngineeringHandler(alarms, _tagHandler, assets);
        _assetHandler = new AssetEngineeringHandler(assets, tags, _visualAssets, dataSources, commands);
        _visualAssetHandler = new VisualAssetEngineeringHandler(_visualAssets);
        _viewHandler = new ViewEngineeringHandler(views, assets, tags, dataSources, _visualAssets, commands, _mediaSources);
        _securityPolicyHandler = new SecurityPolicyEngineeringHandler(securityPolicies, _securityScopeHandler);
        _commandHandler = new CommandEngineeringHandler(commands, tags, dataSources);
        _gatewayHandler = new GatewayEngineeringHandler(gateways, tags, dataSources);
        _scriptHandler = new ScriptEngineeringHandler(_scripts, tags, dataSources, assets, views);
        _reportHandler = new ReportEngineeringHandler(_reports, _visualAssets);
        _operationalEventHandler = new OperationalEventEngineeringHandler(_operationalEvents, tags);
        _mediaSourceHandler = new MediaSourceEngineeringHandler(_mediaSources);
        _driverInteractionHandler = new DriverInteractionEngineeringHandler(_driverInteractions, dataSources, assets);
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
            MediaSources: _mediaSources.Snapshot(),
            Locations: _assets.SnapshotLocations(),
            TransientEventDefinitions: _driverInteractions.SnapshotTransientEventDefinitions(),
            CapabilityEventReferences: _driverInteractions.SnapshotCapabilityEventReferences(),
            RichCommandDefinitions: _driverInteractions.SnapshotRichCommandDefinitions(),
            DriverCommandBindings: _driverInteractions.SnapshotDriverCommandBindings(),
            AssetProcessIntelligence: _assetProcessIntelligence.Snapshot(),
            ResourceOrganizations: SnapshotResourceOrganizations());
    }

    public string ExportJson(bool indented = true)
    {
        var options = new JsonSerializerOptions(_json) { WriteIndented = indented };
        return JsonSerializer.Serialize(ExportPackage(), options);
    }

    public string ExportTagsCsv()
    {
        var package = ExportPackage();
        return _csv.ExportTags(package.Tags, package.DataSources, package.HistorianCaptureProfiles);
    }

    public string ExportAlarmsCsv()
    {
        var package = ExportPackage();
        return _csv.ExportAlarms(package.Alarms, package.Tags, package.Locations, package.Equipment);
    }

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
        if (package.SchemaVersion < 27 && HasVersion2ControlledSemantics(package))
            throw new InvalidDataException("ENGINEERING_CONTROLLED_SEMANTICS_SCHEMA_VERSION_REQUIRED: version 2 controlled semantic annotations require Engineering package schema version 27 or newer.");
        if (package.SchemaVersion < 26 && HasControlledSemantics(package))
            throw new InvalidDataException("ENGINEERING_CONTROLLED_SEMANTICS_SCHEMA_VERSION_REQUIRED: controlled semantic annotations require Engineering package schema version 26 or newer.");
        if (package.SchemaVersion < 25 && package.AssetProcessIntelligence?.Version >= 2)
            throw new InvalidDataException("Asset & Process Intelligence configuration version 2 requires Engineering package schema version 25 or newer.");
        if (package.SchemaVersion < 28 &&
            (package.AssetProcessIntelligence?.Version >= 3 ||
             (package.AssetProcessIntelligence?.OperatingContextProfiles?.Count ?? 0) != 0))
            throw new InvalidDataException("INTELLIGENCE_OPERATING_CONTEXT_SCHEMA_VERSION_REQUIRED: operating-context profiles require Engineering package schema version 28 or newer.");
        if (package.SchemaVersion < 29 && package.ResourceOrganizations is { Count: > 0 })
            throw new InvalidDataException("ENGINEERING_RESOURCE_ORGANIZATION_SCHEMA_VERSION_REQUIRED: resource folders require Engineering package schema version 29 or newer.");

        var normalized = package with
        {
            DataSources = package.DataSources ?? Array.Empty<DataSourceEngineeringDto>(),
            Locations = package.Locations ?? Array.Empty<LocationEngineeringDto>(),
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
            RuntimePresentation = package.RuntimePresentation ?? new RuntimePresentationEngineeringDto(),
            TransientEventDefinitions = package.TransientEventDefinitions ?? Array.Empty<TransientEventDefinitionEngineeringDto>(),
            CapabilityEventReferences = package.CapabilityEventReferences ?? Array.Empty<CapabilityEventReferenceEngineeringDto>(),
            RichCommandDefinitions = package.RichCommandDefinitions ?? Array.Empty<RichCommandDefinitionEngineeringDto>(),
            DriverCommandBindings = package.DriverCommandBindings ?? Array.Empty<DriverCommandBindingEngineeringDto>(),
            AssetProcessIntelligence = package.AssetProcessIntelligence is null
                ? null
                : package.AssetProcessIntelligence with
                {
                    OperatingContextProfiles = package.AssetProcessIntelligence.OperatingContextProfiles ?? Array.Empty<AssetProcessIntelligenceOperatingContextProfileEngineeringDto>()
                }
        };
        return AuthorityScopeEngineeringMigration.Normalize(normalized);
    }

    public EngineeringPackage ParseTagsCsv(string csv)
    {
        var parsed = _csv.ParseTagsWithSchema(csv);
        return EmptyWithCurrentAuthorityReference() with
        {
            SchemaVersion = parsed.SchemaVersion,
            Tags = parsed.Tags
        };
    }

    public EngineeringPackage ParseAlarmsCsv(string csv)
    {
        var package = ExportPackage();
        return EmptyWithCurrentAuthorityReference() with
        {
            Alarms = _csv.ParseAlarms(csv, package.Locations, package.Equipment)
        };
    }

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
        if (package.SchemaVersion < 26 && HasControlledSemantics(package))
        {
            var issue = new ImportIssue(
                "ENGINEERING_CONTROLLED_SEMANTICS_SCHEMA_VERSION_REQUIRED",
                "Controlled semantic annotations require Engineering package schema version 26 or newer.",
                ImportEntityKind.AssetProcessIntelligenceConfiguration,
                "semantic-annotations",
                true);
            items.Add(new ImportPreviewItem(ImportEntityKind.AssetProcessIntelligenceConfiguration, "semantic-annotations", ImportOperation.Error, [issue]));
        }
        if (package.SchemaVersion < 27 && HasVersion2ControlledSemantics(package))
        {
            var issue = new ImportIssue(
                "ENGINEERING_CONTROLLED_SEMANTICS_SCHEMA_VERSION_REQUIRED",
                "Version 2 controlled semantic annotations require Engineering package schema version 27 or newer.",
                ImportEntityKind.AssetProcessIntelligenceConfiguration,
                "semantic-annotations",
                true);
            items.Add(new ImportPreviewItem(ImportEntityKind.AssetProcessIntelligenceConfiguration, "semantic-annotations", ImportOperation.Error, [issue]));
        }
        if (package.SchemaVersion < 25 && package.AssetProcessIntelligence?.Version >= 2)
        {
            var issue = new ImportIssue(
                "INTELLIGENCE_SCHEMA_VERSION_MISMATCH",
                "Asset & Process Intelligence configuration version 2 requires Engineering package schema version 25 or newer.",
                ImportEntityKind.AssetProcessIntelligenceConfiguration,
                "asset-process-intelligence",
                true);
            items.Add(new ImportPreviewItem(ImportEntityKind.AssetProcessIntelligenceConfiguration, "asset-process-intelligence", ImportOperation.Error, [issue]));
        }
        if (package.SchemaVersion < 28 &&
            (package.AssetProcessIntelligence?.Version >= 3 ||
             (package.AssetProcessIntelligence?.OperatingContextProfiles?.Count ?? 0) != 0))
        {
            var issue = new ImportIssue(
                "INTELLIGENCE_OPERATING_CONTEXT_SCHEMA_VERSION_REQUIRED",
                "Operating-context profiles require Engineering package schema 28 or newer.",
                ImportEntityKind.AssetProcessIntelligenceConfiguration,
                "asset-process-intelligence",
                true);
            items.Add(new ImportPreviewItem(ImportEntityKind.AssetProcessIntelligenceConfiguration, "asset-process-intelligence", ImportOperation.Error, [issue]));
        }
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
        _driverInteractionHandler.Preview(package, mode, items);
        if (package.SchemaVersion < 29 && SnapshotResourceOrganizations().Count > 0 && package.ResourceOrganizations is null)
        {
            var issue = new ImportIssue(
                "ENGINEERING_RESOURCE_ORGANIZATION_IMPORT_CONFLICT",
                "This package predates resource-folder organization and would omit the saved folders. Re-export with schema 29 or newer before applying it.",
                ImportEntityKind.ResourceOrganization,
                "resource-organizations",
                true);
            items.Add(new ImportPreviewItem(ImportEntityKind.ResourceOrganization, "resource-organizations", ImportOperation.Error, [issue]));
        }
        if (package.SchemaVersion < 29 && package.ResourceOrganizations is { Count: > 0 })
        {
            var issue = new ImportIssue(
                "ENGINEERING_RESOURCE_ORGANIZATION_SCHEMA_VERSION_REQUIRED",
                "Resource folder organization requires Engineering package schema 29 or newer.",
                ImportEntityKind.ResourceOrganization,
                "resource-organizations",
                true);
            items.Add(new ImportPreviewItem(ImportEntityKind.ResourceOrganization, "resource-organizations", ImportOperation.Error, [issue]));
        }
        if (package.ResourceOrganizations is { } organizations)
            PreviewResourceOrganizations(organizations, items);
        PreviewAssetProcessIntelligence(package, mode, items);
        _securityScopeHandler.Preview(package, mode, items);
        _securityPolicyHandler.Preview(package, mode, items);
        PreviewOperationalHmiReferences(package, items);
        // Runtime presentation is project-level state. Imports may intentionally
        // contain only a partial resource update (for example, an Engineering Lock
        // update); absence must not erase the current header/mobile settings.
        var requestedRuntimePresentation = package.RuntimePresentation ?? _runtimePresentation;
        var runtimeScreenKeys = (package.Screens ?? Array.Empty<ScreenEngineeringDto>())
            .Where(screen => screen is not null && !string.IsNullOrWhiteSpace(screen.Key))
            .Select(screen => screen.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var header = requestedRuntimePresentation.Header;
        static bool IsValidHeaderColor(string? color) => color is null ||
            System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$");
        static bool IsValidHeaderTextStyle(RuntimeHeaderTextStyleEngineeringDto? style)
        {
            if (style is null) return true;
            if (style.FontFamily is not (null or "system-ui" or "Arial, sans-serif" or "Verdana, sans-serif" or "Georgia, serif" or "monospace")) return false;
            if (style.FontSize is < 8 or > 48) return false;
            if (!IsValidHeaderColor(style.Color)) return false;
            if (style.FontWeight is not { } weight) return true;
            if (weight.ValueKind == System.Text.Json.JsonValueKind.String)
                return weight.GetString() is "normal" or "bold" or "400" or "500" or "600" or "700";
            return weight.ValueKind == System.Text.Json.JsonValueKind.Number &&
                weight.TryGetInt32(out var numericWeight) && numericWeight is 400 or 500 or 600 or 700;
        }
        static bool IsValidHeaderDateTime(RuntimeHeaderDateTimeEngineeringDto? dateTime) => dateTime is null ||
            (dateTime.Mode is "off" or "time" or "date" or "dateTime" &&
             dateTime.Position is "left" or "right" &&
             dateTime.Order is >= 0 and <= 20 &&
             dateTime.DateFormat is "dd/MM/yyyy" or "MM/dd/yyyy" or "yyyy-MM-dd" &&
             dateTime.TimeFormat is "24h" or "12h");
        var invalidHeader = package.RuntimePresentation is not null && header is not null &&
            (header.Height is < 32 or > 168 || header.TitlePosition is not ("left" or "center" or "right") ||
             header.ControlsPosition is not ("left" or "right") || header.ControlsOrder is < 0 or > 20 ||
             !IsValidHeaderColor(header.BackgroundColor) ||
             !IsValidHeaderTextStyle(header.TitleStyle) || !IsValidHeaderTextStyle(header.ScreenNameStyle) ||
             !IsValidHeaderDateTime(header.DateTime) ||
             (header.Links?.Count ?? 0) > 16 ||
             (header.Links?.Any(link => link is null || string.IsNullOrWhiteSpace(link.Label) || link.Label.Length > 128 ||
                 string.IsNullOrWhiteSpace(link.ScreenKey) ||
                 !runtimeScreenKeys.Contains(link.ScreenKey) ||
                 (link.VisualAssetId.HasValue && !(package.VisualAssets ?? Array.Empty<VisualAssetEngineeringDto>()).Any(asset => asset is not null && asset.Id == link.VisualAssetId && asset.MediaType?.StartsWith("image/", StringComparison.Ordinal) == true))) ?? false));
        var invalidMobile = package.RuntimePresentation is not null &&
            (requestedRuntimePresentation.MobileScreens?.Any(pair => !runtimeScreenKeys.Contains(pair.Key) || !runtimeScreenKeys.Contains(pair.Value)) ?? false);
        if (invalidHeader || invalidMobile)
            items.Add(new ImportPreviewItem(ImportEntityKind.RuntimePresentation, "runtime-presentation", ImportOperation.Error,
                [new ImportIssue("RUNTIME_PRESENTATION_REFERENCE_INVALID", "Header settings or mobile screen references are invalid.", ImportEntityKind.RuntimePresentation, "runtime-presentation", true)]));
        if (package.RuntimePresentation is not null && requestedRuntimePresentation.MobileOrientation is not ("landscape" or "portrait"))
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
            package.RuntimePresentation is null || requestedRuntimePresentation == _runtimePresentation
                ? ImportOperation.Skip
                : ImportOperation.Update,
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
        if (package.RuntimePresentation is null) skipped++;
        else if (package.RuntimePresentation == _runtimePresentation) skipped++;
        else
        {
            _runtimePresentation = package.RuntimePresentation;
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
        _driverInteractionHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        if (package.AssetProcessIntelligence is not null)
        {
            var intelligence = package.AssetProcessIntelligence;
            if (context?.PreserveActivatedIntelligenceScopes != true)
            {
                intelligence = intelligence with
                {
                    Scopes = (intelligence.Scopes ?? Array.Empty<AssetProcessIntelligenceScopeEngineeringDto>())
                        .Select(scope => scope with { Active = false })
                        .ToArray()
                };
            }
            var before = _assetProcessIntelligence.Snapshot();
            intelligence = PreserveOperatingContextProfileVersions(before, intelligence);
            if (before == intelligence) skipped++;
            else
            {
                _assetProcessIntelligence.Replace(intelligence);
                updated++;
            }
        }
        _securityScopeHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _securityPolicyHandler.Apply(package, mode, ref created, ref updated, ref skipped);
        _engineeringLock.Replace(package.EngineeringLock);

        if (package.ResourceOrganizations is null) skipped++;
        else
        {
            var before = SnapshotResourceOrganizations();
            var after = package.ResourceOrganizations.OrderBy(entry => entry.Kind, StringComparer.Ordinal).ToArray();
            var currentByKind = before.ToDictionary(entry => entry.Kind, StringComparer.Ordinal);
            var changed = after.Any(entry =>
                !currentByKind.TryGetValue(entry.Kind, out var current) ||
                JsonSerializer.Serialize(current, _json) != JsonSerializer.Serialize(entry, _json));
            if (!changed) skipped++;
            else
            {
                lock (_resourceOrganizationGate)
                {
                    foreach (var entry in after) _resourceOrganizations[entry.Kind] = entry.Organization;
                }
                updated++;
            }
        }

        return new ImportResult(mode, created, updated, skipped, Array.Empty<ImportIssue>());
    }

    private IReadOnlyCollection<EngineeringResourceOrganizationEntryEngineeringDto> SnapshotResourceOrganizations()
    {
        lock (_resourceOrganizationGate)
            return _resourceOrganizations
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new EngineeringResourceOrganizationEntryEngineeringDto(pair.Key, pair.Value))
                .ToArray();
    }

    private void PreviewResourceOrganizations(
        IReadOnlyCollection<EngineeringResourceOrganizationEntryEngineeringDto> organizations,
        List<ImportPreviewItem> items)
    {
        var issues = new List<ImportIssue>();
        var seenKinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in organizations)
        {
            var kind = entry?.Kind ?? "<null>";
            if (entry is null)
            {
                issues.Add(new ImportIssue(
                    "ENGINEERING_RESOURCE_ORGANIZATION_NULL",
                    "Resource organization entry cannot be null.",
                    ImportEntityKind.ResourceOrganization,
                    kind,
                    true));
                continue;
            }
            if (!seenKinds.Add(kind))
                issues.Add(new ImportIssue(
                    "ENGINEERING_RESOURCE_ORGANIZATION_KIND_DUPLICATE",
                    $"Resource organization kind '{kind}' is duplicated.",
                    ImportEntityKind.ResourceOrganization,
                    kind,
                    true));
            var validation = EngineeringResourceOrganizationContract.Validate(entry);
            if (validation is not null)
                issues.Add(new ImportIssue(
                    "ENGINEERING_RESOURCE_ORGANIZATION_INVALID",
                    validation,
                    ImportEntityKind.ResourceOrganization,
                    kind,
                    true));
        }

        var currentByKind = SnapshotResourceOrganizations().ToDictionary(entry => entry.Kind, StringComparer.Ordinal);
        var requested = organizations.OrderBy(entry => entry?.Kind, StringComparer.Ordinal).ToArray();
        var operation = requested.All(entry => entry is not null &&
            currentByKind.TryGetValue(entry.Kind, out var current) &&
            JsonSerializer.Serialize(current, _json) == JsonSerializer.Serialize(entry, _json))
            ? ImportOperation.Skip
            : ImportOperation.Update;
        items.Add(new ImportPreviewItem(
            ImportEntityKind.ResourceOrganization,
            "resource-organizations",
            issues.Count > 0 ? ImportOperation.Error : operation,
            issues));
    }

    private void PreviewAssetProcessIntelligence(
        EngineeringPackage package,
        ImportMode mode,
        List<ImportPreviewItem> items)
    {
        var incoming = package.AssetProcessIntelligence;
        if (incoming is null) return;

        var currentConfiguration = _assetProcessIntelligence.Snapshot();
        if (currentConfiguration.OperatingContextProfiles is { Count: > 0 } &&
            package.AssetProcessIntelligence is { } incomingConfiguration &&
            (incomingConfiguration.Version < 3 ||
             (package.SchemaVersion < 28 && (incomingConfiguration.OperatingContextProfiles?.Count ?? 0) == 0)))
        {
            var issue = new ImportIssue(
                "INTELLIGENCE_OPERATING_CONTEXT_PROFILE_IMPORT_CONFLICT",
                "This package omits persisted operating-context profiles. Re-export with schema 28 and configuration version 3, then explicitly retain, change or remove those profiles.",
                ImportEntityKind.AssetProcessIntelligenceConfiguration,
                "asset-process-intelligence",
                true);
            items.Add(new ImportPreviewItem(ImportEntityKind.AssetProcessIntelligenceConfiguration, "asset-process-intelligence", ImportOperation.Error, [issue]));
        }
        if (package.SchemaVersion < 27 && OmitsPersistedVersion2SemanticBindings(currentConfiguration, incoming))
        {
            var issue = new ImportIssue(
                "INTELLIGENCE_SEMANTIC_ANNOTATION_CONFLICT",
                "This schema-26-or-older package omits or changes persisted version 2 signal annotations. Re-import with schema 27 and explicitly retain, change, or remove those annotations.",
                ImportEntityKind.AssetProcessIntelligenceConfiguration,
                "asset-process-intelligence",
                true);
            items.Add(new ImportPreviewItem(ImportEntityKind.AssetProcessIntelligenceConfiguration, "asset-process-intelligence", ImportOperation.Error, [issue]));
        }
        if (package.SchemaVersion < 26 && OmitsPersistedSemanticBindings(currentConfiguration, incoming))
        {
            var issue = new ImportIssue(
                "INTELLIGENCE_SEMANTIC_ANNOTATION_CONFLICT",
                "This schema-25-or-older package omits persisted typed signal annotations. Re-import with schema 26 and explicitly retain, change, or remove those annotations.",
                ImportEntityKind.AssetProcessIntelligenceConfiguration,
                "asset-process-intelligence",
                true);
            items.Add(new ImportPreviewItem(ImportEntityKind.AssetProcessIntelligenceConfiguration, "asset-process-intelligence", ImportOperation.Error, [issue]));
        }

        var currentTags = _tags.Snapshot().Select(EngineeringDtoMapper.ToDto).ToArray();
        var tags = BuildProspectiveResources(
            currentTags, package.Tags ?? Array.Empty<TagEngineeringDto>(), item => item.Id, mode,
            ImportEntityKind.Tag, items);
        var equipment = BuildProspectiveResources(
            _assets.SnapshotEquipment(), package.Equipment ?? Array.Empty<EquipmentEngineeringDto>(), item => item.Id, mode,
            ImportEntityKind.Equipment, items);
        var locations = BuildProspectiveResources(
            _assets.SnapshotLocations(), package.Locations ?? Array.Empty<LocationEngineeringDto>(), item => item.Id, mode,
            ImportEntityKind.Location, items);
        var validationIssues = AssetProcessIntelligenceEngineeringValidator.Validate(incoming, equipment, locations, tags);
        foreach (var group in validationIssues.GroupBy(issue => ImportKind(issue.Entity)))
        {
            var issues = group.Select(issue => new ImportIssue(
                issue.Code,
                issue.Message,
                group.Key,
                issue.ResourceId?.ToString("D") ?? "asset-process-intelligence",
                true)).ToArray();
            items.Add(new ImportPreviewItem(group.Key, "asset-process-intelligence", ImportOperation.Error, issues));
        }
        if (validationIssues.Count == 0)
            items.Add(new ImportPreviewItem(ImportEntityKind.AssetProcessIntelligenceConfiguration, "asset-process-intelligence", ImportOperation.Update, Array.Empty<ImportIssue>()));
    }

    private static AssetProcessIntelligenceEngineeringDto PreserveOperatingContextProfileVersions(
        AssetProcessIntelligenceEngineeringDto current,
        AssetProcessIntelligenceEngineeringDto incoming)
    {
        if (incoming.Version != 3) return incoming;
        var currentById = (current.OperatingContextProfiles ?? Array.Empty<AssetProcessIntelligenceOperatingContextProfileEngineeringDto>())
            .ToDictionary(profile => profile.Id);
        var profiles = (incoming.OperatingContextProfiles ?? Array.Empty<AssetProcessIntelligenceOperatingContextProfileEngineeringDto>())
            .Select(profile =>
            {
                if (!currentById.TryGetValue(profile.Id, out var existing))
                    return profile with { Version = Math.Max(1, profile.Version) };
                var unchanged = string.Equals(existing.Key, profile.Key, StringComparison.Ordinal) &&
                    string.Equals(existing.DisplayLabel, profile.DisplayLabel, StringComparison.Ordinal) &&
                    existing.AssignmentId == profile.AssignmentId &&
                    (existing.Predicates ?? Array.Empty<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto>())
                        .OrderBy(predicate => predicate.TagId).ThenBy(predicate => predicate.Kind)
                        .SequenceEqual((profile.Predicates ?? Array.Empty<AssetProcessIntelligenceOperatingContextPredicateEngineeringDto>())
                            .OrderBy(predicate => predicate.TagId).ThenBy(predicate => predicate.Kind));
                return profile with { Version = unchanged ? existing.Version : checked(existing.Version + 1) };
            })
            .ToArray();
        return incoming with { OperatingContextProfiles = profiles };
    }

    private static bool HasControlledSemantics(EngineeringPackage package) =>
        (package.Tags ?? Array.Empty<TagEngineeringDto>()).Any(tag => tag?.ControlledEngineeringUnit is not null) ||
        (package.AssetProcessIntelligence?.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
            .Where(profile => profile is not null)
            .SelectMany(profile => profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
            .Any(signal => signal?.SemanticBinding is not null);

    private static bool HasVersion2ControlledSemantics(EngineeringPackage package) =>
        (package.Tags ?? Array.Empty<TagEngineeringDto>()).Any(tag => tag?.ControlledEngineeringUnit?.CatalogVersion > 1) ||
        (package.AssetProcessIntelligence?.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
            .Where(profile => profile is not null)
            .SelectMany(profile => profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
            .Any(signal => signal?.SemanticBinding?.CatalogVersion > 1);

    private static bool OmitsPersistedSemanticBindings(
        AssetProcessIntelligenceEngineeringDto current,
        AssetProcessIntelligenceEngineeringDto incoming)
    {
        var persisted = (current.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
            .Where(profile => profile is not null)
            .SelectMany(profile => (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
                .Where(signal => signal?.SemanticBinding is not null)
                .Select(signal => (Key: (profile.Id, signal!.Id), Signal: signal)))
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.First().Signal);
        if (persisted.Count == 0) return false;
        var replacement = (incoming.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
            .Where(profile => profile is not null)
            .SelectMany(profile => (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
                .Where(signal => signal is not null)
                .Select(signal => (Key: (profile.Id, signal!.Id), Signal: signal)))
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.First().Signal);
        return persisted.Any(item =>
            !replacement.TryGetValue(item.Key, out var candidate) ||
            candidate.TagId != item.Value.TagId ||
            candidate.SemanticBinding != item.Value.SemanticBinding);
    }

    private static bool OmitsPersistedVersion2SemanticBindings(
        AssetProcessIntelligenceEngineeringDto current,
        AssetProcessIntelligenceEngineeringDto incoming)
    {
        var persisted = (current.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
            .Where(profile => profile is not null)
            .SelectMany(profile => (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
                .Where(signal => signal?.SemanticBinding?.CatalogVersion == 2)
                .Select(signal => (ProfileId: profile.Id, Signal: signal!)))
            .GroupBy(item => (item.ProfileId, item.Signal.Id))
            .ToDictionary(group => group.Key, group => group.First().Signal);
        if (persisted.Count == 0) return false;
        var replacement = (incoming.Profiles ?? Array.Empty<AssetProcessIntelligenceProfileEngineeringDto>())
            .Where(profile => profile is not null)
            .SelectMany(profile => (profile.Signals ?? Array.Empty<AssetProcessIntelligenceSignalEngineeringDto>())
                .Where(signal => signal is not null)
                .Select(signal => (Key: (profile.Id, signal!.Id), Signal: signal)))
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.First().Signal);
        return persisted.Any(item =>
            !replacement.TryGetValue(item.Key, out var candidate) ||
            candidate.TagId != item.Value.TagId ||
            candidate.SemanticBinding != item.Value.SemanticBinding);
    }

    private static ImportEntityKind ImportKind(AssetProcessIntelligenceIssueEntity entity) => entity switch
    {
        AssetProcessIntelligenceIssueEntity.Scope => ImportEntityKind.AssetProcessIntelligenceScope,
        AssetProcessIntelligenceIssueEntity.Profile => ImportEntityKind.AssetProcessIntelligenceProfile,
        AssetProcessIntelligenceIssueEntity.Assignment => ImportEntityKind.AssetProcessIntelligenceAssignment,
        AssetProcessIntelligenceIssueEntity.ProcessStage => ImportEntityKind.AssetProcessIntelligenceProcessStage,
        _ => ImportEntityKind.AssetProcessIntelligenceConfiguration
    };

    private static IReadOnlyCollection<T> BuildProspectiveResources<T>(
        IReadOnlyCollection<T> current,
        IReadOnlyCollection<T> incoming,
        Func<T, Guid?> identity,
        ImportMode mode,
        ImportEntityKind entityKind,
        List<ImportPreviewItem> items)
    {
        var result = new Dictionary<Guid, T>();
        foreach (var item in current)
        {
            var id = identity(item);
            if (id is { } stableId && stableId != Guid.Empty)
            {
                if (!result.TryAdd(stableId, item))
                    items.Add(new ImportPreviewItem(entityKind, stableId.ToString("D"), ImportOperation.Error,
                        [new ImportIssue("INTELLIGENCE_CANONICAL_RESOURCE_ID_AMBIGUOUS", $"Canonical {entityKind} identity '{stableId:D}' is ambiguous before import.", entityKind, stableId.ToString("D"), true)]));
            }
        }

        var incomingById = incoming
            .Select(item => (Item: item, Id: identity(item)))
            .Where(entry => entry.Id is { } id && id != Guid.Empty)
            .GroupBy(entry => entry.Id!.Value)
            .ToArray();
        foreach (var group in incomingById)
        {
            if (group.Count() > 1)
            {
                items.Add(new ImportPreviewItem(entityKind, group.Key.ToString("D"), ImportOperation.Error,
                    [new ImportIssue("INTELLIGENCE_IMPORTED_RESOURCE_ID_AMBIGUOUS", $"Imported {entityKind} identity '{group.Key:D}' is duplicated.", entityKind, group.Key.ToString("D"), true)]));
                continue;
            }

            var exists = result.ContainsKey(group.Key);
            var operation = mode switch
            {
                ImportMode.CreateOnly => exists ? ImportOperation.Skip : ImportOperation.Create,
                ImportMode.UpdateExisting => exists ? ImportOperation.Update : ImportOperation.Skip,
                ImportMode.CreateAndUpdate => exists ? ImportOperation.Update : ImportOperation.Create,
                _ => ImportOperation.Error
            };
            if (operation is ImportOperation.Create or ImportOperation.Update)
                result[group.Key] = group.Single().Item;
        }
        return result.Values.ToArray();
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

                if (action.Kind == VisualNavigationActionKind.ExecuteRichCommand)
                {
                    if (kind is not (ImportEntityKind.Screen or ImportEntityKind.Popup))
                        issues.Add(new ImportIssue(
                            "VISUAL_RICH_COMMAND_ACTION_KIND_UNSUPPORTED",
                            "ExecuteRichCommand actions are supported only by Screen and Popup visual definitions.",
                            kind,
                            entityKey,
                            true));

                    if (!action.CommandId.HasValue || action.CommandId == Guid.Empty)
                    {
                        issues.Add(new ImportIssue(
                            "VISUAL_RICH_COMMAND_ID_REQUIRED",
                            $"ExecuteRichCommand action '{action.EventKey}' requires a stable Rich Command identity.",
                            kind,
                            entityKey,
                            true));
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(action.TargetKey))
                        issues.Add(new ImportIssue(
                            "VISUAL_RICH_COMMAND_TARGET_NOT_ALLOWED",
                            $"ExecuteRichCommand action '{action.EventKey}' cannot declare a navigation target key.",
                            kind,
                            entityKey,
                            true));

                    if (action.Parameters is not null)
                        issues.Add(new ImportIssue(
                            "VISUAL_RICH_COMMAND_PARAMETERS_NOT_ALLOWED",
                            $"ExecuteRichCommand action '{action.EventKey}' cannot persist parameter values.",
                            kind,
                            entityKey,
                            true));

                    if (!string.IsNullOrWhiteSpace(action.CommandParameterKey))
                        issues.Add(new ImportIssue(
                            "VISUAL_RICH_COMMAND_PARAMETER_KEY_NOT_ALLOWED",
                            $"ExecuteRichCommand action '{action.EventKey}' cannot use Dynamo Command indirection.",
                            kind,
                            entityKey,
                            true));

                    var commandId = action.CommandId.Value;
                    var commandExists = _driverInteractions.FindRichCommandDefinition(commandId) is not null ||
                        (package.RichCommandDefinitions ?? Array.Empty<RichCommandDefinitionEngineeringDto>())
                            .Any(command => command is not null && command.CommandId == commandId);
                    if (!commandExists)
                        issues.Add(new ImportIssue(
                            "VISUAL_RICH_COMMAND_NOT_FOUND",
                            $"ExecuteRichCommand action '{action.EventKey}' references Rich Command identity '{commandId:D}', which was not found in the prospective Engineering model.",
                            kind,
                            entityKey,
                            true));
                }
                else if (action.Kind == VisualNavigationActionKind.ExecuteCommand)
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
