using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

public sealed record LoginInput(string? Username, string? Password);
public sealed record InviteInput(string? Username, string? DisplayName, string? Email, string? Organization);
public sealed record AcceptInviteInput(string? UserId, string? Token, string? Password);
public sealed record ProfileInput(string? DisplayName, string? Locale);
public sealed record PasswordInput(string? CurrentPassword, string? NewPassword);
public sealed record TicketInput(string? Subject, string? Category, string? Body);
public sealed record MessageInput(string? Body);
public sealed record TicketStatusInput(string? Status);
public sealed record TemplateInput(string? SubjectPt, string? SubjectEn, string? BodyPt, string? BodyEn);
public sealed record FaqInput(string? Slug, string? Category, string? TitlePt, string? TitleEn, string? BodyPt, string? BodyEn, bool Published);

public sealed class PortalOperations(
    PortalDb db, UserManager<PortalUser> users, SignInManager<PortalUser> signIn, IConfiguration configuration)
{
    private static string UserId(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static bool Owner(HttpContext context) => context.User.IsInRole("Owner");
    private static bool Valid(string? value, int minimum, int maximum) => value?.Trim().Length >= minimum && value.Trim().Length <= maximum;
    private static IResult Invalid(string code = "invalid-input") => Results.BadRequest(new { error = code });

    public async Task<IResult> Login(LoginInput input, HttpContext context)
    {
        if (!Valid(input.Username, 1, 256) || input.Password is null || input.Password.Length > 1024) return Invalid();
        var user = await users.FindByNameAsync(input.Username!.Trim());
        if (user is null || !user.InvitationAccepted)
            return Results.Json(new { error = "invalid-credentials" }, statusCode: 401);
        var result = await signIn.PasswordSignInAsync(user, input.Password, false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            await AuditAsync(null, "auth.login.denied");
            return Results.Json(new { error = "invalid-credentials" }, statusCode: 401);
        }
        await AuditAsync(user.Id, "auth.login");
        return Results.Ok(new { signedIn = true });
    }

    public async Task<IResult> Logout(HttpContext context)
    {
        await AuditAsync(UserId(context), "auth.logout");
        await signIn.SignOutAsync();
        return Results.Ok(new { signedOut = true });
    }

    public async Task<object?> Profile(HttpContext context)
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null) return null;
        var organizations = await (from membership in db.Memberships.AsNoTracking()
            join organization in db.Organizations on membership.OrganizationId equals organization.Id
            where membership.UserId == user.Id
            select new { organization.Id, organization.Name, membership.Role }).ToListAsync();
        return new { user.Id, user.UserName, user.DisplayName, user.Email, user.EmailConfirmed, user.Locale, owner = Owner(context), organizations };
    }

    public async Task<IResult> UpdateProfile(ProfileInput input, HttpContext context)
    {
        if (!Valid(input.DisplayName, 1, 160) || input.Locale is not ("pt-BR" or "en")) return Invalid();
        var user = await users.GetUserAsync(context.User);
        if (user is null) return Results.Unauthorized();
        user.DisplayName = input.DisplayName!.Trim();
        user.Locale = input.Locale;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) return Invalid();
        await AuditAsync(user.Id, "profile.updated");
        return Results.Ok(await Profile(context));
    }

    public async Task<IResult> ChangePassword(PasswordInput input, HttpContext context)
    {
        if (input.CurrentPassword is null || input.NewPassword is null || input.CurrentPassword.Length > 1024 || input.NewPassword.Length > 1024) return Invalid();
        var user = await users.GetUserAsync(context.User);
        if (user is null) return Results.Unauthorized();
        var result = await users.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
        if (!result.Succeeded) return Invalid("password-not-changed");
        await signIn.RefreshSignInAsync(user);
        await AuditAsync(user.Id, "auth.password.changed");
        return Results.Ok(new { changed = true });
    }

    public async Task<IResult> Invite(InviteInput input, HttpContext context)
    {
        if (!Valid(input.Username, 3, 100) || !Valid(input.DisplayName, 1, 160) || (input.Organization?.Length ?? 0) > 200) return Invalid();
        var email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim();
        if (email is not null && (!MailAddress.TryCreate(email, out var parsed) || !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))) return Invalid("invalid-email");
        if (await users.FindByNameAsync(input.Username!.Trim()) is not null || (email is not null && await users.FindByEmailAsync(email) is not null))
            return Results.Conflict(new { error = "account-already-exists" });

        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new PortalUser { UserName = input.Username.Trim(), DisplayName = input.DisplayName!.Trim(), Email = email };
        var result = await users.CreateAsync(user);
        if (!result.Succeeded) return Invalid("account-not-created");
        PortalSeed.Ensure(await users.AddToRoleAsync(user, "Member"));
        if (!string.IsNullOrWhiteSpace(input.Organization))
        {
            var name = input.Organization.Trim();
            var organization = await db.Organizations.FirstOrDefaultAsync(x => x.Name == name);
            if (organization is null) { organization = new Organization { Name = name }; db.Organizations.Add(organization); }
            db.Memberships.Add(new Membership { OrganizationId = organization.Id, UserId = user.Id });
        }
        db.EmailOutbox.Add(new EmailOutbox { UserId = user.Id, TemplateKey = "account.invitation", Reference = user.Id });
        db.Audit.Add(new PortalAudit { ActorId = UserId(context), Action = "account.invited", ResourceId = user.Id });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(user)));
        var payload = WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { userId = user.Id, token }));
        var publicUrl = configuration["Portal:PublicUrl"] ?? "https://scadivo.com/portal/";
        return Results.Created($"/portal/api/admin/accounts/{user.Id}", new
        {
            user.Id, user.UserName,
            invitationUrl = $"{publicUrl.TrimEnd('/')}/#invite={payload}",
            expiresInHours = 24,
            emailDeliveryConfigured = false
        });
    }

    public async Task<IResult> AcceptInvitation(AcceptInviteInput input)
    {
        if (!Valid(input.UserId, 1, 100) || !Valid(input.Token, 1, 4096) || input.Password is null || input.Password.Length > 1024) return Invalid();
        var user = await users.FindByIdAsync(input.UserId!);
        if (user is null || user.InvitationAccepted) return Invalid("invalid-invitation");
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(input.Token!)); }
        catch (FormatException) { return Invalid("invalid-invitation"); }
        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await users.ResetPasswordAsync(user, token, input.Password);
        if (!result.Succeeded) return Invalid("invalid-invitation-or-password");
        user.InvitationAccepted = true;
        PortalSeed.Ensure(await users.UpdateAsync(user));
        db.Audit.Add(new PortalAudit { ActorId = user.Id, Action = "account.invitation.accepted" });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Ok(new { accepted = true });
    }

    public async Task<IResult> Tickets(HttpContext context)
    {
        var userId = UserId(context);
        var query = db.Tickets.AsNoTracking();
        if (!Owner(context)) query = query.Where(x => x.UserId == userId);
        return Results.Ok(await query.OrderByDescending(x => x.UpdatedAtUtc).Take(100).ToListAsync());
    }

    public async Task<IResult> Ticket(Guid id, HttpContext context)
    {
        var ticket = await AccessibleTicket(id, context);
        if (ticket is null) return Results.NotFound();
        var messages = await db.TicketMessages.AsNoTracking().Where(x => x.TicketId == id)
            .OrderBy(x => x.CreatedAtUtc).Take(100).Select(x => new { x.Id, x.Body, x.StaffReply, x.CreatedAtUtc }).ToListAsync();
        return Results.Ok(new { ticket, messages });
    }

    public async Task<IResult> CreateTicket(TicketInput input, HttpContext context)
    {
        if (!Valid(input.Subject, 3, 180) || !Valid(input.Body, 10, 8000) || input.Category is not ("technical" or "commercial" or "account")) return Invalid();
        var id = UserId(context);
        var ticket = new SupportTicket { UserId = id, Subject = input.Subject!.Trim(), Category = input.Category };
        db.Tickets.Add(ticket);
        db.TicketMessages.Add(new TicketMessage { TicketId = ticket.Id, UserId = id, Body = input.Body!.Trim(), StaffReply = false });
        db.EmailOutbox.Add(new EmailOutbox { UserId = id, TemplateKey = "support.created", Reference = ticket.Id.ToString() });
        db.Audit.Add(new PortalAudit { ActorId = id, Action = "support.created", ResourceId = ticket.Id.ToString() });
        await db.SaveChangesAsync();
        return Results.Created($"/portal/api/support/{ticket.Id}", ticket);
    }

    public async Task<IResult> Reply(Guid id, MessageInput input, HttpContext context)
    {
        if (!Valid(input.Body, 1, 8000)) return Invalid();
        var ticket = await AccessibleTicket(id, context);
        if (ticket is null) return Results.NotFound();
        var staff = Owner(context);
        db.TicketMessages.Add(new TicketMessage { TicketId = id, UserId = UserId(context), Body = input.Body!.Trim(), StaffReply = staff });
        ticket.UpdatedAtUtc = DateTimeOffset.UtcNow;
        if (staff) { ticket.Status = "answered"; db.EmailOutbox.Add(new EmailOutbox { UserId = ticket.UserId, TemplateKey = "support.replied", Reference = id.ToString() }); }
        else ticket.Status = "open";
        db.Audit.Add(new PortalAudit { ActorId = UserId(context), Action = "support.replied", ResourceId = id.ToString() });
        await db.SaveChangesAsync();
        return Results.Ok(new { replied = true });
    }

    public async Task<IResult> UpdateTicketStatus(Guid id, TicketStatusInput input, HttpContext context)
    {
        if (input.Status is not ("open" or "answered" or "closed")) return Invalid();
        var ticket = await db.Tickets.FindAsync(id);
        if (ticket is null) return Results.NotFound();
        ticket.Status = input.Status;
        ticket.UpdatedAtUtc = DateTimeOffset.UtcNow;
        db.Audit.Add(new PortalAudit { ActorId = UserId(context), Action = "support.status", ResourceId = id.ToString() });
        await db.SaveChangesAsync();
        return Results.Ok(ticket);
    }

    private async Task<SupportTicket?> AccessibleTicket(Guid id, HttpContext context)
    {
        var userId = UserId(context);
        return await db.Tickets.FirstOrDefaultAsync(x => x.Id == id && (Owner(context) || x.UserId == userId));
    }

    public async Task<IResult> Faq(string? q, string? lang)
    {
        var query = db.FaqArticles.AsNoTracking().Where(x => x.Published);
        var search = q?.Trim();
        if (search?.Length > 120) return Invalid();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var match = $"%{search.Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(x => EF.Functions.ILike(x.TitlePt, match) || EF.Functions.ILike(x.TitleEn, match) || EF.Functions.ILike(x.BodyPt, match) || EF.Functions.ILike(x.BodyEn, match));
        }
        var english = lang == "en";
        return Results.Ok(await query.OrderBy(x => x.Category).ThenBy(x => x.Slug).Take(80)
            .Select(x => new { x.Id, x.Slug, x.Category, title = english ? x.TitleEn : x.TitlePt, body = english ? x.BodyEn : x.BodyPt }).ToListAsync());
    }

    public async Task<IResult> Releases() => Results.Ok(await db.Releases.AsNoTracking().Where(x => x.Published)
        .OrderByDescending(x => x.CreatedAtUtc).Take(40).Select(x => new { x.Id, x.Version, x.Platform, x.NotesPt, x.NotesEn, x.Sha256, x.CreatedAtUtc }).ToListAsync());

    public async Task<IResult> Download(Guid id, HttpContext context)
    {
        var release = await db.Releases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Published);
        if (release is null) return Results.NotFound();
        if (!Uri.TryCreate(release.OfficialUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com" ||
            !uri.AbsolutePath.StartsWith("/brunolrogerio-collab/EliteSCADA/releases/download/", StringComparison.Ordinal) ||
            !Regex.IsMatch(release.Sha256, "^[a-fA-F0-9]{64}$")) return Results.Conflict(new { error = "release-not-approved" });
        await AuditAsync(UserId(context), "download.requested", id.ToString());
        return Results.Redirect(release.OfficialUrl);
    }

    public async Task<IResult> Overview() => Results.Ok(new
    {
        accounts = await db.Users.CountAsync(),
        pendingInvitations = await db.Users.CountAsync(x => !x.InvitationAccepted),
        openTickets = await db.Tickets.CountAsync(x => x.Status != "closed"),
        queuedMessages = await db.EmailOutbox.CountAsync(x => x.Status == "awaiting-transport"),
        publishedDownloads = await db.Releases.CountAsync(x => x.Published),
        emailDeliveryConfigured = false,
        publicRegistrationEnabled = false,
        licenseIntegrationEnabled = false
    });

    public async Task<IResult> Accounts() => Results.Ok(await db.Users.AsNoTracking().OrderBy(x => x.UserName).Take(100)
        .Select(x => new { x.Id, x.UserName, x.DisplayName, x.Email, x.InvitationAccepted, x.CreatedAtUtc }).ToListAsync());

    public async Task<IResult> UpdateTemplate(string key, TemplateInput input, HttpContext context)
    {
        if (!Valid(input.SubjectPt, 1, 200) || !Valid(input.SubjectEn, 1, 200) || !Valid(input.BodyPt, 1, 8000) || !Valid(input.BodyEn, 1, 8000)) return Invalid();
        var template = await db.EmailTemplates.FindAsync(key);
        if (template is null) return Results.NotFound();
        template.SubjectPt = input.SubjectPt!.Trim(); template.SubjectEn = input.SubjectEn!.Trim();
        template.BodyPt = input.BodyPt!.Trim(); template.BodyEn = input.BodyEn!.Trim();
        template.UpdatedAtUtc = DateTimeOffset.UtcNow;
        db.Audit.Add(new PortalAudit { ActorId = UserId(context), Action = "message-template.updated", ResourceId = key });
        await db.SaveChangesAsync();
        return Results.Ok(template);
    }

    public async Task<IResult> SaveFaq(Guid? id, FaqInput input, HttpContext context)
    {
        if (!Valid(input.Slug, 1, 100) || !Regex.IsMatch(input.Slug!, "^[a-z0-9-]+$") || !Valid(input.TitlePt, 1, 200) ||
            !Valid(input.TitleEn, 1, 200) || !Valid(input.BodyPt, 1, 12000) || !Valid(input.BodyEn, 1, 12000) || !Valid(input.Category, 1, 60)) return Invalid();
        var article = id.HasValue ? await db.FaqArticles.FindAsync(id.Value) : new FaqArticle();
        if (article is null) return Results.NotFound();
        if (await db.FaqArticles.AnyAsync(x => x.Slug == input.Slug && x.Id != article.Id)) return Results.Conflict(new { error = "slug-already-exists" });
        article.Slug = input.Slug!; article.Category = input.Category!; article.TitlePt = input.TitlePt!.Trim(); article.TitleEn = input.TitleEn!.Trim();
        article.BodyPt = input.BodyPt!.Trim(); article.BodyEn = input.BodyEn!.Trim(); article.Published = input.Published;
        if (!id.HasValue) db.FaqArticles.Add(article);
        db.Audit.Add(new PortalAudit { ActorId = UserId(context), Action = "faq.updated", ResourceId = article.Id.ToString() });
        await db.SaveChangesAsync();
        return Results.Ok(article);
    }

    private async Task AuditAsync(string? actor, string action, string? resource = null)
    {
        db.Audit.Add(new PortalAudit { ActorId = actor, Action = action, ResourceId = resource });
        await db.SaveChangesAsync();
    }
}
