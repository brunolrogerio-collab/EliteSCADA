using System.Security.Cryptography;

namespace Scada.Security.Authentication;

public sealed record PasswordCredential(byte[] Salt, byte[] Hash, int Iterations)
{
    public PasswordCredential DeepCopy() => new(Salt.ToArray(), Hash.ToArray(), Iterations);
}

public sealed record LocalUserAccount(
    Guid Id,
    string Username,
    string NormalizedUsername,
    string DisplayName,
    bool IsEnabled,
    IReadOnlyCollection<string> Roles,
    PasswordCredential Credential,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public LocalUserAccount DeepCopy() => this with
    {
        Roles = Roles.ToArray(),
        Credential = Credential.DeepCopy()
    };
}

public static class LocalIdentityNormalization
{
    public static string NormalizeUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.", nameof(username));

        var trimmed = username.Trim();
        if (trimmed.Length is < 3 or > 200)
            throw new ArgumentOutOfRangeException(nameof(username), "Username must contain between 3 and 200 characters.");

        return trimmed.ToUpperInvariant();
    }

    public static IReadOnlyCollection<string> NormalizeRoles(IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        return roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public static class LocalPasswordHasher
{
    public const int DefaultIterations = 210_000;
    private const int SaltSize = 32;
    private const int HashSize = 32;

    public static PasswordCredential Hash(string password, int iterations = DefaultIterations)
    {
        ValidatePassword(password);
        if (iterations < 100_000)
            throw new ArgumentOutOfRangeException(nameof(iterations), "Password hashing iterations must be at least 100000.");

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return new PasswordCredential(salt, hash, iterations);
    }

    public static bool Verify(string password, PasswordCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        if (string.IsNullOrEmpty(password) || credential.Salt.Length < 16 || credential.Hash.Length != HashSize || credential.Iterations < 100_000)
            return false;

        var candidate = Rfc2898DeriveBytes.Pbkdf2(
            password,
            credential.Salt,
            credential.Iterations,
            HashAlgorithmName.SHA256,
            credential.Hash.Length);
        return CryptographicOperations.FixedTimeEquals(candidate, credential.Hash);
    }

    public static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new ArgumentException("Password must contain at least 12 characters.", nameof(password));
        if (password.Length > 1024)
            throw new ArgumentOutOfRangeException(nameof(password), "Password is too long.");
    }
}
