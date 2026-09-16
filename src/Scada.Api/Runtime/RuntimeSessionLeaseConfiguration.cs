using Microsoft.Extensions.DependencyInjection.Extensions;
using Scada.Persistence.PostgreSql;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

public static class RuntimeSessionLeaseConfiguration
{
    public static void AddConfiguredRuntimeSessionLeaseStore(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("EliteScada");
        builder.Services.TryAddSingleton<IRuntimeSessionLeaseStore>(_ =>
            string.IsNullOrWhiteSpace(connectionString)
                ? new InMemoryRuntimeSessionLeaseStore()
                : new PostgreSqlRuntimeSessionLeaseStore(connectionString));
    }

    public static Task InitializeRuntimeSessionLeaseStoreAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default) =>
        app.Services.GetRequiredService<IRuntimeSessionLeaseStore>().InitializeAsync(cancellationToken);
}
