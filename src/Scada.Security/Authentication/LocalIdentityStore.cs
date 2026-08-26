namespace Scada.Security.Authentication;

public interface ILocalIdentityStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<LocalUserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<LocalUserAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LocalUserAccount>> ListAsync(CancellationToken cancellationToken = default);
    Task CreateAsync(LocalUserAccount account, CancellationToken cancellationToken = default);
    Task UpdateAsync(LocalUserAccount account, CancellationToken cancellationToken = default);
}

public sealed class InMemoryLocalIdentityStore : ILocalIdentityStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, LocalUserAccount> _byId = new();
    private readonly Dictionary<string, Guid> _byUsername = new(StringComparer.OrdinalIgnoreCase);

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) return Task.FromResult(_byId.Count);
    }

    public Task<LocalUserAccount?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = LocalIdentityNormalization.NormalizeUsername(username);
        lock (_gate)
        {
            if (!_byUsername.TryGetValue(normalized, out var id) || !_byId.TryGetValue(id, out var account))
                return Task.FromResult<LocalUserAccount?>(null);
            return Task.FromResult<LocalUserAccount?>(account.Clone());
        }
    }

    public Task<LocalUserAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return Task.FromResult(_byId.TryGetValue(id, out var account) ? account.Clone() : null);
    }

    public Task<IReadOnlyCollection<LocalUserAccount>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyCollection<LocalUserAccount> users = _byId.Values
                .OrderBy(account => account.Username, StringComparer.OrdinalIgnoreCase)
                .Select(account => account.Clone())
                .ToArray();
            return Task.FromResult(users);
        }
    }

    public Task CreateAsync(LocalUserAccount account, CancellationToken cancellationToken = default)
    {
        Validate(account);
        lock (_gate)
        {
            if (_byId.ContainsKey(account.Id))
                throw new InvalidOperationException($"Local user ID '{account.Id}' already exists.");
            if (_byUsername.ContainsKey(account.NormalizedUsername))
                throw new InvalidOperationException($"Local username '{account.Username}' already exists.");

            var copy = account.Clone();
            _byId.Add(copy.Id, copy);
            _byUsername.Add(copy.NormalizedUsername, copy.Id);
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(LocalUserAccount account, CancellationToken cancellationToken = default)
    {
        Validate(account);
        lock (_gate)
        {
            if (!_byId.TryGetValue(account.Id, out var current))
                throw new KeyNotFoundException($"Local user '{account.Id}' was not found.");

            if (!string.Equals(current.NormalizedUsername, account.NormalizedUsername, StringComparison.OrdinalIgnoreCase) &&
                _byUsername.TryGetValue(account.NormalizedUsername, out var otherId) &&
                otherId != account.Id)
                throw new InvalidOperationException($"Local username '{account.Username}' already exists.");

            _byUsername.Remove(current.NormalizedUsername);
            var copy = account.Clone();
            _byId[copy.Id] = copy;
            _byUsername[copy.NormalizedUsername] = copy.Id;
        }
        return Task.CompletedTask;
    }

    private static void Validate(LocalUserAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.Id == Guid.Empty) throw new ArgumentException("Local user ID is required.", nameof(account));
        if (string.IsNullOrWhiteSpace(account.Username)) throw new ArgumentException("Username is required.", nameof(account));
        if (string.IsNullOrWhiteSpace(account.DisplayName)) throw new ArgumentException("Display name is required.", nameof(account));
        var normalized = LocalIdentityNormalization.NormalizeUsername(account.Username);
        if (!string.Equals(normalized, account.NormalizedUsername, StringComparison.Ordinal))
            throw new ArgumentException("Normalized username does not match username.", nameof(account));
        if (account.Credential.Salt.Length < 16 || account.Credential.Hash.Length < 16)
            throw new ArgumentException("Password credential is invalid.", nameof(account));
        if (account.CreatedAtUtc == default || account.UpdatedAtUtc == default || account.UpdatedAtUtc < account.CreatedAtUtc)
            throw new ArgumentException("User timestamps are invalid.", nameof(account));
    }
}
