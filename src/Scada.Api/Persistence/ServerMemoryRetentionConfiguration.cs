using Microsoft.Extensions.DependencyInjection.Extensions;
using Scada.Core.Persistence;
using Scada.Core.InternalMemory;
using Scada.Persistence.PostgreSql;

namespace Scada.Api.Persistence;

public static class ServerMemoryRetentionConfiguration
{
    public static void AddConfiguredServerMemoryRetention(this WebApplicationBuilder builder, DatabaseRuntimeConnectionSet? database = null)
    {
        var connectionString = database?.PrimaryConnectionString ?? builder.Configuration.GetConnectionString("EliteScada");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            builder.Services.TryAddSingleton<IServerMemoryRetentionStore, InMemoryServerMemoryRetentionStore>();
            return;
        }

        builder.Services.TryAddSingleton<IServerMemoryRetentionStore>(sp =>
            new PostgreSqlServerMemoryRetentionStore(
                connectionString,
                sp.GetService<IDurableWriteAdmission>()));
    }

    public static async Task InitializeServerMemoryRetentionAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        var store = app.Services.GetRequiredService<IServerMemoryRetentionStore>();
        if (store is PostgreSqlServerMemoryRetentionStore postgreSql)
            await postgreSql.InitializeAsync(cancellationToken);
    }

    public static string DescribeServerMemoryRetention(this IServiceProvider services) =>
        services.GetRequiredService<IServerMemoryRetentionStore>() is PostgreSqlServerMemoryRetentionStore
            ? "postgresql"
            : "memory";
}
