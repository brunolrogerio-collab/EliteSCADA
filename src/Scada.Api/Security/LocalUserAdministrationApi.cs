using Scada.Api.Realtime;
using Scada.Api.Runtime;
using Scada.Engineering.Security;
using Scada.Security.Audit;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Api.Security;

public sealed record LocalUserAdminResponse(
    Guid Id,
    string Username,
    string DisplayName,
    bool IsEnabled,
    IReadOnlyCollection<string> Roles,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record LocalRoleAdminResponse(
    string Key,
    string Name,
    string? Description);

public sealed record CreateLocalUserRequest(
    string Username,
    string DisplayName,
    string Password,
    IReadOnlyCollection<string>? Roles = null,
    bool IsEnabled = true);

public sealed record UpdateLocalUserRequest(
    string DisplayName,
    bool IsEnabled,
    IReadOnlyCollection<string>? Roles = null);

public sealed record ResetLocalUserPasswordRequest(string Password);

public static class LocalUserAdministrationApi
{
    private const string ListAction = "auth.user.list";
    private const string CreateAction = "auth.user.create";
    private const string UpdateAction = "auth.user.update";
    private const string ResetPasswordAction = "auth.user.password_reset";
    private const string RolesAction = "auth.role.list";

    public static IEndpointRouteBuilder MapLocalUserAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth/users", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            ILocalIdentityStore store,
            CancellationToken ct) =>
        {
            var authorization = await AuthorizeAsync(context, runtime, security, audit, ListAction, "users", ct);
            if (authorization.Failure is not null) return authorization.Failure;

            var users = await store.ListAsync(ct);
            await audit.RecordAsync(
                context,
                authorization.Check!.Principal,
                ListAction,
                AuditOutcome.Succeeded,
                "local-users",
                "all",
                new Dictionary<string, string>
                {
                    ["resultCount"] = users.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            return Results.Ok(users.Select(ToResponse).ToArray());
        });

        endpoints.MapGet("/api/auth/roles", async (
            HttpContext context,
            ScadaRuntimeFacade runtime,
            EngineeringWorkspace workspace,
            ApiAuthorizationService security,
            ApiAuditService audit,
            CancellationToken ct) =>
        {
            var authorization = await AuthorizeAsync(context, runtime, security, audit, RolesAction, "roles", ct);
            if (authorization.Failure is not null) return authorization.Failure;

            var roles = workspace.SecurityPolicies.SnapshotRoles()
                .OrderBy(role => role.Key, StringComparer.OrdinalIgnoreCase)
                .Select(role => new LocalRoleAdminResponse(role.Key, role.Name, role.Description))
                .ToArray();
            await audit.RecordAsync(
                context,
                authorization.Check!.Principal,
                RolesAction,
                AuditOutcome.Succeeded,
                "security-roles",
                "engineering-workspace",
                new Dictionary<string, string>
                {
                    ["resultCount"] = roles.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            return Results.Ok(roles);
        });

        endpoints.MapPost("/api/auth/users", async (
            CreateLocalUserRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            EngineeringWorkspace workspace,
            ApiAuthorizationService security,
            ApiAuditService audit,
            ILocalIdentityStore store,
            CancellationToken ct) =>
        {
            var authorization = await AuthorizeAsync(context, runtime, security, audit, CreateAction, "new", ct);
            if (authorization.Failure is not null) return authorization.Failure;

            try
            {
                var username = request.Username?.Trim() ?? string.Empty;
                var displayName = request.DisplayName?.Trim() ?? string.Empty;
                if (displayName.Length is < 1 or > 300)
                    return Results.BadRequest(new { error = "Display name must contain between 1 and 300 characters." });

                var normalizedUsername = LocalIdentityNormalization.NormalizeUsername(username);
                LocalPasswordHasher.ValidatePassword(request.Password);
                var roles = ValidateRoles(request.Roles, workspace);
                if (roles.Unknown.Length > 0)
                    return UnknownRoles(roles.Unknown);

                if (await store.FindByUsernameAsync(username, ct) is not null)
                    return Results.Conflict(new { error = "A local user with this username already exists." });

                var now = DateTimeOffset.UtcNow;
                var account = new LocalUserAccount(
                    Guid.NewGuid(),
                    username,
                    normalizedUsername,
                    displayName,
                    request.IsEnabled,
                    roles.Normalized,
                    LocalPasswordHasher.Hash(request.Password),
                    now,
                    now);
                await store.CreateAsync(account, ct);

                await audit.RecordAsync(
                    context,
                    authorization.Check!.Principal,
                    CreateAction,
                    AuditOutcome.Succeeded,
                    "local-user",
                    account.Id.ToString(),
                    new Dictionary<string, string>
                    {
                        ["username"] = account.Username,
                        ["enabled"] = account.IsEnabled.ToString(),
                        ["roles"] = string.Join(",", account.Roles)
                    });
                return Results.Created($"/api/auth/users/{account.Id}", ToResponse(account));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        endpoints.MapPut("/api/auth/users/{id:guid}", async (
            Guid id,
            UpdateLocalUserRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            EngineeringWorkspace workspace,
            ApiAuthorizationService security,
            ApiAuditService audit,
            ILocalIdentityStore store,
            TagRealtimeHub realtime,
            CancellationToken ct) =>
        {
            var authorization = await AuthorizeAsync(context, runtime, security, audit, UpdateAction, id.ToString(), ct);
            if (authorization.Failure is not null) return authorization.Failure;

            var current = await store.FindByIdAsync(id, ct);
            if (current is null) return Results.NotFound();

            var displayName = request.DisplayName?.Trim() ?? string.Empty;
            if (displayName.Length is < 1 or > 300)
                return Results.BadRequest(new { error = "Display name must contain between 1 and 300 characters." });

            var roles = ValidateRoles(request.Roles, workspace);
            if (roles.Unknown.Length > 0)
                return UnknownRoles(roles.Unknown);

            var updated = current with
            {
                DisplayName = displayName,
                IsEnabled = request.IsEnabled,
                Roles = roles.Normalized,
                UpdatedAtUtc = NextSecurityVersion(current.UpdatedAtUtc)
            };

            var users = await store.ListAsync(ct);
            var projected = users.Select(user => user.Id == id ? updated : user).ToArray();
            if (!projected.Any(user => user.IsEnabled && GrantsUserAdministration(user.Roles, workspace)))
            {
                return Results.BadRequest(new
                {
                    error = "At least one enabled local user must retain the UserRoleAdmin capability."
                });
            }

            await store.UpdateAsync(updated, ct);
            var revokedRealtimeClients = realtime.RevokeSubject(updated.Id.ToString());
            await audit.RecordAsync(
                context,
                authorization.Check!.Principal,
                UpdateAction,
                AuditOutcome.Succeeded,
                "local-user",
                id.ToString(),
                new Dictionary<string, string>
                {
                    ["username"] = updated.Username,
                    ["enabled"] = updated.IsEnabled.ToString(),
                    ["roles"] = string.Join(",", updated.Roles),
                    ["revokedRealtimeClients"] = revokedRealtimeClients.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            return Results.Ok(ToResponse(updated));
        });

        endpoints.MapPost("/api/auth/users/{id:guid}/password-reset", async (
            Guid id,
            ResetLocalUserPasswordRequest request,
            HttpContext context,
            ScadaRuntimeFacade runtime,
            ApiAuthorizationService security,
            ApiAuditService audit,
            ILocalIdentityStore store,
            TagRealtimeHub realtime,
            CancellationToken ct) =>
        {
            var authorization = await AuthorizeAsync(context, runtime, security, audit, ResetPasswordAction, id.ToString(), ct);
            if (authorization.Failure is not null) return authorization.Failure;

            var current = await store.FindByIdAsync(id, ct);
            if (current is null) return Results.NotFound();

            try
            {
                LocalPasswordHasher.ValidatePassword(request.Password);
                var updated = current with
                {
                    Credential = LocalPasswordHasher.Hash(request.Password),
                    UpdatedAtUtc = NextSecurityVersion(current.UpdatedAtUtc)
                };
                await store.UpdateAsync(updated, ct);
                var revokedRealtimeClients = realtime.RevokeSubject(updated.Id.ToString());
                await audit.RecordAsync(
                    context,
                    authorization.Check!.Principal,
                    ResetPasswordAction,
                    AuditOutcome.Succeeded,
                    "local-user",
                    id.ToString(),
                    new Dictionary<string, string>
                    {
                        ["username"] = updated.Username,
                        ["revokedRealtimeClients"] = revokedRealtimeClients.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });
                return Results.NoContent();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return endpoints;
    }

    private static async Task<(ApiAuthorizationCheck? Check, IResult? Failure)> AuthorizeAsync(
        HttpContext context,
        ScadaRuntimeFacade runtime,
        ApiAuthorizationService security,
        ApiAuditService audit,
        string action,
        string targetId,
        CancellationToken ct)
    {
        var check = await security.CheckRuntimeAsync(
            context,
            runtime,
            SecurityCapability.UserRoleAdmin,
            cancellationToken: ct);
        var failure = check.FailureResult();
        if (failure is null) return (check, null);

        await audit.RecordAuthorizationDeniedAsync(
            context,
            check,
            action,
            "local-user-administration",
            targetId);
        return (check, failure);
    }

    private static (IReadOnlyCollection<string> Normalized, string[] Unknown) ValidateRoles(
        IReadOnlyCollection<string>? requested,
        EngineeringWorkspace workspace)
    {
        var normalized = LocalIdentityNormalization.NormalizeRoles(requested ?? Array.Empty<string>());
        var known = workspace.SecurityPolicies.SnapshotRoles()
            .Select(role => role.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = normalized.Where(role => !known.Contains(role)).ToArray();
        return (normalized, unknown);
    }

    private static IResult UnknownRoles(IReadOnlyCollection<string> unknown) =>
        Results.BadRequest(new
        {
            error = "One or more assigned role keys are not defined in the current Engineering workspace.",
            unknownRoles = unknown
        });

    private static bool GrantsUserAdministration(
        IReadOnlyCollection<string> roles,
        EngineeringWorkspace workspace)
    {
        var policies = new InMemoryCapabilityAuthorizationService(
            SecurityPolicyCompiler.Compile(workspace.SecurityPolicies.SnapshotRoles()));
        var candidate = new SecurityPrincipal(
            "local-admin-safety-check",
            "Local admin safety check",
            roles,
            true);
        return policies.Evaluate(candidate, SecurityCapability.UserRoleAdmin).Allowed;
    }

    private static DateTimeOffset NextSecurityVersion(DateTimeOffset previous)
    {
        var now = DateTimeOffset.UtcNow;
        return now.ToUnixTimeMilliseconds() > previous.ToUnixTimeMilliseconds()
            ? now
            : previous.AddMilliseconds(1);
    }

    private static LocalUserAdminResponse ToResponse(LocalUserAccount account) => new(
        account.Id,
        account.Username,
        account.DisplayName,
        account.IsEnabled,
        LocalIdentityNormalization.NormalizeRoles(account.Roles),
        account.CreatedAtUtc,
        account.UpdatedAtUtc);
}
