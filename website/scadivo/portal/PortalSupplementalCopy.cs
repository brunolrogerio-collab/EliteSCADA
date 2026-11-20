using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

public sealed class SupplementalCopy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Reference { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string Status { get; set; } = "pending";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string? ChallengeHash { get; set; }
    public string? BrowserHash { get; set; }
    public string? DownloadHash { get; set; }
    public byte[]? ProtectedArchive { get; set; }
}

public sealed record CreateSupplementalCopyInput(string? Reference, string? Evidence, bool ChannelChecked);
public sealed record SupplementalCopyToken(string? Token);

public sealed class PortalSupplementalCopy(PortalDb db, IDataProtectionProvider protection,
    IConfiguration configuration, IWebHostEnvironment environment, TimeProvider clock)
{
    private const int MaxArchiveSize = 4 * 1024 * 1024;
    private const string PublicPath = "/api/privacy/supplement";
    private const string AdminPath = "/api/admin/privacy/supplement";
    private const string CookiePath = "/portal" + PublicPath;

    public static bool IsEnabled(IConfiguration config) => config.GetValue<bool>("Portal:SupplementalCopyEnabled");

    public static void Map(WebApplication app)
    {
        if (!IsEnabled(app.Configuration)) return;
        var endpoints = app.MapGroup("").AddEndpointFilter((context, next) =>
        {
            if (!context.HttpContext.Request.IsHttps && !app.Environment.IsDevelopment())
                return ValueTask.FromResult<object?>(Results.BadRequest(new { error = "https-required" }));
            return next(context);
        });
        var admin = endpoints.MapGroup(AdminPath).RequireAuthorization("Owner").RequireRateLimiting("writes");
        admin.MapGet("", (PortalSupplementalCopy copies, CancellationToken token) => copies.ListAsync(token));
        admin.MapPost("", (CreateSupplementalCopyInput input, HttpContext http, PortalSupplementalCopy copies, CancellationToken token) =>
            copies.CreateAsync(input, http, token));
        admin.MapPost("/upload", (Guid id, HttpContext http, PortalSupplementalCopy copies, CancellationToken token) =>
            copies.ReleaseAsync(id, http, token));
        admin.MapDelete("/{id:guid}", (Guid id, HttpContext http, PortalSupplementalCopy copies, CancellationToken token) =>
            copies.RevokeAsync(id, http, token));
        var claim = endpoints.MapGroup(PublicPath).RequireRateLimiting("auth");
        claim.MapPost("/verify", (SupplementalCopyToken input, HttpContext http, PortalSupplementalCopy copies, CancellationToken token) =>
            copies.VerifyAsync(input, http, token));
        claim.MapPost("/download", (SupplementalCopyToken input, HttpContext http, PortalSupplementalCopy copies, CancellationToken token) =>
            copies.DownloadAsync(input, http, token));
    }

    private async Task<IResult> ListAsync(CancellationToken token)
    {
        var now = clock.GetUtcNow();
        return Results.Ok(await db.SupplementalCopies.AsNoTracking().Where(copy => copy.ExpiresAtUtc > now)
            .OrderByDescending(copy => copy.CreatedAtUtc).Take(100)
            .Select(copy => new { copy.Id, copy.Reference, copy.Evidence, copy.Status, copy.ExpiresAtUtc }).ToListAsync(token));
    }

    private async Task<IResult> CreateAsync(CreateSupplementalCopyInput input, HttpContext http, CancellationToken token)
    {
        var reference = input.Reference?.Trim().ToUpperInvariant();
        if (!input.ChannelChecked || input.Evidence is not ("account-email" or "original-thread" or "independent-channel") ||
            reference is not { Length: 11 } || !reference.StartsWith("SC-", StringComparison.Ordinal) ||
            !reference.Skip(3).All(char.IsAsciiHexDigit))
            return Results.BadRequest(new { error = "privacy-check-required" });
        if (!await db.EmailOutbox.AnyAsync(row => row.TemplateKey == "contact.received" && row.Reference == reference, token))
            return Results.NotFound(new { error = "privacy-reference-not-found" });

        var now = clock.GetUtcNow();
        var challenge = GenerateSecret();
        var copy = new SupplementalCopy
        {
            Reference = reference, Evidence = input.Evidence, ChallengeHash = Hash(challenge),
            CreatedAtUtc = now, ExpiresAtUtc = now.AddHours(24)
        };
        db.SupplementalCopies.Add(copy);
        Audit(copy.Id, "challenge", http, now);
        await db.SaveChangesAsync(token);
        return Results.Ok(new { copy.Id, copy.ExpiresAtUtc, url = Link("privacy-verify", copy.Id, challenge) });
    }

    private async Task<IResult> VerifyAsync(SupplementalCopyToken input, HttpContext http, CancellationToken token)
    {
        if (!ReadToken(input.Token, out var id, out var hash)) return Unavailable();
        var now = clock.GetUtcNow();
        var browserSecret = GenerateSecret();
        var browserHash = Hash(browserSecret);
        var expires = now.AddHours(72);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var changed = await Active(id, "pending", now).Where(copy => copy.ChallengeHash == hash)
            .ExecuteUpdateAsync(set => set.SetProperty(copy => copy.Status, "verified")
                .SetProperty(copy => copy.ChallengeHash, (string?)null)
                .SetProperty(copy => copy.BrowserHash, browserHash)
                .SetProperty(copy => copy.ExpiresAtUtc, expires), token);
        if (changed != 1) return Unavailable();
        Audit(id, "verified", null, now);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        var cookie = BrowserCookie(http);
        cookie.Expires = expires;
        http.Response.Cookies.Append(BrowserCookieName(id), browserSecret, cookie);
        return Results.Ok();
    }

    private async Task<IResult> ReleaseAsync(Guid id, HttpContext http, CancellationToken token)
    {
        var request = http.Request;
        if (request.Headers["X-Privacy-Reviewed"] != "yes")
            return Results.BadRequest(new { error = "privacy-check-required" });
        if (request.ContentLength is not (> 0 and <= MaxArchiveSize) ||
            !string.Equals(request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = "privacy-copy-invalid-file" });
        if (!await Active(id, "verified", clock.GetUtcNow()).AnyAsync(token)) return Unavailable();

        var limit = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = MaxArchiveSize;
        var archive = new byte[(int)request.ContentLength.Value];
        await request.Body.ReadExactlyAsync(archive, token);
        try
        {
            using var json = JsonDocument.Parse(archive);
            if (json.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                return Results.BadRequest(new { error = "privacy-copy-invalid-file" });
        }
        catch (JsonException) { return Results.BadRequest(new { error = "privacy-copy-invalid-file" }); }

        var now = clock.GetUtcNow();
        var secret = GenerateSecret();
        var hash = Hash(secret);
        var proposedExpiry = now.AddHours(24);
        var encrypted = Protector(id).Protect(archive);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var changed = await Active(id, "verified", now).ExecuteUpdateAsync(set => set
            .SetProperty(copy => copy.Status, "released")
            .SetProperty(copy => copy.ProtectedArchive, encrypted)
            .SetProperty(copy => copy.DownloadHash, hash)
            .SetProperty(copy => copy.ExpiresAtUtc, copy => copy.ExpiresAtUtc < proposedExpiry ? copy.ExpiresAtUtc : proposedExpiry), token);
        if (changed != 1) return Unavailable();
        Audit(id, "released", http, now);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.Ok(new { url = Link("privacy-download", id, secret) });
    }

    private async Task<IResult> DownloadAsync(SupplementalCopyToken input, HttpContext http, CancellationToken token)
    {
        if (!ReadToken(input.Token, out var id, out var hash) ||
            !http.Request.Cookies.TryGetValue(BrowserCookieName(id), out var browserSecret) || !ValidSecret(browserSecret))
            return Unavailable();
        var browserHash = Hash(browserSecret);
        var now = clock.GetUtcNow();
        var selected = Active(id, "released", now).Where(copy => copy.DownloadHash == hash && copy.BrowserHash == browserHash);
        var encrypted = await selected.Select(copy => copy.ProtectedArchive).SingleOrDefaultAsync(token);
        if (encrypted is null) return Unavailable();
        byte[] archive;
        try { archive = Protector(id).Unprotect(encrypted); }
        catch (CryptographicException) { return Unavailable(); }

        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var claimedAt = clock.GetUtcNow();
        if (await ClearAsync(selected.Where(copy => copy.ExpiresAtUtc > claimedAt), "downloaded", token) != 1) return Unavailable();
        Audit(id, "downloaded", null, claimedAt);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        http.Response.Cookies.Delete(BrowserCookieName(id), BrowserCookie(http));
        return Results.File(archive, "application/json; charset=utf-8", "scadivo-copia-complementar.json");
    }

    private async Task<IResult> RevokeAsync(Guid id, HttpContext http, CancellationToken token)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var active = db.SupplementalCopies.Where(copy => copy.Id == id && copy.ExpiresAtUtc > now &&
            (copy.Status == "pending" || copy.Status == "verified" || copy.Status == "released"));
        if (await ClearAsync(active, "revoked", token) != 1) return Unavailable();
        Audit(id, "revoked", http, now);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    }

    public Task<int> PurgeExpiredAsync(CancellationToken token) =>
        db.SupplementalCopies.Where(copy => copy.ExpiresAtUtc <= clock.GetUtcNow()).ExecuteDeleteAsync(token);

    private IQueryable<SupplementalCopy> Active(Guid id, string status, DateTimeOffset now) =>
        db.SupplementalCopies.Where(copy => copy.Id == id && copy.Status == status && copy.ExpiresAtUtc > now);

    private static Task<int> ClearAsync(IQueryable<SupplementalCopy> rows, string status, CancellationToken token) =>
        rows.ExecuteUpdateAsync(set => set.SetProperty(copy => copy.Status, status)
            .SetProperty(copy => copy.ChallengeHash, (string?)null)
            .SetProperty(copy => copy.BrowserHash, (string?)null)
            .SetProperty(copy => copy.DownloadHash, (string?)null)
            .SetProperty(copy => copy.ProtectedArchive, (byte[]?)null), token);

    private void Audit(Guid id, string action, HttpContext? http, DateTimeOffset now) =>
        db.Audit.Add(new PortalAudit { ActorId = http?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            ResourceId = id.ToString(), Action = "privacy.supplement." + action, CreatedAtUtc = now });

    private IDataProtector Protector(Guid id) => protection.CreateProtector("SCADIVO.Portal.SupplementalCopy.v1", id.ToString("N"));
    private string Link(string action, Guid id, string secret) =>
        (configuration["Portal:PublicUrl"] ?? "https://scadivo.com/portal/").TrimEnd('/') + $"/#{action}={id:N}.{secret}";
    private CookieOptions BrowserCookie(HttpContext http) => new()
    {
        HttpOnly = true, Secure = !environment.IsDevelopment() || http.Request.IsHttps, SameSite = SameSiteMode.Strict, Path = CookiePath
    };
    private string BrowserCookieName(Guid id) => (environment.IsDevelopment() ? "" : "__Secure-") + "scadivo.privacy." + id.ToString("N");
    private static IResult Unavailable() => Results.NotFound(new { error = "privacy-link-unavailable" });
    private static string GenerateSecret() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(value)));
    private static bool ValidSecret(string value) =>
        value.Length == 43 && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');

    private static bool ReadToken(string? value, out Guid id, out string hash)
    {
        id = default;
        hash = "";
        if (value is not { Length: 76 } || value[32] != '.' ||
            !Guid.TryParseExact(value.AsSpan(0, 32), "N", out id) || !ValidSecret(value[33..])) return false;
        hash = Hash(value[33..]);
        return true;
    }
}
