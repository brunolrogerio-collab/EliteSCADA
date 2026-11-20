using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

public sealed record DataCopyInput(string? CurrentPassword);

public sealed class PortalDataCopy(PortalDb db, UserManager<PortalUser> users, SignInManager<PortalUser> signIn,
    IConfiguration configuration, IWebHostEnvironment environment, TimeProvider clock)
{
    private static readonly JsonSerializerOptions ExportJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>("Portal:DataCopyEnabled");

    public async Task<IResult> DownloadAsync(DataCopyInput input, HttpContext context, CancellationToken token)
    {
        if (!IsEnabled(configuration)) return Results.NotFound();
        if (!context.Request.IsHttps && !environment.IsDevelopment())
            return Results.BadRequest(new { error = "https-required" });

        var user = await users.GetUserAsync(context.User);
        if (user is not { EmailConfirmed: true, InvitationAccepted: true, IsSuspended: false, ClosedAtUtc: null })
            return Results.Forbid();
        if (input.CurrentPassword is not { Length: > 0 and <= 1024 } ||
            !(await signIn.CheckPasswordSignInAsync(user, input.CurrentPassword, lockoutOnFailure: true)).Succeeded)
            return Results.BadRequest(new { error = "data-copy-password-invalid" });

        var id = user.Id;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        var membership = db.Memberships.AsNoTracking().Where(row => row.UserId == id);
        var relatedOrganizations = membership.Select(row => row.OrganizationId);
        var ownedTickets = db.Tickets.AsNoTracking().Where(row => row.UserId == id);
        var ownActivations = db.ProductLicenseActivations.AsNoTracking().Where(row => row.UserId == id);
        var ownRequests = db.OfflineMachineLicenseRequests.AsNoTracking().Where(row => row.UserId == id);
        var snapshot = new
        {
            formatVersion = 1,
            scope = "scadivo-portal",
            generatedAtUtc = clock.GetUtcNow(),
            account = new
            {
                user.Id, user.UserName, user.DisplayName, user.Email, user.EmailConfirmed, user.Locale,
                user.ProfileType, user.ActivityArea, user.ActivityAreaOther, user.OrganizationOrSchool,
                user.PhoneNumber, user.PhoneNumberConfirmed, user.Address, user.DateOfBirth,
                user.MarketingEmailOptIn, user.MarketingEmailOptInAtUtc, user.CreatedAtUtc,
                roles = await users.GetRolesAsync(user)
            },
            memberships = await (from member in membership
                join organization in db.Organizations.AsNoTracking() on member.OrganizationId equals organization.Id
                orderby organization.Name
                select new { member.OrganizationId, organization.Name, member.Role }).ToListAsync(token),
            applications = await db.ProductApplications.AsNoTracking()
                .Where(row => relatedOrganizations.Contains(row.OrganizationId)).OrderBy(row => row.CreatedAtUtc)
                .Select(row => new { row.Id, row.OrganizationId, row.Name, row.CreatedAtUtc }).ToListAsync(token),
            supportTickets = await ownedTickets.OrderBy(row => row.CreatedAtUtc)
                .Select(row => new { row.Id, row.Subject, row.Category, row.Status, row.CreatedAtUtc, row.UpdatedAtUtc }).ToListAsync(token),
            supportMessages = await db.TicketMessages.AsNoTracking()
                .Where(row => ownedTickets.Any(ticket => ticket.Id == row.TicketId)).OrderBy(row => row.CreatedAtUtc)
                .Select(row => new { row.TicketId, row.Body, row.StaffReply, row.CreatedAtUtc }).ToListAsync(token),
            offlineLicenseRequests = await ownRequests.OrderBy(row => row.SubmittedAtUtc)
                .Select(row => new
                {
                    row.Id, row.ApplicationName, row.RequestKind, row.ExistingLicenseName, row.OnlineLicenseId,
                    row.SignedLicenseId, row.AvailableAtUtc, row.RequestNote, row.Status, row.SubmittedAtUtc, row.UpdatedAtUtc
                }).ToListAsync(token),
            licenses = await db.ProductLicenses.AsNoTracking().Where(row => relatedOrganizations.Contains(row.OrganizationId) ||
                    ownActivations.Any(activation => activation.LicenseId == row.Id) ||
                    ownRequests.Any(request => request.OnlineLicenseId == row.Id)).OrderBy(row => row.CreatedAtUtc)
                .Select(row => new
                {
                    row.Id, row.OrganizationId, row.ApplicationId, row.Tier, row.InteractiveSeats,
                    row.ViewOnlySeats, row.HaRuntime, row.AssetProcessIntelligence, row.Term,
                    row.EntitlementExpiresAtUtc, row.MaxActivations, row.Status, row.CreatedAtUtc, row.RevokedAtUtc
                }).ToListAsync(token),
            activations = await ownActivations.OrderBy(row => row.CreatedAtUtc)
                .Select(row => new
                {
                    row.Id, row.LicenseId, row.OrganizationId, row.ApplicationId, row.MachineFingerprint,
                    row.Status, row.CreatedAtUtc, row.LastRenewedAtUtc, row.RevokedAtUtc
                }).ToListAsync(token),
            activationPreparations = await db.ProductLicensePreparations.AsNoTracking().Where(row => row.UserId == id)
                .OrderBy(row => row.ExpiresAtUtc)
                .Select(row => new { row.LicenseId, row.MachineFingerprint, row.ExpiresAtUtc, row.Cancelled, row.Reactivation }).ToListAsync(token),
            emailDeliveries = await db.EmailOutbox.AsNoTracking().Where(row => row.UserId == id)
                .OrderBy(row => row.CreatedAtUtc)
                .Select(row => new { row.TemplateKey, row.Status, row.CreatedAtUtc, row.SentAtUtc, row.BouncedAtUtc }).ToListAsync(token),
            activity = await db.Audit.AsNoTracking().Where(row => row.ActorId == id || row.ResourceId == id)
                .OrderBy(row => row.CreatedAtUtc)
                .Select(row => new { row.Action, row.CreatedAtUtc, downloadId = row.Action == "download.requested" ? row.ResourceId : null })
                .ToListAsync(token)
        };

        db.Audit.Add(new PortalAudit { ActorId = id, Action = "privacy.data-copy.generated" });
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Stream(stream => JsonSerializer.SerializeAsync(stream, snapshot, ExportJson, token),
            "application/json; charset=utf-8", "scadivo-portal-data.json");
    }
}
