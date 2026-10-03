namespace Scada.Drivers.Abstractions;

/// <summary>
/// Describes one host resource that cannot always be owned concurrently by the
/// previous and candidate Runtime during activation. Shared resources conflict
/// only for the same logical owner or incompatible physical configuration;
/// exclusive claims conflict with every overlapping claim.
/// </summary>
public sealed record CommunicationDriverResourceClaim(
    string Kind,
    string Scope,
    string Identity,
    string OwnerKey,
    string? CompatibilityKey = null,
    bool Exclusive = false,
    bool WildcardIdentity = false)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Kind)) throw new ArgumentException("Resource kind is required.", nameof(Kind));
        if (string.IsNullOrWhiteSpace(Identity)) throw new ArgumentException("Resource identity is required.", nameof(Identity));
        if (string.IsNullOrWhiteSpace(OwnerKey)) throw new ArgumentException("Resource owner key is required.", nameof(OwnerKey));
    }

    public bool ConflictsWith(CommunicationDriverResourceClaim other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Validate();
        other.Validate();
        if (!string.Equals(Kind, other.Kind, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(Scope ?? string.Empty, other.Scope ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return false;

        var identityOverlaps =
            WildcardIdentity ||
            other.WildcardIdentity ||
            string.Equals(Identity, other.Identity, StringComparison.OrdinalIgnoreCase);
        if (!identityOverlaps) return false;

        if (Exclusive || other.Exclusive) return true;
        if (string.Equals(OwnerKey, other.OwnerKey, StringComparison.OrdinalIgnoreCase)) return true;

        return CompatibilityKey is not null &&
               other.CompatibilityKey is not null &&
               !string.Equals(CompatibilityKey, other.CompatibilityKey, StringComparison.OrdinalIgnoreCase);
    }
}

public interface ICommunicationDriverResourceClaimSource
{
    IReadOnlyCollection<CommunicationDriverResourceClaim> ResourceClaims { get; }
}
