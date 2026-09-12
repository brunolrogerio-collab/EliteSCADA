using Scada.Api.Realtime;
using Scada.Api.Runtime;
using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.Security;
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
            IAuthorityPolicyStore policyStore,
            AuthorityBackupService backup,
            CancellationToken ct) =>
        {
            var authorization = await AuthorizeAdministrationAsync(
                context, runtime, security, audit, ExportAction, ct);
            if (authorization.Failure is not null) return authorization.Failure;

            try
            {
                var users = await store.ListAsync(ct);
                var policy = policyStore.Snapshot();
                var payload = backup.Export(
                    users,
                    new AuthorityBackupPolicyPayload(
                        policy.Version,
                        JsonSerializer.Serialize(policy.Roles),
                        JsonSerializer.Serialize(policy.Scopes)),
                    request.Password);
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
            IAuthorityPolicyStore policyStore,
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

            if (opened.Policy is null)
                return Results.Conflict(new { error = "AUTHORITY_POLICY_REQUIRED: v1 backups are readable but cannot restore a complete Security Authority." });

            if (!TryReadPolicy(opened.Policy, out var roles, out var scopes)) return InvalidBackup();

            var currentUsers = await store.ListAsync(ct);
            IReadOnlyCollection<LocalUserAccount> replacement;
            try
            {
                replacement = AuthorityRestoreSecurity.RefreshSecurityVersions(opened.Accounts, currentUsers);
            }
            catch (InvalidDataException)
            {
                return InvalidBackup();
            }

            if (!HasPolicyAdministrator(replacement, roles!, scopes!))
            {
                return Results.BadRequest(new
                {
                    error = "The restored Authority must retain at least one enabled local user with UserRoleAdmin or SystemAdmin in the active runtime policy."
                });
            }

            var previousPolicy = policyStore.Snapshot();
            var policyRestore = await policyStore.TryReplaceAsync(previousPolicy.Version, roles!, scopes!, ct);
            if (!policyRestore.Applied)
                return Results.Conflict(new { error = policyRestore.Error, currentVersion = policyRestore.Snapshot.Version });

            try { await store.ReplaceAllAsync(replacement, ct); }
            catch
            {
                await policyStore.TryReplaceAsync(policyRestore.Snapshot.Version, previousPolicy.Roles, previousPolicy.Scopes, CancellationToken.None);
                throw;
            }
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
            IAuthorityPolicyStore policyStore,
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

            if (opened.Policy is null)
                return Results.Conflict(new { error = "AUTHORITY_POLICY_REQUIRED: v1 backups are readable but cannot restore a complete Security Authority." });

            if (!TryReadPolicy(opened.Policy, out var roles, out var scopes)) return InvalidBackup();

            await using var installationLease = await installationGate.EnterAsync(ct);
            var status = await LocalIdentityApi.ResolveBootstrapStatusAsync(
                context, localRuntime, bootstrapStatus, ct);
            var blocked = BootstrapFailure(status);
            if (blocked is not null) return blocked;

            IReadOnlyCollection<LocalUserAccount> replacement;
            try
            {
                replacement = AuthorityRestoreSecurity.RefreshSecurityVersions(
                    opened.Accounts,
                    Array.Empty<LocalUserAccount>());
            }
            catch (InvalidDataException)
            {
                return InvalidBackup();
            }

            var previousPolicy = policyStore.Snapshot();
            var policyRestore = await policyStore.TryReplaceAsync(previousPolicy.Version, roles!, scopes!, ct);
            if (!policyRestore.Applied)
                return Results.Conflict(new { error = policyRestore.Error, currentVersion = policyRestore.Snapshot.Version });

            if (!await store.TryReplaceAllIfEmptyAsync(replacement, ct))
            {
                await policyStore.TryReplaceAsync(policyRestore.Snapshot.Version, previousPolicy.Roles, previousPolicy.Scopes, CancellationToken.None);
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

    private static bool TryReadPolicy(
        AuthorityBackupPolicyPayload policy,
        out SecurityRoleEngineeringDto[]? roles,
        out SecurityScopeEngineeringDto[]? scopes)
    {
        roles = null;
        scopes = null;
        try
        {
            if (policy.Version < 0) return false;
            roles = JsonSerializer.Deserialize<SecurityRoleEngineeringDto[]>(policy.RolesJson);
            scopes = JsonSerializer.Deserialize<SecurityScopeEngineeringDto[]>(policy.ScopesJson);
            if (roles is null || scopes is null) return false;
            InMemoryAuthorityPolicyStore.Validate(roles, scopes);
            return true;
        }
        catch (JsonException) { return false; }
        catch (InvalidDataException) { return false; }
    }

    private static bool HasPolicyAdministrator(
        IEnumerable<LocalUserAccount> users,
        IReadOnlyCollection<SecurityRoleEngineeringDto> roles,
        IReadOnlyCollection<SecurityScopeEngineeringDto> scopes)
    {
        if (!SecurityScopeGraph.TryCreate(scopes, out var graph, out _)) return false;
        var authorization = new InMemoryCapabilityAuthorizationService(SecurityPolicyCompiler.Compile(roles));
        return users.Where(user => user.IsEnabled).Any(user =>
        {
            var principal = new SecurityPrincipal(user.Id.ToString(), user.DisplayName, LocalIdentityNormalization.NormalizeRoles(user.Roles), true);
            return authorization.Evaluate(principal, SecurityCapability.UserRoleAdmin, graph!.Enrich(new AuthorizationResource())).Allowed ||
                authorization.Evaluate(principal, SecurityCapability.SystemAdmin, graph.Enrich(new AuthorizationResource())).Allowed;
        });
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
