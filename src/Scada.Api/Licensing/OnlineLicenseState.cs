using Scada.Core.Product.Licensing;

namespace Scada.Api.Licensing;

public sealed record OnlineLicenseSelection(Guid LicenseId, Guid OrganizationId, Guid ApplicationId, string LicenseCode);

/// <summary>Safe administrative status, not a source of Runtime authority or entitlements.</summary>
public sealed record OnlineLicenseDeviceStatus(
    Guid ActivationId, Guid? PendingRequestId, bool HasLease, DateTimeOffset? IssuedAtUtc,
    DateTimeOffset? NotAfterUtc, DateTimeOffset? RenewalDueUtc, bool ClockRollbackDetected);

public sealed class OnlineLicenseProtocolException(string code) : InvalidOperationException(code)
{
    public string Code { get; } = code;
}

// These classes deliberately have no record-generated ToString exposing transport credentials.
internal sealed class OnlineLicensePendingIntent
{
    public required string Kind { get; init; }
    public required Guid RequestId { get; init; }
    public required string NextCredential { get; init; }
    public string? Username { get; init; }
    public string? LicenseCode { get; init; }
}

internal sealed class OnlineLicenseDeviceState
{
    public int SchemaVersion { get; init; } = 1;
    public required string PortalOrigin { get; init; }
    public required OnlineLeaseBinding Binding { get; init; }
    public required DateTimeOffset LastObservedUtc { get; set; }
    public string? CurrentCredential { get; set; }
    public string? Lease { get; set; }
    public DateTimeOffset? IssuedAtUtc { get; set; }
    public DateTimeOffset? NotAfterUtc { get; set; }
    public Guid? CompletedRequestId { get; set; }
    public OnlineLicensePendingIntent? Pending { get; set; }
}
