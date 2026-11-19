using System.Data;
using Microsoft.EntityFrameworkCore;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

public sealed record PortalRetentionResult(int Registrations, int ClosedTickets, int DeliveryRecords, int InactivityReview);

public sealed class PortalRetention(PortalDb db, TimeProvider clock)
{
    internal const string InactivityTemplate = "account.inactivity-notice";
    internal const string InactivityQueued = "account.inactivity-notice.queued";
    internal const string InactivityAccepted = "account.inactivity-notice.accepted";
    private const long InactivityQueueLock = 0x53434144494F0012L;

    public async Task<int> QueueInactivityNoticesAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({InactivityQueueLock})", cancellationToken);
        var now = clock.GetUtcNow();
        var candidates = await InactivityCandidates(now)
            .Where(user => user.Email != null && user.Email != "" &&
                !db.Audit.Any(notice => notice.ResourceId == user.Id && notice.Action == InactivityQueued &&
                    !db.Audit.Any(activity => activity.ActorId == user.Id && activity.CreatedAtUtc > notice.CreatedAtUtc)) &&
                !db.EmailOutbox.Any(mail => mail.UserId == user.Id && mail.TemplateKey == InactivityTemplate &&
                    (mail.Status == "queued" || mail.Status == "retry")))
            .OrderBy(user => user.CreatedAtUtc).ThenBy(user => user.Id)
            .Select(user => user.Id).Take(100).ToListAsync(cancellationToken);
        foreach (var id in candidates)
        {
            db.EmailOutbox.Add(new EmailOutbox { TemplateKey = InactivityTemplate, UserId = id, Status = "queued", CreatedAtUtc = now });
            db.Audit.Add(new PortalAudit { Action = InactivityQueued, ResourceId = id, CreatedAtUtc = now });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return candidates.Count;
    }

    internal Task<bool> StillInactiveAsync(EmailOutbox notice, CancellationToken cancellationToken) =>
        InactivityCandidates(clock.GetUtcNow()).AnyAsync(user => user.Id == notice.UserId &&
            !db.Audit.Any(activity => activity.ActorId == user.Id && activity.CreatedAtUtc > notice.CreatedAtUtc),
            cancellationToken);

    public async Task<PortalRetentionResult> RunAsync(bool apply, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var registrations = ExpiredRegistrations(now.AddDays(-30));
        var closedTickets = db.Tickets.Where(ticket => ticket.Status == "closed" && ticket.UpdatedAtUtc <= now.AddMonths(-12));
        var deliveries = db.EmailOutbox.Where(mail =>
            (mail.Status == "sent" || mail.Status == "bounced" || mail.Status == "failed" || mail.Status == "skipped") &&
            (mail.BouncedAtUtc ?? mail.SentAtUtc ?? mail.CreatedAtUtc) <= now.AddDays(-90));
        var inactivity = await InactivityCandidates(now).CountAsync(cancellationToken);

        if (!apply)
            return new(
                await registrations.CountAsync(cancellationToken),
                await closedTickets.CountAsync(cancellationToken),
                await deliveries.CountAsync(cancellationToken),
                inactivity);

        // A concurrent confirmation, ticket reopen or delivery update must agree with the same snapshot.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var registrationsIds = registrations.Select(user => user.Id);
        await db.EmailOutbox.Where(mail => mail.UserId != null && registrationsIds.Contains(mail.UserId))
            .ExecuteDeleteAsync(cancellationToken);
        var removedRegistrations = await registrations.ExecuteDeleteAsync(cancellationToken);
        var closedTicketIds = closedTickets.Select(ticket => ticket.Id);
        await db.TicketMessages.Where(message => closedTicketIds.Contains(message.TicketId))
            .ExecuteDeleteAsync(cancellationToken);
        var removedTickets = await closedTickets.ExecuteDeleteAsync(cancellationToken);
        var removedDeliveries = await deliveries.ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(removedRegistrations, removedTickets, removedDeliveries, inactivity);
    }

    public async Task<IResult> InactivityReview(CancellationToken cancellationToken) =>
        Results.Ok(await InactivityCandidates(clock.GetUtcNow()).OrderBy(user => user.CreatedAtUtc)
            .ThenBy(user => user.Id)
            .Select(user => new
            {
                user.Id, user.UserName, user.Email, user.CreatedAtUtc,
                noticeAcceptedAtUtc = db.Audit.Where(notice => notice.ResourceId == user.Id && notice.Action == InactivityAccepted &&
                    !db.Audit.Any(activity => activity.ActorId == user.Id && activity.CreatedAtUtc > notice.CreatedAtUtc))
                    .Max(notice => (DateTimeOffset?)notice.CreatedAtUtc)
            })
            .ToListAsync(cancellationToken));

    private IQueryable<PortalUser> ExpiredRegistrations(DateTimeOffset cutoff) =>
        db.Users.Where(user =>
            !user.EmailConfirmed && !user.InvitationAccepted && !user.IsSuspended && user.ClosedAtUtc == null &&
            user.CreatedAtUtc <= cutoff &&
            db.Audit.Any(entry => entry.ActorId == user.Id && entry.Action == "account.registered") &&
            !Staff().Contains(user.Id) &&
            !db.Memberships.Any(membership => membership.UserId == user.Id) &&
            !db.Tickets.Any(ticket => ticket.UserId == user.Id) &&
            !db.TicketMessages.Any(message => message.UserId == user.Id) &&
            !db.OfflineMachineLicenseRequests.Any(request => request.UserId == user.Id || request.ReviewedByUserId == user.Id) &&
            !db.ProductLicenses.Any(license => license.CreatedByUserId == user.Id || license.RevokedByUserId == user.Id) &&
            !db.ProductLicenseActivations.Any(activation => activation.UserId == user.Id || activation.RevokedByUserId == user.Id) &&
            !db.ProductLicensePreparations.Any(preparation => preparation.UserId == user.Id));

    private IQueryable<PortalUser> InactivityCandidates(DateTimeOffset now)
    {
        var cutoff = now.AddMonths(-12);
        var licensedMembers = from membership in db.Memberships
            join license in db.ProductLicenses on membership.OrganizationId equals license.OrganizationId
            where license.Status == ProductLicenseStates.Active &&
                (license.EntitlementExpiresAtUtc == null || license.EntitlementExpiresAtUtc > now)
            select membership.UserId;
        return db.Users.AsNoTracking().Where(user =>
            user.EmailConfirmed && user.InvitationAccepted && !user.IsSuspended && user.ClosedAtUtc == null &&
            user.CreatedAtUtc <= cutoff && !Staff().Contains(user.Id) &&
            !db.Audit.Any(entry => entry.ActorId == user.Id && entry.CreatedAtUtc > cutoff) &&
            !licensedMembers.Contains(user.Id) &&
            !db.OfflineMachineLicenseRequests.Any(request => request.UserId == user.Id && request.SignedLicenseId != null));
    }

    private IQueryable<string> Staff() => from membership in db.UserRoles
        join role in db.Roles on membership.RoleId equals role.Id
        where role.Name == "Owner" || role.Name == "Admin"
        select membership.UserId;
}
