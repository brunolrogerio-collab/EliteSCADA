using Microsoft.Extensions.Options;

namespace Scadivo.Portal;

public sealed class PortalPrivacyAutomationOptions
{
    public bool InactivityNoticesEnabled { get; set; }
}

public sealed class PortalPrivacyAutomationWorker(
    IServiceScopeFactory scopes, IOptions<PortalPrivacyAutomationOptions> options,
    IOptions<PortalContactMailboxRetentionOptions> mailboxOptions, PortalEmailSignal emailSignal,
    ILogger<PortalPrivacyAutomationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.InactivityNoticesEnabled && !mailboxOptions.Value.Enabled) return;
        using var daily = new PeriodicTimer(TimeSpan.FromDays(1));
        do
        {
            if (options.Value.InactivityNoticesEnabled)
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var retention = scope.ServiceProvider.GetRequiredService<PortalRetention>();
                    var queued = await retention.QueueInactivityNoticesAsync(stoppingToken);
                    if (queued > 0) emailSignal.Notify();
                    logger.LogInformation("Inactivity notices queued: {Queued}.", queued);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception error) { logger.LogWarning("Inactivity review failed ({ErrorType}).", error.GetType().Name); }
            }
            if (mailboxOptions.Value.Enabled)
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<PortalContactMailboxRetention>().RunAsync(stoppingToken);
                    logger.LogInformation("Closed-contact retention counted {Eligible} and removed {Purged} messages.", result.Eligible, result.Purged);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception error) { logger.LogWarning("Closed-contact retention failed ({ErrorType}).", error.GetType().Name); }
            }
        } while (await daily.WaitForNextTickAsync(stoppingToken));
    }
}
