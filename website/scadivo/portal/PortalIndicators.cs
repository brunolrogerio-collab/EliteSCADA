using Microsoft.EntityFrameworkCore;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

public sealed class PortalPageViewEndpoint;

public sealed class PortalIndicators(PortalDb db, TimeProvider clock)
{
    public async Task<IResult> RecordPageView(string surface, HttpContext context)
    {
        if (surface is not ("site" or "portal") || context.Request.Headers["Sec-Fetch-Site"] != "same-origin")
            return Results.BadRequest();
        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "PortalDailyViews" ("Day", "Surface", "Count") VALUES ({day}, {surface}, 1)
            ON CONFLICT ("Day", "Surface") DO UPDATE SET "Count" = "PortalDailyViews"."Count" + 1
            """, context.RequestAborted);
        return Results.NoContent();
    }

    public async Task<IResult> Report(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var start = from ?? today.AddDays(-29);
        var end = to ?? today;
        if (end > today || end < start || end.DayNumber - start.DayNumber >= 90)
            return Results.BadRequest(new { error = "invalid-indicators-period" });

        var profiles = db.Users.AsNoTracking().Where(user => user.EmailConfirmed && user.InvitationAccepted
            && !user.IsSuspended && user.ProfileType != "" && user.ActivityArea != "" && user.OrganizationOrSchool != "");
        var total = await profiles.CountAsync(cancellationToken);
        var types = await profiles.GroupBy(user => user.ProfileType)
            .Select(group => new { label = group.Key, count = group.Count() }).ToListAsync(cancellationToken);
        var areas = await profiles.GroupBy(user => user.ActivityArea)
            .Select(group => new { label = group.Key, count = group.Count() }).ToListAsync(cancellationToken);
        var affiliations = await profiles.GroupBy(user => user.OrganizationOrSchool.Trim().ToLower())
            .Select(group => new { label = group.Min(user => user.OrganizationOrSchool), count = group.Count() })
            .Where(group => group.count >= 3).OrderByDescending(group => group.count).ThenBy(group => group.label)
            .ToListAsync(cancellationToken);

        var recorded = await db.DailyViews.AsNoTracking().Where(view => view.Day >= start && view.Day <= end)
            .ToDictionaryAsync(view => (view.Day, view.Surface), view => view.Count, cancellationToken);
        var traffic = Enumerable.Range(0, end.DayNumber - start.DayNumber + 1).Select(index =>
        {
            var day = start.AddDays(index);
            return new { day, site = recorded.GetValueOrDefault((day, "site")), portal = recorded.GetValueOrDefault((day, "portal")) };
        });

        var fromUtc = AtMidnight(start);
        var untilUtc = AtMidnight(end).AddDays(1);
        var downloadCounts = await db.Audit.AsNoTracking()
            .Where(entry => entry.Action == "download.requested" && entry.ResourceId != null && entry.CreatedAtUtc >= fromUtc && entry.CreatedAtUtc < untilUtc)
            .GroupBy(entry => entry.ResourceId)
            .Select(group => new { Id = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(row => row.Id!, row => row.Count, cancellationToken);
        var releases = await db.Releases.AsNoTracking().OrderByDescending(release => release.CreatedAtUtc).Take(200)
            .Select(release => new { release.Id, release.Category, release.Platform, release.Version, release.Published })
            .ToListAsync(cancellationToken);
        var downloads = releases.Select(release => new
        {
            release.Id, release.Category, release.Platform, release.Version, release.Published,
            count = downloadCounts.GetValueOrDefault(release.Id.ToString("D"))
        });

        return Results.Ok(new
        {
            from = start, to = end, traffic, downloads,
            profiles = new { total, types, areas, affiliations, otherAffiliations = Math.Max(0, total - affiliations.Sum(x => x.count)) }
        });
    }

    private static DateTimeOffset AtMidnight(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
}
