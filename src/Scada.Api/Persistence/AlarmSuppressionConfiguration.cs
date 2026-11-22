using Microsoft.Extensions.DependencyInjection.Extensions;
using Scada.Core.Alarms;
using Scada.Persistence.PostgreSql;

namespace Scada.Api.Persistence;

public static class AlarmSuppressionConfiguration
{
    public static void AddConfiguredAlarmSuppression(this WebApplicationBuilder builder, DatabaseRuntimeConnectionSet? database = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var connectionString = database?.PrimaryConnectionString ?? builder.Configuration.GetConnectionString("EliteScada");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            builder.Services.TryAddSingleton<IAlarmSuppressionStore, UnavailableAlarmSuppressionStore>();
            return;
        }

        builder.Services.TryAddSingleton<IAlarmSuppressionStore>(sp =>
            new PostgreSqlAlarmSuppressionStore(
                connectionString,
                sp.GetService<Scada.Core.Persistence.IDurableWriteAdmission>()));
    }

    public static async Task InitializeAlarmSuppressionAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        if (app.Services.GetService<IAlarmSuppressionStore>() is PostgreSqlAlarmSuppressionStore store)
            await store.EnsureInitializedAsync(cancellationToken);
    }
}
