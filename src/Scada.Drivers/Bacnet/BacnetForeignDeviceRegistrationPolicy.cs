namespace Scada.Drivers.Bacnet;

/// <summary>
/// Protocol-local deterministic scheduling policy for BACnet/IP Foreign Device
/// Registration (FDR). It deliberately models request intent separately from
/// BBMD acceptance because the selected BACnet stack does not expose positive
/// registration acknowledgement evidence through this adapter seam.
/// </summary>
public static class BacnetForeignDeviceRegistrationPolicy
{
    public static BacnetForeignDeviceRegistrationAttempt Initial(BacnetSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (!options.ForeignDeviceTtlSeconds.HasValue)
            throw new InvalidOperationException("BACnet Foreign Device Registration is not configured.");

        return new BacnetForeignDeviceRegistrationAttempt(
            BacnetForeignDeviceRegistrationAttemptKind.Initial,
            TimeSpan.Zero);
    }

    public static BacnetForeignDeviceRegistrationAttempt AfterSuccess(BacnetSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return new BacnetForeignDeviceRegistrationAttempt(
            BacnetForeignDeviceRegistrationAttemptKind.Renewal,
            options.EffectiveForeignDeviceRenewalInterval
                ?? throw new InvalidOperationException("BACnet Foreign Device Registration is not configured."));
    }

    public static BacnetForeignDeviceRegistrationAttempt AfterFailure(BacnetSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return new BacnetForeignDeviceRegistrationAttempt(
            BacnetForeignDeviceRegistrationAttemptKind.Retry,
            options.EffectiveForeignDeviceRetryInterval
                ?? throw new InvalidOperationException("BACnet Foreign Device Registration is not configured."));
    }

    /// <summary>
    /// Executes one synchronous stack registration request and converts the local
    /// outcome into the next bounded scheduling decision. A transport/parse
    /// failure is lease reachability evidence, not a process-fatal startup error.
    /// The supplied request remains responsible for recording detailed diagnostics.
    /// </summary>
    public static BacnetForeignDeviceRegistrationAttempt ExecuteAndScheduleNext(
        BacnetSessionOptions options,
        Action registrationRequest)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(registrationRequest);
        try
        {
            registrationRequest();
            return AfterSuccess(options);
        }
        catch
        {
            return AfterFailure(options);
        }
    }
}

public enum BacnetForeignDeviceRegistrationAttemptKind
{
    Initial = 0,
    Renewal = 1,
    Retry = 2
}

public sealed record BacnetForeignDeviceRegistrationAttempt(
    BacnetForeignDeviceRegistrationAttemptKind Kind,
    TimeSpan Delay);