using System.Security.Cryptography;
using System.Text;
using Scada.Engineering.Contracts;

namespace Scada.Engineering.Security;

public interface IEngineeringLockRegistry
{
    EngineeringLockEngineeringDto Snapshot();

    void Replace(EngineeringLockEngineeringDto? state);
}

public sealed class InMemoryEngineeringLockRegistry : IEngineeringLockRegistry
{
    private readonly object _sync = new();
    private EngineeringLockEngineeringDto _state = EngineeringLockContract.UnconfiguredUnlocked;

    public EngineeringLockEngineeringDto Snapshot()
    {
        lock (_sync)
            return _state;
    }

    public void Replace(EngineeringLockEngineeringDto? state)
    {
        var normalized = EngineeringLockContract.Normalize(state);
        lock (_sync)
            _state = normalized;
    }
}

public sealed class EngineeringLockSecretService
{
    public EngineeringLockEngineeringDto Configure(string secret, bool locked = false)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Engineering Lock secret must not be empty.", nameof(secret));

        var salt = RandomNumberGenerator.GetBytes(EngineeringLockContract.SaltByteLength);
        var hash = Derive(secret, salt, EngineeringLockContract.CurrentIterations);
        return new EngineeringLockEngineeringDto(
            locked,
            new EngineeringLockVerifierDto(
                EngineeringLockContract.Algorithm,
                EngineeringLockContract.VerifierVersion,
                EngineeringLockContract.CurrentIterations,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash)));
    }

    public bool Verify(EngineeringLockEngineeringDto? state, string secret)
    {
        if (string.IsNullOrEmpty(secret)) return false;

        EngineeringLockEngineeringDto normalized;
        try
        {
            normalized = EngineeringLockContract.Normalize(state);
        }
        catch (InvalidDataException)
        {
            return false;
        }

        var verifier = normalized.Verifier;
        if (verifier is null) return false;

        try
        {
            var salt = Convert.FromBase64String(verifier.Salt);
            var expected = Convert.FromBase64String(verifier.Hash);
            var actual = Derive(secret, salt, verifier.Iterations);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public EngineeringLockEngineeringDto Lock(EngineeringLockEngineeringDto? state)
    {
        var normalized = EngineeringLockContract.Normalize(state);
        if (normalized.Verifier is null)
            throw new InvalidOperationException("Engineering Lock cannot be enabled before a secret is configured.");

        return normalized with { Locked = true };
    }

    public EngineeringLockEngineeringDto Unlock(
        EngineeringLockEngineeringDto? state,
        string secret)
    {
        var normalized = EngineeringLockContract.Normalize(state);
        if (normalized.Verifier is null)
            return EngineeringLockContract.UnconfiguredUnlocked;
        if (!Verify(normalized, secret))
            throw new UnauthorizedAccessException("Engineering Lock secret is invalid.");

        return normalized with { Locked = false };
    }

    public EngineeringLockEngineeringDto Clear() => EngineeringLockContract.UnconfiguredUnlocked;

    private static byte[] Derive(string secret, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(secret),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            EngineeringLockContract.HashByteLength);
}

public static class EngineeringLockContract
{
    public const string Algorithm = "PBKDF2-SHA256";
    public const int VerifierVersion = 1;
    public const int CurrentIterations = 210_000;
    public const int SaltByteLength = 16;
    public const int HashByteLength = 32;
    private const int MaximumAcceptedIterations = 1_000_000;

    public static EngineeringLockEngineeringDto UnconfiguredUnlocked { get; } = new(false, null);

    public static EngineeringLockEngineeringDto Normalize(EngineeringLockEngineeringDto? state)
    {
        if (state is null) return UnconfiguredUnlocked;
        if (state.Verifier is null)
        {
            if (state.Locked)
                throw new InvalidDataException("Engineering Lock cannot be locked without a configured verifier.");
            return UnconfiguredUnlocked;
        }

        ValidateVerifier(state.Verifier);
        return state;
    }

    public static void ValidateVerifier(EngineeringLockVerifierDto verifier)
    {
        if (!string.Equals(verifier.Algorithm, Algorithm, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported Engineering Lock verifier algorithm '{verifier.Algorithm}'.");
        if (verifier.Version != VerifierVersion)
            throw new InvalidDataException($"Unsupported Engineering Lock verifier version {verifier.Version}.");
        if (verifier.Iterations < CurrentIterations || verifier.Iterations > MaximumAcceptedIterations)
            throw new InvalidDataException("Engineering Lock verifier iteration count is outside the accepted security range.");

        byte[] salt;
        byte[] hash;
        try
        {
            salt = Convert.FromBase64String(verifier.Salt);
            hash = Convert.FromBase64String(verifier.Hash);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Engineering Lock verifier contains invalid Base64 data.", exception);
        }

        if (salt.Length != SaltByteLength)
            throw new InvalidDataException($"Engineering Lock verifier salt must be {SaltByteLength} bytes.");
        if (hash.Length != HashByteLength)
            throw new InvalidDataException($"Engineering Lock verifier hash must be {HashByteLength} bytes.");
    }
}