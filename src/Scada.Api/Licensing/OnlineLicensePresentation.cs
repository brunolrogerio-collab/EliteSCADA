using Scada.Core.Product.Licensing;

namespace Scada.Api.Licensing;

// Display metadata is not an entitlement. A confirmed lease remains the only source of rights.
internal sealed record OnlineLicensePresentation(
    Guid LicenseId, Guid OrganizationId, Guid ApplicationId, string LicenseCode,
    string ApplicationName, string Mode, string Term, DateTimeOffset? EntitlementExpiresAtUtc,
    string Tier, int InteractiveSeats, int ViewOnlySeats, bool HaRuntime,
    bool AssetProcessIntelligence, int MaxActivations)
{
    internal bool IsWellFormed() =>
        LicenseId != Guid.Empty && OrganizationId != Guid.Empty && ApplicationId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(LicenseCode) && LicenseCode.Length <= 128 &&
        !string.IsNullOrWhiteSpace(ApplicationName) && ApplicationName.Length <= 256 &&
        Mode == "online-account" && (Term is "perpetual" or "monthly" or "annual") &&
        Enum.TryParse<LicenseTier>(Tier, false, out var tier) && Enum.IsDefined(tier) && tier.ToString() == Tier &&
        InteractiveSeats >= 0 && ViewOnlySeats >= 0 && MaxActivations > 0 &&
        (Term == "perpetual" ? EntitlementExpiresAtUtc is null : EntitlementExpiresAtUtc is not null);

    internal bool MatchesVerifiedLease(OnlineLicensePresentation confirmed,
        OnlineLeaseVerificationResult verification, DateTimeOffset responseNotAfterUtc)
    {
        if (confirmed != this || verification is not { Status: OnlineLeaseVerificationStatus.Valid, Claims: { } claims })
            return false;
        if (!IsWellFormed()) return false;

        return LicenseId == claims.LicenseId && OrganizationId == claims.OrganizationId &&
            ApplicationId == claims.ApplicationId && Tier == claims.Tier.ToString() &&
            InteractiveSeats == claims.InteractiveSeats && ViewOnlySeats == claims.ViewOnlySeats &&
            HaRuntime == claims.HaRuntime && AssetProcessIntelligence == claims.AssetProcessIntelligence &&
            responseNotAfterUtc == claims.NotAfterUtc &&
            (EntitlementExpiresAtUtc is null || claims.NotAfterUtc <= EntitlementExpiresAtUtc);
    }
}
