using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Scada.Api.Security;
using Scada.Security.Audit;
using Scada.Security.Authorization;

namespace Scada.Drivers.Tests;

public sealed class ProtectedMaterialAuthorityTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("user", null, "token")]
    [InlineData(null, "password", null)]
    [InlineData("user", "password", "token")]
    [InlineData("user\n", "password", null)]
    public void MediaSourceCredentialRequest_RejectsAmbiguousIncompleteOrUnsafeInput(
        string? username,
        string? password,
        string? bearerToken)
    {
        var result = new MediaSourceCredentialRequest(username, password, bearerToken).Validate();

        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public async Task MediaSourceCredentialLifecycle_PersistsOnlyOpaqueHostReferenceAndSupportsReplaceResolveDelete()
    {
        var root = TemporaryDirectory();
        try
        {
            var audit = new InMemoryAuditSink();
            using var authority = Authority(root, KeyA, audit);
            var references = new FileMediaSourceCredentialReferenceStore(root);
            var service = new MediaSourceProtectedCredentialService(authority, references);
            var scope = new ProtectedMaterialScope(
                "project:project-a",
                ProtectedMaterialResourceKinds.MediaSource,
                Guid.NewGuid().ToString("D"),
                ProtectedMaterialPurposes.ConnectionCredential);

            Assert.False((await service.GetPublicStateAsync(scope)).Configured);
            await service.ConfigureAsync(
                scope,
                new MediaSourceCredentialRequest("camera-user", SecretA, null),
                Admin());

            Assert.True((await service.GetPublicStateAsync(scope)).Configured);
            var mappingPath = Path.Combine(root, "media-source-references", "credentials.json");
            var mapping = await File.ReadAllTextAsync(mappingPath);
            Assert.Contains(FileHostProtectedMaterialAuthority.ReferencePrefix, mapping, StringComparison.Ordinal);
            Assert.DoesNotContain(SecretA, mapping, StringComparison.Ordinal);
            Assert.DoesNotContain("camera-user", mapping, StringComparison.Ordinal);
            var persistedFiles = string.Join("\n", Directory.GetFiles(root, "*.json").Select(File.ReadAllText));
            Assert.DoesNotContain(SecretA, persistedFiles, StringComparison.Ordinal);
            Assert.DoesNotContain("camera-user", persistedFiles, StringComparison.Ordinal);
            Assert.Equal(ProtectedMaterialAuthorityHealthStatus.Ready, (await authority.GetHealthAsync()).Status);
            Assert.False((await service.GetPublicStateAsync(scope with { ScopeOwnerKey = "project:project-b" })).Configured);
            var otherOwner = await Assert.ThrowsAsync<ProtectedMaterialException>(async () =>
                await service.ResolveAsync(scope with { ScopeOwnerKey = "project:project-b" }));
            Assert.Equal(ProtectedMaterialErrorCodes.CredentialRequired, otherOwner.Code);

            await using (var lease = await service.ResolveAsync(scope))
            {
                var payload = System.Text.Encoding.UTF8.GetString(lease.Material.Span);
                Assert.Contains(SecretA, payload, StringComparison.Ordinal);
                Assert.Contains("camera-user", payload, StringComparison.Ordinal);
            }

            await service.ConfigureAsync(
                scope,
                new MediaSourceCredentialRequest(null, null, "replacement-bearer-token"),
                Admin());
            await using (var lease = await service.ResolveAsync(scope))
            {
                var payload = System.Text.Encoding.UTF8.GetString(lease.Material.Span);
                Assert.Contains("replacement-bearer-token", payload, StringComparison.Ordinal);
                Assert.DoesNotContain(SecretA, payload, StringComparison.Ordinal);
            }

            await service.DeleteAsync(scope, Admin());
            Assert.False((await service.GetPublicStateAsync(scope)).Configured);
            var unavailable = await Assert.ThrowsAsync<ProtectedMaterialException>(async () =>
                await service.ResolveAsync(scope));
            Assert.Equal(ProtectedMaterialErrorCodes.CredentialRequired, unavailable.Code);
            Assert.DoesNotContain(SecretA, JsonSerializer.Serialize(audit.Snapshot()), StringComparison.Ordinal);
            Assert.DoesNotContain("replacement-bearer-token", JsonSerializer.Serialize(audit.Snapshot()), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StoreResolveScopeAndPersistence_AreOpaqueAndFailClosed()
    {
        var root = TemporaryDirectory();
        try
        {
            var audit = new InMemoryAuditSink();
            using var authority = Authority(root, KeyA, audit);
            var scope = MediaPasswordScope();
            var secret = Encoding.UTF8.GetBytes(SecretA);

            var stored = await authority.StoreAsync(scope, secret, Admin());
            Assert.StartsWith(
                FileHostProtectedMaterialAuthority.ReferencePrefix,
                stored.Reference,
                StringComparison.Ordinal);
            Assert.DoesNotContain("CAMERA-01", stored.Reference, StringComparison.Ordinal);
            Assert.DoesNotContain("Password", stored.Reference, StringComparison.Ordinal);

            var persisted = File.ReadAllText(Assert.Single(Directory.GetFiles(root, "*.json")));
            Assert.DoesNotContain(SecretA, persisted, StringComparison.Ordinal);
            Assert.Contains(FileHostProtectedMaterialAuthority.Algorithm, persisted, StringComparison.Ordinal);

            var lease = await authority.ResolveAsync(scope, stored.Reference);
            var leasedMemory = lease.Material;
            Assert.Equal(SecretA, Encoding.UTF8.GetString(leasedMemory.Span));
            await lease.DisposeAsync();
            Assert.True(leasedMemory.Span.ToArray().All(value => value == 0));
            Assert.True(lease.Material.IsEmpty);

            var wrongResource = await Assert.ThrowsAsync<ProtectedMaterialException>(async () =>
                await authority.ResolveAsync(scope with { ResourceId = "CAMERA-02" }, stored.Reference));
            Assert.Equal(ProtectedMaterialErrorCodes.ScopeMismatch, wrongResource.Code);
            Assert.DoesNotContain(SecretA, wrongResource.ToString(), StringComparison.Ordinal);

            var wrongPurpose = await Assert.ThrowsAsync<ProtectedMaterialException>(async () =>
                await authority.ResolveAsync(scope with { Purpose = ProtectedMaterialPurposes.BearerToken }, stored.Reference));
            Assert.Equal(ProtectedMaterialErrorCodes.ScopeMismatch, wrongPurpose.Code);

            var missing = await Assert.ThrowsAsync<ProtectedMaterialException>(async () =>
                await authority.ResolveAsync(
                    scope,
                    FileHostProtectedMaterialAuthority.ReferencePrefix + new string('a', 32)));
            Assert.Equal(ProtectedMaterialErrorCodes.ReferenceNotFound, missing.Code);

            var events = audit.Snapshot();
            var storedAudit = Assert.Single(events);
            Assert.Equal(ProtectedMaterialAuditActions.Store, storedAudit.Action);
            Assert.DoesNotContain(SecretA, JsonSerializer.Serialize(events), StringComparison.Ordinal);
            Assert.DoesNotContain(stored.Reference, JsonSerializer.Serialize(events), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task MissingWrongAndCorruptProtectionState_FailsClosed()
    {
        var root = TemporaryDirectory();
        try
        {
            var audit = new InMemoryAuditSink();
            var scope = MediaPasswordScope();

            using (var missingKey = Authority(root, null, audit))
            {
                var health = await missingKey.GetHealthAsync();
                Assert.Equal(ProtectedMaterialAuthorityHealthStatus.KeyUnavailable, health.Status);
                Assert.Equal(ProtectedMaterialErrorCodes.KeyUnavailable, health.Code);

                var failure = await Assert.ThrowsAsync<ProtectedMaterialException>(async () =>
                    await missingKey.StoreAsync(scope, Encoding.UTF8.GetBytes(SecretA), Admin()));
                Assert.Equal(ProtectedMaterialErrorCodes.KeyUnavailable, failure.Code);
            }

            string reference;
            using (var writer = Authority(root, KeyA, audit))
            {
                reference = (await writer.StoreAsync(
                    scope,
                    Encoding.UTF8.GetBytes(SecretA),
                    Admin())).Reference;
            }

            using (var wrongKey = Authority(root, KeyB, audit))
            {
                var failure = await Assert.ThrowsAsync<ProtectedMaterialException>(async () =>
                    await wrongKey.ResolveAsync(scope, reference));
                Assert.Equal(ProtectedMaterialErrorCodes.ProtectedMaterialUnreadable, failure.Code);
                Assert.DoesNotContain(SecretA, failure.ToString(), StringComparison.Ordinal);
            }

            var file = Assert.Single(Directory.GetFiles(root, "*.json"));
            await File.WriteAllTextAsync(file, "{ corrupt");
            using var corrupt = Authority(root, KeyA, audit);
            var corruptFailure = await Assert.ThrowsAsync<ProtectedMaterialException>(async () =>
                await corrupt.ResolveAsync(scope, reference));
            Assert.Equal(ProtectedMaterialErrorCodes.ProtectedMaterialUnreadable, corruptFailure.Code);

            var corruptHealth = await corrupt.GetHealthAsync();
            Assert.Equal(ProtectedMaterialAuthorityHealthStatus.CorruptConfiguration, corruptHealth.Status);
            Assert.Equal(ProtectedMaterialErrorCodes.ProtectedMaterialUnreadable, corruptHealth.Code);
            Assert.DoesNotContain(SecretA, JsonSerializer.Serialize(corruptHealth), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReplaceRestartDeleteAndConcurrentResolve_AreDeterministic()
    {
        var root = TemporaryDirectory();
        try
        {
            var audit = new InMemoryAuditSink();
            var scope = MediaPasswordScope();
            string firstReference;
            string replacementReference;

            using (var firstHost = Authority(root, KeyA, audit))
            {
                firstReference = (await firstHost.StoreAsync(
                    scope,
                    Encoding.UTF8.GetBytes(SecretA),
                    Admin())).Reference;

                var replacementTask = firstHost.ReplaceAsync(
                    scope,
                    firstReference,
                    Encoding.UTF8.GetBytes(SecretB),
                    Admin()).AsTask();
                var concurrentOldResolves = Enumerable.Range(0, 8)
                    .Select(async _ =>
                    {
                        await using var lease = await firstHost.ResolveAsync(scope, firstReference);
                        return Encoding.UTF8.GetString(lease.Material.Span);
                    })
                    .ToArray();

                var replacement = await replacementTask;
                replacementReference = replacement.Reference;
                Assert.Equal(firstReference, replacement.SupersededReference);
                Assert.NotEqual(firstReference, replacementReference);
                var oldValues = await Task.WhenAll(concurrentOldResolves);
                Assert.All(oldValues, value => Assert.Equal(SecretA, value));

                await using var oldLease = await firstHost.ResolveAsync(scope, firstReference);
                Assert.Equal(SecretA, Encoding.UTF8.GetString(oldLease.Material.Span));
            }

            using (var restartedHost = Authority(root, KeyA, audit))
            {
                var resolves = Enumerable.Range(0, 24)
                    .Select(async _ =>
                    {
                        await using var lease = await restartedHost.ResolveAsync(
                            scope,
                            replacementReference);
                        return Encoding.UTF8.GetString(lease.Material.Span);
                    })
                    .ToArray();

                var values = await Task.WhenAll(resolves);
                Assert.All(values, value => Assert.Equal(SecretB, value));

                // Replacement never mutates/deletes the old reference. This makes replace/resolve
                // deterministic until the feature commits its new reference and owns cleanup.
                var race = await Task.WhenAll(
                    Enumerable.Range(0, 8).Select(async _ =>
                    {
                        await using var lease = await restartedHost.ResolveAsync(scope, firstReference);
                        return Encoding.UTF8.GetString(lease.Material.Span);
                    }));
                Assert.All(race, value => Assert.Equal(SecretA, value));

                await restartedHost.DeleteAsync(scope, firstReference, Admin());
                var deleted = await Assert.ThrowsAsync<ProtectedMaterialException>(async () =>
                    await restartedHost.ResolveAsync(scope, firstReference));
                Assert.Equal(ProtectedMaterialErrorCodes.ReferenceNotFound, deleted.Code);

                await using var active = await restartedHost.ResolveAsync(scope, replacementReference);
                Assert.Equal(SecretB, Encoding.UTF8.GetString(active.Material.Span));
            }

            var serializedAudit = JsonSerializer.Serialize(audit.Snapshot());
            Assert.DoesNotContain(SecretA, serializedAudit, StringComparison.Ordinal);
            Assert.DoesNotContain(SecretB, serializedAudit, StringComparison.Ordinal);
            Assert.DoesNotContain(firstReference, serializedAudit, StringComparison.Ordinal);
            Assert.DoesNotContain(replacementReference, serializedAudit, StringComparison.Ordinal);
            Assert.Contains(ProtectedMaterialAuditActions.Replace, serializedAudit, StringComparison.Ordinal);
            Assert.Contains(ProtectedMaterialAuditActions.Delete, serializedAudit, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UnauthorizedMutationAndPublicContracts_NeverReadBackPlaintext()
    {
        var root = TemporaryDirectory();
        try
        {
            using var authority = Authority(root, KeyA, new InMemoryAuditSink());
            var denied = new ProtectedMaterialMutationContext(
                new SecurityPrincipal("viewer", "Viewer", ["viewer"]),
                AuthorizationDecision.Denied(
                    SecurityCapability.SystemAdmin,
                    "System administration is required."));

            var failure = await Assert.ThrowsAsync<ProtectedMaterialException>(async () =>
                await authority.StoreAsync(
                    MediaPasswordScope(),
                    Encoding.UTF8.GetBytes(SecretA),
                    denied));
            Assert.Equal(ProtectedMaterialErrorCodes.Unauthorized, failure.Code);
            Assert.Empty(Directory.GetFiles(root, "*.json"));

            var methods = typeof(IProtectedMaterialAuthority).GetMethods()
                .Select(method => method.Name)
                .ToArray();
            Assert.DoesNotContain(methods, name =>
                name.Contains("List", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("ReadBack", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("GetSecret", StringComparison.OrdinalIgnoreCase));

            var publicJson = JsonSerializer.Serialize(new ProtectedMaterialPublicState(true));
            Assert.DoesNotContain("material", publicJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", publicJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", publicJson, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task MediaSourceConsumerFixture_PersistsReferenceOnly_AndExportsDependencyOnly()
    {
        var root = TemporaryDirectory();
        try
        {
            using var authority = Authority(root, KeyA, new InMemoryAuditSink());
            var scope = MediaPasswordScope();
            var stored = await authority.StoreAsync(
                scope,
                Encoding.UTF8.GetBytes(SecretA),
                Admin());

            var persistedMediaSource = new MediaSourceFixture(
                "CAMERA-01",
                "rtsp://camera.internal/stream",
                stored.Reference);
            var publicState = new MediaSourcePublicFixture(
                persistedMediaSource.Id,
                CredentialsConfigured: true);

            var persistedJson = JsonSerializer.Serialize(persistedMediaSource);
            var publicJson = JsonSerializer.Serialize(publicState);
            Assert.Contains(stored.Reference, persistedJson, StringComparison.Ordinal);
            Assert.DoesNotContain(SecretA, persistedJson, StringComparison.Ordinal);
            Assert.DoesNotContain(stored.Reference, publicJson, StringComparison.Ordinal);
            Assert.DoesNotContain(SecretA, publicJson, StringComparison.Ordinal);

            await using var resolved = await authority.ResolveAsync(scope, stored.Reference);
            Assert.Equal(SecretA, Encoding.UTF8.GetString(resolved.Material.Span));

            var export = ProtectedMaterialPortability.ForProjectExport(scope, configured: true);
            var exportJson = JsonSerializer.Serialize(export);
            Assert.True(export.CredentialRequired);
            Assert.DoesNotContain(stored.Reference, exportJson, StringComparison.Ordinal);
            Assert.DoesNotContain(SecretA, exportJson, StringComparison.Ordinal);

            var imported = ProtectedMaterialPortability.ForImportedHost(export);
            Assert.False(imported.Configured);
            Assert.Equal(ProtectedMaterialErrorCodes.CredentialRequired, imported.StatusCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DeploymentKeyProvisioning_SupportsContainerStyleEnvironmentAndMountedFile()
    {
        var root = TemporaryDirectory();
        var keyFileRoot = TemporaryDirectory();
        var variable = "ELITESCADA_PROTECTED_MATERIAL_TEST_" + Guid.NewGuid().ToString("N");
        var previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, Convert.ToBase64String(KeyA));
            var environmentConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ProtectedMaterial:Store:Path"] = root,
                    ["ProtectedMaterial:Store:ProtectionKeyEnvironmentVariable"] = variable
                })
                .Build();
            var fromEnvironment = ProtectedMaterialAuthorityOptions.FromConfiguration(
                environmentConfiguration);
            var environmentKey = fromEnvironment.LoadProtectionKey();
            Assert.Equal(KeyA, environmentKey);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(environmentKey!);

            Environment.SetEnvironmentVariable(variable, null);
            var keyFile = Path.Combine(keyFileRoot, "protected-material-key");
            File.WriteAllText(keyFile, Convert.ToBase64String(KeyA));
            var fileConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ProtectedMaterial:Store:Path"] = root,
                    ["ProtectedMaterial:Store:ProtectionKeyEnvironmentVariable"] = variable,
                    ["ProtectedMaterial:Store:ProtectionKeyFile"] = keyFile
                })
                .Build();
            var fromFile = ProtectedMaterialAuthorityOptions.FromConfiguration(fileConfiguration);
            var mountedKey = fromFile.LoadProtectionKey();
            Assert.Equal(KeyA, mountedKey);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(mountedKey!);

            var unsafeConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ProtectedMaterial:Store:Path"] = root,
                    ["ProtectedMaterial:Store:ProtectionKeyFile"] = Path.Combine(root, "key")
                })
                .Build();
            var unsafeFailure = Assert.Throws<ProtectedMaterialException>(() =>
                ProtectedMaterialAuthorityOptions.FromConfiguration(unsafeConfiguration));
            Assert.Equal(ProtectedMaterialErrorCodes.KeyUnavailable, unsafeFailure.Code);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
            Directory.Delete(root, recursive: true);
            Directory.Delete(keyFileRoot, recursive: true);
        }
    }

    private static FileHostProtectedMaterialAuthority Authority(
        string root,
        byte[]? key,
        IAuditSink audit) =>
        new(
            new ProtectedMaterialAuthorityOptions(
                root,
                ProtectedMaterialAuthorityOptions.DefaultProtectionKeyEnvironmentVariable,
                null),
            key,
            audit);

    private static ProtectedMaterialScope MediaPasswordScope() =>
        new(
            "project:project-a",
            ProtectedMaterialResourceKinds.MediaSource,
            "CAMERA-01",
            ProtectedMaterialPurposes.Password);

    private static ProtectedMaterialMutationContext Admin() =>
        new(
            new SecurityPrincipal(
                "admin",
                "Administrator",
                ["administrator"]),
            new AuthorizationDecision(
                true,
                SecurityCapability.SystemAdmin,
                "System administration granted.",
                ["administrator"]));

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "elitescada-protected-material-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed record MediaSourceFixture(
        string Id,
        string Source,
        string PasswordReference);

    private sealed record MediaSourcePublicFixture(
        string Id,
        bool CredentialsConfigured);

    private static readonly byte[] KeyA =
        Convert.FromHexString(
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

    private static readonly byte[] KeyB =
        Convert.FromHexString(
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789");

    private const string SecretA =
        "camera-password-that-must-never-appear-outside-trusted-resolution";

    private const string SecretB =
        "replacement-camera-password-that-must-never-leak";
}
