using System.Security.Cryptography;
using Scada.Api.Runtime;
using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.HistoricalQueries;
using Scada.Historian.TimescaleDb;
using Scada.Persistence.PostgreSql;

namespace Scada.Api.Historian;

public static class HistoricalQueryConfiguration
{
    public const string EnabledKey = "HistoricalQuery:Enabled";
    public const string CursorKeyBase64Key = "HistoricalQuery:CursorKeyBase64";

    public static bool AddConfiguredHistoricalQuery(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Configuration.GetValue<bool>(EnabledKey))
            return false;

        var historianProvider = (builder.Configuration["Historian:Provider"] ?? HistorianConfiguration.MemoryProvider)
            .Trim()
            .ToLowerInvariant();
        if (!string.Equals(historianProvider, HistorianConfiguration.TimescaleDbProvider, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Historical Query requires Historian:Provider=timescaledb so historian.samples has a durable query provider.");

        var connectionString = builder.Configuration.GetConnectionString("Historian")
            ?? builder.Configuration.GetConnectionString("EliteScada");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Historical Query requires ConnectionStrings:Historian or ConnectionStrings:EliteScada.");

        var encodedCursorKey = builder.Configuration[CursorKeyBase64Key];
        if (string.IsNullOrWhiteSpace(encodedCursorKey))
            throw new InvalidOperationException(
                "Historical Query requires an external HistoricalQuery:CursorKeyBase64 secret.");

        byte[] cursorKey;
        try
        {
            cursorKey = Convert.FromBase64String(encodedCursorKey.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "HistoricalQuery:CursorKeyBase64 must be valid Base64.",
                ex);
        }

        try
        {
            builder.Services.AddSingleton(new HistoricalQueryCursorCodec(cursorKey));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(cursorKey);
        }

        builder.Services.AddSingleton<RuntimeTagRegistryView>();
        builder.Services.AddSingleton<TimescaleHistoricalQueryProvider>(sp =>
            new TimescaleHistoricalQueryProvider(
                connectionString,
                sp.GetRequiredService<RuntimeTagRegistryView>()));
        builder.Services.AddSingleton<IHistoricalDatasetProvider>(sp =>
            sp.GetRequiredService<TimescaleHistoricalQueryProvider>());

        builder.Services.AddSingleton<PostgreSqlAlarmHistoryStore>(_ =>
            new PostgreSqlAlarmHistoryStore(connectionString));
        builder.Services.AddSingleton<IHistoricalDatasetProvider>(sp =>
            sp.GetRequiredService<PostgreSqlAlarmHistoryStore>());

        builder.AddHistoricalQueryApiCore();
        builder.Services.AddHostedService<AlarmHistoryPersistenceHostedService>();
        return true;
    }
}

internal sealed class AlarmHistoryPersistenceHostedService(
    IScadaEventBus eventBus,
    PostgreSqlAlarmHistoryStore store,
    RuntimeTagRegistryView tags,
    ILogger<AlarmHistoryPersistenceHostedService> logger) : IHostedService, IDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _subscription = eventBus.Subscribe<AlarmStateChanged>(PersistAsync);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _subscription = null;
        _stopping.Cancel();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _stopping.Cancel();
        _stopping.Dispose();
    }

    private async ValueTask PersistAsync(AlarmStateChanged stateChanged)
    {
        try
        {
            if (!tags.TryGet(stateChanged.Current.TagId, out var tag) || tag is null)
            {
                logger.LogWarning(
                    "Alarm history skipped event for alarm {AlarmId}: active Runtime TAG {TagId} was not available.",
                    stateChanged.Current.DefinitionId,
                    stateChanged.Current.TagId);
                return;
            }

            await store.AppendAsync(stateChanged, tag.Path, _stopping.Token);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Alarm history persistence failed for alarm {AlarmId} and TAG {TagId}; operational alarm processing remains authoritative.",
                stateChanged.Current.DefinitionId,
                stateChanged.Current.TagId);
        }
    }
}
