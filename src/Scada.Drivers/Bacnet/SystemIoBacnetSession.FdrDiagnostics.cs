namespace Scada.Drivers.Bacnet;

public sealed partial class SystemIoBacnetSession : IBacnetForeignDeviceRegistrationBreakdownDiagnostics
{
    private BacnetForeignDeviceRegistrationAttemptKind _currentForeignDeviceRegistrationAttemptKind =
        BacnetForeignDeviceRegistrationAttemptKind.Initial;
    private BacnetForeignDeviceRegistrationAttemptKind _lastForeignDeviceRegistrationAttemptKind =
        BacnetForeignDeviceRegistrationAttemptKind.Initial;
    private long _foreignDeviceInitialAttempts;
    private long _foreignDeviceInitialFailures;
    private long _foreignDeviceRenewalAttempts;
    private long _foreignDeviceRenewalFailures;
    private long _foreignDeviceRetryAttempts;
    private long _foreignDeviceRetryFailures;

    public BacnetForeignDeviceRegistrationBreakdownSnapshot GetForeignDeviceRegistrationBreakdownDiagnostics()
    {
        lock (_foreignDeviceGate)
        {
            return new BacnetForeignDeviceRegistrationBreakdownSnapshot(
                _lastForeignDeviceRegistrationAttemptKind,
                _foreignDeviceInitialAttempts,
                _foreignDeviceInitialFailures,
                _foreignDeviceRenewalAttempts,
                _foreignDeviceRenewalFailures,
                _foreignDeviceRetryAttempts,
                _foreignDeviceRetryFailures);
        }
    }

    private BacnetForeignDeviceRegistrationAttemptKind GetCurrentForeignDeviceRegistrationAttemptKind()
    {
        lock (_foreignDeviceGate)
            return _currentForeignDeviceRegistrationAttemptKind;
    }

    private void SetCurrentForeignDeviceRegistrationAttemptKind(BacnetForeignDeviceRegistrationAttemptKind kind)
    {
        lock (_foreignDeviceGate)
            _currentForeignDeviceRegistrationAttemptKind = kind;
    }

    private void RecordForeignDeviceRegistrationAttempt(
        BacnetForeignDeviceRegistrationAttemptKind kind,
        DateTimeOffset requestedAt)
    {
        lock (_foreignDeviceGate)
        {
            _lastForeignDeviceRegistrationRequestAt = requestedAt;
            _lastForeignDeviceRegistrationAttemptKind = kind;
            switch (kind)
            {
                case BacnetForeignDeviceRegistrationAttemptKind.Initial:
                    _foreignDeviceInitialAttempts++;
                    break;
                case BacnetForeignDeviceRegistrationAttemptKind.Renewal:
                    _foreignDeviceRenewalAttempts++;
                    break;
                case BacnetForeignDeviceRegistrationAttemptKind.Retry:
                    _foreignDeviceRetryAttempts++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown BACnet FDR attempt kind.");
            }
        }
    }

    private void RecordForeignDeviceRegistrationFailure(
        BacnetForeignDeviceRegistrationAttemptKind kind,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        lock (_foreignDeviceGate)
        {
            _foreignDeviceRegistrationFailures++;
            _foreignDeviceRegistrationLastErrorType = exception.GetType().Name;
            switch (kind)
            {
                case BacnetForeignDeviceRegistrationAttemptKind.Initial:
                    _foreignDeviceInitialFailures++;
                    break;
                case BacnetForeignDeviceRegistrationAttemptKind.Renewal:
                    _foreignDeviceRenewalFailures++;
                    break;
                case BacnetForeignDeviceRegistrationAttemptKind.Retry:
                    _foreignDeviceRetryFailures++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown BACnet FDR attempt kind.");
            }
        }
    }
}
