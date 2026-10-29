using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Scadivo.Portal.Data;
using Scadivo.Portal.Licensing;

namespace Scadivo.Portal;

public sealed record CreateOrganizationInput(string? Name);
public sealed record CreateProductApplicationInput(string? Name);
public sealed record AddOrganizationMemberInput(string? UserIdentifier, string? Role);
public sealed record IssueProductLicenseInput(Guid ApplicationId, string? Tier, int InteractiveSeats, int ViewOnlySeats,
    bool HaRuntime, bool AssetProcessIntelligence, string? Term, int MaxActivations);
public sealed record ExtendProductLicenseInput(string? Term);
public sealed record RevokeProductLicenseInput(string? Reason);
public sealed record DeviceActivationInput(string? Username, string? Password, string? LicenseCode,
    Guid ActivationId, Guid RequestId, string? MachineFingerprint, string? RefreshCredential);
public sealed record DeviceRenewalInput(Guid RequestId, string? NextRefreshCredential);

public sealed class PortalLicensingOperations(
    PortalDb db,
    UserManager<PortalUser> users,
    SignInManager<PortalUser> signIn,
    IOnlineLeaseSigner leaseSigner,
    TimeProvider clock,
    ILogger<PortalLicensingOperations> logger)
{
    private static readonly HashSet<string> Tiers = ["Tags500", "Tags1000", "Tags1500", "Tags3000", "Tags5000", "Unlimited"];
    private static readonly Regex FingerprintPattern = new("\\A[0-9a-fA-F]{64}\\z", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RefreshPattern = new("\\A[A-Za-z0-9_-]{43}\\z", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LicenseCodePattern = new("\\A[A-Z2-9]+(?:-[A-Z2-9]+)*\\z", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly string[] OrgManagerRoles = ["Owner", "Admin"];

    public async Task<IResult> Organizations(HttpContext context)
    {
        var actorId = UserId(context);
        if (context.User.IsInRole("Owner"))
            return Results.Ok(await db.Organizations.AsNoTracking().OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Name, role = "PortalOwner" }).ToListAsync());

        return Results.Ok(await (from membership in db.Memberships.AsNoTracking()
            join organization in db.Organizations.AsNoTracking() on membership.OrganizationId equals organization.Id
            where membership.UserId == actorId
            orderby organization.Name
            select new { organization.Id, organization.Name, membership.Role }).ToListAsync());
    }

    public async Task<IResult> CreateOrganization(CreateOrganizationInput input, HttpContext context)
    {
        if (!Valid(input.Name, 1, 200)) return Invalid();
        var user = await CurrentUser(context);
        if (user is null) return Results.Unauthorized();
        if (!user.ProfileComplete) return Results.Json(new { error = "profile-incomplete" }, statusCode: StatusCodes.Status428PreconditionRequired);

        var name = input.Name!.Trim();
        var organization = new Organization { Name = name };
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Organizations.Add(organization);
        db.Memberships.Add(new Membership { OrganizationId = organization.Id, UserId = user.Id, Role = "Owner" });
        db.Audit.Add(new PortalAudit { ActorId = user.Id, Action = "licensing.organization.created", ResourceId = organization.Id.ToString("D") });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Created($"/portal/api/organizations/{organization.Id}", new { organization.Id, organization.Name, role = "Owner" });
    }

    public async Task<IResult> Applications(Guid organizationId, HttpContext context)
    {
        var access = await OrganizationAccess(organizationId, context);
        if (!access.Exists) return Results.NotFound();
        return Results.Ok(await db.ProductApplications.AsNoTracking().Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.CreatedAtUtc }).ToListAsync());
    }

    public async Task<IResult> CreateApplication(Guid organizationId, CreateProductApplicationInput input, HttpContext context)
    {
        if (!Valid(input.Name, 1, 120)) return Invalid();
        var access = await OrganizationAccess(organizationId, context);
        if (!access.Exists) return Results.NotFound();
        if (!access.CanManage) return Results.Forbid();

        var name = input.Name!.Trim();
        var normalized = NormalizeName(name);
        if (await db.ProductApplications.AnyAsync(x => x.OrganizationId == organizationId && x.NormalizedName == normalized))
            return Results.Conflict(new { error = "application-name-already-exists" });

        var application = new ProductApplication { OrganizationId = organizationId, Name = name, NormalizedName = normalized };
        db.ProductApplications.Add(application);
        db.Audit.Add(new PortalAudit { ActorId = UserId(context), Action = "licensing.application.created", ResourceId = application.Id.ToString("D") });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Results.Conflict(new { error = "application-name-already-exists" }); }
        return Results.Created($"/portal/api/organizations/{organizationId}/applications/{application.Id}", new { application.Id, application.Name, application.CreatedAtUtc });
    }

    public async Task<IResult> Members(Guid organizationId, HttpContext context)
    {
        var access = await OrganizationAccess(organizationId, context);
        if (!access.Exists) return Results.NotFound();
        return Results.Ok(await (from membership in db.Memberships.AsNoTracking()
            join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.OrganizationId == organizationId
            orderby user.UserName
            select new { user.Id, user.UserName, user.DisplayName, user.Email, membership.Role }).ToListAsync());
    }

    public async Task<IResult> AddMember(Guid organizationId, AddOrganizationMemberInput input, HttpContext context)
    {
        var access = await OrganizationAccess(organizationId, context);
        if (!access.Exists) return Results.NotFound();
        if (!access.CanManage) return Results.Forbid();
        if (!Valid(input.UserIdentifier, 1, 256)) return Invalid();
        var role = string.IsNullOrWhiteSpace(input.Role) ? "Member" : input.Role.Trim();
        if (role is not ("Member" or "Admin")) return Invalid("unsupported-organization-role");
        if (role == "Admin" && !access.IsPortalOwner && !string.Equals(access.Role, "Owner", StringComparison.Ordinal)) return Results.Forbid();

        var identifier = input.UserIdentifier!.Trim();
        var user = await users.FindByEmailAsync(identifier) ?? await users.FindByNameAsync(identifier);
        if (user is null || !user.InvitationAccepted || !user.EmailConfirmed || user.IsSuspended)
            return Results.NotFound(new { error = "account-not-available" });
        if (await db.Memberships.AnyAsync(x => x.OrganizationId == organizationId && x.UserId == user.Id))
            return Results.Conflict(new { error = "account-already-in-organization" });

        db.Memberships.Add(new Membership { OrganizationId = organizationId, UserId = user.Id, Role = role });
        db.Audit.Add(new PortalAudit { ActorId = UserId(context), Action = "licensing.organization.member-added", ResourceId = organizationId.ToString("D") });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Results.Conflict(new { error = "account-already-in-organization" }); }
        return Results.Created($"/portal/api/organizations/{organizationId}/members/{user.Id}",
            new { user.Id, user.UserName, user.DisplayName, role });
    }

    public async Task<IResult> RemoveMember(Guid organizationId, string memberUserId, HttpContext context)
    {
        var access = await OrganizationAccess(organizationId, context);
        if (!access.Exists) return Results.NotFound();
        if (!access.CanManage) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(memberUserId) || memberUserId.Length > 100) return Invalid();

        await using var transaction = await db.Database.BeginTransactionAsync();
        var membership = await LockMembership(organizationId, memberUserId, "UPDATE");
        if (membership is null) return Results.NotFound();
        if (membership.Role == "Owner" && await db.Memberships.CountAsync(x => x.OrganizationId == organizationId && x.Role == "Owner") <= 1)
            return Results.Conflict(new { error = "organization-needs-an-owner" });

        var now = clock.GetUtcNow();
        await InvalidateActivationsAsync(memberUserId, organizationId, now, ProductLicenseStates.ReauthenticationRequired, "organization-membership-removed");
        db.Memberships.Remove(membership);
        db.Audit.Add(new PortalAudit { ActorId = UserId(context), Action = "licensing.organization.member-removed", ResourceId = organizationId.ToString("D") });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Ok(new { removed = true });
    }

    public async Task<IResult> MyLicenses(HttpContext context)
    {
        var userId = UserId(context);
        var portalOwner = context.User.IsInRole("Owner");
        var organizationIds = portalOwner
            ? await db.Organizations.AsNoTracking().Select(x => x.Id).ToArrayAsync()
            : await db.Memberships.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.OrganizationId).ToArrayAsync();
        var now = clock.GetUtcNow();
        var items = await (from license in db.ProductLicenses.AsNoTracking()
            join organization in db.Organizations.AsNoTracking() on license.OrganizationId equals organization.Id
            join application in db.ProductApplications.AsNoTracking() on license.ApplicationId equals application.Id
            where organizationIds.Contains(license.OrganizationId)
            orderby organization.Name, application.Name, license.CreatedAtUtc descending
            select new
            {
                license.Id, license.PublicCode, license.OrganizationId, organizationName = organization.Name,
                license.ApplicationId, applicationName = application.Name,
                license.Tier, license.InteractiveSeats, license.ViewOnlySeats, license.HaRuntime,
                license.AssetProcessIntelligence, license.Term, license.EntitlementExpiresAtUtc,
                license.MaxActivations, license.Status, license.CreatedAtUtc, license.RevokedAtUtc,
                activationCount = db.ProductLicenseActivations.Count(x => x.LicenseId == license.Id && x.Status != ProductLicenseStates.Revoked),
                activeActivations = db.ProductLicenseActivations.Count(x => x.LicenseId == license.Id && x.Status == ProductLicenseStates.Active)
            }).ToListAsync();
        return Results.Ok(new
        {
            asOfUtc = now,
            licenses = items.Select(x => new
            {
                x.Id, x.PublicCode, x.OrganizationId, x.organizationName,
                x.ApplicationId, x.applicationName,
                x.Tier, x.InteractiveSeats, x.ViewOnlySeats, x.HaRuntime, x.AssetProcessIntelligence,
                x.Term, x.EntitlementExpiresAtUtc, x.MaxActivations, x.CreatedAtUtc, x.RevokedAtUtc,
                status = EffectiveLicenseStatus(x.Status, x.EntitlementExpiresAtUtc, now),
                x.activationCount, x.activeActivations
            })
        });
    }

    public async Task<IResult> MyActivations(HttpContext context)
    {
        var userId = UserId(context);
        var now = clock.GetUtcNow();
        var items = await (from activation in db.ProductLicenseActivations.AsNoTracking()
            join application in db.ProductApplications.AsNoTracking() on activation.ApplicationId equals application.Id
            join organization in db.Organizations.AsNoTracking() on activation.OrganizationId equals organization.Id
            where activation.UserId == userId
            orderby activation.CreatedAtUtc descending
            select new { activation.Id, activation.LicenseId, licenseCode = db.ProductLicenses.Where(x => x.Id == activation.LicenseId).Select(x => x.PublicCode).FirstOrDefault(), organizationName = organization.Name, applicationName = application.Name, activation.Status, activation.CreatedAtUtc, activation.LastRenewedAtUtc, activation.RevokedAtUtc }).ToListAsync();
        return Results.Ok(new { asOfUtc = now, activations = items });
    }

    public async Task<IResult> DeactivateMyActivation(Guid activationId, HttpContext context)
    {
        var userId = UserId(context);
        var initial = await db.ProductLicenseActivations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == activationId && x.UserId == userId);
        if (initial is null) return Results.NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await LockUser(userId, "SHARE");
        if (await LockMembershipForRead(initial.OrganizationId, userId) is null) return Results.NotFound();
        await LockLicense(initial.LicenseId, "SHARE");
        var activation = await LockActivation(activationId, "UPDATE");
        if (activation is null || activation.UserId != userId) return Results.NotFound();
        if (activation.Status == ProductLicenseStates.Revoked)
        {
            await transaction.CommitAsync();
            return Results.Ok(new { revoked = true });
        }
        await RevokeActivationAsync(activation, clock.GetUtcNow(), userId, "user-deactivated");
        db.Audit.Add(new PortalAudit { ActorId = userId, Action = "licensing.activation.deactivated", ResourceId = activation.Id.ToString("D") });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Ok(new { revoked = true });
    }

    public async Task<IResult> AdminOrganizations()
    {
        var items = await db.Organizations.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.CreatedAtUtc, memberCount = db.Memberships.Count(m => m.OrganizationId == x.Id), applicationCount = db.ProductApplications.Count(a => a.OrganizationId == x.Id) }).ToListAsync();
        return Results.Ok(items);
    }

    public async Task<IResult> AdminLicenses()
    {
        var now = clock.GetUtcNow();
        var items = await (from license in db.ProductLicenses.AsNoTracking()
            join organization in db.Organizations.AsNoTracking() on license.OrganizationId equals organization.Id
            join application in db.ProductApplications.AsNoTracking() on new { license.OrganizationId, license.ApplicationId } equals new { application.OrganizationId, ApplicationId = application.Id }
            orderby license.CreatedAtUtc descending
            select new
            {
                license.Id, license.PublicCode, license.OrganizationId, organizationName = organization.Name,
                license.ApplicationId, applicationName = application.Name, license.Tier, license.InteractiveSeats, license.ViewOnlySeats,
                license.HaRuntime, license.AssetProcessIntelligence, license.Term, license.EntitlementExpiresAtUtc,
                license.MaxActivations, license.Status, license.CreatedAtUtc, license.CreatedByUserId, license.RevokedAtUtc,
                activationCount = db.ProductLicenseActivations.Count(x => x.LicenseId == license.Id && x.Status != ProductLicenseStates.Revoked),
                activeActivations = db.ProductLicenseActivations.Count(x => x.LicenseId == license.Id && x.Status == ProductLicenseStates.Active)
            }).ToListAsync();
        return Results.Ok(new { asOfUtc = now, licenses = items.Select(x => new
        {
            x.Id, x.PublicCode, x.OrganizationId, x.organizationName, x.ApplicationId, x.applicationName,
            x.Tier, x.InteractiveSeats, x.ViewOnlySeats, x.HaRuntime, x.AssetProcessIntelligence, x.Term,
            x.EntitlementExpiresAtUtc, x.MaxActivations, x.CreatedAtUtc, x.CreatedByUserId, x.RevokedAtUtc,
            status = EffectiveLicenseStatus(x.Status, x.EntitlementExpiresAtUtc, now), x.activationCount, x.activeActivations
        }) });
    }

    public async Task<IResult> IssueLicense(IssueProductLicenseInput input, HttpContext context)
    {
        if (input.ApplicationId == Guid.Empty || input.InteractiveSeats < 0 || input.ViewOnlySeats < 0 ||
            !Tiers.Contains(input.Tier ?? "") || input.MaxActivations < 1 ||
            input.Term is not ("monthly" or "annual" or "perpetual")) return Invalid();

        var application = await db.ProductApplications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.ApplicationId);
        if (application is null) return Results.NotFound();
        var now = clock.GetUtcNow();
        var license = new ProductLicense
        {
            PublicCode = GeneratePublicCode(), OrganizationId = application.OrganizationId, ApplicationId = application.Id,
            Tier = input.Tier!, InteractiveSeats = input.InteractiveSeats, ViewOnlySeats = input.ViewOnlySeats,
            HaRuntime = input.HaRuntime, AssetProcessIntelligence = input.AssetProcessIntelligence,
            Term = input.Term!, EntitlementExpiresAtUtc = ExpiryFor(input.Term!, now), MaxActivations = input.MaxActivations,
            Status = ProductLicenseStates.Active, CreatedAtUtc = now, CreatedByUserId = UserId(context)
        };
        db.ProductLicenses.Add(license);
        db.Audit.Add(new PortalAudit { ActorId = UserId(context), Action = "licensing.license.issued", ResourceId = license.Id.ToString("D") });
        await db.SaveChangesAsync();
        return Results.Created($"/portal/api/admin/licensing/licenses/{license.Id}", LicenseSummary(license, application.Name, now, 0, 0));
    }

    public async Task<IResult> ExtendLicense(Guid licenseId, ExtendProductLicenseInput input, HttpContext context)
    {
        if (input.Term is not ("monthly" or "annual")) return Invalid("unsupported-renewal-term");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var license = await LockLicense(licenseId, "UPDATE");
        if (license is null) return Results.NotFound();
        if (license.Status == ProductLicenseStates.Revoked) return Results.Conflict(new { error = "license-revoked" });
        if (license.Term == "perpetual") return Results.Conflict(new { error = "perpetual-license-does-not-expire" });
        var now = clock.GetUtcNow();
        var baseDate = license.EntitlementExpiresAtUtc is { } expiry && expiry > now ? expiry : now;
        license.Term = input.Term!;
        license.EntitlementExpiresAtUtc = ExpiryFor(input.Term!, baseDate);
        db.Audit.Add(new PortalAudit { ActorId = UserId(context), Action = "licensing.license.extended", ResourceId = license.Id.ToString("D") });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Ok(new { license.Id, license.Term, license.EntitlementExpiresAtUtc, status = EffectiveLicenseStatus(license.Status, license.EntitlementExpiresAtUtc, now) });
    }

    public async Task<IResult> RevokeLicense(Guid licenseId, RevokeProductLicenseInput input, HttpContext context)
    {
        if (!string.IsNullOrWhiteSpace(input.Reason) && input.Reason.Trim().Length > 240) return Invalid();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var license = await LockLicense(licenseId, "UPDATE");
        if (license is null) return Results.NotFound();
        if (license.Status == ProductLicenseStates.Revoked) return Results.Ok(new { license.Id, revoked = true, license.RevokedAtUtc });
        var now = clock.GetUtcNow();
        license.Status = ProductLicenseStates.Revoked;
        license.RevokedAtUtc = now;
        license.RevokedByUserId = UserId(context);
        license.RevocationReason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim();
        var activations = await db.ProductLicenseActivations.Where(x => x.LicenseId == license.Id && x.Status != ProductLicenseStates.Revoked).ToListAsync();
        foreach (var activation in activations)
            await RevokeActivationAsync(activation, now, UserId(context), "license-revoked");
        db.Audit.Add(new PortalAudit { ActorId = UserId(context), Action = "licensing.license.revoked", ResourceId = license.Id.ToString("D") });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Ok(new { license.Id, revoked = true, license.RevokedAtUtc });
    }

    public async Task<IResult> AdminActivations(Guid licenseId)
    {
        if (!await db.ProductLicenses.AnyAsync(x => x.Id == licenseId)) return Results.NotFound();
        return Results.Ok(await (from activation in db.ProductLicenseActivations.AsNoTracking()
            join user in db.Users.AsNoTracking() on activation.UserId equals user.Id
            where activation.LicenseId == licenseId
            orderby activation.CreatedAtUtc descending
            select new { activation.Id, activation.UserId, user.UserName, user.Email, activation.Status, activation.CreatedAtUtc,
                activation.LastRenewedAtUtc, activation.RevokedAtUtc, activation.RevocationReason }).ToListAsync());
    }

    public async Task<IResult> RevokeAdminActivation(Guid activationId, RevokeProductLicenseInput input, HttpContext context)
    {
        if (!string.IsNullOrWhiteSpace(input.Reason) && input.Reason.Trim().Length > 240) return Invalid();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var activation = await LockActivation(activationId, "UPDATE");
        if (activation is null) return Results.NotFound();
        if (activation.Status != ProductLicenseStates.Revoked)
            await RevokeActivationAsync(activation, clock.GetUtcNow(), UserId(context), string.IsNullOrWhiteSpace(input.Reason) ? "administrative-revocation" : input.Reason.Trim());
        db.Audit.Add(new PortalAudit { ActorId = UserId(context), Action = "licensing.activation.revoked", ResourceId = activation.Id.ToString("D") });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Ok(new { activation.Id, revoked = true, activation.RevokedAtUtc });
    }

    public async Task<IResult> Activate(DeviceActivationInput input, HttpContext context)
    {
        if (!leaseSigner.IsEnabled) return LicenseServiceUnavailable();
        if (!context.Request.IsHttps) return Results.BadRequest(new { error = "https-required" });
        if (!Valid(input.Username, 1, 256) || input.Password is null || input.Password.Length is < 1 or > 1024 ||
            input.ActivationId == Guid.Empty || input.RequestId == Guid.Empty || !IsFingerprint(input.MachineFingerprint) ||
            !IsRefreshCredential(input.RefreshCredential) || !ValidLicenseCode(input.LicenseCode)) return Invalid();

        var identifier = input.Username!.Trim();
        var user = await users.FindByEmailAsync(identifier) ?? await users.FindByNameAsync(identifier);
        if (user is null) return InvalidCredentials();
        var passwordResult = await signIn.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true);
        if (!passwordResult.Succeeded || !UsableAccount(user)) return InvalidCredentials();

        var licenseCode = NormalizeLicenseCode(input.LicenseCode!);
        var licenseId = await db.ProductLicenses.AsNoTracking().Where(x => x.PublicCode == licenseCode)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync();
        if (licenseId is null) return LicenseNotAvailable();

        var credentialHash = HashCredential(input.RefreshCredential!);
        var normalizedFingerprint = input.MachineFingerprint!.ToLowerInvariant();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await LockUser(user.Id, "SHARE");
        var membership = await LockMembershipForRead(await db.ProductLicenses.AsNoTracking().Where(x => x.Id == licenseId.Value).Select(x => x.OrganizationId).SingleAsync(), user.Id);
        if (membership is null) return LicenseNotAvailable();
        var license = await LockLicense(licenseId.Value, "UPDATE");
        if (license is null) return LicenseNotAvailable();
        var activation = await LockActivation(input.ActivationId, "UPDATE");
        if (activation is not null)
        {
            if (activation.LicenseId != license.Id || activation.OrganizationId != license.OrganizationId ||
                activation.ApplicationId != license.ApplicationId || activation.UserId != user.Id ||
                activation.MachineFingerprint != normalizedFingerprint)
                return Results.Conflict(new { error = "activation-id-already-bound" });
            if (activation.LastActivationRequestId == input.RequestId && activation.LastActivationCredentialHash == credentialHash && activation.ActivationLeaseEnvelope is not null)
            {
                await transaction.CommitAsync();
                return LeaseResponse(activation, activation.ActivationLeaseEnvelope);
            }
            if (activation.Status == ProductLicenseStates.Revoked) return Results.Conflict(new { error = "activation-revoked" });
        }
        if (!LicenseCanIssue(license, clock.GetUtcNow())) return LicenseNotAvailable();
        var application = await db.ProductApplications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == license.ApplicationId && x.OrganizationId == license.OrganizationId);
        if (application is null) return LicenseNotAvailable();

        if (activation is not null)
        {
            var currentExists = await db.ProductLicenseRefreshCredentials.AnyAsync(x => x.ActivationId == activation.Id && x.State == RefreshCredentialStates.Current);
            if (activation.Status != ProductLicenseStates.ReauthenticationRequired && currentExists)
                return Results.Conflict(new { error = "activation-already-active" });
        }
        else
        {
            var used = await db.ProductLicenseActivations.CountAsync(x => x.LicenseId == license.Id && x.Status != ProductLicenseStates.Revoked);
            if (used >= license.MaxActivations) return Results.Conflict(new { error = "activation-limit-reached" });
            activation = new ProductLicenseActivation
            {
                Id = input.ActivationId, LicenseId = license.Id, OrganizationId = license.OrganizationId,
                ApplicationId = license.ApplicationId, UserId = user.Id, MachineFingerprint = normalizedFingerprint,
                Status = ProductLicenseStates.Active, CreatedAtUtc = clock.GetUtcNow()
            };
            db.ProductLicenseActivations.Add(activation);
        }

        var exists = await db.ProductLicenseRefreshCredentials.AnyAsync(x => x.CredentialHash == credentialHash);
        if (exists) return Results.Conflict(new { error = "refresh-credential-already-used" });
        var now = clock.GetUtcNow();
        if (activation.LastLeaseIssuedAtUtc is { } previousIssued && now < previousIssued) return LicenseClockError();
        var signed = SignLease(license, activation, now);
        if (signed.Lease is null) return LicenseServiceUnavailable();

        activation.Status = ProductLicenseStates.Active;
        activation.RevocationReason = null;
        activation.LastActivationRequestId = input.RequestId;
        activation.LastActivationCredentialHash = credentialHash;
        activation.ActivationLeaseEnvelope = signed.Lease;
        activation.LastLeaseEnvelope = signed.Lease;
        activation.LastLeaseIssuedAtUtc = now;
        activation.LastRenewedAtUtc = null;
        db.ProductLicenseRefreshCredentials.Add(new ProductLicenseRefreshCredential
        {
            ActivationId = activation.Id, CredentialHash = credentialHash,
            State = RefreshCredentialStates.Current, CreatedAtUtc = now
        });
        db.Audit.Add(new PortalAudit { ActorId = user.Id, Action = "licensing.activation.created", ResourceId = activation.Id.ToString("D") });
        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { error = "activation-request-conflict" });
        }
        return LeaseResponse(activation, signed.Lease);
    }

    public async Task<IResult> Renew(Guid activationId, DeviceRenewalInput input, HttpContext context)
    {
        if (!leaseSigner.IsEnabled) return LicenseServiceUnavailable();
        if (!context.Request.IsHttps) return Results.BadRequest(new { error = "https-required" });
        if (activationId == Guid.Empty || input.RequestId == Guid.Empty || !IsRefreshCredential(input.NextRefreshCredential)) return Invalid();
        if (!TryReadBearer(context, out var currentCredential)) return InvalidCredentials();

        var initial = await db.ProductLicenseActivations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == activationId);
        if (initial is null) return InvalidCredentials();
        var currentHash = HashCredential(currentCredential);
        var nextHash = HashCredential(input.NextRefreshCredential!);
        if (CryptographicOperations.FixedTimeEquals(Convert.FromHexString(currentHash), Convert.FromHexString(nextHash))) return Invalid();

        await using var transaction = await db.Database.BeginTransactionAsync();
        await LockUser(initial.UserId, "SHARE");
        var membership = await LockMembershipForRead(initial.OrganizationId, initial.UserId);
        if (membership is null) return InvalidCredentials();
        var license = await LockLicense(initial.LicenseId, "SHARE");
        var activation = await LockActivation(activationId, "UPDATE");
        if (license is null || activation is null || activation.UserId != initial.UserId)
            return InvalidCredentials();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == activation.UserId);
        if (!UsableAccount(user)) return InvalidCredentials();

        var credential = await db.ProductLicenseRefreshCredentials.SingleOrDefaultAsync(x => x.ActivationId == activationId && x.CredentialHash == currentHash);
        if (credential is not null &&
            activation.LastRenewalRequestId == input.RequestId &&
            activation.LastRenewalInputCredentialHash == currentHash &&
            activation.LastRenewalOutputCredentialHash == nextHash &&
            activation.LastLeaseEnvelope is not null)
        {
            await transaction.CommitAsync();
            return LeaseResponse(activation, activation.LastLeaseEnvelope);
        }
        if (activation.Status != ProductLicenseStates.Active || !LicenseCanIssue(license, clock.GetUtcNow()) || credential is null)
            return InvalidCredentials();
        if (credential.State == RefreshCredentialStates.Superseded)
        {
            var now = clock.GetUtcNow();
            await InvalidateActivationCredentialsAsync(activation, now, ProductLicenseStates.ReauthenticationRequired, "refresh-credential-reuse");
            db.Audit.Add(new PortalAudit { ActorId = activation.UserId, Action = "licensing.refresh.reuse-detected", ResourceId = activation.Id.ToString("D") });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return InvalidCredentials();
        }
        if (credential.State != RefreshCredentialStates.Current) return InvalidCredentials();
        if (activation.LastRenewalRequestId == input.RequestId) return Results.Conflict(new { error = "renewal-request-conflict" });
        if (await db.ProductLicenseActivations.AnyAsync(x => x.LastRenewalRequestId == input.RequestId))
            return Results.Conflict(new { error = "renewal-request-conflict" });
        if (await db.ProductLicenseRefreshCredentials.AnyAsync(x => x.CredentialHash == nextHash))
            return Results.Conflict(new { error = "refresh-credential-already-used" });

        var issuedAt = clock.GetUtcNow();
        if (activation.LastLeaseIssuedAtUtc is { } previousIssued && issuedAt < previousIssued) return LicenseClockError();
        var signed = SignLease(license, activation, issuedAt);
        if (signed.Lease is null) return LicenseServiceUnavailable();

        credential.State = RefreshCredentialStates.Superseded;
        credential.SupersededAtUtc = issuedAt;
        db.ProductLicenseRefreshCredentials.Add(new ProductLicenseRefreshCredential
        {
            ActivationId = activationId, CredentialHash = nextHash,
            State = RefreshCredentialStates.Current, CreatedAtUtc = issuedAt
        });
        activation.LastRenewalRequestId = input.RequestId;
        activation.LastRenewalInputCredentialHash = currentHash;
        activation.LastRenewalOutputCredentialHash = nextHash;
        activation.LastLeaseEnvelope = signed.Lease;
        activation.LastLeaseIssuedAtUtc = issuedAt;
        activation.LastRenewedAtUtc = issuedAt;
        db.Audit.Add(new PortalAudit { ActorId = activation.UserId, Action = "licensing.lease.renewed", ResourceId = activation.Id.ToString("D") });
        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { error = "renewal-request-conflict" });
        }
        return LeaseResponse(activation, signed.Lease);
    }

    public async Task InvalidateUserActivationsAsync(string userId, DateTimeOffset now, string reason)
    {
        var activations = await db.ProductLicenseActivations.Where(x => x.UserId == userId && x.Status != ProductLicenseStates.Revoked).ToListAsync();
        foreach (var activation in activations)
            await InvalidateActivationCredentialsAsync(activation, now, ProductLicenseStates.ReauthenticationRequired, reason);
    }

    private async Task<(bool Exists, bool CanManage, bool IsPortalOwner, string? Role)> OrganizationAccess(Guid organizationId, HttpContext context)
    {
        var portalOwner = context.User.IsInRole("Owner");
        if (portalOwner)
            return (await db.Organizations.AnyAsync(x => x.Id == organizationId), true, true, "PortalOwner");
        var membership = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.UserId == UserId(context));
        return membership is null ? (false, false, false, null) : (true, OrgManagerRoles.Contains(membership.Role), false, membership.Role);
    }

    private async Task<PortalUser?> CurrentUser(HttpContext context)
    {
        var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return id is null ? null : await users.FindByIdAsync(id);
    }

    private static string UserId(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static bool Valid(string? value, int min, int max) => value?.Trim().Length >= min && value.Trim().Length <= max;
    private static IResult Invalid(string code = "invalid-input") => Results.BadRequest(new { error = code });
    private static IResult InvalidCredentials() => Results.Json(new { error = "invalid-credentials" }, statusCode: StatusCodes.Status401Unauthorized);
    private static IResult LicenseNotAvailable() => Results.NotFound(new { error = "license-not-available" });
    private static IResult LicenseServiceUnavailable() => Results.Json(new { error = "licensing-service-unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    private static IResult LicenseClockError() => Results.Json(new { error = "licensing-clock-invalid" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    private static bool IsFingerprint(string? value) => value is not null && FingerprintPattern.IsMatch(value);
    private static bool IsRefreshCredential(string? value) => value is not null && RefreshPattern.IsMatch(value);
    private static bool ValidLicenseCode(string? value) => Valid(value, 8, 48) && LicenseCodePattern.IsMatch(NormalizeLicenseCode(value!));
    private static string NormalizeLicenseCode(string value) => value.Trim().ToUpperInvariant();
    private static string NormalizeName(string value) => value.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
    private static string HashCredential(string token) => Convert.ToHexString(SHA256.HashData(WebEncoders.Base64UrlDecode(token))).ToLowerInvariant();
    private static bool UsableAccount(PortalUser? user) => user is not null && user.InvitationAccepted && user.EmailConfirmed && !user.IsSuspended;

    private static string GeneratePublicCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(16);
        var chars = new char[26];
        var bitBuffer = 0;
        var bitCount = 0;
        var output = 0;
        foreach (var value in bytes)
        {
            bitBuffer = (bitBuffer << 8) | value;
            bitCount += 8;
            while (bitCount >= 5 && output < chars.Length)
            {
                bitCount -= 5;
                chars[output++] = alphabet[(bitBuffer >> bitCount) & 31];
            }
        }
        if (bitCount > 0 && output < chars.Length) chars[output++] = alphabet[(bitBuffer << (5 - bitCount)) & 31];
        var groups = Enumerable.Range(0, chars.Length).Chunk(4).Select(group => new string(group.Select(index => chars[index]).ToArray()));
        return "SCV-" + string.Join('-', groups);
    }

    private static DateTimeOffset? ExpiryFor(string term, DateTimeOffset from) => term switch
    {
        "monthly" => from.AddMonths(1),
        "annual" => from.AddYears(1),
        "perpetual" => null,
        _ => throw new ArgumentOutOfRangeException(nameof(term))
    };

    private static string EffectiveLicenseStatus(string status, DateTimeOffset? expiresAtUtc, DateTimeOffset now) =>
        status == ProductLicenseStates.Revoked ? ProductLicenseStates.Revoked :
        expiresAtUtc is { } expiry && expiry <= now ? "expired" : ProductLicenseStates.Active;

    private static bool LicenseCanIssue(ProductLicense license, DateTimeOffset now) =>
        license.Status == ProductLicenseStates.Active && (license.EntitlementExpiresAtUtc is null || license.EntitlementExpiresAtUtc > now);

    private static object LicenseSummary(ProductLicense license, string applicationName, DateTimeOffset now, int activations, int activeActivations) => new
    {
        license.Id, license.PublicCode, license.OrganizationId, license.ApplicationId, applicationName, license.Tier,
        license.InteractiveSeats, license.ViewOnlySeats, license.HaRuntime, license.AssetProcessIntelligence,
        license.Term, license.EntitlementExpiresAtUtc, license.MaxActivations, license.CreatedAtUtc,
        status = EffectiveLicenseStatus(license.Status, license.EntitlementExpiresAtUtc, now),
        activationCount = activations, activeActivations
    };

    private static IResult LeaseResponse(ProductLicenseActivation activation, string lease) => Results.Ok(new
    {
        activationId = activation.Id,
        lease,
        notAfterUtc = LeaseNotAfter(lease)
    });

    private static DateTimeOffset? LeaseNotAfter(string lease)
    {
        try
        {
            var part = lease.Split('.')[1];
            using var document = System.Text.Json.JsonDocument.Parse(WebEncoders.Base64UrlDecode(part));
            return document.RootElement.GetProperty("notAfterUtc").GetDateTimeOffset();
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or System.Text.Json.JsonException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    private (string? Lease, DateTimeOffset? NotAfterUtc) SignLease(ProductLicense license, ProductLicenseActivation activation, DateTimeOffset now)
    {
        if (activation.LastLeaseIssuedAtUtc is { } previous && now < previous) return (null, null);
        var notAfter = now.AddHours(72);
        if (license.EntitlementExpiresAtUtc is { } entitlementEnd && entitlementEnd < notAfter) notAfter = entitlementEnd;
        if (notAfter <= now) return (null, null);
        var claims = new OnlineLeaseClaims(1, license.Id.ToString("D"), license.OrganizationId.ToString("D"),
            license.ApplicationId.ToString("D"), activation.Id.ToString("D"), activation.MachineFingerprint,
            license.Tier, license.ViewOnlySeats, license.InteractiveSeats, license.HaRuntime,
            license.AssetProcessIntelligence, now, notAfter, OnlineLeaseSigningOptions.ApprovedKeyId);
        try { return (leaseSigner.Sign(claims), notAfter); }
        catch (Exception exception) when (exception is CryptographicException or InvalidOperationException)
        {
            logger.LogError("Online lease signing failed ({ErrorType}).", exception.GetType().Name);
            return (null, null);
        }
    }

    private async Task<PortalUser?> LockUser(string userId, string lockMode) => lockMode == "UPDATE"
        ? await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync()
        : await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR SHARE").SingleOrDefaultAsync();

    private async Task<Membership?> LockMembershipForRead(Guid organizationId, string userId) =>
        await db.Memberships.FromSqlInterpolated($"SELECT * FROM \"Memberships\" WHERE \"OrganizationId\" = {organizationId} AND \"UserId\" = {userId} FOR SHARE").SingleOrDefaultAsync();

    private async Task<Membership?> LockMembership(Guid organizationId, string userId, string lockMode) => lockMode == "UPDATE"
        ? await db.Memberships.FromSqlInterpolated($"SELECT * FROM \"Memberships\" WHERE \"OrganizationId\" = {organizationId} AND \"UserId\" = {userId} FOR UPDATE").SingleOrDefaultAsync()
        : await db.Memberships.FromSqlInterpolated($"SELECT * FROM \"Memberships\" WHERE \"OrganizationId\" = {organizationId} AND \"UserId\" = {userId} FOR SHARE").SingleOrDefaultAsync();

    private async Task<ProductLicense?> LockLicense(Guid licenseId, string lockMode) => lockMode == "UPDATE"
        ? await db.ProductLicenses.FromSqlInterpolated($"SELECT * FROM \"ProductLicenses\" WHERE \"Id\" = {licenseId} FOR UPDATE").SingleOrDefaultAsync()
        : await db.ProductLicenses.FromSqlInterpolated($"SELECT * FROM \"ProductLicenses\" WHERE \"Id\" = {licenseId} FOR SHARE").SingleOrDefaultAsync();

    private async Task<ProductLicenseActivation?> LockActivation(Guid activationId, string lockMode) => lockMode == "UPDATE"
        ? await db.ProductLicenseActivations.FromSqlInterpolated($"SELECT * FROM \"ProductLicenseActivations\" WHERE \"Id\" = {activationId} FOR UPDATE").SingleOrDefaultAsync()
        : await db.ProductLicenseActivations.FromSqlInterpolated($"SELECT * FROM \"ProductLicenseActivations\" WHERE \"Id\" = {activationId} FOR SHARE").SingleOrDefaultAsync();

    private async Task InvalidateActivationsAsync(string userId, Guid organizationId, DateTimeOffset now, string newStatus, string reason)
    {
        var activations = await db.ProductLicenseActivations.Where(x => x.UserId == userId && x.OrganizationId == organizationId && x.Status != ProductLicenseStates.Revoked).ToListAsync();
        foreach (var activation in activations)
            await InvalidateActivationCredentialsAsync(activation, now, newStatus, reason);
    }

    private async Task InvalidateActivationCredentialsAsync(ProductLicenseActivation activation, DateTimeOffset now, string newStatus, string reason)
    {
        var credentials = await db.ProductLicenseRefreshCredentials.Where(x => x.ActivationId == activation.Id && x.State != RefreshCredentialStates.Revoked).ToListAsync();
        foreach (var credential in credentials)
        {
            credential.State = RefreshCredentialStates.Revoked;
            credential.RevokedAtUtc = now;
        }
        activation.Status = newStatus;
        activation.RevocationReason = reason;
    }

    private async Task RevokeActivationAsync(ProductLicenseActivation activation, DateTimeOffset now, string actorId, string reason)
    {
        await InvalidateActivationCredentialsAsync(activation, now, ProductLicenseStates.Revoked, reason);
        activation.RevokedAtUtc = now;
        activation.RevokedByUserId = actorId;
    }

    private static bool TryReadBearer(HttpContext context, out string credential)
    {
        credential = "";
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || header.Length != "Bearer ".Length + 43) return false;
        var token = header["Bearer ".Length..];
        if (!IsRefreshCredential(token)) return false;
        credential = token;
        return true;
    }
}
