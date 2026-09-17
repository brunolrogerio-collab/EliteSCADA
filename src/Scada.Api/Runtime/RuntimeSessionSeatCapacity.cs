using Scada.Core.Product.Licensing;
using Scada.Security.Authorization;

namespace Scada.Api.Runtime;

/// <summary>Host-owned capacity resolution; it reuses the installed-license verification result.</summary>
public enum RuntimeSessionCapacityReasonCode
{
    DemoCapacity,
    Eslic2SignedEntitlements,
    InvalidInstalledLicense,
    LegacySessionEntitlementUnsupported
}

public sealed record RuntimeSessionSeatCapacityResolution(
    RuntimeSessionSeatCapacity? Capacity,
    RuntimeSessionCapacityReasonCode ReasonCode)
{
    public bool IsAvailable => Capacity is not null;
}

public static class RuntimeSessionSeatCapacityPolicy
{
    public const int DemoInteractiveSeats = 2;
    public const int DemoViewOnlySeats = 2;

    public static RuntimeSessionSeatCapacityResolution Resolve(LicenseVerificationResult verification)
    {
        ArgumentNullException.ThrowIfNull(verification);
        return verification.State switch
        {
            LicenseState.Demo => new RuntimeSessionSeatCapacityResolution(
                new RuntimeSessionSeatCapacity(DemoInteractiveSeats, DemoViewOnlySeats),
                RuntimeSessionCapacityReasonCode.DemoCapacity),
            LicenseState.Invalid => new RuntimeSessionSeatCapacityResolution(
                null,
                RuntimeSessionCapacityReasonCode.InvalidInstalledLicense),
            LicenseState.Valid when verification.SessionEntitlements is { } entitlements => new RuntimeSessionSeatCapacityResolution(
                new RuntimeSessionSeatCapacity(entitlements.InteractiveSeats, entitlements.ViewOnlySeats),
                RuntimeSessionCapacityReasonCode.Eslic2SignedEntitlements),
            LicenseState.Valid => new RuntimeSessionSeatCapacityResolution(
                null,
                RuntimeSessionCapacityReasonCode.LegacySessionEntitlementUnsupported),
            _ => throw new ArgumentOutOfRangeException(nameof(verification))
        };
    }
}
