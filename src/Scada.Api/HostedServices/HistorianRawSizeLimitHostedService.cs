using Scada.Historian.Abstractions;
using Scada.Historian.Policies;
using Scada.Security.Audit;

namespace Scada.Api.HostedServices;

public sealed class HistorianRawSizeLimitHostedService(
    IHistorianRetentionDownsamplingStore store,
    IAuditSink audit,
    ILogger<HistorianRawSizeLimitHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var result = await store.EnforceRawSampleStorageLimitAsync(stoppingToken);
                if (result.Status == HistorianStorageLimitEnforcementStatus.ChunksRemoved)
                {
                    logger.LogWarning(
                        "Historian raw storage limit enforcement removed {DeletedChunkCount} complete chunk(s); raw usage changed from {RawBytesBefore} to {RawBytesAfter} bytes against a {LimitBytes}-byte target.",
                        result.DeletedChunkCount,
                        result.RawBytesBefore,
                        result.RawBytesAfter,
                        result.LimitBytes);
                    await WriteDeletionAuditAsync(result, stoppingToken);
                }
                else if (result.IsOverLimit)
                {
                    logger.LogWarning(
                        "Historian raw sample storage is over its configured target ({RawBytesAfter}/{LimitBytes} bytes); no raw chunk was removed during this maintenance pass.",
                        result.RawBytesAfter,
                        result.LimitBytes);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Keep connection strings and provider diagnostics out of routine logs.
                logger.LogError(
                    "Historian raw size-limit maintenance failed with exception type {ExceptionType}; it will retry on the next interval.",
                    ex.GetType().Name);
            }
        }
    }

    private async Task WriteDeletionAuditAsync(
        HistorianStorageLimitEnforcementResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            await audit.WriteAsync(
                AuditEvent.Create(
                    "host:historian-size-limit-maintenance",
                    "Historian storage maintenance",
                    AuditActions.HistorianStorageLimitEnforce,
                    AuditOutcome.Succeeded,
                    "historian-hypertable",
                    "elitescada.tag_history",
                    new Dictionary<string, string>
                    {
                        ["deletedChunkCount"] = result.DeletedChunkCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["limitBytes"] = result.LimitBytes?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown",
                        ["rawBytesBefore"] = result.RawBytesBefore?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown",
                        ["rawBytesAfter"] = result.RawBytesAfter?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"
                    },
                    source: "historian-size-limit-worker"),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The database operation has committed; preserve its outcome and surface the audit failure.
            logger.LogError(
                "Historian size-limit deletion audit could not be persisted; exception type {ExceptionType}.",
                ex.GetType().Name);
        }
    }
}
