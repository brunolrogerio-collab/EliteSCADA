using Scada.Api.Security;
using Scada.DriverHost.Engineering;
using Scada.Engineering.Validation;

namespace Scada.Api.Engineering;

public static class EngineeringDriverCatalogApi
{
    public static void AddEngineeringDriverCatalog(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(_ => CommunicationDriverRuntimeComposition.BuildForCurrentSchema());
        builder.Services.AddSingleton<EngineeringDataSourceTypeCatalog>(sp =>
            EngineeringDataSourceTypeCatalog.BuildForCurrentSchema(
                sp.GetRequiredService<CommunicationDriverRuntimeComponentRegistry>()));
        builder.Services.AddSingleton<IDataSourceConfigurationValidator>(sp =>
            sp.GetRequiredService<EngineeringDataSourceTypeCatalog>());
    }

    public static void MapEngineeringDriverCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/engineering/data-source-types", (EngineeringDataSourceTypeCatalog catalog) =>
            Results.Ok(catalog.Describe()))
            .RequireWorkspaceEngineeringRead();
    }
}
