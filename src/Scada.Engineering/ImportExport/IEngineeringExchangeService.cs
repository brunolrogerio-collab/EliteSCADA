using Scada.Engineering.Contracts;

namespace Scada.Engineering.ImportExport;

public interface IEngineeringExchangeService
{
    EngineeringPackage ExportPackage();
    string ExportJson(bool indented = true);
    string ExportTagsCsv();
    string ExportAlarmsCsv();
    string ExportDataSourcesCsv();

    EngineeringPackage ParseJson(string json);
    EngineeringPackage ParseTagsCsv(string csv);
    EngineeringPackage ParseAlarmsCsv(string csv);
    EngineeringPackage ParseDataSourcesCsv(string csv);

    ImportPreview Preview(EngineeringPackage package, ImportMode mode);
    ImportResult Apply(EngineeringPackage package, ImportMode mode);
}
