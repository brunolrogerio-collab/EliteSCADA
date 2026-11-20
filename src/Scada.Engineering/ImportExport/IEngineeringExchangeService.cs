using System.Linq;
using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.VisualAssets;

namespace Scada.Engineering.ImportExport;

public interface IEngineeringExchangeService
{
    EngineeringPackage ExportPackage();
    string ExportJson(bool indented = true);
    string ExportTagsCsv();
    string ExportAlarmsCsv();
    string ExportDataSourcesCsv();

    EngineeringPackage ParseJson(string json);

    /// <summary>
    /// Keeps omitted project settings out of a partial import. Full package parsing still
    /// supplies the defaults used during project restoration.
    /// </summary>
    EngineeringPackage ParseJsonForImport(string json)
    {
        using var document = JsonDocument.Parse(json);
        var package = ParseJson(json);
        var properties = document.RootElement.EnumerateObject();
        var includesRuntimePresentation = properties.Any(property =>
            string.Equals(property.Name, "runtimePresentation", StringComparison.OrdinalIgnoreCase));
        var engineeringLock = properties.LastOrDefault(property =>
            string.Equals(property.Name, "engineeringLock", StringComparison.OrdinalIgnoreCase));
        return package with
        {
            RuntimePresentation = includesRuntimePresentation ? package.RuntimePresentation : null,
            EngineeringLock = engineeringLock.Value.ValueKind == JsonValueKind.Object ? package.EngineeringLock : null
        };
    }

    EngineeringPackage ParseTagsCsv(string csv);
    EngineeringPackage ParseAlarmsCsv(string csv);
    EngineeringPackage ParseDataSourcesCsv(string csv);

    ImportPreview Preview(EngineeringPackage package, ImportMode mode);

    ImportPreview Preview(
        EngineeringPackage package,
        ImportMode mode,
        EngineeringImportContext? context) =>
        Preview(package, mode);

    ImportResult Apply(EngineeringPackage package, ImportMode mode);

    ImportResult Apply(
        EngineeringPackage package,
        ImportMode mode,
        EngineeringImportContext? context) =>
        Apply(package, mode);
}
