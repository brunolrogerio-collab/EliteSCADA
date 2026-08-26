using Scada.Core.Abstractions;
using Scada.Historian.Abstractions;
using Scada.Historian.Memory;
using Scada.Historian.TimescaleDb;

namespace Scada.Api.Historian;

public static class HistorianConfiguration
{
    public const string MemoryProvider = "memory";
    public const string TimescaleDbProvider = "timescaledb";

    public static void AddConfiguredHistorian(this WebApplicationBuilder builder)
    {
        var provider = (builder.Configuration["Historian:Provider"] ?? MemoryProvider)
            .Trim()
            .ToLowerInvariant();

        switch (provider)
        {
            case MemoryProvider:
                builder.Services.AddSingleton<IHistorian, BufferedInMemoryHistorian>();
                break;

            case TimescaleDbProvider:
                var connectionString = builder.Configuration.GetConnectionString("Historian")
                    ?? builder.Configuration.GetConnectionString("EliteScada");
                if (string.IsNullOrWhiteSpace(connectionString))
                    throw new InvalidOperationException(
                        "TimescaleDB historian requires ConnectionStrings:Historian or ConnectionStrings:EliteScada.");

                builder.Services.AddSingleton<IHistorian>(sp =>
                    new TimescaleDbHistorian(
                        sp.GetRequiredService<IScadaEventBus>(),
                        connectionString));
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported historian provider '{provider}'. Supported providers: '{MemoryProvider}', '{TimescaleDbProvider}'.");
        }
    }

    public static string DescribeProvider(IHistorian historian) => historian switch
    {
        TimescaleDbHistorian => TimescaleDbProvider,
        BufferedInMemoryHistorian => MemoryProvider,
        _ => historian.GetType().Name
    };
}
