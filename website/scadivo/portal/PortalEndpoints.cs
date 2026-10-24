using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

public static class PortalEndpoints
{
    public static void MapPortalEndpoints(this WebApplication app)
    {
        app.MapGet("/api/session", async (HttpContext context, IAntiforgery antiforgery, PortalOperations operations,
            IOptions<PortalEmailOptions> emailSettings, IOptions<CommunitySsoOptions> communitySettings) =>
        {
            var token = antiforgery.GetAndStoreTokens(context);
            var community = communitySettings.Value;
            return Results.Ok(new
            {
                authenticated = context.User.Identity?.IsAuthenticated == true,
                user = context.User.Identity?.IsAuthenticated == true ? await operations.Profile(context) : null,
                csrfToken = token.RequestToken,
                publicRegistrationEnabled = context.RequestServices.GetRequiredService<IConfiguration>().GetValue<bool>("Portal:PublicRegistrationEnabled"),
                emailDeliveryConfigured = emailSettings.Value.Enabled,
                communityEnabled = community.Enabled,
                communityStartUrl = community.Enabled ? community.LoginStartUrl : null
            });
        });
        app.MapPost("/api/auth/login", (LoginInput input, HttpContext context, PortalOperations operations) => operations.Login(input, context)).RequireRateLimiting("auth");
        app.MapPost("/api/auth/accept-invitation", (AcceptInviteInput input, PortalOperations operations) => operations.AcceptInvitation(input)).RequireRateLimiting("auth");
        app.MapPost("/api/auth/register", (RegisterInput input, PortalOperations operations) => operations.Register(input)).RequireRateLimiting("auth");
        app.MapPost("/api/auth/confirm-email", (ConfirmEmailInput input, PortalOperations operations) => operations.ConfirmEmail(input)).RequireRateLimiting("auth");
        app.MapPost("/api/auth/confirmation/resend", (ResendConfirmationInput input, PortalOperations operations) => operations.ResendConfirmation(input)).RequireRateLimiting("auth");
        app.MapPost("/api/auth/password-recovery", (PasswordRecoveryInput input, PortalOperations operations) => operations.RequestPasswordRecovery(input)).RequireRateLimiting("password-recovery");
        app.MapPost("/api/auth/reset-password", (ResetPasswordInput input, PortalOperations operations) => operations.ResetPassword(input)).RequireRateLimiting("password-recovery");
        app.MapPost("/api/auth/logout", (HttpContext context, PortalOperations operations) => operations.Logout(context)).RequireAuthorization();
        app.MapPost("/api/public/contact", (PublicContactInput input, PortalOperations operations) => operations.PublicContact(input))
            .RequireRateLimiting("public-contact");
        app.MapGet("/api/community/sso", DiscourseConnect.HandleAsync)
            .RequireRateLimiting("community-sso");

        var account = app.MapGroup("/api").RequireAuthorization().RequireRateLimiting("writes");
        account.MapGet("/profile", (HttpContext context, PortalOperations operations) => operations.Profile(context));
        account.MapPut("/profile", (ProfileInput input, HttpContext context, PortalOperations operations) => operations.UpdateProfile(input, context));
        account.MapPost("/profile/password", (PasswordInput input, HttpContext context, PortalOperations operations) => operations.ChangePassword(input, context));

        var members = app.MapGroup("/api").RequireAuthorization().RequireRateLimiting("writes");
        members.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (http.User.IsInRole("Owner")) return await next(context);
            var id = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var users = http.RequestServices.GetRequiredService<UserManager<PortalUser>>();
            var user = id is null ? null : await users.FindByIdAsync(id);
            if (user is null) return Results.Unauthorized();
            return user.ProfileComplete
                ? await next(context)
                : Results.Json(new { error = "profile-incomplete" }, statusCode: StatusCodes.Status428PreconditionRequired);
        });
        members.MapGet("/faq", (string? q, string? lang, PortalOperations operations) => operations.Faq(q, lang));
        members.MapGet("/support", (HttpContext context, PortalOperations operations) => operations.Tickets(context));
        members.MapGet("/support/{id:guid}", (Guid id, HttpContext context, PortalOperations operations) => operations.Ticket(id, context));
        members.MapPost("/support", (TicketInput input, HttpContext context, PortalOperations operations) => operations.CreateTicket(input, context));
        members.MapPost("/support/{id:guid}/messages", (Guid id, MessageInput input, HttpContext context, PortalOperations operations) => operations.Reply(id, input, context));
        members.MapGet("/downloads", (PortalOperations operations) => operations.Releases());
        members.MapGet("/downloads/{id:guid}/file", (Guid id, HttpContext context, PortalOperations operations) => operations.Download(id, context));

        var admin = app.MapGroup("/api/admin").RequireAuthorization("Owner").RequireRateLimiting("writes");
        admin.MapGet("/overview", (PortalOperations operations) => operations.Overview());
        admin.MapGet("/accounts", (PortalOperations operations) => operations.Accounts());
        admin.MapPut("/accounts/{id}/access", (string id, AccountAccessInput input, HttpContext context, PortalOperations operations) => operations.UpdateAccountAccess(id, input, context));
        admin.MapPost("/invitations", (InviteInput input, HttpContext context, PortalOperations operations) => operations.Invite(input, context));
        admin.MapPut("/support/{id:guid}/status", (Guid id, TicketStatusInput input, HttpContext context, PortalOperations operations) => operations.UpdateTicketStatus(id, input, context));
        admin.MapGet("/templates", async (PortalDb db) => Results.Ok(await db.EmailTemplates.AsNoTracking().OrderBy(x => x.Key).ToListAsync()));
        admin.MapPut("/templates/{key}", (string key, TemplateInput input, HttpContext context, PortalOperations operations) => operations.UpdateTemplate(key, input, context));
        admin.MapGet("/faq", async (PortalDb db) => Results.Ok(await db.FaqArticles.AsNoTracking().OrderBy(x => x.Slug).ToListAsync()));
        admin.MapPost("/faq", (FaqInput input, HttpContext context, PortalOperations operations) => operations.SaveFaq(null, input, context));
        admin.MapPut("/faq/{id:guid}", (Guid id, FaqInput input, HttpContext context, PortalOperations operations) => operations.SaveFaq(id, input, context));
        admin.MapGet("/audit", async (PortalDb db) => Results.Ok(await db.Audit.AsNoTracking().OrderByDescending(x => x.Id).Take(100).ToListAsync()));
    }
}
