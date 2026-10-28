using Scada.Api.Licensing;
using Scada.Core.Product.Licensing;

namespace Scada.Drivers.Tests;

public sealed class OnlineLicensePresentationTests
{
    [Fact]
    public void ConfirmationRequiresUnchangedPresentationAndMatchingAuthenticatedRights()
    {
        var now = DateTimeOffset.Parse("2026-10-28T00:00:00Z");
        var summary = new OnlineLicensePresentation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "TEST-CODE", "Test application", "online-account", "annual", now.AddDays(30),
            "Tags500", 3, 4, true, false, 2);
        var claims = new OnlineLeaseClaims(1, summary.LicenseId, summary.OrganizationId, summary.ApplicationId,
            Guid.NewGuid(), new string('a', 64), LicenseTier.Tags500, 4, 3, true, false,
            now, now.AddHours(72), "test-key");
        var valid = new OnlineLeaseVerificationResult(OnlineLeaseVerificationStatus.Valid, claims);

        Assert.True(summary.MatchesVerifiedLease(summary, valid, claims.NotAfterUtc));
        Assert.False(summary.MatchesVerifiedLease(summary with { ApplicationName = "Changed" }, valid, claims.NotAfterUtc));
        Assert.False(summary.MatchesVerifiedLease(summary, valid with { Claims = claims with { InteractiveSeats = 4 } }, claims.NotAfterUtc));
        Assert.False(summary.MatchesVerifiedLease(summary, valid with { Claims = claims with { LicenseId = Guid.NewGuid() } }, claims.NotAfterUtc));
        Assert.False(summary.MatchesVerifiedLease(summary, valid, claims.NotAfterUtc.AddSeconds(1)));
        Assert.False(summary.MatchesVerifiedLease(summary, new(OnlineLeaseVerificationStatus.Expired), claims.NotAfterUtc));
        var shortTerm = summary with { EntitlementExpiresAtUtc = now.AddHours(1) };
        Assert.False(shortTerm.MatchesVerifiedLease(shortTerm, valid, claims.NotAfterUtc));
    }
}
