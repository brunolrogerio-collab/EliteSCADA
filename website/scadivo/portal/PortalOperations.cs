using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

public sealed record LoginInput(string? Username, string? Password);
public sealed record AccountAccessInput(bool? Suspended);
public sealed record InviteInput(string? Username, string? DisplayName, string? Email, string? Organization);
public sealed record AcceptInviteInput(string? UserId, string? Token, string? Password, string? Locale, string? ProfileType,
    string? ActivityArea, string? ActivityAreaOther, string? OrganizationOrSchool, string? PhoneNumber,
    string? Address, DateOnly? DateOfBirth, bool? MarketingEmailOptIn);
public sealed record RegisterInput(string? Username, string? DisplayName, string? Email, string? Password, string? Locale, string? ProfileType,
    string? ActivityArea, string? ActivityAreaOther, string? OrganizationOrSchool, string? PhoneNumber,
    string? Address, DateOnly? DateOfBirth, bool? MarketingEmailOptIn);
public sealed record ConfirmEmailInput(string? UserId, string? Token);
public sealed record ResendConfirmationInput(string? Email);
public sealed record PasswordRecoveryInput(string? Email);
public sealed record ResetPasswordInput(string? UserId, string? Token, string? Password);
public sealed record ProfileInput(string? Username, string? DisplayName, string? Locale, string? ProfileType, string? ActivityArea,
    string? ActivityAreaOther, string? OrganizationOrSchool, string? PhoneNumber, string? Address,
    DateOnly? DateOfBirth, bool? MarketingEmailOptIn);
public sealed record PasswordInput(string? CurrentPassword, string? NewPassword);
public sealed record TicketInput(string? Subject, string? Category, string? Body);
public sealed record MessageInput(string? Body);
public sealed record TicketStatusInput(string? Status);
public sealed record TemplateInput(string? SubjectPt, string? SubjectEn, string? SubjectEs, string? BodyPt, string? BodyEn, string? BodyEs);
public sealed record FaqInput(string? Slug, string? Category, string? TitlePt, string? TitleEn, string? TitleEs, string? BodyPt, string? BodyEn, string? BodyEs, bool Published);
public sealed record PublicContactInput(string? Name, string? Email, string? Category, string? SubjectKey,
    string? OtherSubject, string? Message, string? Locale, string? Website);

public sealed class PortalOperations(
    PortalDb db, UserManager<PortalUser> users, SignInManager<PortalUser> signIn, IConfiguration configuration,
    IOptions<PortalEmailOptions> emailSettings, PortalEmailSignal emailSignal)
{
    private static readonly HashSet<string> ProfileTypes = ["student", "professional", "organization"];
    private static readonly HashSet<string> ActivityAreas = ["industrial-automation", "buildings-facilities", "residential", "utilities", "integrator-consulting", "education-research", "other"];
    private static string UserId(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static bool Owner(HttpContext context) => context.User.IsInRole("Owner");
    private static bool ValidLocale(string? value) => value is "pt-BR" or "en" or "es";
    private static bool Valid(string? value, int minimum, int maximum) => value?.Trim().Length >= minimum && value.Trim().Length <= maximum;
    private static IResult Invalid(string code = "invalid-input") => Results.BadRequest(new { error = code });

    public async Task<IResult> Login(LoginInput input, HttpContext context)
    {
        if (!Valid(input.Username, 1, 256) || input.Password is null || input.Password.Length > 1024) return Invalid();
        var username = input.Username!.Trim();
        var user = await users.FindByEmailAsync(username) ?? await users.FindByNameAsync(username);
        if (user is null || !user.InvitationAccepted || user.IsSuspended)
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
        return new
        {
            user.Id, user.UserName, user.DisplayName, user.Email, user.EmailConfirmed, user.Locale,
            user.ProfileType, user.ActivityArea, user.ActivityAreaOther, user.OrganizationOrSchool,
            user.PhoneNumber, user.Address, user.DateOfBirth, user.MarketingEmailOptIn,
            profileComplete = user.ProfileComplete, owner = Owner(context), organizations
        };
    }

    public async Task<IResult> UpdateProfile(ProfileInput input, HttpContext context)
    {
        if (!Valid(input.DisplayName, 1, 160) || !ValidLocale(input.Locale)) return Invalid();
        var user = await users.GetUserAsync(context.User);
        if (user is null) return Results.Unauthorized();
        if (input.Username is not null)
        {
            var username = input.Username.Trim();
            if (!CommunityUsernames.IsValid(username)) return Invalid("username-requirements");
            var usernameOwner = await users.FindByNameAsync(username);
            if (usernameOwner is not null && usernameOwner.Id != user.Id)
                return Results.Conflict(new { error = "account-already-exists" });
            user.UserName = username;
        }
        var profileType = input.ProfileType ?? user.ProfileType;
        var activityArea = input.ActivityArea ?? user.ActivityArea;
        var activityAreaOther = input.ActivityAreaOther ?? user.ActivityAreaOther;
        var organizationOrSchool = input.OrganizationOrSchool ?? user.OrganizationOrSchool;
        var phoneNumber = input.PhoneNumber ?? user.PhoneNumber;
        var address = input.Address ?? user.Address;
        var marketingEmailOptIn = input.MarketingEmailOptIn ?? user.MarketingEmailOptIn;
        if (!ValidProfile(profileType, activityArea, activityAreaOther, organizationOrSchool, phoneNumber, address, input.DateOfBirth)) return Invalid("profile-fields-required");
        user.DisplayName = input.DisplayName!.Trim();
        user.Locale = input.Locale!;
        user.ProfileType = profileType;
        user.ActivityArea = activityArea;
        user.ActivityAreaOther = activityArea == "other" ? activityAreaOther!.Trim() : null;
        user.OrganizationOrSchool = organizationOrSchool.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        user.Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        if (input.DateOfBirth is not null) user.DateOfBirth = input.DateOfBirth;
        if (marketingEmailOptIn && !user.MarketingEmailOptIn) user.MarketingEmailOptInAtUtc = DateTimeOffset.UtcNow;
        else if (!marketingEmailOptIn) user.MarketingEmailOptInAtUtc = null;
        user.MarketingEmailOptIn = marketingEmailOptIn;
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

    public async Task<IResult> Register(RegisterInput input)
    {
        if (!configuration.GetValue<bool>("Portal:PublicRegistrationEnabled")) return Results.NotFound();
        if (!emailSettings.Value.Enabled) return Results.Json(new { error = "email-not-configured" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        var email = input.Email?.Trim();
        var username = input.Username?.Trim();
        if (input.Password is null || input.Password.Length < 8 || input.Password.Length > 1024)
            return Invalid("password-requirements");
        if (!CommunityUsernames.IsValid(username)) return Invalid("username-requirements");
        if (!Valid(input.DisplayName, 1, 160) || !ValidLocale(input.Locale) || !Valid(email, 5, 256) || !MailAddress.TryCreate(email, out var parsedEmail) ||
            !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase) ||
            !ValidProfile(input.ProfileType, input.ActivityArea, input.ActivityAreaOther, input.OrganizationOrSchool,
                input.PhoneNumber, input.Address, input.DateOfBirth)) return Invalid("profile-fields-required");

        // Keep the response identical for an existing address so public registration cannot enumerate accounts.
        if (await users.FindByEmailAsync(email!) is not null) return Results.Accepted(uri: null, value: new { status = "check-email" });
        if (await users.FindByNameAsync(username!) is not null) return Results.Conflict(new { error = "account-already-exists" });

        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new PortalUser
        {
            UserName = username!,
            Email = email,
            DisplayName = input.DisplayName!.Trim(),
            Locale = input.Locale!,
            EmailConfirmed = false,
            InvitationAccepted = false,
            ProfileType = input.ProfileType!,
            ActivityArea = input.ActivityArea!,
            ActivityAreaOther = input.ActivityArea == "other" ? input.ActivityAreaOther!.Trim() : null,
            OrganizationOrSchool = input.OrganizationOrSchool!.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim(),
            Address = string.IsNullOrWhiteSpace(input.Address) ? null : input.Address.Trim(),
            DateOfBirth = input.DateOfBirth,
            MarketingEmailOptIn = input.MarketingEmailOptIn == true,
            MarketingEmailOptInAtUtc = input.MarketingEmailOptIn == true ? DateTimeOffset.UtcNow : null
        };
        var created = await users.CreateAsync(user, input.Password);
        if (!created.Succeeded)
        {
            if (created.Errors.Any(error => error.Code == "DuplicateEmail"))
                return Results.Accepted(uri: null, value: new { status = "check-email" });
            if (created.Errors.Any(error => error.Code == "DuplicateUserName"))
                return Results.Conflict(new { error = "account-already-exists" });
            return Invalid("password-requirements");
        }
        PortalSeed.Ensure(await users.AddToRoleAsync(user, "Member"));
        db.EmailOutbox.Add(Notification(user.Id, "account.confirmation", user.Id));
        db.Audit.Add(new PortalAudit { ActorId = user.Id, Action = "account.registered" });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        emailSignal.Notify();
        return Results.Accepted(uri: null, value: new { status = "check-email" });
    }

    public async Task<IResult> ConfirmEmail(ConfirmEmailInput input)
    {
        if (!Valid(input.UserId, 1, 100) || !Valid(input.Token, 1, 4096)) return Invalid("invalid-confirmation");
        var user = await users.FindByIdAsync(input.UserId!);
        if (user?.Email is null || user.EmailConfirmed) return Invalid("invalid-confirmation");
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(input.Token!)); }
        catch (FormatException) { return Invalid("invalid-confirmation"); }
        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await users.ConfirmEmailAsync(user, token);
        if (!result.Succeeded) return Invalid("invalid-confirmation");
        user.InvitationAccepted = true;
        PortalSeed.Ensure(await users.UpdateAsync(user));
        db.Audit.Add(new PortalAudit { ActorId = user.Id, Action = "account.email.confirmed" });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Ok(new { confirmed = true });
    }

    public async Task<IResult> ResendConfirmation(ResendConfirmationInput input)
    {
        var email = input.Email?.Trim();
        if (!Valid(email, 5, 256) || !MailAddress.TryCreate(email, out var parsedEmail) ||
            !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase)) return Invalid("invalid-email");
        var user = await users.FindByEmailAsync(email!);
        if (emailSettings.Value.Enabled && user is not null && !user.EmailConfirmed)
        {
            var cooldown = DateTimeOffset.UtcNow.AddMinutes(-2);
            var activeStatuses = new[] { "queued", "retry", "sent" };
            var alreadyQueued = await db.EmailOutbox.AnyAsync(x => x.UserId == user.Id && x.TemplateKey == "account.confirmation" &&
                x.CreatedAtUtc >= cooldown && activeStatuses.Contains(x.Status));
            if (!alreadyQueued)
            {
                db.EmailOutbox.Add(Notification(user.Id, "account.confirmation", user.Id));
                await db.SaveChangesAsync();
                emailSignal.Notify();
            }
        }
        return Results.Accepted(uri: null, value: new { status = "check-email" });
    }

    public async Task<IResult> RequestPasswordRecovery(PasswordRecoveryInput input)
    {
        // Always return the same result, including for malformed, unknown, unconfirmed,
        // or ineligible addresses, so this endpoint cannot be used to enumerate accounts.
        var email = input.Email?.Trim();
        if (emailSettings.Value.Enabled && Valid(email, 5, 256) && MailAddress.TryCreate(email, out var parsedEmail) &&
            string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            var user = await users.FindByEmailAsync(email!);
            if (user is not null && user.EmailConfirmed && user.InvitationAccepted && !user.IsSuspended)
            {
                var cooldown = DateTimeOffset.UtcNow.AddMinutes(-15);
                var activeStatuses = new[] { "queued", "retry", "sent" };
                var alreadyQueued = await db.EmailOutbox.AnyAsync(x => x.UserId == user.Id && x.TemplateKey == "account.password-reset" &&
                    x.CreatedAtUtc >= cooldown && activeStatuses.Contains(x.Status));
                if (!alreadyQueued)
                {
                    db.EmailOutbox.Add(Notification(user.Id, "account.password-reset", user.Id));
                    await db.SaveChangesAsync();
                    emailSignal.Notify();
                }
            }
        }
        return Results.Accepted(uri: null, value: new { status = "check-email" });
    }

    public async Task<IResult> ResetPassword(ResetPasswordInput input)
    {
        if (!Valid(input.UserId, 1, 100) || !Valid(input.Token, 1, 4096) || input.Password is null || input.Password.Length > 1024)
            return Invalid("invalid-password-reset");
        var user = await users.FindByIdAsync(input.UserId!);
        if (user?.Email is null || !user.EmailConfirmed || !user.InvitationAccepted || user.IsSuspended)
            return Invalid("invalid-password-reset");
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(input.Token!)); }
        catch (FormatException) { return Invalid("invalid-password-reset"); }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await users.ResetPasswordAsync(user, token, input.Password);
        if (!result.Succeeded)
        {
            var passwordErrors = new HashSet<string>(StringComparer.Ordinal)
            {
                "PasswordTooShort", "PasswordRequiresUniqueChars", "PasswordRequiresNonAlphanumeric",
                "PasswordRequiresDigit", "PasswordRequiresUpper", "PasswordRequiresLower"
            };
            return result.Errors.Any(error => passwordErrors.Contains(error.Code))
                ? Invalid("password-requirements")
                : Invalid("invalid-password-reset");
        }
        await AuditAsync(user.Id, "auth.password.recovered");
        await transaction.CommitAsync();
        return Results.Ok(new { reset = true });
    }

    public async Task<IResult> Invite(InviteInput input, HttpContext context)
    {
        if (!CommunityUsernames.IsValid(input.Username) || !Valid(input.DisplayName, 1, 160) || (input.Organization?.Length ?? 0) > 200) return Invalid();
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
        db.EmailOutbox.Add(Notification(user.Id, "account.invitation", user.Id));
        db.Audit.Add(new PortalAudit { ActorId = UserId(context), Action = "account.invited", ResourceId = user.Id });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        emailSignal.Notify();
        var publicUrl = configuration["Portal:PublicUrl"] ?? "https://scadivo.com/portal/";
        return Results.Created($"/portal/api/admin/accounts/{user.Id}", new
        {
            user.Id, user.UserName,
            invitationUrl = await PortalInvitations.LinkAsync(users, user, publicUrl),
            expiresInHours = 24,
            emailDeliveryConfigured = emailSettings.Value.Enabled,
            emailQueued = emailSettings.Value.Enabled && email is not null
        });
    }

    public async Task<IResult> AcceptInvitation(AcceptInviteInput input)
    {
        if (!Valid(input.UserId, 1, 100) || !Valid(input.Token, 1, 4096) || input.Password is null || input.Password.Length > 1024 || !ValidLocale(input.Locale)) return Invalid();
        var user = await users.FindByIdAsync(input.UserId!);
        if (user is null || user.InvitationAccepted) return Invalid("invalid-invitation");
        if (!ValidProfile(input.ProfileType, input.ActivityArea, input.ActivityAreaOther, input.OrganizationOrSchool,
            input.PhoneNumber, input.Address, input.DateOfBirth)) return Invalid("profile-fields-required");
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(input.Token!)); }
        catch (FormatException) { return Invalid("invalid-invitation"); }
        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await users.ResetPasswordAsync(user, token, input.Password);
        if (!result.Succeeded) return Invalid("invalid-invitation-or-password");
        user.InvitationAccepted = true;
        user.Locale = input.Locale!;
        user.ProfileType = input.ProfileType!;
        user.ActivityArea = input.ActivityArea!;
        user.ActivityAreaOther = input.ActivityArea == "other" ? input.ActivityAreaOther!.Trim() : null;
        user.OrganizationOrSchool = input.OrganizationOrSchool!.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
        user.Address = string.IsNullOrWhiteSpace(input.Address) ? null : input.Address.Trim();
        user.DateOfBirth = input.DateOfBirth;
        user.MarketingEmailOptIn = input.MarketingEmailOptIn == true;
        user.MarketingEmailOptInAtUtc = input.MarketingEmailOptIn == true ? DateTimeOffset.UtcNow : null;
        if (!string.IsNullOrWhiteSpace(user.Email)) user.EmailConfirmed = true;
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
        db.EmailOutbox.Add(Notification(id, "support.created", ticket.Id.ToString()));
        db.EmailOutbox.Add(TeamNotification("support.team-ticket", ticket.Id, ticket.Category));
        db.Audit.Add(new PortalAudit { ActorId = id, Action = "support.created", ResourceId = ticket.Id.ToString() });
        await db.SaveChangesAsync();
        emailSignal.Notify();
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
        if (staff) { ticket.Status = "answered"; db.EmailOutbox.Add(Notification(ticket.UserId, "support.replied", id.ToString())); }
        else { ticket.Status = "open"; db.EmailOutbox.Add(TeamNotification("support.team-reply", ticket.Id, ticket.Category)); }
        db.Audit.Add(new PortalAudit { ActorId = UserId(context), Action = "support.replied", ResourceId = id.ToString() });
        await db.SaveChangesAsync();
        emailSignal.Notify();
        return Results.Ok(new { replied = true });
    }

    public async Task<IResult> PublicContact(PublicContactInput input)
    {
        if (!emailSettings.Value.Enabled) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        if (!string.IsNullOrWhiteSpace(input.Website)) return ContactAccepted("SC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant());
        var name = input.Name?.Trim();
        var email = input.Email?.Trim();
        var message = input.Message?.Trim();
        var otherSubject = input.OtherSubject?.Trim();
        if (!Valid(name, 1, 100) || !Valid(email, 5, 256) || !MailAddress.TryCreate(email, out var parsedEmail) ||
            !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase) || !Valid(message, 10, 4000)) return Invalid();

        var locale = input.Locale is "en" or "es" ? input.Locale : "pt-BR";
        var category = input.Category;
        var subjectKey = input.SubjectKey;
        var allowedSubjects = category switch
        {
            "commercial" => new[] { "commercial-info", "license", "demo", "partnership", "other" },
            "support" => new[] { "installation", "configuration", "integration", "technical-problem", "other" },
            "contact" => new[] { "general-info", "partnership", "press", "privacy", "other" },
            _ => []
        };
        if (subjectKey is null || !allowedSubjects.Contains(subjectKey, StringComparer.Ordinal) ||
            (subjectKey == "other" ? !Valid(otherSubject, 3, 120) : !string.IsNullOrWhiteSpace(otherSubject))) return Invalid();
        if (await db.EmailOutbox.CountAsync(x => x.TemplateKey.StartsWith("contact.") && (x.Status == "queued" || x.Status == "retry")) >= 100)
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

        var categoryName = (category, locale) switch
        {
            ("commercial", "en") => "Sales",
            ("commercial", "es") => "Ventas",
            ("support", "en") => "Support",
            ("support", "es") => "Soporte",
            ("contact", "en") => "General contact",
            ("contact", "es") => "Contacto general",
            ("commercial", _) => "Comercial",
            ("support", _) => "Suporte",
            _ => "Contato geral"
        };
        var subjectName = subjectKey == "other" ? otherSubject! : (subjectKey, locale) switch
        {
            ("commercial-info", "en") => "Commercial information", ("commercial-info", "es") => "Información comercial", ("commercial-info", _) => "Informações comerciais",
            ("license", "en") => "Licensing", ("license", "es") => "Licencias", ("license", _) => "Licenciamento",
            ("demo", "en") => "Product demonstration", ("demo", "es") => "Demostración del producto", ("demo", _) => "Demonstração do produto",
            ("partnership", "en") => "Partnership", ("partnership", "es") => "Asociación", ("partnership", _) => "Parceria",
            ("installation", "en") => "Installation", ("installation", "es") => "Instalación", ("installation", _) => "Instalação",
            ("configuration", "en") => "Configuration", ("configuration", "es") => "Configuración", ("configuration", _) => "Configuração",
            ("integration", "en") => "Integration", ("integration", "es") => "Integración", ("integration", _) => "Integração",
            ("technical-problem", "en") => "Technical issue", ("technical-problem", "es") => "Problema técnico", ("technical-problem", _) => "Problema técnico",
            ("general-info", "en") => "General information", ("general-info", "es") => "Información general", ("general-info", _) => "Informações gerais",
            ("press", "en") => "Press", ("press", "es") => "Prensa", ("press", _) => "Imprensa",
            ("privacy", "en") => "Privacy", ("privacy", "es") => "Privacidad", _ => "Privacidade"
        };
        var recipient = category switch
        {
            "commercial" => "sales@scadivo.com",
            "support" => "support@scadivo.com",
            _ => "contact@scadivo.com"
        };
        var reference = "SC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var notificationData = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["name"] = name!, ["email"] = email!, ["category"] = categoryName,
            ["subject"] = subjectName, ["message"] = message!, ["reference"] = reference,
            ["locale"] = locale
        });
        var receiptData = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["name"] = name!, ["category"] = categoryName, ["subject"] = subjectName,
            ["reference"] = reference, ["locale"] = locale
        });
        db.EmailOutbox.Add(new EmailOutbox
        {
            TemplateKey = "contact.notification", RecipientAddress = recipient, ReplyToAddress = email,
            TemplateDataJson = notificationData, Reference = reference, Status = "queued"
        });
        db.EmailOutbox.Add(new EmailOutbox
        {
            TemplateKey = "contact.received", RecipientAddress = email, ReplyToAddress = recipient,
            TemplateDataJson = receiptData, Reference = reference, Status = "queued"
        });
        await db.SaveChangesAsync();
        emailSignal.Notify();
        return ContactAccepted(reference);
    }

    private static IResult ContactAccepted(string reference) => Results.Json(new { accepted = true, reference }, statusCode: 202);

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
            query = query.Where(x => EF.Functions.ILike(x.TitlePt, match) || EF.Functions.ILike(x.TitleEn, match) || EF.Functions.ILike(x.TitleEs, match) ||
                EF.Functions.ILike(x.BodyPt, match) || EF.Functions.ILike(x.BodyEn, match) || EF.Functions.ILike(x.BodyEs, match));
        }
        var language = lang is "en" or "es" ? lang : "pt-BR";
        return Results.Ok(await query.OrderBy(x => x.Category).ThenBy(x => x.Slug).Take(80)
            .Select(x => new { x.Id, x.Slug, x.Category, title = language == "en" ? x.TitleEn : language == "es" ? x.TitleEs : x.TitlePt,
                body = language == "en" ? x.BodyEn : language == "es" ? x.BodyEs : x.BodyPt }).ToListAsync());
    }

    public async Task<IResult> Releases() => Results.Ok(await db.Releases.AsNoTracking().Where(x => x.Published)
        .OrderByDescending(x => x.CreatedAtUtc).Take(40).Select(x => new { x.Id, x.Version, x.Platform, x.NotesPt, x.NotesEn, x.NotesEs, x.Sha256, x.CreatedAtUtc }).ToListAsync());

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
        queuedMessages = await db.EmailOutbox.CountAsync(x => x.Status == "queued" || x.Status == "retry"),
        recordedMessages = await db.EmailOutbox.CountAsync(x => x.Status == "awaiting-transport"),
        sentMessages = await db.EmailOutbox.CountAsync(x => x.Status == "sent"),
        failedMessages = await db.EmailOutbox.CountAsync(x => x.Status == "failed"),
        publishedDownloads = await db.Releases.CountAsync(x => x.Published),
        emailDeliveryConfigured = emailSettings.Value.Enabled,
        publicRegistrationEnabled = configuration.GetValue<bool>("Portal:PublicRegistrationEnabled"),
        licenseIntegrationEnabled = false
    });

    public async Task<IResult> Accounts()
    {
        var ownerIds = await (from membership in db.UserRoles
            join role in db.Roles on membership.RoleId equals role.Id
            where role.Name == "Owner"
            select membership.UserId).ToArrayAsync();
        return Results.Ok(await db.Users.AsNoTracking().OrderBy(x => x.UserName).Take(100)
            .Select(x => new { x.Id, x.UserName, x.DisplayName, x.Email, x.EmailConfirmed, x.InvitationAccepted, x.IsSuspended,
                owner = ownerIds.Contains(x.Id), profileComplete = x.ProfileType != "" && x.ActivityArea != "" && x.OrganizationOrSchool != "", x.CreatedAtUtc }).ToListAsync());
    }

    public async Task<IResult> UpdateAccountAccess(string id, AccountAccessInput input, HttpContext context)
    {
        if (input.Suspended is null || !Valid(id, 1, 100)) return Invalid();
        var account = await users.FindByIdAsync(id);
        if (account is null) return Results.NotFound();
        if (await users.IsInRoleAsync(account, "Owner"))
            return Results.Conflict(new { error = "owner-account-protected" });
        if (account.IsSuspended == input.Suspended.Value)
            return Results.Ok(new { account.Id, account.IsSuspended });

        await using var transaction = await db.Database.BeginTransactionAsync();
        account.IsSuspended = input.Suspended.Value;
        var updated = await users.UpdateSecurityStampAsync(account);
        if (!updated.Succeeded) return Results.Conflict(new { error = "account-not-updated" });
        await AuditAsync(UserId(context), account.IsSuspended ? "account.suspended" : "account.reactivated", account.Id);
        await transaction.CommitAsync();
        return Results.Ok(new { account.Id, account.IsSuspended });
    }

    public async Task<IResult> UpdateTemplate(string key, TemplateInput input, HttpContext context)
    {
        if (!Valid(input.SubjectPt, 1, 200) || !Valid(input.SubjectEn, 1, 200) || !Valid(input.SubjectEs, 1, 200) ||
            !Valid(input.BodyPt, 1, 8000) || !Valid(input.BodyEn, 1, 8000) || !Valid(input.BodyEs, 1, 8000)) return Invalid();
        var template = await db.EmailTemplates.FindAsync(key);
        if (template is null) return Results.NotFound();
        template.SubjectPt = input.SubjectPt!.Trim(); template.SubjectEn = input.SubjectEn!.Trim(); template.SubjectEs = input.SubjectEs!.Trim();
        template.BodyPt = input.BodyPt!.Trim(); template.BodyEn = input.BodyEn!.Trim(); template.BodyEs = input.BodyEs!.Trim();
        template.UpdatedAtUtc = DateTimeOffset.UtcNow;
        db.Audit.Add(new PortalAudit { ActorId = UserId(context), Action = "message-template.updated", ResourceId = key });
        await db.SaveChangesAsync();
        return Results.Ok(template);
    }

    public async Task<IResult> SaveFaq(Guid? id, FaqInput input, HttpContext context)
    {
        if (!Valid(input.Slug, 1, 100) || !Regex.IsMatch(input.Slug!, "^[a-z0-9-]+$") || !Valid(input.TitlePt, 1, 200) ||
            !Valid(input.TitleEn, 1, 200) || !Valid(input.TitleEs, 1, 200) || !Valid(input.BodyPt, 1, 12000) ||
            !Valid(input.BodyEn, 1, 12000) || !Valid(input.BodyEs, 1, 12000) || !Valid(input.Category, 1, 60)) return Invalid();
        var article = id.HasValue ? await db.FaqArticles.FindAsync(id.Value) : new FaqArticle();
        if (article is null) return Results.NotFound();
        if (await db.FaqArticles.AnyAsync(x => x.Slug == input.Slug && x.Id != article.Id)) return Results.Conflict(new { error = "slug-already-exists" });
        article.Slug = input.Slug!; article.Category = input.Category!; article.TitlePt = input.TitlePt!.Trim(); article.TitleEn = input.TitleEn!.Trim(); article.TitleEs = input.TitleEs!.Trim();
        article.BodyPt = input.BodyPt!.Trim(); article.BodyEn = input.BodyEn!.Trim(); article.BodyEs = input.BodyEs!.Trim(); article.Published = input.Published;
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

    private EmailOutbox Notification(string userId, string template, string reference) => new()
    {
        UserId = userId, TemplateKey = template, Reference = reference,
        Status = emailSettings.Value.Enabled ? "queued" : "awaiting-transport"
    };

    private EmailOutbox TeamNotification(string template, Guid ticketId, string category) => new()
    {
        RecipientAddress = emailSettings.Value.ReplyToAddress,
        TemplateKey = template,
        Reference = ticketId.ToString(),
        TemplateDataJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["locale"] = "pt-BR",
            ["category"] = category
        }),
        Status = emailSettings.Value.Enabled ? "queued" : "awaiting-transport"
    };

    private static bool ValidProfile(string? profileType, string? activityArea, string? activityAreaOther,
        string? organizationOrSchool, string? phoneNumber, string? address, DateOnly? dateOfBirth) =>
        ProfileTypes.Contains(profileType ?? "") && ActivityAreas.Contains(activityArea ?? "") &&
        (activityArea != "other" || Valid(activityAreaOther, 2, 120)) &&
        Valid(organizationOrSchool, 1, 200) &&
        ((phoneNumber?.Length ?? 0) <= 40 && (phoneNumber is null || Regex.IsMatch(phoneNumber, @"^[+0-9() .-]*$"))) &&
        (address?.Length ?? 0) <= 300 &&
        (dateOfBirth is null || dateOfBirth.Value <= DateOnly.FromDateTime(DateTime.UtcNow));
}
