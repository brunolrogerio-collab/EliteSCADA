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
    string? ExistingLicenseName,
    string? MachineRequestCode,
    string? Note);

public sealed record OfflineMachineLicenseRequestStatusInput(string? Status);

public sealed class PortalLicenseOperations
{
    private const string Submitted = "submitted";
    private const string Reviewing = "reviewing";
    private const string NeedsInformation = "needs-information";
    private const string ApprovedForSigning = "approved-for-signing";
    private const string Declined = "declined";

    private static readonly HashSet<string> Statuses = [Submitted, Reviewing, NeedsInformation, ApprovedForSigning, Declined];
    private readonly PortalDb _db;
    private readonly IDataProtector _requestCodeProtector;

    public PortalLicenseOperations(PortalDb db, IDataProtectionProvider dataProtection)
    {
        _db = db;
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
                x.UpdatedAtUtc
            })
            .ToListAsync();
        return Results.Ok(requests);
    }

    public async Task<IResult> SubmitOfflineMachineRequest(OfflineMachineLicenseRequestInput input, HttpContext context)
    {
        var applicationName = input.ApplicationName?.Trim();
        var requestKind = input.RequestKind?.Trim();
        var existingLicenseName = input.ExistingLicenseName?.Trim();
        var machineRequestCode = input.MachineRequestCode?.Trim();
        var note = input.Note?.Trim();

        if (!Valid(applicationName, 2, 120) || requestKind is not ("new" or "convert") ||
            machineRequestCode is null || !ValidMachineRequestCode(machineRequestCode) ||
            (requestKind == "convert" && !Valid(existingLicenseName, 1, 120)) ||
            (requestKind == "new" && !string.IsNullOrWhiteSpace(existingLicenseName)) ||
            (!string.IsNullOrWhiteSpace(note) && note.Length > 800))
            return Results.BadRequest(new { error = "invalid-offline-license-request" });

        var userId = UserId(context);
        var openCount = await _db.OfflineMachineLicenseRequests.CountAsync(x => x.UserId == userId &&
            x.Status != Declined && x.Status != ApprovedForSigning);
        if (openCount >= 10) return Results.Conflict(new { error = "too-many-open-license-requests" });

        string protectedCode;
        try { protectedCode = _requestCodeProtector.Protect(machineRequestCode); }
        catch (CryptographicException) { return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "license-request-protection-unavailable"); }

        var request = new OfflineMachineLicenseRequest
        {
            UserId = userId,
            ApplicationName = applicationName!,
            RequestKind = requestKind!,
            ExistingLicenseName = requestKind == "convert" ? existingLicenseName : null,
            MachineRequestCodeProtected = protectedCode,
            RequestNote = string.IsNullOrWhiteSpace(note) ? null : note,
            Status = Submitted
        };
        _db.OfflineMachineLicenseRequests.Add(request);
        _db.Audit.Add(new PortalAudit { ActorId = userId, Action = "licensing.offline-request.submitted", ResourceId = request.Id.ToString("D") });
        await _db.SaveChangesAsync();
        return Results.Created($"/api/licenses/offline-requests/{request.Id:D}", new
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
        var request = await _db.OfflineMachineLicenseRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (request is null) return Results.NotFound();
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
            request.UserId,
            request.ApplicationName,
            request.RequestKind,
            request.ExistingLicenseName,
            machineRequestCode,
            request.RequestNote,
            request.Status,
            request.SubmittedAtUtc,
            request.UpdatedAtUtc
        });
    }

    public async Task<IResult> UpdateOfflineMachineRequestStatus(Guid id, OfflineMachineLicenseRequestStatusInput input, HttpContext context)
    {
        var status = input.Status?.Trim();
        if (status is null || !Statuses.Contains(status)) return Results.BadRequest(new { error = "invalid-license-request-status" });
        var request = await _db.OfflineMachineLicenseRequests.FirstOrDefaultAsync(x => x.Id == id);
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
        return Results.Ok(new { request.Id, request.Status, request.UpdatedAtUtc });
    }

    private static bool CanTransition(string current, string next) => current switch
    {
        Submitted => next is Reviewing or NeedsInformation or ApprovedForSigning or Declined,
        Reviewing => next is NeedsInformation or ApprovedForSigning or Declined,
        NeedsInformation => next is Reviewing or Declined,
        _ => false
    };

    private static bool ValidMachineRequestCode(string? requestCode)
    {
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
                !schema.TryGetInt32(out var schemaVersion) || schemaVersion != 1 ||
                !payload.TryGetProperty("machineFingerprint", out var fingerprint) ||
                fingerprint.ValueKind != JsonValueKind.String)
                return false;

            var value = fingerprint.GetString();
            return value is { Length: 64 } && value.All(Uri.IsHexDigit);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return false;
        }
    }

    private static bool Valid(string? value, int minimum, int maximum) => value is not null && value.Length >= minimum && value.Length <= maximum;
    private static string UserId(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
