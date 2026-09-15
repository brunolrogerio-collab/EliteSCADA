using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Security.Authentication;

namespace Scada.Api.Security;

/// <summary>
/// Coordinates a complete Authority replacement. The backup is opened and validated while the
/// current Authority is intact; only then does it cross the durable detach/attach journals.
/// </summary>
public sealed class AuthoritySwitchService(
    IAuthorityLifecycleStore lifecycle,
    AuthorityDetachService detach,
    AuthorityAttachService attach,
    ILocalIdentityStore identities,
    AuthorityBackupService backups)
{
    public async Task<AuthoritySwitchResult> SwitchAsync(
        string backup,
        string password,
        CancellationToken cancellationToken = default)
    {
        await using var operation = await lifecycle.AcquireOperationLeaseAsync(cancellationToken);
        var prepared = await PrepareAsync(backup, password, cancellationToken);
        return await SwitchPreparedAsync(prepared, cancellationToken);
    }

    private async Task<AuthoritySwitchPreparation> PrepareAsync(
        string backup,
        string password,
        CancellationToken cancellationToken = default)
    {
        var opened = backups.Open(backup, password);
        var currentAccounts = await identities.ListAsync(cancellationToken);
        var target = PrepareTarget(opened, currentAccounts);
        return new AuthoritySwitchPreparation(target, opened.Preview);
    }

    private async Task<AuthoritySwitchResult> SwitchPreparedAsync(
        AuthoritySwitchPreparation prepared,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        var detached = await detach.DetachAsync(cancellationToken);
        var attached = await attach.AttachAsync(prepared.Target, cancellationToken);
        return new AuthoritySwitchResult(detached, attached, prepared.Preview);
    }

    internal static AuthorityAttachTarget PrepareTarget(
        AuthorityBackupOpenResult opened,
        IReadOnlyCollection<LocalUserAccount> currentAccounts)
    {
        ArgumentNullException.ThrowIfNull(opened);
        ArgumentNullException.ThrowIfNull(currentAccounts);
        var policy = opened.Policy
            ?? throw new InvalidDataException("AUTHORITY_POLICY_REQUIRED: a complete Authority backup is required for switch.");
        if (policy.Version < 0)
            throw new InvalidDataException("Authority backup policy version is invalid.");

        SecurityRoleEngineeringDto[] roles;
        SecurityScopeEngineeringDto[] scopes;
        try
        {
            roles = JsonSerializer.Deserialize<SecurityRoleEngineeringDto[]>(policy.RolesJson)
                ?? throw new InvalidDataException("Authority backup policy roles are missing.");
            scopes = JsonSerializer.Deserialize<SecurityScopeEngineeringDto[]>(policy.ScopesJson)
                ?? throw new InvalidDataException("Authority backup policy scopes are missing.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Authority backup policy is not valid JSON.", exception);
        }

        var accounts = AuthorityRestoreSecurity.RefreshSecurityVersions(opened.Accounts, currentAccounts);
        var target = new AuthorityAttachTarget(accounts, roles, scopes);
        AuthorityAttachService.ValidateTarget(target);
        return target;
    }

    private sealed record AuthoritySwitchPreparation(
        AuthorityAttachTarget Target,
        AuthorityBackupPreview Preview);
}

public sealed record AuthoritySwitchResult(
    AuthorityDetachResult Detach,
    AuthorityAttachResult Attach,
    AuthorityBackupPreview Preview);
