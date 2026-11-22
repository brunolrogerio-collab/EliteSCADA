namespace Scada.Api.Licensing;

/// <summary>
/// Waits for canonical Runtime/Authority/HA readiness before attempting the
/// single startup recovery decision. This avoids consuming recovery while the
/// API's hosted HA services are still converging.
/// </summary>
public sealed class AssetProcessIntelligenceStartupRecoveryHostedService(
    AssetProcessIntelligenceStartupRecovery recovery,
    ILogger<AssetProcessIntelligenceStartupRecoveryHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await recovery.RecoverAsync(stoppingToken) is not null)
                    return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Intelligence startup recovery is waiting for its canonical Authority and HA state; execution remains closed.");
            }

            await Task.Delay(RetryInterval, stoppingToken);
        }
    }
}
