using System.Text.Json;
using System.Text.Json.Serialization;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.ImportExport;

public enum EngineeringTabularEntity
{
    Tags,
    Alarms,
    DataSources,
    GatewayRoutes,
    OperationalEvents
}

/// <summary>
/// Narrow tabular projection over the canonical Engineering package. Parsing only
/// replaces one flat entity collection; preview/apply remain owned by the exchange
/// service and its normal validation, merge, audit, and workspace-version lifecycle.
/// </summary>
public sealed class EngineeringTabularExchange
{
    private readonly EngineeringCsvExchange _csv;

    public EngineeringTabularExchange()
    {
        var json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
        };
        _csv = new EngineeringCsvExchange(json);
    }

    public string ExportCsv(EngineeringPackage package, EngineeringTabularEntity entity) => entity switch
    {
        EngineeringTabularEntity.Tags => _csv.ExportTags(package.Tags),
        EngineeringTabularEntity.Alarms => _csv.ExportAlarms(package.Alarms),
        EngineeringTabularEntity.DataSources => _csv.ExportDataSources(package.DataSources ?? []),
        EngineeringTabularEntity.GatewayRoutes => _csv.ExportGatewayRoutes(package.Gateways ?? []),
        EngineeringTabularEntity.OperationalEvents => _csv.ExportOperationalEvents(package.OperationalEvents ?? []),
        _ => throw new ArgumentOutOfRangeException(nameof(entity))
    };

    public byte[] ExportXlsx(EngineeringPackage package, EngineeringTabularEntity entity)
    {
        var rows = CsvCodec.Read(ExportCsv(package, entity));
        return XlsxCodec.Write(rows, SheetName(entity));
    }

    public EngineeringPackage ParseCsv(EngineeringPackage current, EngineeringTabularEntity entity, string csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) throw new InvalidDataException("CSV import requires a header row.");
        return ReplaceTabularEntities(current, entity, ParseRows(entity, csv));
    }

    public EngineeringPackage ParseXlsx(EngineeringPackage current, EngineeringTabularEntity entity, ReadOnlyMemory<byte> xlsx)
    {
        var rows = XlsxCodec.Read(xlsx, SheetName(entity));
        if (rows.Count == 0) throw new InvalidDataException("XLSX import requires a header row.");
        var csv = CsvCodec.Write(rows.Select(row => (IReadOnlyList<string?>)row));
        return ReplaceTabularEntities(current, entity, ParseRows(entity, csv));
    }

    public static bool TryParseEntity(string? value, out EngineeringTabularEntity entity)
    {
        entity = value?.Trim().ToLowerInvariant() switch
        {
            "tags" or "tag" => EngineeringTabularEntity.Tags,
            "alarms" or "alarm" => EngineeringTabularEntity.Alarms,
            "datasources" or "data-sources" => EngineeringTabularEntity.DataSources,
            "gateway-routes" or "gateways" => EngineeringTabularEntity.GatewayRoutes,
            "operational-events" or "events" => EngineeringTabularEntity.OperationalEvents,
            _ => (EngineeringTabularEntity)(-1)
        };
        return Enum.IsDefined(entity);
    }

    public static string FileStem(EngineeringTabularEntity entity) => entity switch
    {
        EngineeringTabularEntity.Tags => "tags",
        EngineeringTabularEntity.Alarms => "alarms",
        EngineeringTabularEntity.DataSources => "datasources",
        EngineeringTabularEntity.GatewayRoutes => "gateway-routes",
        EngineeringTabularEntity.OperationalEvents => "operational-events",
        _ => throw new ArgumentOutOfRangeException(nameof(entity))
    };

    private static string SheetName(EngineeringTabularEntity entity) => entity switch
    {
        EngineeringTabularEntity.Tags => "Tags",
        EngineeringTabularEntity.Alarms => "Alarms",
        EngineeringTabularEntity.DataSources => "DataSources",
        EngineeringTabularEntity.GatewayRoutes => "GatewayRoutes",
        EngineeringTabularEntity.OperationalEvents => "OperationalEvents",
        _ => throw new ArgumentOutOfRangeException(nameof(entity))
    };

    private EngineeringPackage ParseRows(EngineeringTabularEntity entity, string csv) => entity switch
    {
        EngineeringTabularEntity.Tags => new EngineeringPackage(EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion, DateTimeOffset.UtcNow, _csv.ParseTags(csv), []),
        EngineeringTabularEntity.Alarms => new EngineeringPackage(EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion, DateTimeOffset.UtcNow, [], _csv.ParseAlarms(csv)),
        EngineeringTabularEntity.DataSources => new EngineeringPackage(EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion, DateTimeOffset.UtcNow, [], [],
            DataSources: _csv.ParseDataSources(csv)),
        EngineeringTabularEntity.GatewayRoutes => new EngineeringPackage(EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion, DateTimeOffset.UtcNow, [], [],
            Gateways: _csv.ParseGatewayRoutes(csv)),
        EngineeringTabularEntity.OperationalEvents => new EngineeringPackage(EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion, DateTimeOffset.UtcNow, [], [],
            OperationalEvents: _csv.ParseOperationalEvents(csv)),
        _ => throw new ArgumentOutOfRangeException(nameof(entity))
    };

    private static EngineeringPackage ReplaceTabularEntities(
        EngineeringPackage current,
        EngineeringTabularEntity entity,
        EngineeringPackage parsed)
    {
        ArgumentNullException.ThrowIfNull(current);
        return new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            EngineeringExchangeService.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            entity == EngineeringTabularEntity.Tags ? parsed.Tags : [],
            entity == EngineeringTabularEntity.Alarms ? parsed.Alarms : [],
            DataSources: entity == EngineeringTabularEntity.DataSources ? parsed.DataSources : [],
            Templates: [], Equipment: [], Dynamos: [], Screens: [], Popups: [], SecurityRoles: [], Commands: [],
            Gateways: entity == EngineeringTabularEntity.GatewayRoutes ? parsed.Gateways : [],
            Scripts: [], ScriptVisualEventReferences: [], VisualAssets: [], Reports: [],
            OperationalEvents: entity == EngineeringTabularEntity.OperationalEvents ? parsed.OperationalEvents : [],
            StartupScreenId: null,
            EngineeringLock: current.EngineeringLock,
            SecurityScopes: [],
            AuthorityPolicyReference: current.AuthorityPolicyReference,
            HistorianCaptureProfiles: [], DataQueries: [], AlarmViews: [], Branding: null,
            RuntimePresentation: current.RuntimePresentation ?? new RuntimePresentationEngineeringDto(),
            MediaSources: [], Locations: [], TransientEventDefinitions: [], CapabilityEventReferences: [],
            RichCommandDefinitions: [], DriverCommandBindings: []);
    }
}
