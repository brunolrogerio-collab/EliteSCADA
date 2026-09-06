using Scada.Security.Authentication;

namespace Scada.Drivers.Tests;

public sealed class AuthorityBackupServiceTests_RestoreSecurity
{
    private const string LoginPassword = "Local-login-2026";

    [Fact]
    public void RefreshSecurityVersions_AdvancesBackupVersionAndPreservesCredential()
    {
        var timestamp = new DateTimeOffset(2026, 9, 6, 3, 0, 0, TimeSpan.Zero);
        var restored = CreateAccount(timestamp);
        var originalSalt = restored.Credential.Salt.ToArray();
        var originalHash = restored.Credential.Hash.ToArray();

        var refreshed = Assert.Single(AuthorityRestoreSecurity.RefreshSecurityVersions(
            [restored],
            Array.Empty<LocalUserAccount>(),
            timestamp.AddMinutes(1)));

        Assert.True(refreshed.UpdatedAtUtc > restored.UpdatedAtUtc);
        Assert.Equal(restored.CreatedAtUtc, refreshed.CreatedAtUtc);
        Assert.Equal(restored.Credential.Iterations, refreshed.Credential.Iterations);
        Assert.Equal(originalSalt, refreshed.Credential.Salt);
        Assert.Equal(originalHash, refreshed.Credential.Hash);
        Assert.True(LocalPasswordHasher.Verify(LoginPassword, refreshed.Credential));

        refreshed.Credential.Salt[0] ^= 0x7f;
        Assert.Equal(originalSalt, restored.Credential.Salt);
    }

    [Fact]
    public void RefreshSecurityVersions_AdvancesBeyondCurrentlyInstalledVersion()
    {
        var backupTimestamp = new DateTimeOffset(2026, 9, 6, 3, 0, 0, TimeSpan.Zero);
        var currentTimestamp = backupTimestamp.AddHours(2);
        var restored = CreateAccount(backupTimestamp);
        var current = restored with { UpdatedAtUtc = currentTimestamp };

        var refreshed = Assert.Single(AuthorityRestoreSecurity.RefreshSecurityVersions(
            [restored],
            [current],
            backupTimestamp.AddMinutes(5)));

        Assert.True(refreshed.UpdatedAtUtc > currentTimestamp);
        Assert.True(refreshed.UpdatedAtUtc > restored.UpdatedAtUtc);
    }

    [Fact]
    public void RefreshSecurityVersions_MaximumVersionFailsClosed()
    {
        var restored = CreateAccount(DateTimeOffset.MaxValue);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthorityRestoreSecurity.RefreshSecurityVersions(
                [restored],
                Array.Empty<LocalUserAccount>(),
                new DateTimeOffset(2026, 9, 6, 3, 0, 0, TimeSpan.Zero)));

        Assert.Contains("cannot be advanced safely", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static LocalUserAccount CreateAccount(DateTimeOffset timestamp) => new(
        Guid.Parse("92000000-0000-0000-0000-000000000001"),
        "administrator",
        LocalIdentityNormalization.NormalizeUsername("administrator"),
        "Administrator",
        true,
        ["developer"],
        LocalPasswordHasher.Hash(LoginPassword),
        timestamp,
        timestamp);
}
