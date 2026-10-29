namespace Scadivo.Portal.Data;

public static class ProductLicenseStates
{
    public const string Active = "active";
    public const string Revoked = "revoked";
    public const string ReauthenticationRequired = "reauthentication-required";
}

public static class RefreshCredentialStates
{
    public const string Current = "current";
    public const string Superseded = "superseded";
    public const string Revoked = "revoked";
}

public sealed class ProductApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProductLicense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PublicCode { get; set; } = "";
    public Guid OrganizationId { get; set; }
    public Guid ApplicationId { get; set; }
    public string Tier { get; set; } = "";
    public int InteractiveSeats { get; set; }
    public int ViewOnlySeats { get; set; }
    public bool HaRuntime { get; set; }
    public bool AssetProcessIntelligence { get; set; }
    public string Term { get; set; } = "perpetual";
    public DateTimeOffset? EntitlementExpiresAtUtc { get; set; }
    public int MaxActivations { get; set; }
    public string Status { get; set; } = ProductLicenseStates.Active;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? RevokedByUserId { get; set; }
    public string? RevocationReason { get; set; }
}

public sealed class ProductLicenseActivation
{
    public Guid Id { get; set; }
    public Guid LicenseId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ApplicationId { get; set; }
    public string UserId { get; set; } = "";
    public string MachineFingerprint { get; set; } = "";
    public string Status { get; set; } = ProductLicenseStates.Active;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastRenewedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? RevokedByUserId { get; set; }
    public string? RevocationReason { get; set; }
    public Guid? LastActivationRequestId { get; set; }
    public string? LastActivationCredentialHash { get; set; }
    public string? ActivationLeaseEnvelope { get; set; }
    public Guid? LastRenewalRequestId { get; set; }
    public string? LastRenewalInputCredentialHash { get; set; }
    public string? LastRenewalOutputCredentialHash { get; set; }
    public string? LastLeaseEnvelope { get; set; }
    public DateTimeOffset? LastLeaseIssuedAtUtc { get; set; }
}

public sealed class ProductLicenseRefreshCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActivationId { get; set; }
    public string CredentialHash { get; set; } = "";
    public string State { get; set; } = RefreshCredentialStates.Current;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SupersededAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
