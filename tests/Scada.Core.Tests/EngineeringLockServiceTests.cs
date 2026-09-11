using System.Text.Json;
using Scada.Engineering.Contracts;
using Scada.Engineering.Security;

namespace Scada.Core.Tests;

public sealed class EngineeringLockServiceTests
{
    private readonly EngineeringLockSecretService _service = new();

    [Fact]
    public void Configure_PersistsOnlyOneWayVerifier_AndKeepsConfiguredStateIndependentFromLockedState()
    {
        const string secret = "C25-lock-secret-very-specific";

        var state = _service.Configure(secret, locked: false);

        Assert.False(state.Locked);
        Assert.NotNull(state.Verifier);
        Assert.Equal(EngineeringLockContract.Algorithm, state.Verifier!.Algorithm);
        Assert.Equal(EngineeringLockContract.VerifierVersion, state.Verifier.Version);
        Assert.Equal(EngineeringLockContract.CurrentIterations, state.Verifier.Iterations);
        Assert.Equal(EngineeringLockContract.SaltByteLength, Convert.FromBase64String(state.Verifier.Salt).Length);
        Assert.Equal(EngineeringLockContract.HashByteLength, Convert.FromBase64String(state.Verifier.Hash).Length);
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(state), StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_AcceptsCorrectSecret_AndRejectsWrongSecret()
    {
        var state = _service.Configure("correct-secret", locked: true);

        Assert.True(_service.Verify(state, "correct-secret"));
        Assert.False(_service.Verify(state, "wrong-secret"));
    }

    [Fact]
    public void Lock_RequiresConfiguredVerifier_ButConfiguredApplicationMayRemainUnlocked()
    {
        var configured = _service.Configure("configured-secret", locked: false);

        Assert.NotNull(configured.Verifier);
        Assert.False(configured.Locked);
        Assert.True(_service.Lock(configured).Locked);
        Assert.Throws<InvalidOperationException>(() => _service.Lock(EngineeringLockContract.UnconfiguredUnlocked));
    }

    [Fact]
    public void Unlock_RequiresCorrectSecret_AndPreservesVerifier()
    {
        var locked = _service.Configure("correct-secret", locked: true);

        Assert.Throws<UnauthorizedAccessException>(() => _service.Unlock(locked, "wrong-secret"));

        var unlocked = _service.Unlock(locked, "correct-secret");
        Assert.False(unlocked.Locked);
        Assert.Equal(locked.Verifier, unlocked.Verifier);
    }

    [Fact]
    public void Normalize_TreatsMissingMetadataAsUnconfiguredUnlocked_ForLegacyPackages()
    {
        var normalized = EngineeringLockContract.Normalize(null);

        Assert.False(normalized.Locked);
        Assert.Null(normalized.Verifier);
    }

    [Fact]
    public void Normalize_RejectsLockedStateWithoutVerifier()
    {
        Assert.Throws<InvalidDataException>(() =>
            EngineeringLockContract.Normalize(new EngineeringLockEngineeringDto(Locked: true)));
    }

    [Theory]
    [InlineData("PBKDF2-SHA1", 1, 210000)]
    [InlineData("PBKDF2-SHA256", 2, 210000)]
    [InlineData("PBKDF2-SHA256", 1, 1000)]
    public void Normalize_RejectsUnsupportedOrWeakenedVerifier(
        string algorithm,
        int version,
        int iterations)
    {
        var state = new EngineeringLockEngineeringDto(
            Locked: true,
            new EngineeringLockVerifierDto(
                algorithm,
                version,
                iterations,
                Convert.ToBase64String(new byte[EngineeringLockContract.SaltByteLength]),
                Convert.ToBase64String(new byte[EngineeringLockContract.HashByteLength])));

        Assert.Throws<InvalidDataException>(() => EngineeringLockContract.Normalize(state));
        Assert.False(_service.Verify(state, "anything"));
    }

    [Fact]
    public void Registry_RejectsMalformedState_AndDoesNotReplaceLastValidState()
    {
        var registry = new InMemoryEngineeringLockRegistry();
        var configured = _service.Configure("stable-secret", locked: false);
        registry.Replace(configured);

        Assert.Throws<InvalidDataException>(() =>
            registry.Replace(new EngineeringLockEngineeringDto(Locked: true)));

        Assert.Equal(configured, registry.Snapshot());
    }
}