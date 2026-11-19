using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Scadivo.Portal.Data;

namespace Scadivo.Portal;

public sealed record OfflineMachineLicenseRequestInput(
    string? ApplicationName,
    string? RequestKind,
    string? MachineRequestCode,
    string? Note,
    Guid? OnlineLicenseId = null);

public sealed record OfflineMachineLicenseRequestStatusInput(string? Status);

public sealed record DeliverOfflineLicenseInput(string? SignedLicense, bool RetireConfirmed = false);

public sealed class PortalLicenseOperations
{
    private const string Submitted = "submitted";
    private const string Reviewing = "reviewing";
    private const string NeedsInformation = "needs-information";
    private const string ApprovedForSigning = "approved-for-signing";
    private const string Delivered = "delivered";
    private const string Declined = "declined";

    private static readonly HashSet<string> Statuses = [Submitted, Reviewing, NeedsInformation, ApprovedForSigning, Declined];
    private readonly PortalDb _db;
    private readonly IDataProtector _requestCodeProtector;
    private readonly IDataProtector _signedLicenseProtector;
    private readonly TimeProvider _clock;
    private readonly OfflineLicenseVerifier _verifier;

    public PortalLicenseOperations(PortalDb db, IDataProtectionProvider dataProtection, TimeProvider clock, OfflineLicenseVerifier verifier)
    {
        _db = db;
        _clock = clock;
        _verifier = verifier;
        _signedLicenseProtector = dataProtection.CreateProtector("SCADIVO.Portal.OfflineSignedLicense.v1");
        _requestCodeProtector = dataProtection.CreateProtector("SCADIVO.Portal.OfflineMachineLicenseRequest.v1");
    }

    public async Task<IResult> MyOfflineMachineRequests(HttpContext context)
    {
        var userId = UserId(context);
        var requests = await _db.OfflineMachineLicenseRequests.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.SubmittedAtUtc)
            .Take(50)
            .Select(x => new
            {
                x.Id,
                x.ApplicationName,
                x.RequestKind,
                x.ExistingLicenseName,
                x.Status,
                x.SubmittedAtUtc,
                x.AvailableAtUtc,
                x.UpdatedAtUtc
            })
            .ToListAsync();
        return Results.Ok(requests);
    }

    public async Task<IResult> MyOfflineMachineRequest(Guid id, HttpContext context)
    {
        var userId = UserId(context);
        var request = await _db.OfflineMachineLicenseRequests.AsNoTracking()
            .Where(x => x.Id == id && x.UserId == userId)
            .Select(x => new
            {
                x.Id,
                x.ApplicationName,
                x.RequestKind,
                x.ExistingLicenseName,
                x.RequestNote,
                x.Status,
                x.SubmittedAtUtc,
                x.AvailableAtUtc,
                x.UpdatedAtUtc
            })
            .FirstOrDefaultAsync();
        return request is null ? Results.NotFound() : Results.Ok(request);
    }

    public async Task<IResult> SubmitOfflineMachineRequest(OfflineMachineLicenseRequestInput input, HttpContext context)
    {
        var applicationName = input.ApplicationName?.Trim();
        var requestKind = input.RequestKind?.Trim();

        var machineRequestCode = input.MachineRequestCode?.Trim();
        var note = input.Note?.Trim();

        if (!Valid(applicationName, 2, 120) || requestKind is not ("new" or "convert") ||
            machineRequestCode is null || !ValidMachineRequestCode(machineRequestCode, out _) ||
            (requestKind == "convert" && (input.OnlineLicenseId is null || input.OnlineLicenseId == Guid.Empty)) ||
            (requestKind == "new" && input.OnlineLicenseId is not null) ||
            (!string.IsNullOrWhiteSpace(note) && note.Length > 800))
            return Results.BadRequest(new { error = "invalid-offline-license-request" });

        var userId = UserId(context);
        string? existingLicenseName = null;
        if (input.OnlineLicenseId is { } licenseId)
        {
            existingLicenseName = await ConversionOptions(userId)
                .Where(x => x.Id == licenseId && x.ApplicationName == applicationName)
                .Select(x => x.ApplicationName).SingleOrDefaultAsync();
            if (existingLicenseName is null) return ConversionUnavailable();
        }

        var openCount = await _db.OfflineMachineLicenseRequests.CountAsync(x => x.UserId == userId &&
            x.Status != Declined && x.Status != Delivered);
        if (openCount >= 10) return Results.Conflict(new { error = "too-many-open-license-requests" });

        string protectedCode;
        try { protectedCode = _requestCodeProtector.Protect(machineRequestCode); }
        catch (CryptographicException) { return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "license-request-protection-unavailable"); }

        var request = new OfflineMachineLicenseRequest
        {
            UserId = userId,
            ApplicationName = applicationName!,
            RequestKind = requestKind!,
            OnlineLicenseId = input.OnlineLicenseId,
            ExistingLicenseName = requestKind == "convert" ? existingLicenseName : null,
            MachineRequestCodeProtected = protectedCode,
            RequestNote = string.IsNullOrWhiteSpace(note) ? null : note,
            Status = Submitted
        };
        _db.OfflineMachineLicenseRequests.Add(request);
        _db.Audit.Add(new PortalAudit { ActorId = userId, Action = "licensing.offline-request.submitted", ResourceId = request.Id.ToString("D") });
        await _db.SaveChangesAsync();
        return Results.Created($"/portal/api/licenses/offline-requests/{request.Id:D}", new
        {
            request.Id,
            request.ApplicationName,
            request.RequestKind,
            request.ExistingLicenseName,
            request.Status,
            request.SubmittedAtUtc
        });
    }

    public async Task<IResult> AdminOfflineMachineRequests()
    {
        var requests = await (from request in _db.OfflineMachineLicenseRequests.AsNoTracking()
                              join user in _db.Users.AsNoTracking() on request.UserId equals user.Id
                              orderby request.SubmittedAtUtc descending
                              select new
                              {
                                  request.Id,
                                  request.UserId,
                                  user.UserName,
                                  user.DisplayName,
                                  user.Email,
                                  request.ApplicationName,
                                  request.RequestKind,
                                  request.ExistingLicenseName,
                                  request.Status,
                                  request.SubmittedAtUtc,
                                  request.UpdatedAtUtc
                              })
            .Take(200)
            .ToListAsync();
        return Results.Ok(requests);
    }

    public async Task<IResult> AdminOfflineMachineRequest(Guid id, HttpContext context)
    {
        var result = await (from candidate in _db.OfflineMachineLicenseRequests.AsNoTracking()
                            join user in _db.Users.AsNoTracking() on candidate.UserId equals user.Id
                            where candidate.Id == id
                            select new { Request = candidate, user.UserName, user.DisplayName, user.Email })
            .FirstOrDefaultAsync();
        if (result is null) return Results.NotFound();
        var request = result.Request;
        string machineRequestCode;
        try { machineRequestCode = _requestCodeProtector.Unprotect(request.MachineRequestCodeProtected); }
        catch (CryptographicException) { return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "license-request-protection-key-unavailable"); }

        _db.Audit.Add(new PortalAudit
        {
            ActorId = UserId(context),
            Action = "licensing.offline-request.code-viewed",
            ResourceId = request.Id.ToString("D")
        });
        await _db.SaveChangesAsync();
        return Results.Ok(new
        {
            request.Id,
            result.UserName,
            result.DisplayName,
            result.Email,
            request.ApplicationName,
            request.RequestKind,
            request.ExistingLicenseName,
            machineRequestCode,
            request.RequestNote,
            request.OnlineLicenseId,
            request.AvailableAtUtc,
            signingRights = request.OnlineLicenseId is { } licenseId
                ? await _db.ProductLicenses.AsNoTracking().Where(x => x.Id == licenseId)
                    .Select(x => new { x.Tier, x.InteractiveSeats, x.ViewOnlySeats, x.HaRuntime, x.AssetProcessIntelligence }).SingleOrDefaultAsync()
                : null,
            request.Status,
            request.SubmittedAtUtc,
            request.UpdatedAtUtc
        });
    }

    public async Task<IResult> UpdateOfflineMachineRequestStatus(Guid id, OfflineMachineLicenseRequestStatusInput input, HttpContext context)
    {
        var status = input.Status?.Trim();
        if (status is null || !Statuses.Contains(status)) return Results.BadRequest(new { error = "invalid-license-request-status" });
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var request = await LockRequest(id);
        if (request is null) return Results.NotFound();
        if (request.Status == status) return Results.Ok(new { request.Id, request.Status, request.UpdatedAtUtc });
        if (!CanTransition(request.Status, status)) return Results.Conflict(new { error = "invalid-license-request-transition" });

        request.Status = status;
        request.ReviewedByUserId = UserId(context);
        request.UpdatedAtUtc = DateTimeOffset.UtcNow;
        _db.Audit.Add(new PortalAudit
        {
            ActorId = UserId(context),
            Action = "licensing.offline-request.status-updated",
            ResourceId = request.Id.ToString("D")
        });
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Results.Ok(new { request.Id, request.Status, request.UpdatedAtUtc });
    }

    public async Task<IResult> MyConversionOptions(HttpContext context) =>
        Results.Ok(await ConversionOptions(UserId(context))
            .OrderBy(x => x.ApplicationName).ThenBy(x => x.PublicCode)
            .ToListAsync());

    public async Task<IResult> GetSignedLicense(Guid id, HttpContext context)
    {
        var request = await _db.OfflineMachineLicenseRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == UserId(context));
        if (request is not { Status: Delivered, SignedLicenseProtected: not null, AvailableAtUtc: not null })
            return Results.NotFound();
        if (request.AvailableAtUtc > _clock.GetUtcNow())
            return Results.Conflict(new { error = "offline-license-awaiting-expiry" });
        try
        {
            return Results.Ok(new { signedLicense = _signedLicenseProtector.Unprotect(request.SignedLicenseProtected) });
        }
        catch (CryptographicException)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "license-request-protection-key-unavailable");
        }
    }

    public async Task<IResult> DeliverSignedLicense(Guid id, DeliverOfflineLicenseInput input, HttpContext context,
        PortalLicensingOperations licensing)
    {
        var code = input.SignedLicense?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length > 8192) return InvalidSignedLicense();

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var request = await LockRequest(id);
        if (request is null) return Results.NotFound();
        if (request.Status != ApprovedForSigning)
            return Results.Conflict(new { error = "invalid-license-request-transition" });

        var now = _clock.GetUtcNow();
        VerifiedOfflineLicense? verified;
        string protectedLicense;
        try
        {
            var machineRequest = _requestCodeProtector.Unprotect(request.MachineRequestCodeProtected);
            if (!ValidMachineRequestCode(machineRequest, out var fingerprint)) return InvalidSignedLicense();
            verified = _verifier.Verify(code, fingerprint!, now);
            if (verified is null || verified.Id != request.Id.ToString("D")) return InvalidSignedLicense();
            protectedLicense = _signedLicenseProtector.Protect(code);
        }
        catch (CryptographicException)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "license-request-protection-key-unavailable");
        }

        var availableAt = now;
        var actor = UserId(context);
        if (request.RequestKind == "convert")
        {
            if (!input.RetireConfirmed || request.OnlineLicenseId is not { } onlineId) return ConversionUnavailable();
            var organizationId = await _db.ProductLicenses.AsNoTracking().Where(x => x.Id == onlineId)
                .Select(x => (Guid?)x.OrganizationId).SingleOrDefaultAsync();
            if (organizationId is null ||
                (await licensing.LockMembershipForRead(organizationId.Value, request.UserId))?.Role != "Owner")
                return ConversionUnavailable();
            var online = await licensing.LockLicense(onlineId, "UPDATE");
            if (online is not { Status: ProductLicenseStates.Active, Term: "perpetual", MaxActivations: 1, EntitlementExpiresAtUtc: null })
                return ConversionUnavailable();
            if (verified.Tier != online.Tier || verified.InteractiveSeats != online.InteractiveSeats ||
                verified.ViewOnlySeats != online.ViewOnlySeats || verified.HaRuntime != online.HaRuntime ||
                verified.Intelligence != online.AssetProcessIntelligence || verified.ExpiresAtUtc != null)
                return InvalidSignedLicense();

            // The license lock serializes conversion with new activations and renewal.
            // A lease already signed before retirement can remain valid for up to 72 hours.
            now = _clock.GetUtcNow();
            availableAt = now;
            if (await _db.ProductLicenseActivations.AnyAsync(x => x.LicenseId == onlineId))
                availableAt = now.AddHours(72);
            await licensing.RetireOnlineLicenseAsync(online, actor, now, "offline-machine-conversion");
        }
        request.SignedLicenseId = verified.Id;
        request.SignedLicenseProtected = protectedLicense;
        request.AvailableAtUtc = availableAt;
        request.Status = Delivered;
        request.ReviewedByUserId = actor;
        request.UpdatedAtUtc = now;
        _db.Audit.Add(new PortalAudit { ActorId = actor, Action = "licensing.offline-request.delivered", ResourceId = id.ToString("D") });
        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException
            { SqlState: "23505", ConstraintName: "IX_OfflineMachineLicenseRequests_SignedLicenseId" })
        {
            return Results.Conflict(new { error = "offline-license-already-delivered" });
        }
        return Results.Ok(new { request.Id, request.Status, request.AvailableAtUtc });
    }

    private async Task<OfflineMachineLicenseRequest?> LockRequest(Guid id) =>
        await _db.OfflineMachineLicenseRequests
            .FromSqlInterpolated($"SELECT * FROM \"OfflineMachineLicenseRequests\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync();

    private IQueryable<ConversionOption> ConversionOptions(string userId) =>
        from license in _db.ProductLicenses.AsNoTracking()
        join application in _db.ProductApplications.AsNoTracking() on license.ApplicationId equals application.Id
        join membership in _db.Memberships.AsNoTracking() on license.OrganizationId equals membership.OrganizationId
        where membership.UserId == userId && membership.Role == "Owner" && license.Status == ProductLicenseStates.Active
            && license.Term == "perpetual" && license.EntitlementExpiresAtUtc == null && license.MaxActivations == 1
        select new ConversionOption { Id = license.Id, PublicCode = license.PublicCode, ApplicationName = application.Name };

    private sealed class ConversionOption
    {
        public Guid Id { get; init; }
        public required string PublicCode { get; init; }
        public required string ApplicationName { get; init; }
    }

    private static IResult ConversionUnavailable() =>
        Results.Conflict(new { error = "offline-conversion-unavailable" });

    private static IResult InvalidSignedLicense() =>
        Results.BadRequest(new { error = "invalid-signed-offline-license" });

    private static bool CanTransition(string current, string next) => current switch
    {
        Submitted => next is Reviewing or NeedsInformation or ApprovedForSigning or Declined,
        Reviewing => next is NeedsInformation or ApprovedForSigning or Declined,
        NeedsInformation => next is Reviewing or Declined,
        ApprovedForSigning => next is Reviewing or Declined,
        _ => false
    };

    private static bool ValidMachineRequestCode(string? requestCode, out string? machineFingerprint)
    {
        machineFingerprint = null;
        if (requestCode is null || requestCode.Length is < 20 or > 8192) return false;
        var parts = requestCode.Split('.', StringSplitOptions.None);
        if (parts.Length != 2 || !string.Equals(parts[0], "ESREQ1", StringComparison.Ordinal) ||
            parts[1].Length == 0 || parts[1].Length % 4 == 1 ||
            parts[1].Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
            return false;

        var base64 = parts[1].Replace('-', '+').Replace('_', '/');
        base64 += (base64.Length % 4) switch { 0 => "", 2 => "==", 3 => "=", _ => string.Empty };
        try
        {
            using var document = JsonDocument.Parse(Convert.FromBase64String(base64));
            var payload = document.RootElement;
            if (payload.ValueKind != JsonValueKind.Object ||
                !payload.TryGetProperty("schemaVersion", out var schema) ||
                schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var schemaVersion) || schemaVersion != 1 ||
                !payload.TryGetProperty("machineFingerprint", out var fingerprint) ||
                fingerprint.ValueKind != JsonValueKind.String)
                return false;

            var value = fingerprint.GetString();
            if (value is not { Length: 64 } || !value.All(Uri.IsHexDigit)) return false;
            machineFingerprint = value;
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return false;
        }
    }

    private static bool Valid(string? value, int minimum, int maximum) => value is not null && value.Length >= minimum && value.Length <= maximum;
    private static string UserId(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
