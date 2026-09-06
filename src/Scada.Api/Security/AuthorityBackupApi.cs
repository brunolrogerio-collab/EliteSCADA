using Scada.Api.Realtime;
using Scada.Api.Runtime;
using Scada.Security.Audit;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public sealed record AuthorityBackupPasswordRequest(string Password);
public sealed record AuthorityBackupPreviewRequest(string Backup, string Password);
public sealed record AuthorityBackupApplyRequest(string Backup, string Password);

public static class AuthorityBackupApi
{
    private const string ExportAction = "auth.authority_backup.export";
    private const string PreviewAction = "auth.authority_backup.preview";
    private const string ApplyAction = "auth.authority_backup.apply";
    private const string BootstrapPreviewAction = "auth.bootstrap.authority_backup.preview";
    private const string BootstrapApplyAction = "auth.bootstrap.authority_backup.apply";

    public static IEndpointRouteBuilder MapAuthorityBackupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var localRuntime = endpoints.ServiceProvider.GetRequiredService<LocalIdentityRuntimeOptions>();
        if (!localRuntime.Enabled) return endpoints;

        endpoints.MapPost("/api/auth/authority-backup/export", async (
            AuthorityBackupPasswordRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            ILocalIdentityStore store,
            AuthorityBackupService backup,
            CancellationToken ct) =>
        {
            var authorization = await AuthorizeAdministrationAsync(
                context, runtime, security, audit, ExportAction, ct);
            if (authorization.Failure is not null) return authorization.Failure;

            try
            {
                var users = await store.ListAsync(ct);
                var payload = backup.Export(users, request.Password);
                await audit.RecordAsync(
                    context,
                    authorization.Check!.Principal,
                    ExportAction,
                    AuditOutcome.Succeeded,
                    "local-authority",
                    "backup",
                    new Dictionary<string, string>
                    {
                        ["userCount"] = users.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });

                return Results.Ok(new
                {
                    format = AuthorityBackupService.CurrentFormat,
                    formatVersion = AuthorityBackupService.CurrentFormatVersion,
                    backup = payload
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidDataException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        endpoints.MapPost("/api/auth/authority-backup/preview", async (
            AuthorityBackupPreviewRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            AuthorityBackupService backup,
            CancellationToken ct) =>
        {
            var authorization = await AuthorizeAdministrationAsync(
                context, runtime, security, audit, PreviewAction, ct);
            if (authorization.Failure is not null) return authorization.Failure;

            try
            {
                var preview = backup.Preview(request.Backup, request.Password);
                await audit.RecordAsync(
                    context,
                    authorization.Check!.Principal,
                    PreviewAction,
                    AuditOutcome.Succeeded,
                    "local-authority",
                    "backup",
                    PreviewAuditDetails(preview));
                return Results.Ok(preview);
            }
            catch (ArgumentException)
            {
                return InvalidBackup();
            }
            catch (InvalidDataException)
            {
                return InvalidBackup();
            }
        });

        endpoints.MapPost("/api/auth/authority-backup/apply", async (
            AuthorityBackupApplyRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            ILocalIdentityStore store,
            AuthorityBackupService backup,
            TagRealtimeHub realtime,
            CancellationToken ct) =>
        {
            var authorization = await AuthorizeAdministrationAsync(
                context, runtime, security, audit, ApplyAction, ct);
            if (authorization.Failure is not null) return authorization.Failure;

            AuthorityBackupOpenResult opened;
            try
            {
                opened = backup.Open(request.Backup, request.Password);
            }
            catch (ArgumentException)
            {
                return InvalidBackup();
            }
            catch (InvalidDataException)
            {
                return InvalidBackup();
            }

            var currentUsers = await store.ListAsync(ct);
            IReadOnlyCollection<LocalUserAccount> replacement;
            try
            {
                replacement = RefreshSecurityVersions(opened.Accounts, currentUsers);
            }
            catch (InvalidDataException)
            {
                return InvalidBackup();
            }

            if (!await HasEnabledLocalAdministratorAsync(replacement, security, runtime, ct))
            {
                return Results.BadRequest(new
                {
                    error = "The restored Authority must retain at least one enabled local user with UserRoleAdmin or SystemAdmin in the active runtime policy."
                });
            }

            await store.ReplaceAllAsync(replacement, ct);
            var revokedRealtimeClients = RevokeRealtimeSubjects(realtime, currentUsers, replacement);
            LocalIdentityApi.DeleteLocalCookie(context, localRuntime);

            await audit.RecordAsync(
                context,
                authorization.Check!.Principal,
                ApplyAction,
                AuditOutcome.Succeeded,
                "local-authority",
                "restore",
                new Dictionary<string, string>
                {
                    ["userCount"] = replacement.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["revokedRealtimeClients"] = revokedRealtimeClients.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["signInRequired"] = bool.TrueString
                });

            return Results.Ok(new
            {
                applied = true,
                signInRequired = true,
                preview = opened.Preview
            });
        });

        endpoints.MapPost("/api/auth/bootstrap/authority-backup/preview", async (
            AuthorityBackupPreviewRequest request,
            HttpContext context,
            LocalIdentityBootstrapService bootstrapStatus,
            LocalLoginAttemptLimiter limiter,
            ApiAuditService audit,
            AuthorityBackupService backup,
            CancellationToken ct) =>
        {
            var remoteKey = $"authority-backup-preview:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
            if (!limiter.TryAcquire(remoteKey))
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);

            var status = await LocalIdentityApi.ResolveBootstrapStatusAsync(
                context, localRuntime, bootstrapStatus, ct);
            var blocked = BootstrapFailure(status);
            if (blocked is not null) return blocked;

            try
            {
                var preview = backup.Preview(request.Backup, request.Password);
                await audit.RecordAsync(
                    context,
                    AnonymousPrincipal(),
                    BootstrapPreviewAction,
                    AuditOutcome.Succeeded,
                    "local-authority",
                    "backup",
                    PreviewAuditDetails(preview));
                return Results.Ok(preview);
            }
            catch (ArgumentException)
            {
                return InvalidBackup();
            }
            catch (InvalidDataException)
            {
                return InvalidBackup();
            }
        });

        endpoints.MapPost("/api/auth/bootstrap/authority-backup/apply", async (
            AuthorityBackupApplyRequest request,
            HttpContext context,
            LocalIdentityBootstrapService bootstrapStatus,
            LocalLoginAttemptLimiter limiter,
            InitialInstallationGate installationGate,
            ILocalIdentityStore store,
            ApiAuditService audit,
            AuthorityBackupService backup,
            TagRealtimeHub realtime,
            CancellationToken ct) =>
        {
            var remoteKey = $"authority-backup-apply:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
            if (!limiter.TryAcquire(remoteKey))
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);

            AuthorityBackupOpenResult opened;
            try
            {
                opened = backup.Open(request.Backup, request.Password);
            }
            catch (ArgumentException)
            {
                return InvalidBackup();
            }
            catch (InvalidDataException)
            {
                return InvalidBackup();
            }

            await using var installationLease = await installationGate.EnterAsync(ct);
            var status = await LocalIdentityApi.ResolveBootstrapStatusAsync(
                context, localRuntime, bootstrapStatus, ct);
            var blocked = BootstrapFailure(status);
            if (blocked is not null) return blocked;

            IReadOnlyCollection<LocalUserAccount> replacement;
            try
            {
                replacement = RefreshSecurityVersions(opened.Accounts, Array.Empty<LocalUserAccount>());
            }
            catch (InvalidDataException)
            {
                return InvalidBackup();
            }

            if (!await store.TryReplaceAllIfEmptyAsync(replacement, ct))
            {
                return Results.Conflict(new
                {
                    error = "Anonymous Authority restore is already closed because another identity initialization won the race."
                });
            }

            var revokedRealtimeClients = RevokeRealtimeSubjects(
                realtime,
                Array.Empty<LocalUserAccount>(),
                replacement);
            LocalIdentityApi.DeleteLocalCookie(context, localRuntime);

            await audit.RecordAsync(
                context,
                AnonymousPrincipal(),
                BootstrapApplyAction,
                AuditOutcome.Succeeded,
                "local-authority",
                "restore",
                new Dictionary<string, string>
                {
                    ["userCount"] = replacement.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["revokedRealtimeClients"] = revokedRealtimeClients.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["signInRequired"] = bool.TrueString
                });

            return Results.Ok(new
            {
                applied = true,
                signInRequired = true,
                preview = opened.Preview
            });
        });

        return endpoints;
    }

    internal static IReadOnlyCollection<LocalUserAccount> RefreshSecurityVersions(
        IReadOnlyCollection<LocalUserAccount> restored,
        IReadOnlyCollection<LocalUserAccount> current,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(restored);
        ArgumentNullException.ThrowIfNull(current);

        var currentById = current.ToDictionary(user => user.Id);
        var nowMs = (nowUtc ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds();
        var maximumMs = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();
        var refreshed = new List<LocalUserAccount>(restored.Count);

        foreach (var account in restored)
        {
            var previousMs = account.UpdatedAtUtc.ToUnixTimeMilliseconds();
            if (currentById.TryGetValue(account.Id, out var existing))
                previousMs = Math.Max(previousMs, existing.UpdatedAtUtc.ToUnixTimeMilliseconds());
            if (previousMs >= maximumMs)
                throw new InvalidDataException("Authority backup contains a security version that cannot be advanced safely.");

            var nextMs = Math.Max(nowMs, previousMs + 1);
            refreshed.Add(account with { UpdatedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(nextMs) });
        }

        return refreshed;
    }

    private static async Task<(ApiAuthorizationCheck? Check, IResult? Failure)> AuthorizeAdministrationAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        ApiAuthorizationService security,
        ApiAuditService audit,
        string action,
        CancellationToken ct)
    {
        var roleAdmin = await security.CheckRuntimeAsync(
            context,
            runtime,
            SecurityCapability.UserRoleAdmin,
            cancellationToken: ct);
        if (roleAdmin.Allowed) return (roleAdmin, null);

        if (roleAdmin.IsAuthenticated)
        {
            var systemAdmin = await security.CheckRuntimeAsync(
                context,
                runtime,
                SecurityCapability.SystemAdmin,
                cancellationToken: ct);
            if (systemAdmin.Allowed) return (systemAdmin, null);
        }

        var failure = roleAdmin.FailureResult() ?? Results.Json(
            new { error = "Forbidden." },
            statusCode: StatusCodes.Status403Forbidden);
        await audit.RecordAuthorizationDeniedAsync(
            context,
            roleAdmin,
            action,
            "local-authority",
            "backup",
            new Dictionary<string, string>
            {
                ["requiredCapabilities"] = "UserRoleAdmin|SystemAdmin"
            });
        return (roleAdmin, failure);
    }

    private static async Task<bool> HasEnabledLocalAdministratorAsync(
        IEnumerable<LocalUserAccount> users,
        ApiAuthorizationService security,
        ScadaRuntimeFacade runtime,
        CancellationToken ct)
    {
        foreach (var user in users.Where(user => user.IsEnabled))
        {
            var candidate = new SecurityPrincipal(
                user.Id.ToString(),
                user.DisplayName,
                LocalIdentityNormalization.NormalizeRoles(user.Roles),
                true);

            var roleAdmin = await security.CheckRuntimeAsync(
                candidate,
                runtime,
                SecurityCapability.UserRoleAdmin,
                cancellationToken: ct);
            if (roleAdmin.Allowed) return true;

            var systemAdmin = await security.CheckRuntimeAsync(
                candidate,
                runtime,
                SecurityCapability.SystemAdmin,
                cancellationToken: ct);
            if (systemAdmin.Allowed) return true;
        }

        return false;
    }

    private static int RevokeRealtimeSubjects(
        TagRealtimeHub realtime,
        IEnumerable<LocalUserAccount> previous,
        IEnumerable<LocalUserAccount> replacement)
    {
        var subjectIds = previous
            .Select(user => user.Id)
            .Concat(replacement.Select(user => user.Id))
            .Distinct()
            .Select(id => id.ToString());

        var revoked = 0;
        foreach (var subjectId in subjectIds)
            revoked += realtime.RevokeSubject(subjectId);
        return revoked;
    }

    private static Dictionary<string, string> PreviewAuditDetails(AuthorityBackupPreview preview) => new()
    {
        ["userCount"] = preview.UserCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["enabledUserCount"] = preview.EnabledUserCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["enabledAdministratorCount"] = preview.EnabledAdministratorCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
    };

    private static IResult? BootstrapFailure(LocalIdentityApi.InitialAdministratorBootstrapStatus status)
    {
        if (!status.Required)
        {
            return Results.Conflict(new
            {
                error = "Anonymous Authority restore is already closed. Sign in with an existing account."
            });
        }

        if (!status.Available)
        {
            return Results.Conflict(new
            {
                error = "Anonymous Authority restore is not available because this server cannot prove that the installation is empty.",
                reason = status.BlockedReason
            });
        }

        return null;
    }

    private static IResult InvalidBackup() => Results.BadRequest(new
    {
        error = "Authority backup could not be opened. Verify the backup password, file integrity, and compatibility."
    });

    private static SecurityPrincipal AnonymousPrincipal() =>
        new("anonymous", null, Array.Empty<string>(), false);
}
