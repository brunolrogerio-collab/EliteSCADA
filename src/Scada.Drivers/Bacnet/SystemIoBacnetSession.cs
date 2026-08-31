namespace Scada.Drivers.Bacnet;

public sealed partial class SystemIoBacnetSession
{
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0) return;

        if (_foreignDeviceRenewalCts is not null)
        {
            await _foreignDeviceRenewalCts.CancelAsync().ConfigureAwait(false);
            if (_foreignDeviceRenewalTask is not null)
            {
                try { await _foreignDeviceRenewalTask.ConfigureAwait(false); }
                catch (OperationCanceledException) when (_foreignDeviceRenewalCts.IsCancellationRequested) { }
            }
        }

        await CancelAllCovRoutesAsync().ConfigureAwait(false);
        _started = false;
        _disposed = true;
        _client.OnIam -= OnIam;
        _client.OnCOVNotification -= OnCovNotification;
        _client.Dispose();
        _foreignDeviceRenewalCts?.Dispose();
    }

    private bool IsForeignDeviceRegistrationConfigured()
        => !string.IsNullOrWhiteSpace(_options.BbmdAddress) && _options.ForeignDeviceTtlSeconds.HasValue;

    private void SendForeignDeviceRegistration()
    {
        var requestedAt = DateTimeOffset.UtcNow;
        try
        {
            _client.RegisterAsForeignDevice(
                _options.BbmdAddress!,
                checked((short)_options.ForeignDeviceTtlSeconds!.Value));
            lock (_foreignDeviceGate)
            {
                _lastForeignDeviceRegistrationRequestAt = requestedAt;
                _foreignDeviceRegistrationRequestsSent++;
                _foreignDeviceRegistrationLastErrorType = null;
            }
        }
        catch (Exception ex)
        {
            lock (_foreignDeviceGate)
            {
                _lastForeignDeviceRegistrationRequestAt = requestedAt;
                _foreignDeviceRegistrationFailures++;
                _foreignDeviceRegistrationLastErrorType = ex.GetType().Name;
            }
            throw;
        }
    }

    private async Task RenewForeignDeviceRegistrationAsync(
        BacnetForeignDeviceRegistrationAttempt nextAttempt,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(nextAttempt.Delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            nextAttempt = BacnetForeignDeviceRegistrationPolicy.ExecuteAndScheduleNext(
                _options,
                SendForeignDeviceRegistration);
            SetNextForeignDeviceRegistrationAttempt(DateTimeOffset.UtcNow + nextAttempt.Delay);
        }
    }

    private void SetNextForeignDeviceRegistrationAttempt(DateTimeOffset nextAttemptAt)
    {
        lock (_foreignDeviceGate)
            _nextForeignDeviceRegistrationAttemptAt = nextAttemptAt;
    }
}
