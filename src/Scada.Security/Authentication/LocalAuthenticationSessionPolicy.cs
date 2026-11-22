namespace Scada.Security.Authentication;

/// <summary>
/// Controls the maximum lifetime of JWTs issued by the local username/password login.
/// It does not change already issued tokens or Public Runtime visitor tokens.
/// </summary>
public sealed record LocalAuthenticationSessionPolicy(long Version, int AccessTokenMinutes)
{
    public const int DefaultAccessTokenMinutes = 480;
    public const int MinimumAccessTokenMinutes = 5;
    public const int MaximumAccessTokenMinutes = 1440;

    public static int ValidateAccessTokenMinutes(int minutes)
    {
        if (minutes is < MinimumAccessTokenMinutes or > MaximumAccessTokenMinutes)
            throw new ArgumentOutOfRangeException(
                nameof(minutes),
                $"Local login session duration must be between {MinimumAccessTokenMinutes} and {MaximumAccessTokenMinutes} minutes.");

        return minutes;
    }
}

public interface ILocalAuthenticationSessionPolicyStore
{
    Task InitializeAsync(int initialAccessTokenMinutes, CancellationToken cancellationToken = default);
    Task<LocalAuthenticationSessionPolicy> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the policy only when the caller has observed the current version.
    /// A null result means another administrator saved a newer version first.
    /// </summary>
    Task<LocalAuthenticationSessionPolicy?> TryUpdateAsync(
        long expectedVersion,
        int accessTokenMinutes,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryLocalAuthenticationSessionPolicyStore : ILocalAuthenticationSessionPolicyStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LocalAuthenticationSessionPolicy? _policy;

    public async Task InitializeAsync(int initialAccessTokenMinutes, CancellationToken cancellationToken = default)
    {
        LocalAuthenticationSessionPolicy.ValidateAccessTokenMinutes(initialAccessTokenMinutes);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _policy ??= new LocalAuthenticationSessionPolicy(1, initialAccessTokenMinutes);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LocalAuthenticationSessionPolicy> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _policy ??= new LocalAuthenticationSessionPolicy(
                1,
                LocalAuthenticationSessionPolicy.DefaultAccessTokenMinutes);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LocalAuthenticationSessionPolicy?> TryUpdateAsync(
        long expectedVersion,
        int accessTokenMinutes,
        CancellationToken cancellationToken = default)
    {
        LocalAuthenticationSessionPolicy.ValidateAccessTokenMinutes(accessTokenMinutes);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = _policy ??= new LocalAuthenticationSessionPolicy(
                1,
                LocalAuthenticationSessionPolicy.DefaultAccessTokenMinutes);
            if (current.Version != expectedVersion) return null;

            return _policy = new LocalAuthenticationSessionPolicy(
                checked(current.Version + 1),
                accessTokenMinutes);
        }
        finally
        {
            _gate.Release();
        }
    }
}
