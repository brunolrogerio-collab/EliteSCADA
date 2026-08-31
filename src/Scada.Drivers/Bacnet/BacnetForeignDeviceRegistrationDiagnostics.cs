namespace Scada.Drivers.Bacnet;

/// <summary>
/// Protocol-local breakdown of BACnet/IP Foreign Device Registration activity.
/// Attempt counters include every local stack invocation, including calls that
/// fail before a registration request can be handed off successfully. Failure
/// counters are a subset of the matching attempt counters.
/// </summary>
public sealed record BacnetForeignDeviceRegistrationBreakdownSnapshot(
    BacnetForeignDeviceRegistrationAttemptKind LastAttemptKind,
    long InitialAttempts,
    long InitialFailures,
    long RenewalAttempts,
    long RenewalFailures,
    long RetryAttempts,
    long RetryFailures)
{
    public long TotalAttempts => InitialAttempts + RenewalAttempts + RetryAttempts;
    public long TotalFailures => InitialFailures + RenewalFailures + RetryFailures;
}

/// <summary>
/// Optional BACnet-specific diagnostic seam for distinguishing the initial FDR
/// request from normal lease renewal traffic and bounded retry traffic. It stays
/// outside shared driver contracts because these categories are BACnet-specific.
/// </summary>
public interface IBacnetForeignDeviceRegistrationBreakdownDiagnostics
{
    BacnetForeignDeviceRegistrationBreakdownSnapshot GetForeignDeviceRegistrationBreakdownDiagnostics();
}
