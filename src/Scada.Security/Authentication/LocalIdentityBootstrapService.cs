namespace Scada.Security.Authentication;

public sealed record InitialAdministratorCreationResult(
    bool Created,
    LocalUserAccount? Account);

public sealed class LocalIdentityBootstrapService(ILocalIdentityStore store)
{
    public const string InitialAdministratorRole = "developer";

    public async Task<bool> IsInitialAdministratorRequiredAsync(CancellationToken cancellationToken = default) =>
        await store.CountAsync(cancellationToken) == 0;

    public async Task<InitialAdministratorCreationResult> CreateInitialAdministratorAsync(
        string username,
        string? displayName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var trimmedUsername = username?.Trim() ?? string.Empty;
        var trimmedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? trimmedUsername
            : displayName.Trim();

        var normalizedUsername = LocalIdentityNormalization.NormalizeUsername(trimmedUsername);
        if (trimmedDisplayName.Length is < 1 or > 300)
            throw new ArgumentOutOfRangeException(nameof(displayName), "Display name must contain between 1 and 300 characters.");
        LocalPasswordHasher.ValidatePassword(password);

        await using var mutationLease = await store.AcquireMutationLeaseAsync(cancellationToken);
        if (await store.CountAsync(cancellationToken) > 0)
            return new InitialAdministratorCreationResult(false, null);

        var now = DateTimeOffset.UtcNow;
        var account = new LocalUserAccount(
            Guid.NewGuid(),
            trimmedUsername,
            normalizedUsername,
            trimmedDisplayName,
            true,
            new[] { InitialAdministratorRole },
            LocalPasswordHasher.Hash(password),
            now,
            now);

        await store.CreateAsync(account, cancellationToken);
        return new InitialAdministratorCreationResult(true, account);
    }
}
