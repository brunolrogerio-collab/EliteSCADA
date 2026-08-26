using Scada.Security.Audit;

namespace Scada.Api.HostedServices;

public sealed class AuditRetentionHostedService : BackgroundService
{
    private readonly AuditRetentionCoordinator _coordinator;
    private readonly AuditRetentionPolicy _policy;
    private readonly ILogger<AuditRetentionHostedService> _logger;

    public AuditRetentionHostedService(
        AuditRetentionCoordinator coordinator,
        AuditRetentionPolicy policy,
        ILogger<AuditRetentionHostedService> logger)
    {
        _coordinator = coordinator;
        _policy = policy;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_policy.Enabled || !_policy.MaximumAge.HasValue || !_policy.Interval.HasValue)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _coordinator.RunOnceAsync(DateTimeOffset.UtcNow, stoppingToken);
                if (result.Executed && result.DeletedCount > 0)
                {
                    _logger.LogInformation(
                        "Audit retention deleted {DeletedCount} events in {BatchCount} batches. Backlog may remain: {BacklogMayRemain}.",
                        result.DeletedCount,
                        result.BatchCount,
                        result.BacklogMayRemain);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Do not surface arbitrary storage exception text, which may contain environment details.
                _logger.LogError(
                    "Audit retention run failed with exception type {ExceptionType}; the hosted service will retry on the next interval.",
                    ex.GetType().Name);
            }

            try
            {
                await Task.Delay(_policy.Interval.Value, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
