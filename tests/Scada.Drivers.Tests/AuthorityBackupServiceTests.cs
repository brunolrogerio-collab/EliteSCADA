using System.Text.Json;
using Scada.Api.Security;
using Scada.Engineering.Contracts;
using Scada.Engineering.ImportExport;
using Scada.Engineering.Persistence;
using Scada.Engineering.Security;
using Scada.Security.Authentication;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class AuthorityBackupServiceTests
{
    private const string BackupPassword = "Authority-backup-2026";
    private const string LoginPassword = "Local-login-2026";

    [Fact]
    public void ExportOpen_RoundTripsPortableIdentityStateWithoutPlaintextSecrets()
    {
        var service = new AuthorityBackupService();
        var createdAt = new DateTimeOffset(2026, 9, 6, 3, 0, 0, TimeSpan.Zero);
        var account = CreateAccount(
            Guid.Parse("91000000-0000-0000-0000-000000000001"),
            "admin",
            "Recovered Administrator",
            enabled: true,
            ["developer"],
            createdAt);

        var backup = service.Export([account], BackupPassword, createdAt);
        var opened = service.Open(backup, BackupPassword);

        Assert.DoesNotContain(LoginPassword, backup, StringComparison.Ordinal);
        Assert.DoesNotContain(BackupPassword, backup, StringComparison.Ordinal);
        Assert.Single(opened.Accounts);
        var restored = opened.Accounts.Single();
        Assert.Equal(account.Id, restored.Id);
        Assert.Equal(account.Username, restored.Username);
        Assert.Equal(account.NormalizedUsername, restored.NormalizedUsername);
        Assert.Equal(account.DisplayName, restored.DisplayName);
        Assert.Equal(account.IsEnabled, restored.IsEnabled);
        Assert.Equal(account.Roles, restored.Roles);
        Assert.Equal(account.CreatedAtUtc, restored.CreatedAtUtc);
        Assert.Equal(account.UpdatedAtUtc, restored.UpdatedAtUtc);
        Assert.Equal(account.Credential.Iterations, restored.Credential.Iterations);
        Assert.Equal(account.Credential.Salt, restored.Credential.Salt);
        Assert.Equal(account.Credential.Hash, restored.Credential.Hash);
        Assert.True(LocalPasswordHasher.Verify(LoginPassword, restored.Credential));

        Assert.Equal(1, opened.Preview.UserCount);
        Assert.Equal(1, opened.Preview.EnabledUserCount);
        Assert.Equal(1, opened.Preview.EnabledAdministratorCount);
        var safePreviewJson = JsonSerializer.Serialize(opened.Preview);
        Assert.DoesNotContain("hash", safePreviewJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("salt", safePreviewJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", safePreviewJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Open_WrongBackupPasswordFailsAuthentication()
    {
        var service = new AuthorityBackupService();
        var backup = service.Export([CreateAdministrator()], BackupPassword);

        var exception = Assert.Throws<InvalidDataException>(() =>
            service.Open(backup, "Definitely-wrong-password"));

        Assert.Contains("authentication failed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportOpenV2_RoundTripsEncryptedAuthorityPolicy()
    {
        var service = new AuthorityBackupService();
        var policy = new AuthorityBackupPolicyPayload(7, "[]", "[]");

        var backup = service.Export([CreateAdministrator()], policy, BackupPassword);
        var opened = service.Open(backup, BackupPassword);

        Assert.Equal(2, opened.Preview.FormatVersion);
        Assert.True(opened.Preview.PolicyIncluded);
        Assert.Equal("complete", opened.Preview.PolicyStatus);
        Assert.NotNull(opened.Policy);
        Assert.Equal(policy, opened.Policy);
        Assert.DoesNotContain(policy.RolesJson, backup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InMemoryPolicyStore_RejectsStaleVersionWithoutLastWriterWins()
    {
        var store = new InMemoryAuthorityPolicyStore();
        var first = await store.TryReplaceAsync(0, [], []);
        var stale = await store.TryReplaceAsync(0, [], []);

        Assert.True(first.Applied);
        Assert.False(stale.Applied);
        Assert.Equal("AUTHORITY_POLICY_CONCURRENCY_CONFLICT", stale.Error);
        Assert.Equal(1, stale.Snapshot.Version);
    }

    [Fact]
    public async Task BootstrapMigration_UsesConfiguredLegacyProjectExactlyOnce()
    {
        var now = new DateTimeOffset(2026, 9, 12, 13, 0, 0, TimeSpan.Zero);
        var sourceA = LegacySnapshot(
            "plant-a",
            new SecurityRoleEngineeringDto(
                Guid.Parse("92000000-0000-0000-0000-000000000001"),
                "legacy-a",
                "Legacy A"),
            now);
        var sourceBRoleId = Guid.Parse("92000000-0000-0000-0000-000000000002");
        var sourceB = LegacySnapshot(
            "plant-b",
            new SecurityRoleEngineeringDto(sourceBRoleId, "developer", "Legacy Developer"),
            now);
        var projects = new LegacyMigrationProjectStore([sourceA, sourceB]);
        var catalog = new LegacyMigrationProjectCatalog([sourceA, sourceB]);
        var identities = new InMemoryLocalIdentityStore();
        await identities.InitializeAsync();
        await identities.CreateAsync(CreateAdministrator());
        var authority = new InMemoryAuthorityPolicyStore();
        var bootstrap = new AuthorityPolicyBootstrapService(
            authority,
            identities,
            catalog,
            projects,
            new AuthorityPolicyBootstrapOptions("plant-b"));

        await bootstrap.EnsureInitializedAsync();
        var first = authority.Snapshot();
        await bootstrap.EnsureInitializedAsync();
        var second = authority.Snapshot();

        Assert.Equal(1, first.Version);
        Assert.Equal(first.Version, second.Version);
        Assert.Equal(sourceBRoleId, Assert.Single(second.Roles).Id);
        Assert.Equal(new[] { "plant-b" }, projects.LoadedProjectKeys);
    }

    [Fact]
    public async Task BootstrapMigration_AmbiguousLegacyProjectsRequirePreStartSelector()
    {
        var now = new DateTimeOffset(2026, 9, 12, 13, 0, 0, TimeSpan.Zero);
        var sourceA = LegacySnapshot(
            "plant-a",
            new SecurityRoleEngineeringDto(
                Guid.Parse("92000000-0000-0000-0000-000000000011"),
                "legacy-a",
                "Legacy A"),
            now);
        var sourceB = LegacySnapshot(
            "plant-b",
            new SecurityRoleEngineeringDto(
                Guid.Parse("92000000-0000-0000-0000-000000000012"),
                "legacy-b",
                "Legacy B"),
            now);
        var identities = new InMemoryLocalIdentityStore();
        await identities.InitializeAsync();
        await identities.CreateAsync(CreateAdministrator());
        var authority = new InMemoryAuthorityPolicyStore();
        var bootstrap = new AuthorityPolicyBootstrapService(
            authority,
            identities,
            new LegacyMigrationProjectCatalog([sourceA, sourceB]),
            new LegacyMigrationProjectStore([sourceA, sourceB]),
            new AuthorityPolicyBootstrapOptions(null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            bootstrap.EnsureInitializedAsync());

        Assert.Contains(AuthorityPolicyBootstrapOptions.ConfigurationPath, exception.Message, StringComparison.Ordinal);
        Assert.Empty(authority.Snapshot().Roles);
    }

    [Fact]
    public void AuthorityPolicyWire_UsesExplicitSchemaAndStringCapabilityIds()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new SecurityCapabilityJsonConverter());
        var document = new AuthorityPolicyDocument(
            AuthorityPolicyAdministrationApi.WireSchema,
            AuthorityPolicyAdministrationApi.WireSchemaVersion,
            3,
            [new SecurityRoleEngineeringDto(Guid.NewGuid(), "arbitrary", "Arbitrary", Grants: [new CapabilityGrantEngineeringDto(SecurityCapability.EngineeringView)])],
            Array.Empty<SecurityScopeEngineeringDto>());

        var json = JsonSerializer.Serialize(document, options);

        Assert.Contains("engineeringView", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"capability\":10", json, StringComparison.Ordinal);
        Assert.Contains("elitescada.authority-policy", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("10")]
    [InlineData("\"not-a-capability\"")]
    public void AuthorityPolicyWire_RejectsNumericAndUnknownCapabilities(string capability)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new SecurityCapabilityJsonConverter());
        var json = $$"""{"schema":"elitescada.authority-policy","schemaVersion":1,"expectedVersion":0,"roles":[{"id":"91000000-0000-0000-0000-000000000010","key":"arbitrary","name":"Arbitrary","grants":[{"capability":{{capability}}]} }],"scopes":[]}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AuthorityPolicyMutationRequest>(json, options));
    }

    [Fact]
    public void Open_TamperedCiphertextFailsAuthentication()
    {
        var service = new AuthorityBackupService();
        var backup = service.Export([CreateAdministrator()], BackupPassword);
        var envelope = JsonSerializer.Deserialize<AuthorityBackupEnvelope>(
            backup,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var bytes = Convert.FromBase64String(envelope.CiphertextBase64);
        bytes[^1] ^= 0x01;
        var tampered = JsonSerializer.Serialize(
            envelope with { CiphertextBase64 = Convert.ToBase64String(bytes) },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var exception = Assert.Throws<InvalidDataException>(() =>
            service.Open(tampered, BackupPassword));

        Assert.Contains("authentication failed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Open_UnsupportedOrHostileKdfParametersFailBeforeDerivation()
    {
        var service = new AuthorityBackupService();
        var backup = service.Export([CreateAdministrator()], BackupPassword);
        var envelope = JsonSerializer.Deserialize<AuthorityBackupEnvelope>(
            backup,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var hostile = JsonSerializer.Serialize(
            envelope with
            {
                Kdf = envelope.Kdf with { Iterations = int.MaxValue }
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var exception = Assert.Throws<InvalidDataException>(() =>
            service.Open(hostile, BackupPassword));

        Assert.Contains("KDF parameters", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("{\"format\":\"elitescada.authority-backup\",\"formatVersion\":1,\"createdAtUtc\":\"2026-09-06T03:00:00Z\",\"kdf\":null,\"encryption\":null,\"ciphertextBase64\":\"AA==\"}")]
    [InlineData("{not-json")]
    public void Open_MalformedEnvelopeFailsAsInvalidData(string malformed)
    {
        var service = new AuthorityBackupService();
        Assert.Throws<InvalidDataException>(() => service.Open(malformed, BackupPassword));
    }

    [Fact]
    public void ValidateAndCopyAccounts_RejectsMissingEnabledAdministrator()
    {
        var account = CreateAccount(
            Guid.NewGuid(),
            "operator",
            "Operator",
            enabled: true,
            ["operator"],
            DateTimeOffset.UtcNow);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthorityBackupService.ValidateAndCopyAccounts([account]));

        Assert.Contains("developer", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndCopyAccounts_RejectsDuplicateNormalizedUsernames()
    {
        var now = DateTimeOffset.UtcNow;
        var first = CreateAccount(Guid.NewGuid(), "Admin", "Admin 1", true, ["developer"], now);
        var second = CreateAccount(Guid.NewGuid(), "admin", "Admin 2", true, ["developer"], now);

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthorityBackupService.ValidateAndCopyAccounts([first, second]));

        Assert.Contains("duplicate local usernames", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndCopyAccounts_RejectsMissingCredentialWithoutNullReferenceLeak()
    {
        var invalid = CreateAdministrator() with { Credential = null! };

        var exception = Assert.Throws<InvalidDataException>(() =>
            AuthorityBackupService.ValidateAndCopyAccounts([invalid]));

        Assert.Contains("credential", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static LocalUserAccount CreateAdministrator() => CreateAccount(
        Guid.Parse("91000000-0000-0000-0000-000000000002"),
        "administrator",
        "Administrator",
        enabled: true,
        ["developer"],
        new DateTimeOffset(2026, 9, 6, 3, 0, 0, TimeSpan.Zero));

    private static LocalUserAccount CreateAccount(
        Guid id,
        string username,
        string displayName,
        bool enabled,
        IReadOnlyCollection<string> roles,
        DateTimeOffset timestamp) =>
        new(
            id,
            username,
            LocalIdentityNormalization.NormalizeUsername(username),
            displayName,
            enabled,
            roles,
            LocalPasswordHasher.Hash(LoginPassword),
            timestamp,
            timestamp);

    private static EngineeringProjectSnapshot LegacySnapshot(
        string projectKey,
        SecurityRoleEngineeringDto role,
        DateTimeOffset savedAtUtc)
    {
        var package = new EngineeringPackage(
            EngineeringExchangeService.CurrentSchema,
            18,
            savedAtUtc,
            Array.Empty<TagEngineeringDto>(),
            Array.Empty<AlarmEngineeringDto>(),
            SecurityRoles: [role],
            SecurityScopes: Array.Empty<SecurityScopeEngineeringDto>());
        return new EngineeringProjectSnapshot(
            1,
            projectKey,
            projectKey,
            EngineeringExchangeService.CurrentSchema,
            18,
            savedAtUtc,
            JsonSerializer.Serialize(package));
    }

    private sealed class LegacyMigrationProjectCatalog(
        IReadOnlyCollection<EngineeringProjectSnapshot> snapshots) : IEngineeringProjectCatalog
    {
        public Task<IReadOnlyCollection<EngineeringProjectCatalogEntry>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectCatalogEntry>>(
                snapshots.Select(snapshot => new EngineeringProjectCatalogEntry(
                    snapshot.ProjectKey,
                    snapshot.ProjectName,
                    snapshot.Revision,
                    snapshot.SavedAtUtc)).ToArray());
    }

    private sealed class LegacyMigrationProjectStore(
        IEnumerable<EngineeringProjectSnapshot> snapshots) : IEngineeringProjectStore
    {
        private readonly Dictionary<string, EngineeringProjectSnapshot> _snapshots = snapshots
            .ToDictionary(snapshot => snapshot.ProjectKey, StringComparer.OrdinalIgnoreCase);

        public List<string> LoadedProjectKeys { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<EngineeringProjectSnapshot> SaveAsync(
            string projectKey,
            string projectName,
            string engineeringSchema,
            int engineeringSchemaVersion,
            string engineeringJson,
            string? savedBy = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException<EngineeringProjectSnapshot>(new NotSupportedException());

        public Task<EngineeringProjectSnapshot?> LoadLatestAsync(
            string projectKey,
            CancellationToken cancellationToken = default)
        {
            LoadedProjectKeys.Add(projectKey);
            return Task.FromResult(_snapshots.GetValueOrDefault(projectKey));
        }

        public Task<EngineeringProjectSnapshot?> LoadRevisionAsync(
            string projectKey,
            long revision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectSnapshot?>(null);

        public Task<IReadOnlyCollection<EngineeringProjectSnapshot>> ListRevisionsAsync(
            string projectKey,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EngineeringProjectSnapshot>>(Array.Empty<EngineeringProjectSnapshot>());

        public Task<EngineeringProjectPublication?> GetPublicationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectPublication?>(null);

        public Task<EngineeringProjectPublication?> PublishRevisionAsync(
            string projectKey,
            long revision,
            string? publishedBy = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectPublication?>(null);

        public Task<EngineeringProjectActivation?> GetActivationAsync(
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectActivation?>(null);

        public Task<EngineeringProjectActivation?> RecordActivationAsync(
            string projectKey,
            long revision,
            string? activatedBy = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EngineeringProjectActivation?>(null);
    }
}
