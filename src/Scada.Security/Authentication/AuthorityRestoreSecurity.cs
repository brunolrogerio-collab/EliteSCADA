namespace Scada.Security.Authentication;

public static class AuthorityRestoreSecurity
{
    /// <summary>
    /// Preserves restored local identities and password credentials while advancing every
    /// local security version so JWTs issued before the restore cannot remain valid.
    /// </summary>
    public static IReadOnlyCollection<LocalUserAccount> RefreshSecurityVersions(
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
            ArgumentNullException.ThrowIfNull(account);
            var previousMs = account.UpdatedAtUtc.ToUnixTimeMilliseconds();
            if (currentById.TryGetValue(account.Id, out var existing))
                previousMs = Math.Max(previousMs, existing.UpdatedAtUtc.ToUnixTimeMilliseconds());
            if (previousMs >= maximumMs)
                throw new InvalidDataException("Authority backup contains a security version that cannot be advanced safely.");

            var nextMs = Math.Max(nowMs, previousMs + 1);
            refreshed.Add(account with
            {
                UpdatedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(nextMs),
                Roles = account.Roles.ToArray(),
                Credential = account.Credential.DeepCopy()
            });
        }

        return refreshed;
    }
}
