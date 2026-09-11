using Scada.Engineering.Contracts;

namespace Scada.Engineering.Validation;

/// <summary>
/// Host-provided authoritative validation for Data Source type-specific settings.
/// Engineering owns the import/apply lifecycle while the product host owns the
/// catalog of source/driver types compiled into the current build.
/// </summary>
public interface IDataSourceConfigurationValidator
{
    IReadOnlyCollection<ImportIssue> Validate(DataSourceEngineeringDto dataSource);
}
