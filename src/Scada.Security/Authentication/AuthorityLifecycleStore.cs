namespace Scada.Security.Authentication;

/// <summary>
/// Durable lifecycle marker for the Security Authority installed on this node group.
/// The marker is intentionally independent from identities and policy so a crash cannot
/// turn an empty table into an implicit, anonymously recoverable Authority.
/// </summary>
public enum AuthorityLifecycleState
{
    InitialInstallation,
    AuthorityPresent,
    DetachInProgress,
    DeliberatelyDetached,
    AttachInProgress,
    Invalid
}

public sealed record AuthorityLifecycleSnapshot(AuthorityLifecycleState State, long Epoch)
{
    public bool AllowsAuthorization => State == AuthorityLifecycleState.AuthorityPresent;
}

/// <summary>
/// Coordinates the Authority half of an installation detach. Implementations must persist
/// transitions and serialize them across processes. The caller clears Authority-owned data
/// idempotently after <see cref="BeginDetachAsync"/> and only then completes the detach.
/// </summary>
public interface IAuthorityLifecycleStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<AuthorityLifecycleSnapshot> GetAsync(CancellationToken cancellationToken = default);
    Task<AuthorityLifecycleSnapshot> MarkAuthorityPresentAsync(CancellationToken cancellationToken = default);
    Task<AuthorityLifecycleSnapshot> MarkInvalidAsync(CancellationToken cancellationToken = default);
    Task<AuthorityLifecycleSnapshot> BeginDetachAsync(CancellationToken cancellationToken = default);
    Task<AuthorityLifecycleSnapshot> CompleteDetachAsync(CancellationToken cancellationToken = default);
    Task<AuthorityLifecycleSnapshot> BeginAttachAsync(CancellationToken cancellationToken = default);
    Task<AuthorityLifecycleSnapshot> CompleteAttachAsync(CancellationToken cancellationToken = default);
    Task<AuthorityLifecycleSnapshot> AbortAttachAsync(CancellationToken cancellationToken = default);
}

/// <summary>In-memory implementation for non-durable development hosts and focused tests.</summary>
public sealed class InMemoryAuthorityLifecycleStore : IAuthorityLifecycleStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AuthorityLifecycleSnapshot _snapshot = new(AuthorityLifecycleState.InitialInstallation, 1);

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<AuthorityLifecycleSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return _snapshot; }
        finally { _gate.Release(); }
    }

    public Task<AuthorityLifecycleSnapshot> MarkAuthorityPresentAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(
            AuthorityLifecycleState.InitialInstallation,
            AuthorityLifecycleState.AuthorityPresent,
            advanceEpoch: false,
            idempotentState: AuthorityLifecycleState.AuthorityPresent,
            cancellationToken);

    public async Task<AuthorityLifecycleSnapshot> MarkInvalidAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_snapshot.State != AuthorityLifecycleState.Invalid)
                _snapshot = _snapshot with { State = AuthorityLifecycleState.Invalid };
            return _snapshot;
        }
        finally { _gate.Release(); }
    }

    public Task<AuthorityLifecycleSnapshot> BeginDetachAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(
            AuthorityLifecycleState.AuthorityPresent,
            AuthorityLifecycleState.DetachInProgress,
            advanceEpoch: false,
            idempotentState: AuthorityLifecycleState.DetachInProgress,
            cancellationToken);

    public Task<AuthorityLifecycleSnapshot> CompleteDetachAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(
            AuthorityLifecycleState.DetachInProgress,
            AuthorityLifecycleState.DeliberatelyDetached,
            advanceEpoch: true,
            idempotentState: AuthorityLifecycleState.DeliberatelyDetached,
            cancellationToken);

    public Task<AuthorityLifecycleSnapshot> BeginAttachAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(
            AuthorityLifecycleState.DeliberatelyDetached,
            AuthorityLifecycleState.AttachInProgress,
            advanceEpoch: false,
            idempotentState: AuthorityLifecycleState.AttachInProgress,
            cancellationToken);

    public Task<AuthorityLifecycleSnapshot> CompleteAttachAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(
            AuthorityLifecycleState.AttachInProgress,
            AuthorityLifecycleState.AuthorityPresent,
            advanceEpoch: true,
            idempotentState: AuthorityLifecycleState.AuthorityPresent,
            cancellationToken);

    public Task<AuthorityLifecycleSnapshot> AbortAttachAsync(CancellationToken cancellationToken = default) =>
        TransitionAsync(
            AuthorityLifecycleState.AttachInProgress,
            AuthorityLifecycleState.DeliberatelyDetached,
            advanceEpoch: true,
            idempotentState: AuthorityLifecycleState.DeliberatelyDetached,
            cancellationToken);

    private async Task<AuthorityLifecycleSnapshot> TransitionAsync(
        AuthorityLifecycleState expected,
        AuthorityLifecycleState next,
        bool advanceEpoch,
        AuthorityLifecycleState idempotentState,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_snapshot.State == idempotentState) return _snapshot;
            if (_snapshot.State != expected)
                throw new InvalidOperationException($"Authority lifecycle cannot transition from '{_snapshot.State}' to '{next}'.");

            _snapshot = new AuthorityLifecycleSnapshot(
                next,
                advanceEpoch ? checked(_snapshot.Epoch + 1) : _snapshot.Epoch);
            return _snapshot;
        }
        finally { _gate.Release(); }
    }
}

public static class AuthorityLifecycleSessionFence
{
    /// <summary>Accepts a local session only while the durable Authority is present at its issuing epoch.</summary>
    public static bool IsCurrent(AuthorityLifecycleSnapshot snapshot, long tokenEpoch) =>
        snapshot.AllowsAuthorization && tokenEpoch > 0 && snapshot.Epoch == tokenEpoch;
}
