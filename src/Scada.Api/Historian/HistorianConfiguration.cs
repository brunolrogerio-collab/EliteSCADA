using Scada.Api.Persistence;
using Scada.Core.Persistence;
using Scada.Core.Abstractions;
using Scada.Historian.Abstractions;
using Scada.Historian.Archives;
using Scada.Historian.Memory;
using Scada.Historian.TimescaleDb;
using Scada.Historian.TimescaleDb.Archives;
using Scada.Api.HostedServices;

namespace Scada.Api.Historian;

public static class HistorianConfiguration
{
    public const string MemoryProvider = "memory";
    public const string TimescaleDbProvider = "timescaledb";

    public static void AddConfiguredHistorian(this WebApplicationBuilder builder, DatabaseRuntimeConnectionSet? database = null)
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
                var connectionString = database?.HistorianConnectionString
                    ?? builder.Configuration.GetConnectionString("Historian")
                    ?? builder.Configuration.GetConnectionString("EliteScada");
                if (string.IsNullOrWhiteSpace(connectionString))
                    throw new InvalidOperationException(
                        "TimescaleDB historian requires ConnectionStrings:Historian or ConnectionStrings:EliteScada.");

                builder.Services.AddSingleton<IHistorian>(sp =>
                    new TimescaleDbHistorian(
                        sp.GetRequiredService<IScadaEventBus>(),
                        connectionString,
                        writeAdmission: sp.GetService<IDurableWriteAdmission>()));
                builder.Services.AddSingleton<IHistorianRetentionDownsamplingStore>(sp =>
                    new TimescaleDbHistorianRetentionDownsamplingStore(
                        connectionString,
                        sp.GetService<IDurableWriteAdmission>()));
                var archiveRoot = builder.Configuration["Historian:Archive:RootPath"];
                if (string.IsNullOrWhiteSpace(archiveRoot))
                {
                    if (OperatingSystem.IsLinux())
                    {
                        archiveRoot = "/var/lib/scadivo/historian-archives";
                    }
                    else
                    {
                        var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                        archiveRoot = !string.IsNullOrWhiteSpace(commonData)
                            ? Path.Combine(commonData, "SCADIVO", "Historian", "Archives")
                            : Path.Combine(AppContext.BaseDirectory, "data", "historian-archives");
                    }
                }
                builder.Services.AddSingleton<IHistorianArchiveService>(sp =>
                    new TimescaleDbHistorianArchiveService(
                        connectionString,
                        archiveRoot,
                        sp.GetService<IDurableWriteAdmission>()));
                builder.Services.AddHostedService<HistorianRawSizeLimitHostedService>();
                builder.Services.AddHostedService<HistorianArchiveHostedService>();
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
