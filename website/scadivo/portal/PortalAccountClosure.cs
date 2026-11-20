using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

public sealed class PortalAccountClosure(PortalDb db, UserManager<PortalUser> users, TimeProvider clock)
{
    public async Task<IResult> Close(string id, HttpContext context)
    {
        var cancellationToken = context.RequestAborted;
        if (string.IsNullOrWhiteSpace(id) || id.Length > 100) return Results.BadRequest();

        // The same row is locked by activation confirmation and renewal.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var account = await db.Users.FromSqlInterpolated($"""
            SELECT * FROM "AspNetUsers" WHERE "Id" = {id} FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);
        if (account is null) return Results.NotFound();
        if (await users.IsInRoleAsync(account, "Owner") || await users.IsInRoleAsync(account, "Admin"))
            return Results.Conflict(new { error = "staff-account-protected" });
        if (account.ClosedAtUtc is not null) return Results.Ok(new { account.ClosedAtUtc });
        if (!account.EmailConfirmed || !account.InvitationAccepted || account.IsSuspended)
            return Results.Conflict(new { error = "account-not-eligible-for-closure" });

        var now = clock.GetUtcNow();
        account.ClosedAtUtc = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
        account.PasswordHash = null;
        account.ProfileType = "";
        account.ActivityArea = "";
        account.ActivityAreaOther = null;
        account.OrganizationOrSchool = "";
        account.PhoneNumber = null;
        account.PhoneNumberConfirmed = false;
        account.Address = null;
        account.DateOfBirth = null;
        account.MarketingEmailOptIn = false;
        account.MarketingEmailOptInAtUtc = null;
        if (!(await users.UpdateSecurityStampAsync(account)).Succeeded)
            return Results.Conflict(new { error = "account-not-updated" });
        db.Audit.Add(new PortalAudit
        {
            ActorId = context.User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = "account.closed",
            ResourceId = account.Id,
            CreatedAtUtc = account.ClosedAtUtc.Value
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new { account.ClosedAtUtc });
    }
}
